// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Channels;

public static class ModerationReportKind
{
    public const int ProfilePhoto = 1;
    public const int PeerSpam = 2;
    public const int MessageOption = 3;
    public const int Reaction = 4;
    public const int ChannelSpam = 5;
    public const int MessageDelivery = 6;
    public const int AntiSpamFalsePositive = 7;
}

public readonly record struct ActionBarState(bool Hidden, bool ReportedSpam)
{
    public bool IsDismissed => Hidden || ReportedSpam;
}

public static class MessageReportMenu
{
    public const string Title = "Report";

    public static readonly (string Option, string Text)[] Options =
    [
        ("spam", "Spam"),
        ("violence", "Violence"),
        ("child_abuse", "Child Abuse"),
        ("pornography", "Pornography"),
        ("illegal_drugs", "Illegal Drugs"),
        ("personal_details", "Personal Details"),
        ("copyright", "Copyright"),
        ("fake", "Fake Account"),
        ("other", "Other"),
    ];

    public static bool IsKnownOption(ReadOnlySpan<byte> option)
    {
        foreach ((string token, _) in Options)
        {
            if (option.SequenceEqual(Encoding.UTF8.GetBytes(token)))
            {
                return true;
            }
        }
        return false;
    }
}

public sealed class ModerationStore
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IContactsRepository _contactsRepository;
    private readonly IModerationRepository _moderationRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IAtomicCounter _reportIds;

    public ModerationStore(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IContactsRepository contactsRepository, IModerationRepository moderationRepository, IUserRepository userRepository, TimeProvider timeProvider,
        ICounterFactory counterFactory)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _contactsRepository = contactsRepository;
        _moderationRepository = moderationRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _reportIds = counterFactory.GetCounter("counter_moderation_report_id");
    }

    public async ValueTask<ActionBarState> GetActionBarAsync(long userId,
        TLPeer.PeerType peerType, long peerId)
    {
        using TLPeerActionBarState? stored = await _moderationRepository
            .GetActionBarStateAsync(userId, (int)peerType, peerId);
        if (stored == null)
        {
            return default;
        }

        var row = stored.Value.AsPeerActionBarState();
        return new ActionBarState(row.Flags[0], row.Flags[1]);
    }

    public async ValueTask<bool> ShouldOfferPrivateActionBarAsync(long viewerUserId,
        long peerUserId)
    {
        if (peerUserId <= 0 || peerUserId == viewerUserId ||
            _contactsRepository.HasContact(viewerUserId, peerUserId))
        {
            return false;
        }

        ActionBarState dismissed = await GetActionBarAsync(viewerUserId,
            TLPeer.PeerType.PeerUser, peerUserId);
        return !dismissed.Hidden && !dismissed.ReportedSpam;
    }

    public async ValueTask<bool> SetActionBarAsync(long userId,
        TLPeer.PeerType peerType, long peerId, bool hidden, bool reportedSpam)
    {
        ActionBarState current = await GetActionBarAsync(userId, peerType, peerId);
        bool nextHidden = current.Hidden || hidden;
        bool nextReported = current.ReportedSpam || reportedSpam;
        int date = Now();

        var builder = PeerActionBarState.Builder()
            .UserId(userId)
            .PeerType((int)peerType)
            .PeerId(peerId)
            .Date(date);
        if (nextHidden)
        {
            builder = builder.Hidden(true);
        }
        if (nextReported)
        {
            builder = builder.ReportedSpam(true);
        }

        using TLPeerActionBarState row = builder.Build();
        return _moderationRepository.PutActionBarState(row);
    }

    public async ValueTask<long> RecordReportAsync(long reporterUserId, int kind,
        TLPeer.PeerType peerType, long peerId, string? option = null,
        string? comment = null, IReadOnlyList<int>? messageIds = null,
        long? photoId = null, long? subjectUserId = null)
    {
        long reportId = await _reportIds.IncrementAndGet();
        int date = Now();

        var builder = ModerationReport.Builder()
            .ReportId(reportId)
            .ReporterUserId(reporterUserId)
            .Kind(kind)
            .PeerType((int)peerType)
            .PeerId(peerId)
            .Date(date);
        if (!string.IsNullOrEmpty(option))
        {
            builder = builder.Option(Encoding.UTF8.GetBytes(option));
        }
        if (!string.IsNullOrEmpty(comment))
        {
            builder = builder.Comment(Encoding.UTF8.GetBytes(comment));
        }
        if (messageIds is { Count: > 0 })
        {
            var ids = new VectorOfInt();
            foreach (int id in messageIds)
            {
                ids.Append(id);
            }
            builder = builder.MessageIds(ids);
        }
        if (photoId is { } photo)
        {
            builder = builder.PhotoId(photo);
        }
        if (subjectUserId is { } subject)
        {
            builder = builder.SubjectUserId(subject);
        }

        using TLModerationReport report = builder.Build();
        return _moderationRepository.PutReport(report) ? reportId : 0;
    }

    public async ValueTask<string?> ValidateReportablePeerAsync(long callerUserId,
        TLPeer.PeerType peerType, long peerId)
    {
        if (peerId <= 0)
        {
            return "PEER_ID_INVALID";
        }

        if (peerType == TLPeer.PeerType.PeerUser)
        {
            using TLUser? user = _userRepository.GetUser(peerId);
            return user == null ? "PEER_ID_INVALID" : null;
        }

        if (peerType == TLPeer.PeerType.PeerChat)
        {
            using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(peerId, callerUserId);
            if (participant == null)
            {
                return "USER_NOT_PARTICIPANT";
            }
            int role = participant.Value.AsChatParticipantInfo().Role;
            return role is (int)ChatParticipantRole.Banned or (int)ChatParticipantRole.Left
                ? "USER_NOT_PARTICIPANT"
                : null;
        }

        if (peerType == TLPeer.PeerType.PeerChannel)
        {
            return await ChannelAccess.ValidateReadAsync(_chatRepository, _chatParticipantsRepository, peerId,
                callerUserId);
        }

        return "PEER_ID_INVALID";
    }

    private int Now() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
}
