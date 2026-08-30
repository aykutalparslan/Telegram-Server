// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

public sealed class EditGroupCallTitleHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private const int MaxTitleLength = 64;

    public EditGroupCallTitleHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

    }

    [TLFunction(Constructors.baseLayer_EditGroupCallTitle)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (EditGroupCallTitle)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        string title = Encoding.UTF8.GetString(request.Title).Trim();

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        if (title.Length > MaxTitleLength)
        {
            return Error(GroupCallErrors.TitleInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Manage);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        GroupCallMutationResult updated = await _groupCallsRepository
            .TrySetTitleAsync(callId, title);
        if (updated.Status == GroupCallMutationStatus.NoChange)
        {
            return Error(GroupCallErrors.GroupCallNotModified);
        }
        if (updated.Status != GroupCallMutationStatus.Updated)
        {
            updated.Call?.Dispose();
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        await UnitOfWork.SaveAsync();

        using TLDto.TLGroupCallState updatedCall = updated.Call!.Value;
        int videoCount = await CountUnmutedVideoAsync(callId);
        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        var updates = new List<byte[]>(1)
        {
            BuildCallUpdateBytes(updatedCall, viewer, access.Peer.Id, videoCount)
        };
        await PushCallUpdateToOtherMembersAsync(updatedCall, access.Peer.Id,
            access.CurrentUserId, videoCount);

        Log.Debug($"📞 editGroupCallTitle call:{callId} by:{access.CurrentUserId} " +
                  $"title:\"{title}\"");
        return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId, updates,
            access.ChatBytes!);
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
