// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.toggleGroupCallSettings. join_muted and the invite rotation are
/// CALL-ONLY settings behind the manage-call gate: neither consumes a
/// participants version (a version step with no matching participants update
/// would make every pinned client resync its list), and the result is
/// viewer-correct updateGroupCall on both channels. join_muted only affects rows
/// written by FUTURE joins; nobody already in the call is muted retroactively.
/// reset_invite_hash rotates the call's invite generation, which invalidates
/// every outstanding invite link at once. A request that changes nothing answers
/// GROUPCALL_NOT_MODIFIED, which pinned TDLib maps back to success.
/// </summary>
public sealed class ToggleGroupCallSettingsHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    public ToggleGroupCallSettingsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

    }

    [TLFunction(Constructors.baseLayer_ToggleGroupCallSettings)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        bool? joinMuted = null;
        var request = (ToggleGroupCallSettings)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        bool resetInviteHash = request.ResetInviteHash;
        if (request.Flags[0])
        {
            joinMuted = request.JoinMuted;
        }

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Manage);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;

        bool joinMutedChanged = false;
        TLDto.TLGroupCallState? latest = null;
        try
        {
            if (joinMuted is { } muted)
            {
                GroupCallMutationResult mutedResult = await _groupCallsRepository.TrySetJoinMutedAsync(callId, muted);
                if (mutedResult.Status == GroupCallMutationStatus.Updated)
                {
                    joinMutedChanged = true;
                    latest = mutedResult.Call!.Value;
                }
                else
                {
                    mutedResult.Call?.Dispose();
                    if (mutedResult.Status != GroupCallMutationStatus.NoChange)
                    {
                        return Error(GroupCallErrors.GroupCallInvalid);
                    }
                }
            }

            if (resetInviteHash)
            {
                GroupCallMutationResult rotated = await _groupCallsRepository
                    .TryRotateInviteGenerationAsync(callId);
                if (rotated.Status != GroupCallMutationStatus.Updated)
                {
                    rotated.Call?.Dispose();
                    return Error(GroupCallErrors.GroupCallInvalid);
                }
                latest?.Dispose();
                latest = rotated.Call!.Value;
            }

            if (latest == null)
            {
                // Neither half changed anything; pinned TDLib treats this exact
                // error as success.
                return Error(GroupCallErrors.GroupCallNotModified);
            }

            await UnitOfWork.SaveAsync();

            TLDto.TLGroupCallState updatedCall = latest.Value;
            int videoCount = await CountUnmutedVideoAsync(callId);
            GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
                access.CanManageCall);
            var updates = new List<byte[]>(1)
            {
                BuildCallUpdateBytes(updatedCall, viewer, access.Peer.Id, videoCount)
            };
            await PushCallUpdateToOtherMembersAsync(updatedCall, access.Peer.Id,
                access.CurrentUserId, videoCount);

            Log.Debug($"📞 toggleGroupCallSettings call:{callId} " +
                      $"by:{access.CurrentUserId} join_muted:" +
                      $"{joinMuted?.ToString() ?? "-"} changed:{joinMutedChanged} " +
                      $"reset_invite:{resetInviteHash}");
            return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId,
                updates, access.ChatBytes!);
        }
        finally
        {
            latest?.Dispose();
        }
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
