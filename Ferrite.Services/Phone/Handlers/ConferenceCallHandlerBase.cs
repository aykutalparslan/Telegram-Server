// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// One resolved conference request. A conference has no hosting chat, so there
/// is no <see cref="GroupCallPeerAccess"/> to speak of: the only membership that
/// exists is the call's own participant list, and the creator of a call nobody
/// has joined yet is authorized by the chain instead.
/// </summary>
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

    /// <summary>Whether the caller holds an active (non-left) row in the call.</summary>
    public bool IsParticipant { get; }

    /// <summary>
    /// The call's own access hash. A caller that arrived through an invite message
    /// never sent one, and the updates it is about to receive have to name the
    /// call in full, so the resolved value is reported rather than echoed.
    /// </summary>
    public long AccessHash { get; }

    public string? Error { get; }

    public static ConferenceResolution Failed(string error) =>
        new(null, 0, false, false, 0, error);

    public static ConferenceResolution Resolved(TLDto.TLGroupCallState call,
        long currentUserId, bool isCreator, bool isParticipant, long accessHash) =>
        new(call, currentUserId, isCreator, isParticipant, accessHash, null);

    public void Dispose() => Call?.Dispose();
}

/// <summary>
/// An InputGroupCall as the request carried it, captured before the first await
/// because the request view is a ref struct. A conference is named either by
/// (id, access hash) or by the invite message the caller holds: an invitee is
/// never told the access hash, so the message form is the only way it can reach
/// the call before it has joined.
/// </summary>
public readonly record struct ConferenceCallRef(long CallId, long AccessHash,
    int InviteMsgId);

/// <summary>
/// Shared mechanics for the peerless conference surface. It deliberately does not
/// reuse <see cref="GroupCallHandlerBase.ResolveCallAsync"/>: that gate resolves a
/// hosting chat row and its participant list, and a conference has neither.
/// </summary>
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

    /// <summary>
    /// Resolves the calling account without a peer gate. Every conference
    /// endpoint needs the user id before it can decide anything, and a conference
    /// has no chat membership to consult.
    /// </summary>
    protected async ValueTask<long> ResolveUserIdAsync(long authKeyId)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        return auth?.AsAuthInfo().UserId ?? 0;
    }

    /// <summary>
    /// Reads the InputGroupCall forms a conference method accepts. A slug names a
    /// call link, which this server does not issue, so it resolves to nothing
    /// rather than to some other call.
    /// </summary>
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

    /// <summary>
    /// Resolves a conference call by id and access hash. A hosted call reached
    /// through a conference method is GROUPCALL_INVALID rather than an access
    /// error: the two surfaces are disjoint, and reporting anything else would
    /// tell the caller a call exists under a name it may not use.
    /// </summary>
    protected ValueTask<ConferenceResolution> ResolveConferenceAsync(long authKeyId,
        long callId, long accessHash, bool requireActive = true,
        CancellationToken cancellationToken = default) =>
        ResolveConferenceAsync(authKeyId, new ConferenceCallRef(callId, accessHash, 0),
            requireActive, cancellationToken);

    /// <summary>
    /// The same resolution for a call named either way. An invite message is
    /// resolved against the CALLER'S OWN copy, so it is a capability the server
    /// itself wrote for this account rather than anything the client asserts.
    /// </summary>
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
        // Only an explicitly named call carries an access hash to check; an invite
        // message names the call without one, and the hash it resolves to is what
        // the answer's own InputGroupCall fields will carry back.
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

    /// <summary>
    /// The conference an invite service message in this account's own box names,
    /// or 0 when the message is unknown or is not a conference invitation. A
    /// declined invitation still resolves: declining stops the ringing, it does
    /// not withdraw the invitation, and the chain is what decides whether the
    /// caller may actually rejoin.
    /// </summary>
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

    /// <summary>
    /// The chain-blocks update for one sub-chain, ready to be added to a result or
    /// fanned out.
    /// </summary>
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

    /// <summary>
    /// The wire error one chain rejection maps to. A height or hash mismatch is
    /// the client's cue to refetch the head and rebuild; everything else is a
    /// block it must not retry unchanged.
    /// </summary>
    protected static string TranslateChainError(ChainValidationError error) => error switch
    {
        ChainValidationError.HeightMismatch => GroupCallErrors.BlockHeightMismatch,
        ChainValidationError.HashMismatch => GroupCallErrors.BlockHashMismatch,
        ChainValidationError.NoPermissions => GroupCallErrors.GroupCallForbidden,
        _ => GroupCallErrors.BlockInvalid,
    };
}
