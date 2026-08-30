// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public abstract class ChannelUsernameHandlerBase : ChannelsHandlerBase
{
    private readonly IChatRepository _chatRepository;

    protected ChannelUsernameHandlerBase(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatRepository = chatRepository;

    }

    protected static List<ChannelUsername> ReadUsernames(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        return ChannelUsernames.Read(stored.AsChannel());
    }

    protected async Task<TLBool> ApplyUsernamesAsync(long actorUserId,
        long channelId, byte[] channelBytes,
        IReadOnlyList<ChannelUsername> usernames)
    {
        string title;
        bool publiclyAddressable = ChannelUsernames.HasActive(usernames);
        string editable = ChannelUsernames.Editable(usernames);
        List<ChannelUsername> previous = ReadUsernames(channelBytes);
        using (var stored = new TLChat(channelBytes, 0, channelBytes.Length))
        {
            var channel = stored.AsChannel();
            title = System.Text.Encoding.UTF8.GetString(channel.Title);
            using TLChat updated = ChannelUsernames.Apply(channel, usernames);
            _chatRepository.PutChat(updated);
        }

        List<string> previousActive = ActiveNames(previous);
        List<string> currentActive = ActiveNames(usernames);
        if (!previousActive.SequenceEqual(currentActive, StringComparer.Ordinal))
        {
            await AppendAdminLogEventAsync(channelId, actorUserId,
                BuildUsernamesAction(previous, usernames),
                (int)DateTimeOffset.Now.ToUnixTimeSeconds(),
                string.Join(' ', currentActive));
        }

        await _unitOfWork.SaveAsync();

        if (publiclyAddressable && editable.Length > 0)
        {
            await _search.IndexChat(new ChatSearchModel(channelId, editable, title));
        }
        else
        {
            await _search.DeleteChat(channelId);
        }

        await _fanout.PushUpdateChannelToOtherMembersAsync(channelId, actorUserId);
        return new BoolTrue();
    }

    private static List<string> ActiveNames(
        IReadOnlyList<ChannelUsername> usernames) => usernames
        .Where(x => x.Active)
        .Select(x => x.Username)
        .ToList();

    private static byte[] BuildUsernamesAction(
        IReadOnlyList<ChannelUsername> previous,
        IReadOnlyList<ChannelUsername> current)
    {
        var previousNames = new VectorOfString();
        AppendActive(ref previousNames, previous);
        var currentNames = new VectorOfString();
        AppendActive(ref currentNames, current);

        using TLChannelAdminLogEventAction action =
            ChannelAdminLogEventActionChangeUsernames.Builder()
                .PrevValue(previousNames)
                .NewValue(currentNames)
                .Build();
        return action.AsSpan().ToArray();

        static void AppendActive(ref VectorOfString names,
            IReadOnlyList<ChannelUsername> usernames)
        {
            foreach (ChannelUsername username in usernames)
            {
                if (username.Active)
                {
                    names.AppendTLBytes(
                        System.Text.Encoding.UTF8.GetBytes(username.Username));
                }
            }
        }
    }
}
