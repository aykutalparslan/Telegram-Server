// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Services.Calls;

namespace Ferrite.Services;

public class UploadService : IUploadService
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IDocumentsRepository _documentsRepository;
    private readonly IFileInfoRepository _fileInfoRepository;
    private readonly IGroupCallsRepository _groupCallsRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly ISecretChatsRepository _secretChatsRepository;
    private readonly IUserRepository _userRepository;

    private const int UploadGateCount = 256;
    private static readonly SemaphoreSlim[] UploadGates = Enumerable.Range(0, UploadGateCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    // Documented upload rules (https://core.telegram.org/api/files): each
    // part is at most 512 KB, part numbers range over
    // 0..upload_max_fileparts_default-1, and part_size must be 1 KB aligned
    // and divide 512 KB evenly; only the last part may be smaller.
    public const int MaxFileParts = 4000;
    public const int MaxPartSize = 524288;
    private const int DownloadChunkSize = 1024 * 1024;
    // The 5 MB cap applies to photo uploads only; generic finalize lets
    // documents grow bigger, so photo consumers enforce this at processing
    // time instead.
    public const int PhotoSizeLimit = 5242880;

    private readonly IObjectStore _objectStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRandomGenerator _random;
    private readonly IGroupCallBroadcastPlane _broadcast;

    public UploadService(IObjectStore objectStore, IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IDocumentsRepository documentsRepository, IFileInfoRepository fileInfoRepository, IGroupCallsRepository groupCallsRepository, IPhotoRepository photoRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        IRandomGenerator random)
        : this(objectStore, unitOfWork, authorizationRepository, chatRepository,
            documentsRepository, fileInfoRepository, groupCallsRepository,
            photoRepository, secretChatsRepository, userRepository, random,
            new UnavailableGroupCallBroadcastPlane())
    {
    }

    public UploadService(IObjectStore objectStore, IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IDocumentsRepository documentsRepository, IFileInfoRepository fileInfoRepository, IGroupCallsRepository groupCallsRepository, IPhotoRepository photoRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        IRandomGenerator random, IGroupCallBroadcastPlane broadcast)
    {
        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _documentsRepository = documentsRepository;
        _fileInfoRepository = fileInfoRepository;
        _groupCallsRepository = groupCallsRepository;
        _photoRepository = photoRepository;
        _secretChatsRepository = secretChatsRepository;
        _userRepository = userRepository;

        _objectStore = objectStore;
        _unitOfWork = unitOfWork;
        _random = random;
        _broadcast = broadcast;
    }
    public async Task<ServiceResult<bool>> SaveFilePart(long fileId, int filePart, Stream data)
    {
        ErrorMessage? validationError = null;
        var gate = GetUploadGate(fileId, false);
        await gate.WaitAsync();
        try
        {
            var validation = ValidatePart(fileId, false, filePart, data.Length, null);
            validationError = validation.Error;
            if (validationError == null)
            {
                bool saved = await _objectStore.SaveFilePart(fileId, filePart, data);
                if (!saved) return new ServiceResult<bool>(false, true, ErrorMessages.None);

                using var part = (TLFilePart)new FilePart(fileId, filePart, (int)data.Length);
                bool queued = _fileInfoRepository.PutFilePart(part);
                if (validation.NextState is { } nextState)
                {
                    queued = PutUploadState(nextState) && queued;
                }
                bool persisted = queued && await _unitOfWork.SaveAsync();
                return new ServiceResult<bool>(persisted, true, ErrorMessages.None);
            }
        }
        finally
        {
            gate.Release();
        }

        // Drain the streamed body so the connection pipe is not stalled.
        await data.CopyToAsync(Stream.Null);
        return new ServiceResult<bool>(false, false, validationError!.Value);
    }

    public async Task<ServiceResult<bool>> SaveBigFilePart(long fileId, int filePart, int fileTotalParts, Stream data)
    {
        ErrorMessage? validationError = null;
        var gate = GetUploadGate(fileId, true);
        await gate.WaitAsync();
        try
        {
            var validation = ValidatePart(fileId, true, filePart, data.Length, fileTotalParts);
            validationError = validation.Error;
            if (validationError == null)
            {
                bool saved = await _objectStore.SaveBigFilePart(fileId, filePart, fileTotalParts, data);
                if (!saved) return new ServiceResult<bool>(false, true, ErrorMessages.None);

                using var part = (TLFilePart)new FilePart(fileId, filePart, (int)data.Length);
                bool queued = _fileInfoRepository.PutBigFilePart(part);
                if (validation.NextState is { } nextState)
                {
                    queued = PutUploadState(nextState) && queued;
                }
                bool persisted = queued && await _unitOfWork.SaveAsync();
                return new ServiceResult<bool>(persisted, true, ErrorMessages.None);
            }
        }
        finally
        {
            gate.Release();
        }

        await data.CopyToAsync(Stream.Null);
        return new ServiceResult<bool>(false, false, validationError!.Value);
    }

    public async Task<ServiceResult<TLUploadedFileInfo?>> SaveFile(TLInputFile file)
    {
        return await SaveFile(file, false);
    }

    public async Task<ServiceResult<TLUploadedFileInfo?>> SaveEncryptedFile(
        TLInputFile file)
    {
        return await SaveFile(file, true);
    }

    private async Task<ServiceResult<TLUploadedFileInfo?>> SaveFile(
        TLInputFile file, bool encrypted)
    {
        long id;
        int parts;
        byte[] name;
        string? md5Checksum;
        bool isBigFile;
        if (file.Type == TLInputFile.InputFileType.InputFile)
        {
            var input = file.AsInputFile();
            id = input.Id;
            parts = input.Parts;
            name = input.Name.ToArray();
            md5Checksum = Encoding.UTF8.GetString(input.Md5Checksum);
            isBigFile = false;
        }
        else if (file.Type == TLInputFile.InputFileType.InputFileBig)
        {
            var input = file.AsInputFileBig();
            id = input.Id;
            parts = input.Parts;
            name = input.Name.ToArray();
            md5Checksum = null;
            isBigFile = true;
        }
        else
        {
            return new ServiceResult<TLUploadedFileInfo?>(null, false,
                ErrorMessages.PhotoFileMissing);
        }
        var gate = GetUploadGate(id, isBigFile);
        await gate.WaitAsync();
        try
        {
            return await SaveFileCore(id, parts, name, md5Checksum, isBigFile,
                encrypted);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ServiceResult<TLUploadedFileInfo?>> SaveFileCore(
        long fileId, int parts, byte[] name, string? md5Checksum,
        bool isBigFile, bool encrypted)
    {
        if (parts is < 1 or > MaxFileParts)
        {
            return new ServiceResult<TLUploadedFileInfo?>(null, false, ErrorMessages.FilePartsInvalid);
        }
        var fileParts = isBigFile
            ? _fileInfoRepository.GetBigFileParts(fileId)
            : _fileInfoRepository.GetFileParts(fileId);
        var partSizes = new Dictionary<int, int>();
        foreach (var part in fileParts)
        {
            using (part)
            {
                var partInfo = part.AsFilePart();
                if (partInfo.PartNum >= parts)
                {
                    return new ServiceResult<TLUploadedFileInfo?>(null, false,
                        ErrorMessages.FilePartsInvalid);
                }
                partSizes[partInfo.PartNum] = partInfo.PartSize;
            }
        }
        for (int i = 0; i < parts; i++)
        {
            if (!partSizes.ContainsKey(i))
            {
                return new ServiceResult<TLUploadedFileInfo?>(null, false, ErrorMessages.FilePartMissing(i));
            }
        }
        int partSize = partSizes[0];
        if (parts > 1)
        {
            for (int i = 0; i < parts - 1; i++)
            {
                if (partSizes[i] != partSize)
                {
                    return new ServiceResult<TLUploadedFileInfo?>(null, false, ErrorMessages.FilePartSizeChanged);
                }
            }
            if (partSizes[parts - 1] > partSize)
            {
                return new ServiceResult<TLUploadedFileInfo?>(null, false, ErrorMessages.FilePartSizeChanged);
            }
            if (!IsValidPartSize(partSize))
            {
                return new ServiceResult<TLUploadedFileInfo?>(null, false, ErrorMessages.FilePartSizeInvalid);
            }
        }

        if (encrypted && !string.IsNullOrEmpty(md5Checksum) &&
            !await HasExpectedMd5Async(fileId, isBigFile, parts,
                partSizes, md5Checksum))
        {
            return new ServiceResult<TLUploadedFileInfo?>(null, false,
                ErrorMessages.Md5ChecksumInvalid);
        }

        // Ciphertext is opaque. Secret-chat finalization validates only the
        // supplied checksum and multipart shape; it never probes the content.
        var fileType = encrypted
            ? StreamFileType.Unknown
            : await DetectFileType(fileId, isBigFile);
        var accessHash = _random.NextLong();
        byte[] reference = _random.GetRandomBytes(16);
        TLUploadedFileInfo info = UploadedFileInfo.Builder()
            .Id(fileId)
            .PartSize(partSize)
            .Parts(parts)
            .AccessHash(accessHash)
            .Name(name)
            .Md5Checksum(md5Checksum != null ? Encoding.UTF8.GetBytes(md5Checksum) : ReadOnlySpan<byte>.Empty)
            .SavedOn(DateTimeOffset.Now.ToUnixTimeMilliseconds())
            .IsBigFile(isBigFile)
            .FileReference(reference)
            .FileType((int)fileType)
            .Build();
        bool persisted;
        if (info.AsUploadedFileInfo().IsBigFile)
        {
            persisted = _fileInfoRepository.PutBigFileInfo(info);
        }
        else
        {
            persisted = _fileInfoRepository.PutFileInfo(info);
        }

        using var fileReference = (TLFileReference)new FileReference(reference,
            fileId, isBigFile);
        persisted = _fileInfoRepository.PutFileReference(fileReference) && persisted;
        persisted = persisted && await _unitOfWork.SaveAsync();
        if (!persisted)
        {
            info.Dispose();
            return new ServiceResult<TLUploadedFileInfo?>(null, false, ErrorMessages.InternalServerError);
        }
        return new ServiceResult<TLUploadedFileInfo?>(info, true, ErrorMessages.None);
    }

    private async Task<bool> HasExpectedMd5Async(long fileId, bool isBigFile,
        int parts, IReadOnlyDictionary<int, int> partSizes, string expected)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        byte[] buffer = new byte[MaxPartSize];
        for (int partNum = 0; partNum < parts; partNum++)
        {
            await using Stream stream = await (isBigFile
                ? _objectStore.GetBigFilePart(fileId, partNum)
                : _objectStore.GetFilePart(fileId, partNum));
            int remaining = partSizes[partNum];
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, remaining)));
                if (read == 0)
                {
                    return false;
                }
                hash.AppendData(buffer.AsSpan(0, read));
                remaining -= read;
            }
        }
        string actual = Convert.ToHexString(hash.GetHashAndReset());
        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    // Registers a finalized upload as a wire document# row. The document id is the
    // file id (as with photos), so its bytes stream straight from the file store on
    // download. Identical content dedups through documents_by_sha256 (the first
    // upload's row is returned; the redundant file bytes are left orphaned - storage
    // GC is deferred per design). Client attributes/mime/thumbs are
    // trusted and stored as-is; the server never probes non-photo media.
    public async Task<ServiceResult<TLBytes?>> RegisterDocument(TLUploadedFileInfo finalized,
        byte[] mimeType, byte[] attributesVectorBytes, byte[]? thumbsVectorBytes)
    {
        var info = finalized.AsUploadedFileInfo();
        long fileId = info.Id;
        bool isBigFile = info.IsBigFile;
        long accessHash = info.AccessHash;
        byte[] fileReference = info.Flags[1] ? info.FileReference.ToArray() : Array.Empty<byte>();
        int date = (int)DateTimeOffset.FromUnixTimeMilliseconds(info.SavedOn).ToUnixTimeSeconds();

        var (size, sha256) = await HashFileAsync(fileId, isBigFile);
        if (size < 0)
        {
            return new ServiceResult<TLBytes?>(null, false, ErrorMessages.FilePartsInvalid);
        }

        TLBytes? existing = _documentsRepository.GetDocumentBySha256(sha256);
        if (existing != null &&
            ((Document)existing.Value.AsSpan()).Constructor == Constructors.baseLayer_Document)
        {
            // The store-read value is byte[]-backed; transfer it to the caller
            // instead of copying its bytes into a fresh TLBytes.
            return new ServiceResult<TLBytes?>(existing.Value, true, ErrorMessages.None);
        }
        existing?.Dispose();

        var attributes = new Vector(attributesVectorBytes.AsSpan());
        var documentBuilder = Document.Builder()
            .Id(fileId)
            .AccessHash(accessHash)
            .FileReference(fileReference)
            .Date(date)
            .MimeType(mimeType)
            .Size(size)
            .DcId(MediaDefaults.DcId)
            .Attributes(attributes);
        if (thumbsVectorBytes != null)
        {
            documentBuilder = documentBuilder.Thumbs(new Vector(thumbsVectorBytes.AsSpan()));
        }

        using TLDocument document = documentBuilder.Build();
        bool queued = _documentsRepository.PutDocument((TLBytes)document, sha256);
        if (!queued || !await _unitOfWork.SaveAsync())
        {
            return new ServiceResult<TLBytes?>(null, false, ErrorMessages.InternalServerError);
        }

        byte[] bytes = document.AsSpan().ToArray();
        return new ServiceResult<TLBytes?>(new TLBytes(bytes, 0, bytes.Length), true,
            ErrorMessages.None);
    }

    // Streams the finalized parts through an incremental SHA-256, returning the total
    // byte size and digest. Big documents are never buffered whole; each part is read
    // into a bounded buffer. Returns size -1 on a missing/short part.
    private async Task<(long Size, byte[] Sha256)> HashFileAsync(long fileId, bool isBigFile)
    {
        var partRows = isBigFile
            ? _fileInfoRepository.GetBigFileParts(fileId)
            : _fileInfoRepository.GetFileParts(fileId);
        var parts = new List<(int Number, int Size)>(partRows.Count);
        foreach (var row in partRows)
        {
            var part = row.AsFilePart();
            parts.Add((part.PartNum, part.PartSize));
            row.Dispose();
        }
        parts.Sort((x, y) => x.Number.CompareTo(y.Number));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;
        byte[] buffer = new byte[MaxPartSize];
        foreach (var (number, partSize) in parts)
        {
            await using var stream = await (isBigFile
                ? _objectStore.GetBigFilePart(fileId, number)
                : _objectStore.GetFilePart(fileId, number));
            int remaining = partSize;
            while (remaining > 0)
            {
                int toRead = Math.Min(remaining, buffer.Length);
                int read = await stream.ReadAsync(buffer.AsMemory(0, toRead));
                if (read == 0)
                {
                    return (-1, Array.Empty<byte>());
                }
                hash.AppendData(buffer.AsSpan(0, read));
                total += read;
                remaining -= read;
            }
        }
        return (total, hash.GetHashAndReset());
    }

    private PartValidation ValidatePart(long fileId, bool isBigFile, int partNum, long partSize,
        int? totalParts)
    {
        if (partNum is < 0 or >= MaxFileParts) return new PartValidation(ErrorMessages.FilePartInvalid, null);
        if (totalParts is { } total && total != -1)
        {
            // Streamed uploads legitimately send file_total_parts = -1 for
            // every part except the last one.
            if (total is < 1 or > MaxFileParts) return new PartValidation(ErrorMessages.FilePartsInvalid, null);
            if (partNum >= total) return new PartValidation(ErrorMessages.FilePartInvalid, null);
        }
        if (partSize == 0) return new PartValidation(ErrorMessages.FilePartEmpty, null);
        if (partSize > MaxPartSize) return new PartValidation(ErrorMessages.FilePartTooBig, null);
        int size = (int)partSize;

        var state = _fileInfoRepository.GetUploadState(fileId, isBigFile);
        int persistedTotalParts = -1;
        if (state != null)
        {
            persistedTotalParts = state.Value.AsUploadPartState().TotalParts;
        }
        int incomingTotalParts = totalParts is > 0 ? totalParts.Value : -1;
        if (persistedTotalParts > 0 && incomingTotalParts > 0 &&
            persistedTotalParts != incomingTotalParts)
        {
            return new PartValidation(ErrorMessages.FilePartsInvalid, null);
        }
        int nextTotalParts = persistedTotalParts > 0 ? persistedTotalParts : incomingTotalParts;

        var existing = isBigFile
            ? _fileInfoRepository.GetBigFilePart(fileId, partNum)
            : _fileInfoRepository.GetFilePart(fileId, partNum);
        if (existing != null)
        {
            // Resaving a part is documented behavior; the size must not change.
            if (existing.Value.AsFilePart().PartSize != size)
                return new PartValidation(ErrorMessages.FilePartSizeChanged, null);
            if (state != null && persistedTotalParts != nextTotalParts)
            {
                var existingState = state.Value.AsUploadPartState();
                return new PartValidation(null,
                    new UploadState(fileId, isBigFile, existingState.PartSize,
                        existingState.PartSizeFirstNum, existingState.PartSizeCount,
                        existingState.ShortPartNum, existingState.ShortPartSize, nextTotalParts));
            }
            return new PartValidation(null, null);
        }

        if (state == null)
        {
            return new PartValidation(null,
                new UploadState(fileId, isBigFile, size, partNum, 1, -1, -1,
                    nextTotalParts));
        }

        // Parts can arrive out of order (clients upload over parallel
        // connections), so the canonical part_size is the largest size seen
        // for the file and at most one smaller part - the file's last part -
        // may exist. Divisibility only binds once a second part proves the
        // file is multi-part; a single-part file may have any size.
        var current = state.Value.AsUploadPartState();
        if (size == current.PartSize)
        {
            if (!IsValidPartSize(current.PartSize))
                return new PartValidation(ErrorMessages.FilePartSizeInvalid, null);
            return new PartValidation(null,
                new UploadState(fileId, isBigFile, current.PartSize, current.PartSizeFirstNum,
                    current.PartSizeCount + 1, current.ShortPartNum, current.ShortPartSize,
                    nextTotalParts));
        }
        if (size < current.PartSize)
        {
            if (current.ShortPartNum != -1)
                return new PartValidation(ErrorMessages.FilePartSizeChanged, null);
            if (!IsValidPartSize(current.PartSize))
                return new PartValidation(ErrorMessages.FilePartSizeInvalid, null);
            return new PartValidation(null,
                new UploadState(fileId, isBigFile, current.PartSize, current.PartSizeFirstNum,
                    current.PartSizeCount, partNum, size, nextTotalParts));
        }
        // partSize > canonical: this part fixes the real part_size and the
        // single previous canonical part becomes the candidate last part.
        if (!IsValidPartSize(size)) return new PartValidation(ErrorMessages.FilePartSizeInvalid, null);
        if (current.PartSizeCount > 1 || current.ShortPartNum != -1)
        {
            return new PartValidation(ErrorMessages.FilePartSizeChanged, null);
        }
        return new PartValidation(null,
            new UploadState(fileId, isBigFile, size, partNum, 1,
                current.PartSizeFirstNum, current.PartSize, nextTotalParts));
    }

    private bool PutUploadState(UploadState value)
    {
        using var state = (TLUploadPartState)new UploadPartState(value.FileId, value.IsBigFile,
            value.PartSize, value.FirstNum, value.Count, value.ShortNum, value.ShortSize,
            value.TotalParts);
        return _fileInfoRepository.PutUploadState(state);
    }

    private static SemaphoreSlim GetUploadGate(long fileId, bool isBigFile) =>
        UploadGates[(HashCode.Combine(fileId, isBigFile) & int.MaxValue) % UploadGateCount];

    private readonly record struct PartValidation(ErrorMessage? Error, UploadState? NextState);
    private readonly record struct UploadState(long FileId, bool IsBigFile, int PartSize,
        int FirstNum, int Count, int ShortNum, int ShortSize, int TotalParts);

    private static bool IsValidPartSize(int partSize) =>
        partSize % 1024 == 0 && MaxPartSize % partSize == 0;

    private async Task<StreamFileType> DetectFileType(long fileId, bool isBigFile)
    {
        await using var head = await (isBigFile
            ? _objectStore.GetBigFilePart(fileId, 0)
            : _objectStore.GetFilePart(fileId, 0));
        byte[] buffer = new byte[16];
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await head.ReadAsync(buffer.AsMemory(read));
            if (n == 0) break;
            read += n;
        }
        return FileTypeDetector.Detect(buffer.AsSpan(0, read));
    }

    public async Task<ServiceResult<IFileOwner>> GetFile(long authKeyId, TLBytes request, long reqMsgId)
    {
        DownloadRequest parsed = ParseDownloadRequest(request);
        if (parsed.Kind == DownloadLocationKind.Invalid)
        {
            return DownloadError(ErrorMessages.LocationInvalid);
        }

        ErrorMessage? rangeError = ValidateDownloadRange(parsed.Offset, parsed.Limit, parsed.Precise);
        if (rangeError != null)
        {
            return DownloadError(rangeError.Value);
        }

        var authorization = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (authorization == null)
        {
            return DownloadError(ErrorMessages.InvalidAuthKey);
        }

        long currentUserId;
        using (TLAuthInfo auth = authorization.Value)
        {
            currentUserId = auth.AsAuthInfo().UserId;
        }

        if (parsed.Kind == DownloadLocationKind.GroupCallStream)
        {
            return await GetGroupCallStream(parsed, currentUserId, reqMsgId);
        }

        DownloadResolution resolution;
        if (parsed.Kind == DownloadLocationKind.Encrypted)
        {
            resolution = await ResolveEncryptedFile(authKeyId, parsed);
        }
        else if (parsed.Kind is DownloadLocationKind.PeerChat or DownloadLocationKind.PeerChannel)
        {
            using var chatRow = await _chatRepository.GetChatAsync(parsed.PeerId);
            resolution = chatRow == null
                ? DownloadResolution.InvalidLocation
                : ResolveChatPeerThumbnail(parsed, chatRow.Value);
        }
        else
        {
            resolution = parsed.Kind switch
            {
                DownloadLocationKind.Photo => ResolvePhotoThumbnail(parsed),
                DownloadLocationKind.Document => ResolveDocument(parsed),
                DownloadLocationKind.PeerSelf => ResolvePeerThumbnail(parsed, currentUserId, currentUserId),
                DownloadLocationKind.PeerUser => ResolvePeerThumbnail(parsed, parsed.PeerId, currentUserId),
                _ => DownloadResolution.InvalidLocation
            };
        }
        if (resolution.Error != null)
        {
            return DownloadError(resolution.Error.Value);
        }
        long thumbnailFileId = resolution.FileId;

        TLUploadedFileInfo? storedFile = _fileInfoRepository.GetFileInfo(thumbnailFileId);
        if (storedFile == null)
        {
            storedFile = _fileInfoRepository.GetBigFileInfo(thumbnailFileId);
        }
        if (storedFile == null)
        {
            return DownloadError(ErrorMessages.LocationInvalid);
        }

        using TLUploadedFileInfo file = storedFile.Value;
        var info = file.AsUploadedFileInfo();
        if (info.Id != thumbnailFileId)
        {
            return DownloadError(ErrorMessages.LocationInvalid);
        }

        DateTimeOffset savedOn = DateTimeOffset.FromUnixTimeMilliseconds(info.SavedOn);
        StreamFileType fileType = Enum.IsDefined(typeof(StreamFileType), info.FileType)
            ? (StreamFileType)info.FileType
            : StreamFileType.Unknown;
        int mtime = checked((int)savedOn.ToUnixTimeSeconds());
        byte[] header = UploadStreamHeader.GenerateStreamHeader(reqMsgId, fileType, mtime);
        IFileOwner owner = _objectStore.GetFileOwner(file, parsed.Offset,
            parsed.Limit, reqMsgId, header);
        return new ServiceResult<IFileOwner>(owner, true, ErrorMessages.None);
    }

    private async Task<ServiceResult<IFileOwner>> GetGroupCallStream(
        DownloadRequest request, long currentUserId, long reqMsgId)
    {
        if (request.Offset != 0)
        {
            return DownloadError(ErrorMessages.OffsetInvalid);
        }
        using var call = await _groupCallsRepository
            .GetCallAsync(request.CallId);
        if (call == null)
        {
            return DownloadError(ErrorMessages.LocationInvalid);
        }
        var callView = call.Value.AsGroupCallState();
        bool validCall = callView.AccessHash == request.AccessHash &&
            callView.State == (int)GroupCallPersistenceState.Active;
        if (!validCall)
        {
            return DownloadError(ErrorMessages.LocationInvalid);
        }
        using var participant = await _groupCallsRepository
            .GetParticipantAsync(request.CallId, currentUserId);
        if (participant == null || participant.Value.AsGroupCallParticipantState().Left)
        {
            return DownloadError(ErrorMessages.GroupCallJoinMissing);
        }

        try
        {
            ReadOnlyMemory<byte> segment = await _broadcast.ReadSegmentAsync(
                new GroupCallBroadcastSegmentRequest(request.CallId,
                    request.TimeMs, request.Scale, request.VideoChannel,
                    request.VideoQuality));
            int mtime = checked((int)(request.TimeMs / 1000));
            byte[] header = UploadStreamHeader.GenerateStreamHeader(reqMsgId,
                StreamFileType.Unknown, mtime);
            IFileOwner owner = new BufferedFileOwner(segment, reqMsgId, header);
            return new ServiceResult<IFileOwner>(owner, true, ErrorMessages.None);
        }
        catch (GroupCallBroadcastException e)
        {
            return e.Kind switch
            {
                GroupCallBroadcastFailureKind.NotReady =>
                    DownloadError(ErrorMessages.TimeTooBig),
                GroupCallBroadcastFailureKind.Unavailable =>
                    DownloadError(ErrorMessages.InternalServerError),
                _ => DownloadError(ErrorMessages.LocationInvalid)
            };
        }
    }

    private async ValueTask<DownloadResolution> ResolveEncryptedFile(
        long authKeyId, DownloadRequest request)
    {
        TLSecretChatEncryptedFile? storedValue = await _secretChatsRepository.GetEncryptedFileAsync(request.PhotoId,
                request.AccessHash);
        if (storedValue is null)
        {
            return DownloadResolution.InvalidLocation;
        }

        long uploadFileId;
        using (TLSecretChatEncryptedFile stored = storedValue.Value)
        {
            var row = stored.AsSecretChatEncryptedFile();
            if (row.FileId != request.PhotoId ||
                row.AccessHash != request.AccessHash)
            {
                return DownloadResolution.InvalidLocation;
            }
            uploadFileId = row.UploadFileId;
        }

        bool authorized = false;
        IReadOnlyList<TLSecretChatEncryptedFileAssociation> associations =
            await _secretChatsRepository
                .GetEncryptedFileAssociationsAsync(request.PhotoId);
        foreach (TLSecretChatEncryptedFileAssociation association in associations)
        {
            using (association)
            {
                var associationRow = association.AsSecretChatEncryptedFileAssociation();
                if (associationRow.AccessHash != request.AccessHash)
                {
                    continue;
                }
                TLSecretChatState? chatValue = await _secretChatsRepository.GetChatAsync(associationRow.ChatId);
                if (chatValue is null)
                {
                    continue;
                }
                using TLSecretChatState chat = chatValue.Value;
                var chatRow = chat.AsSecretChatState();
                if (chatRow.InitiatorAuthKeyId == authKeyId ||
                    chatRow.Flags[1] && chatRow.RecipientAuthKeyId == authKeyId)
                {
                    authorized = true;
                }
            }
        }
        return authorized
            ? new DownloadResolution(uploadFileId, null)
            : DownloadResolution.InvalidLocation;
    }

    private DownloadResolution ResolvePhotoThumbnail(DownloadRequest request)
    {
        using TLBytes? photoBytes = _photoRepository.GetPhoto(request.PhotoId);
        if (photoBytes == null)
        {
            return DownloadResolution.InvalidLocation;
        }

        var photo = (Photo)photoBytes.Value.AsSpan();
        if (photo.Constructor != Constructors.baseLayer_Photo ||
            photo.Id != request.PhotoId || photo.AccessHash != request.AccessHash)
        {
            return DownloadResolution.InvalidLocation;
        }
        if (!photo.FileReference.SequenceEqual(request.FileReference))
        {
            return DownloadResolution.ExpiredReference;
        }

        using TLFileReference? storedReference =
            _fileInfoRepository.GetFileReference(request.FileReference);
        if (storedReference == null ||
            storedReference.Value.AsFileReference().FileId != request.PhotoId)
        {
            return DownloadResolution.ExpiredReference;
        }

        long? thumbnailFileId = FindThumbnail(request.PhotoId, request.ThumbSize);
        return thumbnailFileId == null
            ? DownloadResolution.InvalidLocation
            : new DownloadResolution(thumbnailFileId.Value, null);
    }

    // Resolves an inputDocumentFileLocation. An empty thumb_size streams the document's
    // own bytes (the file id is the document id, as with photos); a non-empty thumb_size
    // resolves a stored client thumbnail keyed under the document id.
    private DownloadResolution ResolveDocument(DownloadRequest request)
    {
        using TLBytes? documentBytes = _documentsRepository.GetDocument(request.PhotoId);
        if (documentBytes == null)
        {
            return DownloadResolution.InvalidLocation;
        }

        var document = (Document)documentBytes.Value.AsSpan();
        if (document.Constructor != Constructors.baseLayer_Document ||
            document.Id != request.PhotoId || document.AccessHash != request.AccessHash)
        {
            return DownloadResolution.InvalidLocation;
        }
        if (!document.FileReference.SequenceEqual(request.FileReference))
        {
            return DownloadResolution.ExpiredReference;
        }

        if (request.ThumbSize.Length == 0)
        {
            return new DownloadResolution(request.PhotoId, null);
        }

        long? thumbnailFileId = FindThumbnail(request.PhotoId, request.ThumbSize);
        return thumbnailFileId == null
            ? DownloadResolution.InvalidLocation
            : new DownloadResolution(thumbnailFileId.Value, null);
    }

    private DownloadResolution ResolvePeerThumbnail(DownloadRequest request, long userId, long currentUserId)
    {
        using TLUser? userBytes = _userRepository.GetUser(userId);
        if (userBytes == null)
        {
            return DownloadResolution.InvalidLocation;
        }

        var user = userBytes.Value.AsUser();
        if (request.Kind == DownloadLocationKind.PeerUser &&
            request.PeerAccessHash != user.AccessHash)
        {
            return DownloadResolution.InvalidLocation;
        }
        if (request.Kind == DownloadLocationKind.PeerSelf && user.Id != currentUserId)
        {
            return DownloadResolution.InvalidLocation;
        }
        if (!user.Get_PhotoView().Is(out UserProfilePhoto profilePhoto) ||
            profilePhoto.PhotoId != request.PhotoId)
        {
            return DownloadResolution.InvalidLocation;
        }

        using TLBytes? storedPhoto = _photoRepository.GetPhoto(request.PhotoId);
        if (storedPhoto == null ||
            ((Photo)storedPhoto.Value.AsSpan()).Constructor != Constructors.baseLayer_Photo)
        {
            return DownloadResolution.InvalidLocation;
        }

        long? thumbnailFileId = request.Big
            ? FindThumbnail(request.PhotoId, "c", "b", "a", "s")
            : FindThumbnail(request.PhotoId, "a", "s");
        return thumbnailFileId == null
            ? DownloadResolution.InvalidLocation
            : new DownloadResolution(thumbnailFileId.Value, null);
    }

    // Resolves an inputPeerPhotoFileLocation for a basic-group (chat#) or
    // supergroup/channel (channel#) peer. The compact row carries the small
    // chatPhoto# object; the requested photo id must match it, and the full
    // photo# row must be stored so its thumbnail ladder resolves.
    private DownloadResolution ResolveChatPeerThumbnail(DownloadRequest request, TLChat row)
    {
        long? photoId;
        if (request.Kind == DownloadLocationKind.PeerChannel)
        {
            var channel = row.AsChannel();
            if (channel.Constructor != Constructors.baseLayer_Channel ||
                channel.AccessHash != request.PeerAccessHash)
            {
                return DownloadResolution.InvalidLocation;
            }
            photoId = ChatPhotos.ReadPhotoId(channel.Get_PhotoView());
        }
        else
        {
            var chat = row.AsChat();
            if (chat.Constructor != Constructors.baseLayer_Chat)
            {
                return DownloadResolution.InvalidLocation;
            }
            photoId = ChatPhotos.ReadPhotoId(chat.Get_PhotoView());
        }

        if (photoId == null || photoId != request.PhotoId)
        {
            return DownloadResolution.InvalidLocation;
        }

        using TLBytes? storedPhoto = _photoRepository.GetPhoto(request.PhotoId);
        if (storedPhoto == null ||
            ((Photo)storedPhoto.Value.AsSpan()).Constructor != Constructors.baseLayer_Photo)
        {
            return DownloadResolution.InvalidLocation;
        }

        long? thumbnailFileId = request.Big
            ? FindThumbnail(request.PhotoId, "c", "b", "a", "s")
            : FindThumbnail(request.PhotoId, "a", "s");
        return thumbnailFileId == null
            ? DownloadResolution.InvalidLocation
            : new DownloadResolution(thumbnailFileId.Value, null);
    }

    private long? FindThumbnail(long photoId, params string[] preferredTypes)
    {
        int bestPriority = int.MaxValue;
        long? bestFileId = null;
        IReadOnlyList<TLBytes> thumbnails = _photoRepository.GetThumbnails(photoId);
        foreach (TLBytes thumbnailBytes in thumbnails)
        {
            using (thumbnailBytes)
            {
                var thumbnail = (Thumbnail)thumbnailBytes.AsSpan();
                byte[] photoSizeBytes = thumbnail.PhotoSize.ToArray();
                var photoSize = (PhotoSize)photoSizeBytes.AsSpan();
                if (photoSize.Constructor != Constructors.baseLayer_PhotoSize)
                {
                    continue;
                }

                string type = Encoding.UTF8.GetString(photoSize.Type);
                int priority = Array.IndexOf(preferredTypes, type);
                if (priority >= 0 && priority < bestPriority)
                {
                    bestPriority = priority;
                    bestFileId = thumbnail.ThumbFileId;
                }
            }
        }
        return bestFileId;
    }

    private static DownloadRequest ParseDownloadRequest(TLBytes request)
    {
        var getFile = new Ferrite.TL.baseLayer.upload.GetFile(request.AsSpan());
        InputFileLocationView location = getFile.Get_LocationView();
        if (location.Is(out InputPhotoFileLocation photo))
        {
            return new DownloadRequest(DownloadLocationKind.Photo, photo.Id, photo.AccessHash,
                photo.FileReference.ToArray(), Encoding.UTF8.GetString(photo.ThumbSize),
                0, 0, false, getFile.Offset, getFile.Limit, getFile.Precise);
        }
        if (location.Is(out InputDocumentFileLocation document))
        {
            return new DownloadRequest(DownloadLocationKind.Document, document.Id,
                document.AccessHash, document.FileReference.ToArray(),
                Encoding.UTF8.GetString(document.ThumbSize),
                0, 0, false, getFile.Offset, getFile.Limit, getFile.Precise);
        }
        if (location.Is(out InputEncryptedFileLocation encrypted))
        {
            return new DownloadRequest(DownloadLocationKind.Encrypted, encrypted.Id,
                encrypted.AccessHash, Array.Empty<byte>(), string.Empty, 0, 0,
                false, getFile.Offset, getFile.Limit, getFile.Precise);
        }
        if (location.Is(out InputGroupCallStream stream) &&
            stream.Get_CallView().Is(out InputGroupCall call))
        {
            return new DownloadRequest(DownloadLocationKind.GroupCallStream,
                0, call.AccessHash, Array.Empty<byte>(), string.Empty, 0, 0,
                false, getFile.Offset, getFile.Limit, getFile.Precise,
                call.Id, stream.TimeMs, stream.Scale,
                stream.Flags[0] ? stream.VideoChannel : 0,
                stream.Flags[0] ? stream.VideoQuality : 0);
        }
        if (!location.Is(out InputPeerPhotoFileLocation peerPhoto))
        {
            return DownloadRequest.Invalid;
        }

        InputPeerView peer = peerPhoto.Get_PeerView();
        if (peer.Is(out InputPeerSelf _))
        {
            return new DownloadRequest(DownloadLocationKind.PeerSelf, peerPhoto.PhotoId, 0,
                Array.Empty<byte>(), string.Empty, 0, 0, peerPhoto.Big,
                getFile.Offset, getFile.Limit, getFile.Precise);
        }
        if (peer.Is(out InputPeerUser inputUser))
        {
            return new DownloadRequest(DownloadLocationKind.PeerUser, peerPhoto.PhotoId, 0,
                Array.Empty<byte>(), string.Empty, inputUser.UserId, inputUser.AccessHash,
                peerPhoto.Big, getFile.Offset, getFile.Limit, getFile.Precise);
        }
        if (peer.Is(out InputPeerChat inputChat))
        {
            return new DownloadRequest(DownloadLocationKind.PeerChat, peerPhoto.PhotoId, 0,
                Array.Empty<byte>(), string.Empty, inputChat.ChatId, 0,
                peerPhoto.Big, getFile.Offset, getFile.Limit, getFile.Precise);
        }
        if (peer.Is(out InputPeerChannel inputChannel))
        {
            return new DownloadRequest(DownloadLocationKind.PeerChannel, peerPhoto.PhotoId, 0,
                Array.Empty<byte>(), string.Empty, inputChannel.ChannelId, inputChannel.AccessHash,
                peerPhoto.Big, getFile.Offset, getFile.Limit, getFile.Precise);
        }
        return DownloadRequest.Invalid;
    }

    private static ErrorMessage? ValidateDownloadRange(long offset, int limit, bool precise)
    {
        int alignment = precise ? 1024 : 4096;
        if (offset < 0 || offset % alignment != 0)
        {
            return ErrorMessages.OffsetInvalid;
        }
        if (limit <= 0 || limit % alignment != 0 || limit > DownloadChunkSize ||
            (!precise && DownloadChunkSize % limit != 0))
        {
            return ErrorMessages.LimitInvalid;
        }
        if (offset > long.MaxValue - limit + 1 ||
            offset / DownloadChunkSize != (offset + limit - 1) / DownloadChunkSize)
        {
            return ErrorMessages.LimitInvalid;
        }
        return null;
    }

    private static ServiceResult<IFileOwner> DownloadError(ErrorMessage error) =>
        new(null, false, error);

    private enum DownloadLocationKind
    {
        Invalid,
        Photo,
        Document,
        Encrypted,
        PeerSelf,
        PeerUser,
        PeerChat,
        PeerChannel,
        GroupCallStream
    }

    private readonly record struct DownloadRequest(DownloadLocationKind Kind,
        long PhotoId, long AccessHash, byte[] FileReference, string ThumbSize,
        long PeerId, long PeerAccessHash, bool Big, long Offset, int Limit,
        bool Precise, long CallId = 0, long TimeMs = 0, int Scale = 0,
        int VideoChannel = 0, int VideoQuality = 0)
    {
        public static DownloadRequest Invalid { get; } = new(DownloadLocationKind.Invalid,
            0, 0, Array.Empty<byte>(), string.Empty, 0, 0, false, 0, 0, false);
    }

    private readonly record struct DownloadResolution(long FileId, ErrorMessage? Error)
    {
        public static DownloadResolution InvalidLocation { get; } =
            new(0, ErrorMessages.LocationInvalid);
        public static DownloadResolution ExpiredReference { get; } =
            new(0, ErrorMessages.FileReferenceExpired);
    }
}
