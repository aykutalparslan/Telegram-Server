// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SaveMusicHandler : AccountAudioHandlerBase
{
    public SaveMusicHandler(AccountAudioStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_SaveMusic)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new SaveMusic(q.AsSpan());
        if (!TryReadDocument(request.Get_IdView(), out var input))
            return DocumentError();
        AudioDocumentInput? after = null;
        if (request.Flags[1])
        {
            if (!TryReadDocument(request.Get_AfterIdView(), out var parsed))
                return DocumentError();
            after = parsed;
        }
        return await Store.SaveMusicAsync(userId.Value, input, request.Unsave,
            after);
    }
}
