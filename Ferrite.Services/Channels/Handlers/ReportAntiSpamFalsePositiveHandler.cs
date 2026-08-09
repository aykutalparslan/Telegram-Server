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
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Records that an administrator considers one message a false positive of the
/// aggressive anti-spam filter.
///
/// It is an ADMIN report about the filter, not a report about a user, so it is
/// appended to the same moderation ledger every other report uses rather than
/// being counted anywhere: Ferrite runs no anti-spam classifier, so there is no
/// model for the report to correct, and inventing a counter would be a
/// placeholder. The report is only accepted where the filter is actually on --
/// reporting a false positive of a filter that is off would record something
/// that cannot have happened.
/// </summary>
public sealed class ReportAntiSpamFalsePositiveHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly ModerationStore _moderation;

    public ReportAntiSpamFalsePositiveHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout, ModerationStore moderation)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _moderation = moderation;
    }

    [TLFunction(Constructors.baseLayer_ReportAntiSpamFalsePositive)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReportAntiSpamFalsePositive)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        int messageId = request.MsgId;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutationCore(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.DeleteMessages);
        if (error != null)
        {
            return ErrorBool(Encoding.UTF8.GetBytes(error));
        }

        if (!ReadChannelFacts(channelBytes).Megagroup)
        {
            return ErrorBool("MEGAGROUP_REQUIRED"u8);
        }
        if (messageId <= 0)
        {
            return ErrorBool("MSG_ID_INVALID"u8);
        }

        long id = channelId!.Value;
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        using (TLChannelAdminState state = await LoadAdminStateAsync(id, date))
        {
            if (!state.AsChannelAdminState().Antispam)
            {
                return ErrorBool("CHAT_NOT_MODIFIED"u8);
            }
        }

        using (TLSavedMessage? stored = await _channelMessagesRepository
                   .GetMessageAsync(id, messageId))
        {
            if (stored == null)
            {
                return ErrorBool("MSG_ID_INVALID"u8);
            }
        }

        long reportId = await _moderation.RecordReportAsync(currentUserId,
            ModerationReportKind.AntiSpamFalsePositive, TLPeer.PeerType.PeerChannel,
            id, messageIds: [messageId]);
        if (reportId == 0)
        {
            return ErrorBool("MSG_ID_INVALID"u8);
        }

        await _unitOfWork.SaveAsync();
        _log.Debug($"📣 ReportAntiSpamFalsePositive user:{currentUserId} " +
                   $"channel:{id} message:{messageId} report:{reportId}");
        return new BoolTrue();
    }
}
