// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SaveDraftHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DraftStore _drafts;

    public SaveDraftHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, DraftStore drafts)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _drafts = drafts;
    }

    [TLFunction(Constructors.baseLayer_SaveDraft)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        var request = (SaveDraft)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer) || peer.Id <= 0)
        {
            return Error("PEER_ID_INVALID");
        }
        int topMsgId = request.Flags[4]
            ? DraftStore.ResolveTopMsgId(request.Get_ReplyToView())
            : 0;
        DraftAddress address = new DraftAddress(peer.Type, peer.Id, topMsgId);
        bool empty = DraftStore.IsEmpty(request);
        byte[] draftBytes = empty
            ? Array.Empty<byte>()
            : DraftStore.BuildDraftBytes(request, _drafts.CurrentDate);

        bool saved = empty
            ? await _drafts.DeleteAsync(authKeyId, userId, address,
                requireExisting: false)
            : await _drafts.SaveAsync(authKeyId, userId, address, draftBytes);
        return saved ? BoolTrue.Builder().Build() : Error("INTERNAL_SERVER_ERROR");
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
