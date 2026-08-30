// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

public sealed class SetCallRatingHandler : PhoneCallHandlerBase
{
    private const int MinRating = 1;
    private const int MaxRating = 5;
    private const int MaxCommentLength = 4096;

    public SetCallRatingHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
    }

    [TLFunction(Constructors.baseLayer_SetCallRating)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (SetCallRating)q;
        (long callId, long accessHash) = ReadInputPhoneCall(request.Peer);
        int rating = request.Rating;
        int commentLength = request.Comment.Length;

        long? userIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (userIdValue is not long userId)
        {
            return (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }

        if (rating is < MinRating or > MaxRating || commentLength > MaxCommentLength)
        {
            return (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
                "CALL_RATING_INVALID"u8);
        }

        if (ResolveParticipantCall(callId, accessHash, userId) is null)
        {
            return (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
                "CALL_PEER_INVALID"u8);
        }

        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(new Vector())
            .Users(new Vector())
            .Chats(new Vector())
            .Date(Now())
            .Seq(0)
            .Build();
    }
}
