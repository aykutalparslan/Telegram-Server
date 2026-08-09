// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>One published nearby row, already resolved to scalars.</summary>
public readonly record struct NearbyPeer(long UserId, double Lat, double Lon,
    int ExpiresAt);

/// <summary>
/// The nearby-people state behind `contacts.getLocated`: publish, refresh, stop,
/// and read. Expiry is enforced on read against the injected clock and an expired
/// row is deleted as it is passed over, so nothing has to sweep the table.
/// </summary>
public sealed class NearbyLocationStore
{
    private readonly INearbyLocationsRepository _nearbyLocationsRepository;

    /// <summary>Mean Earth radius in metres, the WGS-84 value Telegram uses.</summary>
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

    /// <summary>
    /// The caller's own live row, or null when they are not sharing. A row that
    /// has expired is deleted here rather than reported.
    /// </summary>
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

    /// <summary>
    /// Everybody else who is still sharing, nearest first. Distance is the
    /// great-circle distance in whole metres, which is the unit
    /// `peerLocated.distance` is defined in.
    /// </summary>
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

    /// <summary>Haversine great-circle distance, rounded to whole metres.</summary>
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
