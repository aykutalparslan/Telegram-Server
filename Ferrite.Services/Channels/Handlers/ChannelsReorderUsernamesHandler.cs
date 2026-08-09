// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Reorders a channel's ACTIVE usernames. The order must be a permutation of
/// exactly the active set, which is what `Usernames::can_reorder_to`
/// (`Usernames.cpp:146-163`) checks before the client will even send it; the
/// server checks it again rather than trusting the caller. Deactivated
/// usernames keep their own relative order after the active ones.
///
/// The answer must be `boolTrue`: `ReorderChannelUsernamesQuery`
/// (`ChatManager.cpp:295-299`) turns a `boolFalse` into a fabricated
/// `500 Supergroup usernames weren't updated`, so a refusal has to be a real
/// error rather than a false result.
/// </summary>
public sealed class ChannelsReorderUsernamesHandler : ChannelUsernameHandlerBase
{
    public ChannelsReorderUsernamesHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_ChannelsReorderUsernames)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChannelsReorderUsernames)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        VectorOfString requested = request.Order;
        var order = new List<string>(requested.Count);
        for (int i = 0; i < requested.Count; i++)
        {
            order.Add(Encoding.UTF8.GetString(requested.ReadTLBytes()));
        }

        var (currentUserId, channelBytes, error) =
            await PrepareChannelMutationCore(authKeyId, channelId, creatorOnly: true);
        if (error != null)
        {
            return ErrorBool(Encoding.UTF8.GetBytes(error));
        }

        if (order.Count != order.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return ErrorBool("ORDER_INVALID"u8);
        }

        List<ChannelUsername> usernames = ReadUsernames(channelBytes);
        List<ChannelUsername> active = usernames.Where(x => x.Active).ToList();
        if (active.Count != order.Count || order.Any(name =>
                active.All(x => !string.Equals(x.Username, name,
                    StringComparison.OrdinalIgnoreCase))))
        {
            return ErrorBool("ORDER_INVALID"u8);
        }

        var reordered = new List<ChannelUsername>(usernames.Count);
        foreach (string name in order)
        {
            reordered.Add(active.Single(x => string.Equals(x.Username, name,
                StringComparison.OrdinalIgnoreCase)));
        }
        reordered.AddRange(usernames.Where(x => !x.Active));

        long id = channelId!.Value;
        _log.Debug($"📣 ChannelsReorderUsernames user:{currentUserId} channel:{id} " +
                   $"order:{order.Count}");
        return await ApplyUsernamesAsync(currentUserId, id, channelBytes, reordered);
    }
}
