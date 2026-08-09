// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using DotNext.Collections.Generic;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;
using PeerBlocked = Ferrite.TL.baseLayer.PeerBlocked;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class SearchHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    public SearchHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_ContactsSearch)]
    public async Task<TLFound> Handle(long authKeyId, TLBytes q)
        {
            var query = Encoding.UTF8.GetString(new ContactsSearch(q.AsSpan()).Q);
            var limit = new ContactsSearch(q.AsSpan()).Limit;
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            long searcherUserId = auth == null ? 0 : auth.Value.AsAuthInfo().UserId;
            var searchResults = await _search.SearchUser(query, limit);
            List<TLPeer> peers = new();
            List<TLUser> users = new();
            foreach (var u in searchResults)
            {
                var user = _userRepository.GetUser(u.Id);
                if (user != null)
                {
                    peers.Add(new PeerUser(u.Id));
                    users.Add(user.Value);
                }
            }

            // Public channels (indexed when a username is assigned) surface after users;
            // rows are adjusted per viewer so non-members do not see the creator flags.
            var chatResults = await _search.SearchChats(query, limit);
            var chatRowBytes = new List<byte[]>();
            foreach (var c in chatResults)
            {
                byte[]? channelBytes = null;
                {
                    using var chat = await _chatRepository.GetChatAsync(c.Id);
                    if (chat != null && chat.Value.Type == TLChat.ChatType.Channel)
                    {
                        channelBytes = chat.Value.AsSpan().ToArray();
                    }
                }
                if (channelBytes != null)
                {
                    peers.Add(new PeerChannel(c.Id));
                    chatRowBytes.Add(await ChannelRows.ForViewerAsync(
                        _chatParticipantsRepository, searcherUserId, c.Id,
                        channelBytes));
                }
            }

            var chatVector = new Vector();
            foreach (byte[] rowBytes in chatRowBytes)
            {
                chatVector.AppendTLObject(rowBytes);
            }

            return new Found(new Vector(),
                ToPeerVector(peers),
                chatVector,
                ToUserVector(users));
        }
}
