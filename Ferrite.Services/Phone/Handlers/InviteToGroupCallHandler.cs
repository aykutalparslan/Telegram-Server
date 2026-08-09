// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.inviteToGroupCall. Writes the invite service action into the hosting
/// peer's ordinary message box. An invite is signaling only: it never creates a
/// participant row or reserves a media source before the invited user joins.
/// </summary>
public sealed class InviteToGroupCallHandler : GroupCallHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IUserRepository _userRepository;

    private const int MaxInvitedUsers = 10;
    private readonly GroupCallActionMessages _actions;

    public InviteToGroupCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IUserRepository userRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log,
        GroupCallActionMessages actions)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _groupCallsRepository = groupCallsRepository;

        _userRepository = userRepository;

        _actions = actions;
    }

    [TLFunction(Constructors.baseLayer_InviteToGroupCall)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (InviteToGroupCall)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        List<RequestedUser> requestedUsers = ReadUsers(request.Users);
        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        if (requestedUsers.Count > MaxInvitedUsers)
        {
            return Error(GroupCallErrors.UsersTooMuch);
        }
        if (requestedUsers.Any(x => !x.Valid))
        {
            return Error(GroupCallErrors.UserIdInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Participate);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        TLDto.TLGroupCallState call = resolution.Call!.Value;
        if (call.AsGroupCallState().State != (int)GroupCallPersistenceState.Active)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        List<long> inviteeIds = requestedUsers
            .Select(x => x.IsSelf ? access.CurrentUserId : x.UserId)
            .Where(id => id != access.CurrentUserId)
            .Distinct()
            .ToList();
        if (inviteeIds.Count == 0)
        {
            return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId,
                Array.Empty<byte[]>(), access.ChatBytes!);
        }

        Dictionary<long, RequestedUser> requestedById = requestedUsers
            .Where(x => !x.IsSelf)
            .GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.First());
        IReadOnlyCollection<TLDto.TLChatParticipantInfo> memberRows = await _chatParticipantsRepository.GetParticipantsAsync(access.Peer.Id);
        HashSet<long> activeMemberIds = new();
        try
        {
            foreach (TLDto.TLChatParticipantInfo member in memberRows)
            {
                var view = member.AsChatParticipantInfo();
                if (view.Role is not ((int)ChatParticipantRole.Banned) and
                    not ((int)ChatParticipantRole.Left))
                {
                    activeMemberIds.Add(view.UserId);
                }
            }
        }
        finally
        {
            foreach (TLDto.TLChatParticipantInfo member in memberRows)
            {
                member.Dispose();
            }
        }

        foreach (long inviteeId in inviteeIds)
        {
            RequestedUser requested = requestedById[inviteeId];
            using TLUser? user = _userRepository.GetUser(inviteeId);
            if (!IsValidUser(user, requested))
            {
                return Error(GroupCallErrors.UserIdInvalid);
            }
            if (!activeMemberIds.Contains(inviteeId))
            {
                return Error(GroupCallErrors.UserNotParticipant);
            }

            using TLDto.TLGroupCallParticipantState? joined = await _groupCallsRepository.GetParticipantAsync(callId, inviteeId);
            if (joined != null && !joined.Value.AsGroupCallParticipantState().Left)
            {
                return Error(GroupCallErrors.UserAlreadyInvited);
            }
        }

        byte[] actionBytes;
        using (TLInputGroupCall inputCall = GroupCallBuilders.BuildInputGroupCall(call))
        using (TLMessageAction action = GroupCallActionMessages.BuildInviteAction(
                   inputCall, inviteeIds))
        {
            actionBytes = action.AsSpan().ToArray();
        }

        Log.Debug($"📞 inviteToGroupCall call:{callId} user:{access.CurrentUserId} " +
                  $"invitees:{inviteeIds.Count}");
        return await _actions.EmitAsync(authKeyId, access.CurrentUserId, access.Kind,
            access.Peer.Id, access.ChatBytes!, actionBytes,
            relatedUserIds: inviteeIds, peerStateChanged: false);
    }

    private readonly record struct RequestedUser(bool Valid, bool IsSelf, long UserId,
        long? AccessHash);

    private static List<RequestedUser> ReadUsers(Vector users)
    {
        var result = new List<RequestedUser>(users.Count);
        for (int i = 0; i < users.Count; i++)
        {
            InputUserView user = users.ReadTLObject();
            if (user.Is(out InputUserSelf _))
            {
                result.Add(new RequestedUser(true, true, 0, null));
            }
            else if (user.Is(out InputUser input) && input.UserId > 0)
            {
                result.Add(new RequestedUser(true, false, input.UserId,
                    input.AccessHash));
            }
            else if (user.Is(out InputUserFromMessage fromMessage) &&
                     fromMessage.UserId > 0)
            {
                result.Add(new RequestedUser(true, false, fromMessage.UserId, null));
            }
            else
            {
                result.Add(default);
            }
        }

        return result;
    }

    private static bool IsValidUser(TLUser? user, RequestedUser requested)
    {
        if (user == null || user.Value.Type != TLUser.UserType.User)
        {
            return false;
        }

        var view = user.Value.AsUser();
        return view.Id == requested.UserId && !view.Deleted && !view.Bot &&
               (requested.AccessHash == null ||
                (view.Flags[0] && view.AccessHash == requested.AccessHash.Value));
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
