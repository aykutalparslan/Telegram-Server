// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class GetLocatedHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly NearbyLocationStore _nearby;
    private readonly UpdateFanout _fanout;

    public GetLocatedHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        NearbyLocationStore nearby, UpdateFanout fanout)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;

        _nearby = nearby;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_GetLocated)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error(401, "AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (GetLocated)q;
        bool hasPoint = request.Get_GeoPointView().Is(out InputGeoPoint point);
        double lat = hasPoint ? point.Lat : 0;
        double lon = hasPoint ? point.Longitude : 0;
        int accuracyRadius = hasPoint ? point.AccuracyRadius : 0;
        bool publishes = request.Flags[0];
        int selfExpires = request.SelfExpires;

        if (hasPoint && (double.IsNaN(lat) || double.IsNaN(lon) ||
                         lat is < -90 or > 90 || lon is < -180 or > 180))
        {
            return Error(400, "GEO_POINT_INVALID");
        }

        bool stopping = publishes && selfExpires <= 0;
        if (!hasPoint && !stopping)
        {
            return Error(400, "GEO_POINT_INVALID");
        }

        if (stopping)
        {
            _nearby.Remove(userId);
        }
        else if (publishes)
        {
            _nearby.Publish(userId, lat, lon, accuracyRadius,
                _nearby.Now() + selfExpires);
        }
        if (publishes && !await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        List<(NearbyPeer Peer, int DistanceMetres)> nearby = hasPoint
            ? await _nearby.FindNearbyAsync(userId, lat, lon)
            : [];
        NearbyPeer? self = await _nearby.GetLiveAsync(userId);
        await _unitOfWork.SaveAsync();

        return BuildResult(userId, nearby, self);
    }

    private TLUpdates BuildResult(long userId,
        IReadOnlyList<(NearbyPeer Peer, int DistanceMetres)> nearby,
        NearbyPeer? self)
    {
        var peers = new Vector();
        var relatedUserIds = new List<long> { userId };
        if (self is { } live)
        {
            using TLPeerLocated located = PeerSelfLocated.Builder()
                .Expires(live.ExpiresAt)
                .Build();
            peers.AppendTLObject(located.AsSpan());
        }
        foreach ((NearbyPeer peer, int distance) in nearby)
        {
            using TLPeer peerId = new PeerUser(peer.UserId);
            using TLPeerLocated located = PeerLocated.Builder()
                .Peer(peerId.AsSpan())
                .Expires(peer.ExpiresAt)
                .Distance(distance)
                .Build();
            peers.AppendTLObject(located.AsSpan());
            relatedUserIds.Add(peer.UserId);
        }

        byte[] updateBytes;
        using (TLUpdate update = UpdatePeerLocated.Builder().Peers(peers).Build())
        {
            updateBytes = update.AsSpan().ToArray();
        }
        return _fanout.BuildUpdates(userId, [updateBytes], relatedUserIds, [], _nearby.Now(),
            seq: 0);
    }

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
