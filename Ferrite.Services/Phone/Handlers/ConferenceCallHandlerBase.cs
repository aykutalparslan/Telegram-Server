// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Phone.Handlers;

public sealed class ConferenceResolution : IDisposable
{
    private ConferenceResolution(TLDto.TLGroupCallState? call, long currentUserId,
        bool isCreator, bool isParticipant, long accessHash, string? error)
    {
        Call = call;
        CurrentUserId = currentUserId;
        IsCreator = isCreator;
        IsParticipant = isParticipant;
        AccessHash = accessHash;
        Error = error;
    }

    public TLDto.TLGroupCallState? Call { get; }

    public long CurrentUserId { get; }

    public bool IsCreator { get; }

    public bool IsParticipant { get; }

    public long AccessHash { get; }

    public string? Error { get; }

    public static ConferenceResolution Failed(string error) =>
        new(null, 0, false, false, 0, error);

    public static ConferenceResolution Resolved(TLDto.TLGroupCallState call,
        long currentUserId, bool isCreator, bool isParticipant, long accessHash) =>
        new(call, currentUserId, isCreator, isParticipant, accessHash, null);

    public void Dispose() => Call?.Dispose();
}

public readonly record struct ConferenceCallRef(long CallId, long AccessHash,
    int InviteMsgId);

public abstract class ConferenceCallHandlerBase : GroupCallHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IGroupCallsRepository _groupCallsRepository;
    private readonly IMessageRepository _messageRepository;

    protected ConferenceCallHandlerBase(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallChainService chain)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _authorizationRepository = authorizationRepository;
        _groupCallsRepository = groupCallsRepository;
        _messageRepository = messageRepository;

        Chain = chain;
    }

    protected readonly IGroupCallChainService Chain;

    protected static TLUpdates Error(string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    protected async ValueTask<long> ResolveUserIdAsync(long authKeyId)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        return auth?.AsAuthInfo().UserId ?? 0;
    }

    public static bool TryReadConferenceRef(InputGroupCallView view,
        out ConferenceCallRef reference)
    {
        if (view.Is(out InputGroupCall call) && call.Id != 0)
        {
            reference = new ConferenceCallRef(call.Id, call.AccessHash, 0);
            return true;
        }
        if (view.Is(out InputGroupCallInviteMessage invite) && invite.MsgId != 0)
        {
            reference = new ConferenceCallRef(0, 0, invite.MsgId);
            return true;
        }

        reference = default;
        return false;
    }

    protected ValueTask<ConferenceResolution> ResolveConferenceAsync(long authKeyId,
        long callId, long accessHash, bool requireActive = true,
        CancellationToken cancellationToken = default) =>
        ResolveConferenceAsync(authKeyId, new ConferenceCallRef(callId, accessHash, 0),
            requireActive, cancellationToken);

    protected async ValueTask<ConferenceResolution> ResolveConferenceAsync(long authKeyId,
        ConferenceCallRef reference, bool requireActive = true,
        CancellationToken cancellationToken = default)
    {
        long currentUserId = await ResolveUserIdAsync(authKeyId);
        if (currentUserId == 0)
        {
            return ConferenceResolution.Failed(GroupCallErrors.AuthKeyInvalid);
        }

        long callId = reference.CallId;
        long? presentedHash = callId != 0 ? reference.AccessHash : null;
        if (callId == 0)
        {
            callId = await ReadInvitedCallIdAsync(currentUserId, reference.InviteMsgId);
            if (callId == 0)
            {
                return ConferenceResolution.Failed(GroupCallErrors.GroupCallInvalid);
            }
        }

        TLDto.TLGroupCallState? call = await _groupCallsRepository
            .GetCallAsync(callId, cancellationToken);
        if (call == null)
        {
            return ConferenceResolution.Failed(GroupCallErrors.GroupCallInvalid);
        }

        var view = call.Value.AsGroupCallState();
        if ((presentedHash != null && view.AccessHash != presentedHash.Value) ||
            !view.Conference ||
            (requireActive && view.State != (int)GroupCallPersistenceState.Active))
        {
            call.Value.Dispose();
            return ConferenceResolution.Failed(GroupCallErrors.GroupCallInvalid);
        }
        bool isCreator = view.CreatorUserId == currentUserId;
        long accessHash = view.AccessHash;

        bool isParticipant;
        try
        {
            isParticipant = await IsActiveParticipantAsync(callId, currentUserId,
                cancellationToken);
        }
        catch
        {
            call.Value.Dispose();
            throw;
        }

        return ConferenceResolution.Resolved(call.Value, currentUserId, isCreator,
            isParticipant, accessHash);
    }

    private async ValueTask<long> ReadInvitedCallIdAsync(long userId, int msgId)
    {
        if (msgId == 0)
        {
            return 0;
        }

        using TLDto.TLSavedMessage? saved = await _messageRepository
            .GetMessageAsync(userId, msgId);
        if (saved == null)
        {
            return 0;
        }

        TLMessage message = saved.Value.AsSavedMessage().Get_OriginalMessage();
        if (message.Type != TLMessage.MessageType.MessageService)
        {
            return 0;
        }

        var action = new MessageActionView(message.AsMessageService().Action);
        return action.Is(out MessageActionConferenceCall conference)
            ? conference.CallId
            : 0;
    }

    protected async ValueTask<byte[]> BuildChainBlocksBytesAsync(long callId,
        long accessHash, int subChainId, int offset, int limit,
        CancellationToken cancellationToken = default)
    {
        GroupCallChainWindow window = await Chain.GetWindowAsync(callId, subChainId,
            offset, limit, cancellationToken);
        using TLUpdate update = GroupCallBuilders.BuildChainBlocksUpdate(callId,
            accessHash, subChainId, window.Blocks, window.NextOffset);
        return update.AsSpan().ToArray();
    }

    protected static string TranslateChainError(ChainValidationError error) => error switch
    {
        ChainValidationError.HeightMismatch => GroupCallErrors.BlockHeightMismatch,
        ChainValidationError.HashMismatch => GroupCallErrors.BlockHashMismatch,
        ChainValidationError.NoPermissions => GroupCallErrors.GroupCallForbidden,
        _ => GroupCallErrors.BlockInvalid,
    };
}
