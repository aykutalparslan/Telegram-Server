// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.users;

namespace Ferrite.Services;

public readonly record struct AudioDocumentInput(long Id, long AccessHash,
    byte[] FileReference);

public enum AudioUserInputKind
{
    Self,
    User,
    FromMessage,
}

public readonly record struct AudioUserInput(AudioUserInputKind Kind, long Id,
    long AccessHash);

public sealed class AccountAudioStore
{
    private readonly IBlockedPeersRepository _blockedPeersRepository;
    private readonly IDocumentsRepository _documentsRepository;
    private readonly IUserRepository _userRepository;

    private const int MaxCollectionSize = 100;
    private readonly IAccountSettingsRepository _repository;
    private readonly IUnitOfWork _transactions;
    private readonly IUploadService _upload;
    private readonly IUpdatesService _updates;
    private readonly TimeProvider _time;

    public AccountAudioStore(IAccountSettingsRepository repository,
        IUnitOfWork transactions, IBlockedPeersRepository blockedPeersRepository, IDocumentsRepository documentsRepository, IUserRepository userRepository, IUploadService upload,
        IUpdatesService updates, TimeProvider time)
    {
        _blockedPeersRepository = blockedPeersRepository;
        _documentsRepository = documentsRepository;
        _userRepository = userRepository;

        _repository = repository;
        _transactions = transactions;
        _upload = upload;
        _updates = updates;
        _time = time;
    }

    /// <summary>
    /// Uploads a ringtone AND saves it to the account. Pinned TDLib never calls
    /// account.saveRingtone after an upload: it reloads account.getSavedRingtones
    /// and looks the new file up in the result, failing the client action with
    /// 500 when it is absent (NotificationSettingsManager.cpp:1196-1229). The
    /// upload is therefore the save.
    /// </summary>
    public async Task<TLBytes> UploadRingtoneAsync(long userId, long authKeyId,
        TLInputFile file, string fileName, string mimeType)
    {
        if (mimeType is not ("audio/mpeg" or "audio/ogg" or "audio/opus"))
            return DocumentInvalid();
        ServiceResult<TLUploadedFileInfo?> saved = await _upload.SaveFile(file);
        if (!saved.Success || saved.Result is null)
            return Error(saved.ErrorMessage.Code, saved.ErrorMessage.Message);
        using TLUploadedFileInfo uploaded = saved.Result.Value;
        byte[] attributes;
        using (DocumentAttributeFilename name = DocumentAttributeFilename.Builder()
                   .FileName(Encoding.UTF8.GetBytes(fileName)).Build())
        using (TLDocumentAttribute audio = DocumentAttributeAudio.Builder()
                   .Duration(0).Build())
        {
            var vector = new Vector();
            vector.AppendTLObject(name.ToReadOnlySpan());
            vector.AppendTLObject(audio.AsSpan());
            attributes = vector.ToReadOnlySpan().ToArray();
        }
        ServiceResult<TLBytes?> registered = await _upload.RegisterDocument(
            uploaded, Encoding.UTF8.GetBytes(mimeType), attributes, null);
        if (!registered.Success || registered.Result is null)
            return Error(registered.ErrorMessage.Code,
                registered.ErrorMessage.Message);
        TLBytes document = registered.Result.Value;
        TLBytes? stored = await AddRingtoneAsync(userId, authKeyId, document);
        if (stored is null) return document;
        document.Dispose();
        return stored.Value;
    }

