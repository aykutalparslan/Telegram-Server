// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public abstract class ChannelsHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelAdminLogRepository _channelAdminLogRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;

    protected static readonly Regex UsernameRegex = new("(^[a-zA-Z0-9_]{5,32}$)", RegexOptions.Compiled);
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly ICounterFactory _counterFactory;
    protected readonly IdAllocators _ids;
    protected readonly IUpdatesContextFactory _updatesContextFactory;
    protected readonly IUpdatesService _updates;
    protected readonly ISearchEngine _search;
    protected readonly IUploadService _upload;
    protected readonly IPhotoProcessingService _photos;
    protected readonly ILogger _log;
    protected readonly ChatRowStore _chatRows;
    protected readonly UpdateFanout _fanout;

    protected ChannelsHandlerBase(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _authorizationRepository = authorizationRepository;
        _channelAdminLogRepository = channelAdminLogRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _ids = ids;
        _updatesContextFactory = updatesContextFactory;
        _updates = updates;
        _search = search;
        _upload = upload;
        _photos = photos;
        _log = log;
        _chatRows = chatRows;
        _fanout = fanout;
    }

    protected static TLChatParticipantInfo BuildParticipantRow(long chatId, long userId,
        ChatParticipantRole role, long inviterId, int date, byte[]? adminRights,
        byte[]? bannedRights, byte[]? rank)
    {
        var builder = ChatParticipantInfo.Builder()
            .ChatId(chatId)
            .UserId(userId)
            .Role((int)role)
            .InviterId(inviterId)
            .Date(date);
        if (adminRights is { Length: > 0 })
        {
            builder = builder.AdminRights(adminRights);
        }
        if (bannedRights is { Length: > 0 })
        {
            builder = builder.BannedRights(bannedRights);
        }
        if (rank is { Length: > 0 })
        {
            builder = builder.Rank(rank);
        }

        return builder.Build();
    }

    protected async Task<Ferrite.TL.baseLayer.TLUpdates> BuildEmptyChannelUpdates(long authKeyId,
        long actorUserId)
    {
        await _unitOfWork.SaveAsync();
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, actorUserId);
        int seq = await seqCtx.IncrementSeq();
        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(new Vector())
            .Users(new Vector())
            .Chats(new Vector())
            .Date(date)
            .Seq(seq)
            .Build();
    }

    protected static Ferrite.TL.baseLayer.messages.TLAffectedMessages ErrorAffectedMessages(
        ReadOnlySpan<byte> message) =>
        (Ferrite.TL.baseLayer.messages.TLAffectedMessages)RpcErrorGenerator.GenerateError(400, message);

    protected static Ferrite.TL.baseLayer.messages.TLAffectedHistory ErrorAffectedHistory(
        ReadOnlySpan<byte> message) =>
        (Ferrite.TL.baseLayer.messages.TLAffectedHistory)RpcErrorGenerator.GenerateError(400, message);

    protected static long ResolveMessageSenderId(Span<byte> messageSpan)
    {
        var message = (Ferrite.TL.baseLayer.Message)messageSpan;
        if (message.Constructor == Constructors.baseLayer_Message)
        {
            return message.Get_FromIdView().Is(out PeerUser user) ? user.UserId : 0;
        }

        var serviceMessage = (MessageService)messageSpan;
        return serviceMessage.Constructor == Constructors.baseLayer_MessageService &&
               serviceMessage.Get_FromIdView().Is(out PeerUser serviceUser)
            ? serviceUser.UserId
            : 0;
    }

    protected async Task<int> FindMigrationBoundaryMessageId(long userId, long oldChatId,
        long channelId)
    {
        var savedMessages = await _messageRepository.GetMessagesAsync(userId);
        int boundary = 0;
        foreach (var savedMessage in savedMessages)
        {
            using var stored = savedMessage;
            var message = stored.AsSavedMessage().Get_OriginalMessage();
            if (message.Type != TLMessage.MessageType.MessageService)
            {
                continue;
            }

            var service = message.AsMessageService();
            if (!service.Get_PeerIdView().Is(out PeerChat peer) ||
                peer.ChatId != oldChatId)
            {
                continue;
            }
            if (service.Get_ActionView().Is(out MessageActionChatMigrateTo migrateTo) &&
                migrateTo.ChannelId == channelId)
            {
                boundary = Math.Max(boundary, service.Id);
            }
        }

        return boundary;
    }

    protected static long ResolveMigrationActionChatId(Span<byte> messageSpan)
    {
        var service = (MessageService)messageSpan;
        if (service.Constructor != Constructors.baseLayer_MessageService)
        {
            return 0;
        }

        return service.Get_ActionView().Is(out MessageActionChannelMigrateFrom migrateFrom)
            ? migrateFrom.ChatId
            : 0;
    }

    protected async Task<(long CurrentUserId, byte[] ChannelBytes, Ferrite.TL.baseLayer.TLUpdates? Error)>
        PrepareChannelMutation(long authKeyId, long? channelId, bool creatorOnly,
            ChatAdminRightRequirement requiredRight = ChatAdminRightRequirement.Any)
    {
        var (currentUserId, channelBytes, error) =
            await PrepareChannelMutationCore(authKeyId, channelId, creatorOnly, requiredRight);
        return (currentUserId, channelBytes,
            error == null ? null : ErrorUpdates(Encoding.UTF8.GetBytes(error)));
    }

    protected async Task<(long CurrentUserId, byte[] ChannelBytes, string? Error)>
        PrepareChannelMutationCore(long authKeyId, long? channelId, bool creatorOnly,
            ChatAdminRightRequirement requiredRight = ChatAdminRightRequirement.Any)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (0, Array.Empty<byte>(), "AUTH_KEY_INVALID");
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        if (channelId is not > 0)
        {
            return (0, Array.Empty<byte>(), "CHANNEL_INVALID");
        }

        using var channel = await _chatRepository.GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return (0, Array.Empty<byte>(), "CHANNEL_INVALID");
        }

        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(channelId.Value, currentUserId);
        if (participant == null || !IsActiveParticipant(participant.Value))
        {
            participant?.Dispose();
            return (0, Array.Empty<byte>(), "USER_NOT_PARTICIPANT");
        }

        bool authorized = creatorOnly
            ? participant.Value.AsChatParticipantInfo().Role == (int)ChatParticipantRole.Creator
            : ChatRights.HasAdminRight(participant.Value, requiredRight);
        participant.Value.Dispose();
        if (!authorized)
        {
            return (0, Array.Empty<byte>(), "CHAT_ADMIN_REQUIRED");
        }

        return (currentUserId, channel.Value.AsSpan().ToArray(), null);
    }

    protected static TLBool ErrorBool(ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, message);

    protected bool IsUsernameOccupied(string username, long? excludeChatId)
    {
        using var user = _userRepository.GetUserByUsername(username);
        if (user != null)
        {
            return true;
        }
        long? existingChatId = _chatRepository.GetChatIdByUsername(username);
        return existingChatId != null && existingChatId != excludeChatId;
    }

    protected static string ReadChannelUsername(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        if (stored.Type != TLChat.ChatType.Channel)
        {
            return "";
        }
        return ChannelUsernames.Editable(ChannelUsernames.Read(stored.AsChannel()));
    }

    protected byte[] RebuildChannelRowWithUsername(byte[] channelBytes, string username)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        using TLChat updated = ChannelUsernames.Apply(channel,
            ChannelUsernames.WithEditable(ChannelUsernames.Read(channel), username));
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    protected async Task<Ferrite.TL.baseLayer.TLUpdates> EmitChannelServiceMessage(long authKeyId,
        long actorUserId, long channelId, byte[] channelBytes, byte[] actionBytes)
    {
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        var (serviceMessageBytes, pts) =
            await WriteChannelServiceMessage(channelId, actorUserId, actionBytes, date);

        await _unitOfWork.SaveAsync();

        await _fanout.PushUpdateChannelToOtherMembersAsync(channelId, actorUserId);

        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, actorUserId);
        int seq = await seqCtx.IncrementSeq();

        var resultUpdates = new Vector();
        using (TLUpdate updateNewChannelMessage = UpdateNewChannelMessage.Builder()
                   .Message(serviceMessageBytes)
                   .Pts(pts)
                   .PtsCount(1)
                   .Build())
        {
            resultUpdates.AppendTLObject(updateNewChannelMessage.AsSpan());
        }
        using (TLUpdate updateChannel = UpdateChannel.Builder().ChannelId(channelId).Build())
        {
            resultUpdates.AppendTLObject(updateChannel.AsSpan());
        }

        var userVector = new Vector();
        AppendUser(actorUserId, ref userVector, actorUserId);
        var chatVector = new Vector();
        chatVector.AppendTLObject(channelBytes);

        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(resultUpdates)
            .Users(userVector)
            .Chats(chatVector)
            .Date(date)
            .Seq(seq)
            .Build();
    }

    protected async Task<(byte[] MessageBytes, int Pts)> WriteChannelServiceMessage(
        long channelId, long actorUserId, byte[] actionBytes, int date,
        byte[]? replyToHeaderBytes = null)
    {
        StoredMessageWrite write = await MessageStore.PutChannelServiceMessageAsync(_channelMessagesRepository, _counterFactory, channelId, actorUserId, actionBytes, date,
            replyToHeaderBytes);
        return (write.Bytes, write.Pts);
    }

    protected async Task AppendAdminLogEventAsync(long channelId, long actorUserId,
        byte[] actionBytes, int date, string searchText = "")
    {
        long id = await _counterFactory
            .GetCounter($"counter_channel_admin_log_{channelId}")
            .IncrementAndGet();
        using TLAdminLogEvent row = ChannelAdminLogRows.Build(channelId, id, date,
            actorUserId, actionBytes, searchText);
        _channelAdminLogRepository.PutEvent(row);
    }

    protected string ReadUserSearchText(long userId)
    {
        using var user = _userRepository.GetUser(userId);
        if (user == null)
        {
            return string.Empty;
        }

        var view = user.Value.AsUser();
        var parts = new List<string>(3);
        AppendText(parts, view.FirstName);
        AppendText(parts, view.LastName);
        AppendText(parts, view.Username);
        return string.Join(' ', parts);

        static void AppendText(List<string> parts, ReadOnlySpan<byte> value)
        {
            if (value.Length > 0)
            {
                parts.Add(Encoding.UTF8.GetString(value));
            }
        }
    }

    protected static Ferrite.TL.baseLayer.TLUpdates ErrorUpdates(ReadOnlySpan<byte> message) =>
        (Ferrite.TL.baseLayer.TLUpdates)RpcErrorGenerator.GenerateError(400, message);

    protected static long? ResolveInputChannelId(InputChannelView channel)
    {
        if (channel.Is(out InputChannel inputChannel))
        {
            return inputChannel.ChannelId;
        }

        if (channel.Is(out InputChannelFromMessage fromMessage))
        {
            return fromMessage.ChannelId;
        }

        return null;
    }

    protected static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    protected void AppendUsers(long viewerUserId, ref Vector userVector, IEnumerable<long> userIds)
    {
        _fanout.AppendUsers(viewerUserId, ref userVector, userIds);
    }

    protected void AppendUser(long viewerUserId, ref Vector userVector, long userId)
    {
        _fanout.AppendUsers(viewerUserId, ref userVector, new[] { userId });
    }

    protected async Task<(long CurrentUserId, byte[] ChannelBytes, bool Megagroup,
        Ferrite.TL.baseLayer.TLUpdates? Error)>
        ResolveChannelForMembership(long authKeyId, long? channelId)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (0, Array.Empty<byte>(), false, ErrorUpdates("AUTH_KEY_INVALID"u8));
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        if (channelId is not > 0)
        {
            return (0, Array.Empty<byte>(), false, ErrorUpdates("CHANNEL_INVALID"u8));
        }

        using var channel = await _chatRepository.GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return (0, Array.Empty<byte>(), false, ErrorUpdates("CHANNEL_INVALID"u8));
        }

        bool megagroup = channel.Value.AsChannel().Megagroup;
        return (currentUserId, channel.Value.AsSpan().ToArray(), megagroup, null);
    }

    protected async Task<Ferrite.TL.baseLayer.TLUpdates> BuildChannelUpdates(long authKeyId,
        long actorUserId, byte[] channelBytes, IReadOnlyCollection<long> extraUserIds)
    {
        await _unitOfWork.SaveAsync();
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        return await _fanout.BuildChannelStateResultAsync(authKeyId, actorUserId,
            channelBytes, extraUserIds, date);
    }

    protected static List<long> ResolveInputUserIds(Vector users, long selfUserId)
    {
        var result = new List<long>(users.Count);
        for (int i = 0; i < users.Count; i++)
        {
            InputUserView user = users.ReadTLObject();
            long? userId = ResolveInputUserId(user, selfUserId);
            if (userId is > 0)
            {
                result.Add(userId.Value);
            }
        }

        return result;
    }

    protected static long? ResolveInputUserId(InputUserView user, long selfUserId)
    {
        if (user.Is(out InputUser inputUser))
        {
            return inputUser.UserId;
        }

        if (user.Is(out InputUserFromMessage fromMessage))
        {
            return fromMessage.UserId;
        }

        if (user.Is(out InputUserSelf _))
        {
            return selfUserId;
        }

        return null;
    }

    protected static long? ResolveInputPeerUserId(InputPeerView peer, long selfUserId)
    {
        if (peer.Is(out InputPeerUser user))
        {
            return user.UserId;
        }

        if (peer.Is(out InputPeerSelf _))
        {
            return selfUserId;
        }

        return null;
    }

    protected byte[] BuildChannelParticipantBytes(TLChatParticipantInfo storedInfo, long currentUserId)
    {
        var info = storedInfo.AsChatParticipantInfo();
        long userId = info.UserId;
        byte[]? adminRights = info.Flags[0] ? info.AdminRights.ToArray() : null;
        byte[]? bannedRights = info.Flags[1] ? info.BannedRights.ToArray() : null;
        byte[]? rank = info.Flags[2] && info.Rank.Length > 0 ? info.Rank.ToArray() : null;
        switch (info.Role)
        {
            case (int)ChatParticipantRole.Creator:
            {
                var builder = ChannelParticipantCreator.Builder()
                    .UserId(userId)
                    .AdminRights(adminRights ?? BuildFullAdminRights());
                if (rank != null)
                {
                    builder = builder.Rank(rank);
                }
                using Ferrite.TL.baseLayer.TLChannelParticipant p = builder.Build();
                return p.AsSpan().ToArray();
            }
            case (int)ChatParticipantRole.Admin:
            {
                var builder = ChannelParticipantAdmin.Builder()
                    .UserId(userId)
                    .PromotedBy(info.InviterId)
                    .Date(info.Date)
                    .AdminRights(adminRights ?? BuildFullAdminRights());
                if (info.InviterId == currentUserId)
                {
                    builder = builder.CanEdit(true);
                }
                if (rank != null)
                {
                    builder = builder.Rank(rank);
                }
                using Ferrite.TL.baseLayer.TLChannelParticipant p = builder.Build();
                return p.AsSpan().ToArray();
            }
            case (int)ChatParticipantRole.Banned:
            {
                return BuildBannedParticipantBytes(info, left: true,
                    bannedRights ?? BuildKickedBannedRights());
            }
            case (int)ChatParticipantRole.Left:
            {
                if (bannedRights != null)
                {
                    return BuildBannedParticipantBytes(info, left: true, bannedRights);
                }
                using TLPeer peer = new PeerUser(userId);
                using Ferrite.TL.baseLayer.TLChannelParticipant p = ChannelParticipantLeft.Builder()
                    .Peer(peer.AsSpan())
                    .Build();
                return p.AsSpan().ToArray();
            }
            default:
            {
                if (bannedRights != null)
                {
                    return BuildBannedParticipantBytes(info, left: false, bannedRights);
                }
                if (userId == currentUserId)
                {
                    using Ferrite.TL.baseLayer.TLChannelParticipant self = ChannelParticipantSelf.Builder()
                        .UserId(userId)
                        .InviterId(info.InviterId)
                        .Date(info.Date)
                        .Build();
                    return self.AsSpan().ToArray();
                }

                using Ferrite.TL.baseLayer.TLChannelParticipant member = ChannelParticipant.Builder()
                    .UserId(userId)
                    .Date(info.Date)
                    .Build();
                return member.AsSpan().ToArray();
            }
        }
    }

    protected static byte[] BuildBannedParticipantBytes(ChatParticipantInfo info, bool left,
        byte[] bannedRights)
    {
        using TLPeer peer = new PeerUser(info.UserId);
        var builder = ChannelParticipantBanned.Builder()
            .Peer(peer.AsSpan())
            .KickedBy(info.InviterId)
            .Date(info.Date)
            .BannedRights(bannedRights);
        if (left)
        {
            builder = builder.Left(true);
        }
        using Ferrite.TL.baseLayer.TLChannelParticipant p = builder.Build();
        return p.AsSpan().ToArray();
    }

    protected static byte[] BuildLeftParticipantBytes(long userId)
    {
        using TLPeer peer = new PeerUser(userId);
        using Ferrite.TL.baseLayer.TLChannelParticipant participant =
            ChannelParticipantLeft.Builder().Peer(peer.AsSpan()).Build();
        return participant.AsSpan().ToArray();
    }

    protected static byte[] BuildKickedBannedRights()
    {
        using var rights = ChatBannedRights.Builder()
            .ViewMessages(true)
            .UntilDate(0)
            .Build();
        return rights.ToReadOnlySpan().ToArray();
    }

    protected static byte[] BuildFullAdminRights()
    {
        using var rights = ChatAdminRights.Builder()
            .ChangeInfo(true)
            .PostMessages(true)
            .EditMessages(true)
            .DeleteMessages(true)
            .BanUsers(true)
            .InviteUsers(true)
            .PinMessages(true)
            .AddAdmins(true)
            .ManageCall(true)
            .ManageTopics(true)
            .Build();
        return rights.ToReadOnlySpan().ToArray();
    }

    protected enum ParticipantFilterKind
    {
        Recent,
        Admins,
        Kicked,
        Banned,
        Bots,
        Contacts,
        Search,
        Mentions,
    }

    protected static (ParticipantFilterKind Kind, string Query) ReadParticipantsFilter(
        ChannelParticipantsFilterView filter)
    {
        if (filter.Is(out ChannelParticipantsAdmins _))
        {
            return (ParticipantFilterKind.Admins, string.Empty);
        }
        if (filter.Is(out ChannelParticipantsKicked kicked))
        {
            return (ParticipantFilterKind.Kicked, ReadQuery(kicked.Q));
        }
        if (filter.Is(out ChannelParticipantsBanned banned))
        {
            return (ParticipantFilterKind.Banned, ReadQuery(banned.Q));
        }
        if (filter.Is(out ChannelParticipantsBots _))
        {
            return (ParticipantFilterKind.Bots, string.Empty);
        }
        if (filter.Is(out ChannelParticipantsSearch search))
        {
            return (ParticipantFilterKind.Search, ReadQuery(search.Q));
        }
        if (filter.Is(out ChannelParticipantsContacts contacts))
        {
            return (ParticipantFilterKind.Contacts, ReadQuery(contacts.Q));
        }
        if (filter.Is(out ChannelParticipantsMentions _))
        {
            return (ParticipantFilterKind.Mentions, string.Empty);
        }

        return (ParticipantFilterKind.Recent, string.Empty);
    }

    protected static string ReadQuery(ReadOnlySpan<byte> q) =>
        q.Length == 0 ? string.Empty : Encoding.UTF8.GetString(q);

    protected static bool MatchesParticipantFilter(int role, bool hasBannedRights,
        ParticipantFilterKind kind) => kind switch
    {
        ParticipantFilterKind.Admins =>
            role is (int)ChatParticipantRole.Creator or (int)ChatParticipantRole.Admin,
        ParticipantFilterKind.Kicked => role == (int)ChatParticipantRole.Banned,
        ParticipantFilterKind.Banned =>
            hasBannedRights && role != (int)ChatParticipantRole.Banned,
        ParticipantFilterKind.Bots => false,
        _ => role != (int)ChatParticipantRole.Banned && role != (int)ChatParticipantRole.Left,
    };

    protected bool MatchesQuery(long userId, string query)
    {
        using var user = _userRepository.GetUser(userId);
        if (user == null)
        {
            return false;
        }

        var firstName = user.Value.AsUser().FirstName;
        return firstName.Length > 0 &&
               Encoding.UTF8.GetString(firstName).Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    protected static Ferrite.TL.baseLayer.messages.TLInvitedUsers ErrorInvitedUsers(
        ReadOnlySpan<byte> message) =>
        (Ferrite.TL.baseLayer.messages.TLInvitedUsers)RpcErrorGenerator.GenerateError(400, message);

    protected static void AppendMissingInvitees(ref Vector missingInvitees, IEnumerable<long> userIds)
    {
        foreach (long userId in userIds)
        {
            using var missingInvitee = MissingInvitee.Builder().UserId(userId).Build();
            missingInvitees.AppendTLObject(missingInvitee.ToReadOnlySpan());
        }
    }

}
