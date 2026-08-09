// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Shared plumbing for username-collection methods:
/// `toggleUsername`, `deactivateAllUsernames` and `reorderUsernames`. All three
/// rewrite the whole collection rather than one entry, because the compact row
/// stores it in one of two mutually exclusive forms
/// (<see cref="ChannelUsernames"/>).
///
/// None of them touches the global username index. A deactivated username stays
/// RESERVED to its channel -- that is the difference between deactivating a
/// username and clearing it through `channels.updateUsername`, which is the only
/// method that releases a reservation.
/// </summary>
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

    /// <summary>
    /// Reads the channel's usernames in stored order. Kept in one synchronous
    /// frame so no view outlives its buffer across an await.
    /// </summary>
    protected static List<ChannelUsername> ReadUsernames(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        return ChannelUsernames.Read(stored.AsChannel());
    }

    /// <summary>
    /// Persists a rewritten collection, keeps the public search index in step
    /// with whether the channel is still publicly addressable, and tells every
    /// OTHER member. Skipping the fanout is the `7630f49c` defect: the actor
    /// sees the change and no other member ever asks again.
    /// </summary>
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

        // Appended before the save so the ledger entry commits with the change it
        // describes. The whole ACTIVE collection is recorded on both sides, which
        // is what `channelAdminLogEventActionChangeUsernames` means: a deactivated
        // username has left the set even though its reservation survives.
        //
        // An unchanged active list records nothing. `reorderUsernames` accepts any
        // permutation of the stored set, including the one already stored, and an
        // event whose prev and new sides are identical would show up in the client
        // as a username change nobody made.
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

        // A channel with no ACTIVE username is no longer publicly discoverable,
        // even though its reservations survive.
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

    // The active usernames before and after, as the action's two bare string
    // vectors. Built in one synchronous frame because VectorOfString is a ref
    // struct and cannot survive the caller's awaits.
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

        // By ref or the appended entries are lost when the vector regrows.
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