    /// <summary>
    /// Appends an uploaded ringtone to the account collection. Returns null when
    /// the document is already saved or the collection is full, in which case
    /// the caller keeps the uploaded document as the answer.
    /// </summary>
    private async ValueTask<TLBytes?> AddRingtoneAsync(long userId,
        long authKeyId, TLBytes document)
    {
        // The row must be read BEFORE the document is parsed: a ref struct may
        // not be used after an await.
        using TLAccountRingtonesState? state = await _repository
            .GetRingtonesAsync(userId);
        DocumentView view = (DocumentView)document.AsSpan();
        if (!view.Is(out Document value)) return null;
        long documentId = value.Id;
        var documents = state is null ? new List<TLDocument>()
            : CloneDocuments(state.Value.AsAccountRingtonesState().Documents);
        try
        {
            if (documents.Count >= MaxCollectionSize ||
                documents.Exists(saved => saved.AsDocument().Id == documentId))
                return null;
            documents.Add(value.Clone().Build());
            long revision = state is null ? 1 : state.Value
                .AsAccountRingtonesState().Revision + 1;
            using TLAccountRingtonesState row = AccountRingtonesState.Builder()
                .UserId(userId).Revision(revision)
                .Documents(ToVector(documents)).Date(Now()).Build();
            if (!_repository.PutRingtones(row) ||
                !await _transactions.SaveAsync()) return Internal();
            using TLUpdate update = UpdateSavedRingtones.Builder().Build();
            await _updates.EnqueueUpdate(userId, update,
                UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
            return null;
        }
        finally
        {
            Dispose(documents);
        }
    }

    public async Task<TLBytes> SaveRingtoneAsync(long userId, long authKeyId,
        AudioDocumentInput input, bool unsave)
    {
        using TLDocument? resolved = ResolveAudio(input, false);
        if (resolved is null) return DocumentInvalid();
        using TLAccountRingtonesState? state = await _repository
            .GetRingtonesAsync(userId);
        var documents = state is null ? new List<TLDocument>()
            : CloneDocuments(state.Value.AsAccountRingtonesState().Documents);
        bool changed = false;
        try
        {
            int index = documents.FindIndex(value => value.AsDocument().Id ==
                input.Id);
            if (unsave)
            {
                if (index >= 0)
                {
                    documents[index].Dispose();
                    documents.RemoveAt(index);
                    changed = true;
                }
            }
            else if (index < 0)
            {
                if (documents.Count >= MaxCollectionSize)
                    return Error(400, "RINGTONES_TOO_MUCH");
                documents.Add(resolved.Value.AsDocument().Clone().Build());
                changed = true;
            }
            if (changed)
            {
                long revision = state is null ? 1 : state.Value
                    .AsAccountRingtonesState().Revision + 1;
                using TLAccountRingtonesState row = AccountRingtonesState.Builder()
                    .UserId(userId).Revision(revision)
                    .Documents(ToVector(documents)).Date(Now()).Build();
                if (!_repository.PutRingtones(row) ||
                    !await _transactions.SaveAsync()) return Internal();
                using TLUpdate update = UpdateSavedRingtones.Builder().Build();
                await _updates.EnqueueUpdate(userId, update,
                    UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
            }
            // A save must answer savedRingtoneConverted carrying the stored
            // document. Pinned TDLib's on_add_saved_ringtone only takes the
            // document off that constructor; given a plain savedRingtone it
            // searches its own saved_ringtone_file_ids_ cache, which cannot yet
            // contain a ringtone this very call created, and fails the client
            // action with 500 (NotificationSettingsManager.cpp:1218-1229).
            return unsave
                ? SavedRingtone.Builder().Build().TLBytes!.Value
                : SavedRingtoneConverted.Builder()
                    .Document(resolved.Value.AsSpan()).Build().TLBytes!.Value;
        }
        finally
        {
            Dispose(documents);
        }
    }

    public async Task<TLBytes> GetRingtonesAsync(long userId,
        long requestedHash)
    {
        using TLAccountRingtonesState? state = await _repository
            .GetRingtonesAsync(userId);
        Vector documents = state is null ? new Vector()
            : state.Value.AsAccountRingtonesState().Documents;
        long hash = HashDocuments(documents);
        if (requestedHash != 0 && requestedHash == hash)
            return SavedRingtonesNotModified.Builder().Build().TLBytes!.Value;
        return SavedRingtones.Builder().Hash(hash)
            .Ringtones(CopyVector(documents)).Build().TLBytes!.Value;
    }

    public async Task<TLBool> SaveMusicAsync(long userId,
        AudioDocumentInput input, bool unsave, AudioDocumentInput? after)
    {
        using TLDocument? resolved = ResolveAudio(input, false);
        if (resolved is null) return DocumentInvalidBool();
        using TLProfileMusicState? state = await _repository
            .GetProfileMusicAsync(userId);
        var documents = state is null ? new List<TLDocument>()
            : CloneDocuments(state.Value.AsProfileMusicState().Documents);
        try
        {
            int currentIndex = documents.FindIndex(value =>
                value.AsDocument().Id == input.Id);
            if (unsave)
            {
                if (currentIndex < 0) return True();
                documents[currentIndex].Dispose();
                documents.RemoveAt(currentIndex);
            }
            else
            {
                if (currentIndex < 0 && documents.Count >= MaxCollectionSize)
                    return (TLBool)Error(400, "MUSIC_TOO_MUCH");
                if (after is not null && after.Value.Id == input.Id)
                    return False();
                int afterIndex = -1;
                if (after is not null)
                {
                    afterIndex = documents.FindIndex(value =>
                        Matches(value.AsDocument(), after.Value, false));
                    if (afterIndex < 0) return False();
                }
                TLDocument moved;
                if (currentIndex >= 0)
                {
                    moved = documents[currentIndex];
                    documents.RemoveAt(currentIndex);
                    if (afterIndex > currentIndex) afterIndex--;
                }
                else moved = resolved.Value.AsDocument().Clone().Build();
                documents.Insert(after is null ? 0 : afterIndex + 1, moved);
            }
            long revision = state is null ? 1 : state.Value
                .AsProfileMusicState().Revision + 1;
            using TLProfileMusicState row = ProfileMusicState.Builder()
                .UserId(userId).Revision(revision).Documents(ToVector(documents))
                .Date(Now()).Build();
            return _repository.PutProfileMusic(row) &&
                await _transactions.SaveAsync() ? True() : InternalBool();
        }
        finally
        {
            Dispose(documents);
        }
    }

    public async Task<TLBytes> GetMusicIdsAsync(long userId,
        long requestedHash)
    {
        using TLProfileMusicState? state = await _repository
            .GetProfileMusicAsync(userId);
        long[] ids = state is null ? [] : DocumentIds(state.Value
            .AsProfileMusicState().Documents).Order().ToArray();
        long hash = HashIds(ids);
        if (requestedHash != 0 && requestedHash == hash)
            return SavedMusicIdsNotModified.Builder().Build().TLBytes!.Value;
        var vector = new VectorOfLong();
        foreach (long id in ids) vector.Append(id);
        return SavedMusicIds.Builder().Ids(vector).Build().TLBytes!.Value;
    }

    public async Task<TLBytes> GetMusicAsync(long viewerUserId,
        AudioUserInput input, int offset, int limit, long requestedHash)
    {
        long? targetUserId = ResolveUser(viewerUserId, input);
        if (!targetUserId.HasValue) return UserInvalid();
        if (offset < 0 || limit <= 0) return Error(400, "LIMIT_INVALID");
        limit = Math.Min(limit, MaxCollectionSize);
        using TLProfileMusicState? state = await _repository
            .GetProfileMusicAsync(targetUserId.Value);
        Vector source = state is null ? new Vector()
            : state.Value.AsProfileMusicState().Documents;
        int count = source.Count;
        long hash = HashIds(DocumentIds(source));
        if (requestedHash != 0 && requestedHash == hash)
            return SavedMusicNotModified.Builder().Count(count).Build()
                .TLBytes!.Value;
        var page = new Vector();
        for (int i = 0; i < count; i++)
        {
            Span<byte> document = source.ReadTLObject();
            if (i >= offset && i < offset + limit)
                page.AppendTLObject(document);
        }
        return SavedMusic.Builder().Count(count).Documents(page).Build()
            .TLBytes!.Value;
    }

    public async Task<TLBytes> GetMusicByIdAsync(long viewerUserId,
        AudioUserInput input, IReadOnlyList<AudioDocumentInput> requested)
    {
        long? targetUserId = ResolveUser(viewerUserId, input);
        if (!targetUserId.HasValue) return UserInvalid();
        if (requested.Count > MaxCollectionSize)
            return Error(400, "LIMIT_INVALID");
        using TLProfileMusicState? state = await _repository
            .GetProfileMusicAsync(targetUserId.Value);
        Vector source = state is null ? new Vector()
            : state.Value.AsProfileMusicState().Documents;
        var byId = new Dictionary<long, TLDocument>();
        try
        {
            foreach (TLDocument document in CloneDocuments(source))
                byId[document.AsDocument().Id] = document;
            var result = new Vector();
            foreach (AudioDocumentInput item in requested)
            {
                if (byId.TryGetValue(item.Id, out TLDocument document) &&
                    Matches(document.AsDocument(), item, true))
                    result.AppendTLObject(document.AsSpan());
            }
            return SavedMusic.Builder().Count(source.Count).Documents(result)
                .Build().TLBytes!.Value;
        }
        finally
        {
            Dispose(byId.Values);
        }
    }

    public async ValueTask<TLDocument?> GetFirstMusicAsync(long userId)
    {
        using TLProfileMusicState? state = await _repository
            .GetProfileMusicAsync(userId);
        if (state is null) return null;
        Vector documents = state.Value.AsProfileMusicState().Documents;
        if (documents.Count == 0) return null;
        DocumentView view = (DocumentView)documents.ReadTLObject();
        return view.Is(out Document document) ? document.Clone().Build() : null;
    }

    private TLDocument? ResolveAudio(AudioDocumentInput input,
        bool allowEmptyReference)
    {
        using TLBytes? stored = _documentsRepository
            .GetDocument(input.Id);
        if (stored is null) return null;
        DocumentView view = (DocumentView)stored.Value.AsSpan();
        if (!view.Is(out Document document) ||
            !Matches(document, input, allowEmptyReference) ||
            !IsAudio(document)) return null;
        return document.Clone().Build();
    }

    private long? ResolveUser(long viewerUserId, AudioUserInput input)
    {
        long userId = input.Kind == AudioUserInputKind.Self
            ? viewerUserId : input.Id;
        if (userId <= 0 || IsBlocked(userId, viewerUserId) ||
            IsBlocked(viewerUserId, userId)) return null;
        using TLBytes? stored = _userRepository.GetUser(userId);
        if (stored is null) return null;
        UserView view = (UserView)stored.Value.AsSpan();
        if (!view.Is(out User user)) return null;
        return input.Kind != AudioUserInputKind.User ||
               user.AccessHash == input.AccessHash ? userId : null;
    }

    private bool IsBlocked(long ownerUserId, long peerUserId)
    {
        if (ownerUserId == peerUserId) return false;
        IReadOnlyList<TLBlockedPeer> rows = _blockedPeersRepository
            .GetBlockedPeers(ownerUserId);
        foreach (TLBlockedPeer row in rows)
        {
            using (row)
            {
                var value = row.AsBlockedPeer();
                if (value.PeerType == (int)PeerType.User &&
                    value.PeerId == peerUserId) return true;
            }
        }
        return false;
    }

    private static bool Matches(Document document, AudioDocumentInput input,
        bool allowEmptyReference) => document.Id == input.Id &&
        document.AccessHash == input.AccessHash &&
        (allowEmptyReference && input.FileReference.Length == 0 ||
         document.FileReference.SequenceEqual(input.FileReference));

    private static bool IsAudio(Document document)
    {
        Vector attributes = document.Attributes;
        for (int i = 0; i < attributes.Count; i++)
        {
            DocumentAttributeView view =
                (DocumentAttributeView)attributes.ReadTLObject();
            if (view.Is(out DocumentAttributeAudio _)) return true;
        }
        return false;
    }

    private static List<TLDocument> CloneDocuments(Vector source)
    {
        var documents = new List<TLDocument>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            DocumentView view = (DocumentView)source.ReadTLObject();
            if (view.Is(out Document document))
                documents.Add(document.Clone().Build());
        }
        return documents;
    }

    private static Vector ToVector(IEnumerable<TLDocument> documents)
    {
        var vector = new Vector();
        foreach (TLDocument document in documents)
            vector.AppendTLObject(document.AsSpan());
        return vector;
    }

    private static Vector CopyVector(Vector source)
    {
        var result = new Vector();
        for (int i = 0; i < source.Count; i++)
            result.AppendTLObject(source.ReadTLObject());
        return result;
    }

    private static IEnumerable<long> DocumentIds(Vector source)
    {
        var ids = new List<long>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            DocumentView view = (DocumentView)source.ReadTLObject();
            if (view.Is(out Document document)) ids.Add(document.Id);
        }
        return ids;
    }

    private static long HashDocuments(Vector source) =>
        HashIds(DocumentIds(source));

    private static long HashIds(IEnumerable<long> ids)
    {
        long hash = 1;
        foreach (long id in ids) hash = unchecked(hash * 20261 + id);
        return hash;
    }

    private int Now() => (int)_time.GetUtcNow().ToUnixTimeSeconds();
    private static void Dispose(IEnumerable<TLDocument> documents)
    {
        foreach (TLDocument document in documents) document.Dispose();
    }

    private static TLBool True() => BoolTrue.Builder().Build();
    private static TLBool False() => BoolFalse.Builder().Build();
    private static TLBytes DocumentInvalid() => Error(400, "DOCUMENT_INVALID");
    private static TLBool DocumentInvalidBool() => (TLBool)DocumentInvalid();
    private static TLBytes UserInvalid() => Error(400, "USER_ID_INVALID");
    private static TLBytes Internal() => Error(500, "INTERNAL_SERVER_ERROR");
    private static TLBool InternalBool() => (TLBool)Internal();
    private static TLBytes Error(int code, string message) =>
        RpcErrorGenerator.GenerateError(code, Encoding.UTF8.GetBytes(message));
}
