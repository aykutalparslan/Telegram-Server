// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using DotNext.Collections.Generic;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;
using PeerBlocked = Ferrite.TL.baseLayer.PeerBlocked;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class ResolveUsernameHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    public ResolveUsernameHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_ResolveUsername)]
    public async Task<TLResolvedPeer> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLResolvedPeer)RpcErrorGenerator.GenerateError(400, "INVALID_AUTH_KEY"u8);
            }

            var username = Encoding.UTF8.GetString(new ResolveUsername(q.AsSpan()).Username);
            var peerUser = _userRepository.GetUserByUsername(username);
            if (peerUser == null)
            {
                // User and channel usernames share one namespace; fall back to channels.
                long? chatId = _chatRepository.GetChatIdByUsername(username);
                if (chatId != null)
                {
                    byte[]? channelBytes = null;
                    {
                        using var chat = await _chatRepository.GetChatAsync(chatId.Value);
                        if (chat != null && chat.Value.Type == TLChat.ChatType.Channel &&
                            // A DEACTIVATED username stays reserved to its channel but
                            // stops being a public address, so the index alone is not
                            // enough to resolve one: `channels.toggleUsername` and
                            // `channels.deactivateAllUsernames` deliberately leave the
                            // reservation behind.
                            IsActiveUsername(chat.Value.AsChannel(), username))
                        {
                            channelBytes = chat.Value.AsSpan().ToArray();
                        }
                    }
                    if (channelBytes != null)
                    {
                        // A resolving non-member must not receive the stored creator flags.
                        channelBytes = await ChannelRows.ForViewerAsync(
                            _chatParticipantsRepository,
                            auth.Value.AsAuthInfo().UserId, chatId.Value, channelBytes);
                        var chatVector = new Vector();
                        chatVector.AppendTLObject(channelBytes);
                        using TLPeer channelPeer = new PeerChannel(chatId.Value);
                        return ResolvedPeer.Builder()
                            .Peer(channelPeer.AsSpan())
                            .Users(new Vector())
                            .Chats(chatVector)
                            .Build();
                    }
                }
                return (TLResolvedPeer)RpcErrorGenerator.GenerateError(400, "USERNAME_INVALID"u8);
            }

            List<TLUser> users = new() { peerUser.Value };
            using TLPeer peer = new PeerUser(peerUser.Value.AsUser().Id);
            TLResolvedPeer resolved = ResolvedPeer.Builder()
                .Peer(peer.AsSpan())
                .Users(ToUserVector(users))
                .Chats(new Vector())
                .Build();
            return resolved;
        }

    private static bool IsActiveUsername(Channel channel, string username)
    {
        foreach (ChannelUsername stored in ChannelUsernames.Read(channel))
        {
            if (stored.Active && string.Equals(stored.Username, username,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
