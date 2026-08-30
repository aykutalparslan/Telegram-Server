// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Chatlists;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ChatlistMethods;

public abstract class ChatlistHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    protected readonly IUnitOfWork UnitOfWork;

    protected ChatlistHandlerBase(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository)
    {
        _authorizationRepository = authorizationRepository;

        UnitOfWork = unitOfWork;
    }

    protected async ValueTask<long?> GetUserIdAsync(long authKeyId)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        return auth != null && auth.Value.AsAuthInfo().LoggedIn
            ? auth.Value.AsAuthInfo().UserId : null;
    }

    protected static bool TryReadFilterId(InputChatlistView chatlist,
        out int filterId)
    {
        if (chatlist.Is(out InputChatlistDialogFilter filter) &&
            filter.FilterId >= 2)
        {
            filterId = filter.FilterId;
            return true;
        }
        filterId = 0;
        return false;
    }

    protected static bool TryReadPeers(Vector source, long userId,
        out DialogPeerKey[] peers)
    {
        var result = new List<DialogPeerKey>(source.Count);
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = source.ReadTLObject();
            InputPeerView peer = bytes;
            if (!PeerResolver.TryResolveInputPeerDialogKey(peer, userId,
                    out DialogPeerKey key))
            {
                peers = [];
                return false;
            }
            result.Add(key);
        }
        peers = result.ToArray();
        return true;
    }

    protected static TLBytes AuthError() =>
        RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);

    protected static TLBytes RequestError() =>
        RpcErrorGenerator.GenerateError(400, "FILTER_ID_INVALID"u8);

    protected static string ReadSlug(ReadOnlySpan<byte> value)
    {
        const string prefix = "https://t.me/addlist/";
        string slug = Encoding.UTF8.GetString(value);
        return slug.StartsWith(prefix, StringComparison.Ordinal)
            ? slug[prefix.Length..] : slug;
    }
}
