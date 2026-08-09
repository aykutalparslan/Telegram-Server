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

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.exportGroupCallInvite. Public members may export a listen-only deep
/// link, while only manage-call admins may mint the dedicated invite hash that
/// grants can_self_unmute on a muted join.
/// </summary>
public sealed class ExportGroupCallInviteHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private const int HashAttempts = 8;

    public ExportGroupCallInviteHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

    }

    [TLFunction(Constructors.baseLayer_ExportGroupCallInvite)]
    public async ValueTask<TLExportedGroupCallInvite> Handle(long authKeyId, TLBytes q)
    {
        var request = (ExportGroupCallInvite)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        bool canSelfUnmute = request.CanSelfUnmute;
        if (!callRead)
        {
            return Error(400, GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Participate);
        if (resolution.Error != null)
        {
            return Error(400, resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        TLDto.TLGroupCallState call = resolution.Call!.Value;
        if (call.AsGroupCallState().State != (int)GroupCallPersistenceState.Active)
        {
            return Error(400, GroupCallErrors.GroupCallInvalid);
        }
        if (!TryReadPublicUsername(access, out string username, out bool liveStream))
        {
            return Error(403, GroupCallErrors.PublicChannelMissing);
        }
        if (canSelfUnmute && !access.CanManageCall)
        {
            return Error(400, GroupCallErrors.ChatAdminRequired);
        }

        string? hash = null;
        if (canSelfUnmute)
        {
            hash = await StoreInviteAsync(callId,
                call.AsGroupCallState().InviteGeneration, access.CurrentUserId);
            if (hash == null)
            {
                return Error(400, GroupCallErrors.GroupCallInvalid);
            }
            await UnitOfWork.SaveAsync();
        }

        string link = GroupCallInviteLinks.Build(username, liveStream, hash);
        Log.Debug($"📞 exportGroupCallInvite call:{callId} user:{access.CurrentUserId} " +
                  $"speak:{canSelfUnmute} generation:{call.AsGroupCallState().InviteGeneration}");
        return ExportedGroupCallInvite.Builder()
            .Link(Encoding.UTF8.GetBytes(link))
            .Build();
    }

    private async ValueTask<string?> StoreInviteAsync(long callId, int generation,
        long creatorUserId)
    {
        for (int attempt = 0; attempt < HashAttempts; attempt++)
        {
            string hash = GroupCallInviteLinks.GenerateHash();
            using TLDto.TLGroupCallInvite invite = TLDto.GroupCallInvite.Builder()
                .CallId(callId)
                .Hash(Encoding.UTF8.GetBytes(hash))
                .Generation(generation)
                .CreatorUserId(creatorUserId)
                .Date(Now())
                .CanSelfUnmute(true)
                .Build();
            if (await _groupCallsRepository.PutInviteAsync(invite))
            {
                return hash;
            }

            using TLDto.TLGroupCallState? current = await _groupCallsRepository.GetCallAsync(callId);
            if (current == null || current.Value.AsGroupCallState().State !=
                (int)GroupCallPersistenceState.Active)
            {
                return null;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique group-call invite hash.");
    }

    private static bool TryReadPublicUsername(GroupCallPeerAccess access,
        out string username, out bool liveStream)
    {
        username = string.Empty;
        liveStream = false;
        if (access.Kind == GroupCallPeerKind.BasicGroup || access.ChatBytes == null)
        {
            return false;
        }

        var channel = (Channel)access.ChatBytes.AsSpan();
        if (channel.Username.IsEmpty)
        {
            return false;
        }

        username = Encoding.UTF8.GetString(channel.Username);
        liveStream = channel.Broadcast;
        return true;
    }

    private static TLExportedGroupCallInvite Error(int code, string message) =>
        (TLExportedGroupCallInvite)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
