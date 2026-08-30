// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

public sealed class InviteConferenceCallParticipantHandler : ConferenceCallHandlerBase
{
    private readonly IUserRepository _userRepository;

    private readonly MessageStore _messages;

    public InviteConferenceCallParticipantHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        UpdateFanout fanout, GroupCallChatLink chatLink,
        IUpdatesContextFactory updatesContexts, IMTProtoTime time,
        GroupCallVideoOptions videoOptions, GroupCallMediaSourceMap sourceMap,
        ILogger log, IGroupCallChainService chain, MessageStore messages)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, messageRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log, chain)
    {
        _userRepository = userRepository;

        _messages = messages;
    }

    [TLFunction(Constructors.baseLayer_InviteConferenceCallParticipant)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (InviteConferenceCallParticipant)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        bool video = request.Video;
        bool userRead = TryReadUser(request.Get_UserIdView(), out long inviteeId,
            out long? inviteeAccessHash);

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        if (!userRead)
        {
            return Error(GroupCallErrors.UserIdInvalid);
        }

        using ConferenceResolution resolution = await ResolveConferenceAsync(authKeyId,
            callId, accessHash);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }
        if (!resolution.IsParticipant)
        {
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }
        if (inviteeId == resolution.CurrentUserId)
        {
            return Error(GroupCallErrors.UserIdInvalid);
        }

        using (TLUser? invitee = _userRepository.GetUser(inviteeId))
        {
            if (!IsInvitableUser(invitee, inviteeId, inviteeAccessHash))
            {
                return Error(GroupCallErrors.UserIdInvalid);
            }
        }
        if (await IsActiveParticipantAsync(callId, inviteeId))
        {
            return Error(GroupCallErrors.UserAlreadyInvited);
        }

        byte[] actionBytes = BuildInviteAction(callId, video);
        int date = Now();
        long inviterUserId = resolution.CurrentUserId;

        StoredMessageWrite inviterWrite = await _messages.PutPrivateServiceMessageAsync(
            inviterUserId, authKeyId, inviteeId, inviterUserId, outgoing: true,
            actionBytes, date);
        StoredMessageWrite inviteeWrite = await _messages.PutPrivateServiceMessageAsync(
            inviteeId, null, inviterUserId, inviterUserId, outgoing: false, actionBytes,
            date);
        await UnitOfWork.SaveAsync();

        await Fanout.EnqueueNewMessageAsync(inviteeId, inviteeWrite.Bytes,
            inviteeWrite.Pts);

        Log.Debug($"📞 inviteConferenceCallParticipant call:{callId} " +
                  $"user:{inviterUserId} invitee:{inviteeId} video:{video}");
        using TLUpdate update = UpdateNewMessage.Builder()
            .Message(inviterWrite.Bytes)
            .Pts(inviterWrite.Pts)
            .PtsCount(1)
            .Build();
        return await BuildConferenceResultAsync(authKeyId, inviterUserId,
            new[] { update.AsSpan().ToArray() }, new[] { inviteeId });
    }

    private static byte[] BuildInviteAction(long callId, bool video)
    {
        var builder = MessageActionConferenceCall.Builder().CallId(callId);
        if (video)
        {
            builder = builder.Video(true);
        }

        using TLMessageAction action = builder.Build();
        return action.AsSpan().ToArray();
    }

    private static bool TryReadUser(InputUserView view, out long userId,
        out long? accessHash)
    {
        if (view.Is(out InputUser user) && user.UserId > 0)
        {
            userId = user.UserId;
            accessHash = user.AccessHash;
            return true;
        }
        if (view.Is(out InputUserFromMessage fromMessage) && fromMessage.UserId > 0)
        {
            userId = fromMessage.UserId;
            accessHash = null;
            return true;
        }

        userId = 0;
        accessHash = null;
        return false;
    }

    private static bool IsInvitableUser(TLUser? user, long userId, long? accessHash)
    {
        if (user == null || user.Value.Type != TLUser.UserType.User)
        {
            return false;
        }

        var view = user.Value.AsUser();
        return view.Id == userId && !view.Deleted && !view.Bot &&
               (accessHash == null ||
                (view.Flags[0] && view.AccessHash == accessHash.Value));
    }
}
