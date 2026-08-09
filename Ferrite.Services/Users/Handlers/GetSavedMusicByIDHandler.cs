// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services.Handlers.AccountMethods;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.users;

namespace Ferrite.Services.Handlers.UserMethods;

public sealed class GetSavedMusicByIDHandler : AccountAudioHandlerBase
{
    public GetSavedMusicByIDHandler(AccountAudioStore store,
        ProfileStore profiles) : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_GetSavedMusicByID)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new GetSavedMusicByID(q.AsSpan());
        if (!TryReadUser(request.Get_IdView(), out var input)) return UserError();
        Vector vector = request.Documents;
        var documents = new List<AudioDocumentInput>(vector.Count);
        for (int i = 0; i < vector.Count; i++)
        {
            if (!TryReadDocument((InputDocumentView)vector.ReadTLObject(),
                    out var document)) return DocumentError();
            documents.Add(document);
        }
        return await Store.GetMusicByIdAsync(userId.Value, input, documents);
    }
}
