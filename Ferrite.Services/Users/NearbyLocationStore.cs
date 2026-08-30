// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Users;

public readonly record struct NearbyPeer(long UserId, double Lat, double Lon,
    int ExpiresAt);

public sealed class NearbyLocationStore
{
    private readonly INearbyLocationsRepository _nearbyLocationsRepository;

    private const double EarthRadiusMetres = 6_371_000d;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public NearbyLocationStore(IUnitOfWork unitOfWork, INearbyLocationsRepository nearbyLocationsRepository, TimeProvider timeProvider)
    {
        _nearbyLocationsRepository = nearbyLocationsRepository;

        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public int Now() => (int)_timeProvider.GetUtcNow().ToUnixTimeSeconds();

    public bool Publish(long userId, double lat, double lon, int accuracyRadius,
        int expiresAt)
    {
        var builder = NearbyLocation.Builder()
            .UserId(userId)
            .Lat(lat)
            .Lon(lon)
            .ExpiresAt(expiresAt)
            .Date(Now());
        if (accuracyRadius > 0)
        {
            builder = builder.AccuracyRadius(accuracyRadius);
        }
        using TLNearbyLocation row = builder.Build();
        return _nearbyLocationsRepository.PutLocation(row);
    }

    public bool Remove(long userId) =>
        _nearbyLocationsRepository.DeleteLocation(userId);

    public async ValueTask<NearbyPeer?> GetLiveAsync(long userId)
    {
        using TLNearbyLocation? stored = await _nearbyLocationsRepository
            .GetLocationAsync(userId);
        if (stored == null)
        {
            return null;
        }

        var row = stored.Value.AsNearbyLocation();
        if (row.ExpiresAt <= Now())
        {
            Remove(userId);
            return null;
        }
        return new NearbyPeer(row.UserId, row.Lat, row.Lon, row.ExpiresAt);
    }

    public async Task<List<(NearbyPeer Peer, int DistanceMetres)>> FindNearbyAsync(
        long selfUserId, double lat, double lon)
    {
        int now = Now();
        var expired = new List<long>();
        var found = new List<(NearbyPeer Peer, int DistanceMetres)>();

        await foreach (TLNearbyLocation stored in _nearbyLocationsRepository.IterateLocationsAsync())
        {
            using (stored)
            {
                var row = stored.AsNearbyLocation();
                if (row.ExpiresAt <= now)
                {
                    expired.Add(row.UserId);
                    continue;
                }
                if (row.UserId == selfUserId)
                {
                    continue;
                }
                found.Add((new NearbyPeer(row.UserId, row.Lat, row.Lon,
                        row.ExpiresAt),
                    DistanceMetres(lat, lon, row.Lat, row.Lon)));
            }
        }

        foreach (long userId in expired)
        {
            Remove(userId);
        }

        found.Sort((left, right) =>
        {
            int byDistance = left.DistanceMetres.CompareTo(right.DistanceMetres);
            return byDistance != 0
                ? byDistance
                : left.Peer.UserId.CompareTo(right.Peer.UserId);
        });
        return found;
    }

    public static int DistanceMetres(double fromLat, double fromLon, double toLat,
        double toLon)
    {
        double fromLatRad = double.DegreesToRadians(fromLat);
        double toLatRad = double.DegreesToRadians(toLat);
        double deltaLat = double.DegreesToRadians(toLat - fromLat);
        double deltaLon = double.DegreesToRadians(toLon - fromLon);

        double a = Math.Pow(Math.Sin(deltaLat / 2), 2) +
                   Math.Cos(fromLatRad) * Math.Cos(toLatRad) *
                   Math.Pow(Math.Sin(deltaLon / 2), 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double metres = EarthRadiusMetres * c;
        return metres >= int.MaxValue ? int.MaxValue : (int)Math.Round(metres);
    }
}
