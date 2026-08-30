// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Stickers;

public readonly record struct StickerAccountSnapshot(long Revision,
    long[] Installed, long[] Archived, long[] SavedGifs, long[] Recent,
    int[] RecentDates, long[] AttachedRecent, int[] AttachedRecentDates,
    long[] Faved, long[] FeaturedRead)
{
    public static StickerAccountSnapshot Empty { get; } =
        new(0, [], [], [], [], [], [], [], [], []);
}

public sealed class StickerAccountStore
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStickerRepository _repository;

    public StickerAccountStore(IUnitOfWork unitOfWork,
        IStickerRepository repository)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
    }

    public async ValueTask<StickerAccountSnapshot> ReadAsync(long userId)
    {
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        if (state is null)
        {
            return StickerAccountSnapshot.Empty;
        }
        var view = state.Value.AsStickerAccountState();
        return new StickerAccountSnapshot(view.Revision,
            view.Installed.ToArray(), view.Archived.ToArray(),
            view.SavedGifs.ToArray(), view.Recent.ToArray(),
            view.RecentDates.ToArray(), view.AttachedRecent.ToArray(),
            view.AttachedRecentDates.ToArray(), view.Faved.ToArray(),
            view.FeaturedRead.ToArray());
    }

    public async ValueTask<bool> PersistAsync(long userId,
        StickerAccountSnapshot snapshot)
    {
        using TLStickerAccountState state = Build(userId,
            snapshot.Revision + 1, snapshot);
        return _repository.PutAccountState(state) &&
               await _unitOfWork.SaveAsync();
    }

    public async ValueTask<bool> PurgeSetAsync(long setId,
        HashSet<long> documentIds)
    {
        IReadOnlyCollection<TLStickerAccountState> accounts =
            await _repository.GetAccountStatesAsync();
        try
        {
            foreach (TLStickerAccountState account in accounts)
            {
                var state = account.AsStickerAccountState();
                var snapshot = new StickerAccountSnapshot(state.Revision,
                    Remove(state.Installed.ToArray(), setId),
                    Remove(state.Archived.ToArray(), setId),
                    Keep(state.SavedGifs.ToArray(), documentIds),
                    Keep(state.Recent.ToArray(), documentIds),
                    KeepDates(state.Recent.ToArray(),
                        state.RecentDates.ToArray(), documentIds),
                    Keep(state.AttachedRecent.ToArray(), documentIds),
                    KeepDates(state.AttachedRecent.ToArray(),
                        state.AttachedRecentDates.ToArray(), documentIds),
                    Keep(state.Faved.ToArray(), documentIds),
                    Remove(state.FeaturedRead.ToArray(), setId));
                using TLStickerAccountState updated = Build(state.UserId,
                    state.Revision + 1, snapshot);
                if (!_repository.PutAccountState(updated)) return false;
            }
        }
        finally
        {
            foreach (TLStickerAccountState account in accounts) account.Dispose();
        }
        return true;
    }

    public static long[] Prepend(long[] source, long id) =>
        [id, .. source.Where(value => value != id)
            .Take(StickerRows.CollectionLimit - 1)];

    public static long[] Remove(long[] source, long id) =>
        source.Where(value => value != id).ToArray();

    public static (long[] Ids, int[] Dates) MoveRecent(long[] ids, int[] dates,
        long id, int now, bool remove)
    {
        var entries = ids.Select((value, index) => (Id: value,
                Date: index < dates.Length ? dates[index] : 0))
            .Where(entry => entry.Id != id).ToList();
        if (!remove)
        {
            entries.Insert(0, (id, now));
        }
        return (entries.Take(StickerRows.CollectionLimit).Select(x => x.Id)
                .ToArray(),
            entries.Take(StickerRows.CollectionLimit).Select(x => x.Date)
                .ToArray());
    }

    private static TLStickerAccountState Build(long userId, long revision,
        StickerAccountSnapshot snapshot) => StickerAccountState.Builder()
        .UserId(userId).Revision(revision)
        .Installed(StickerVectors.ToLongVector(snapshot.Installed))
        .Archived(StickerVectors.ToLongVector(snapshot.Archived))
        .SavedGifs(StickerVectors.ToLongVector(snapshot.SavedGifs))
        .Recent(StickerVectors.ToLongVector(snapshot.Recent))
        .RecentDates(StickerVectors.ToIntVector(snapshot.RecentDates))
        .AttachedRecent(StickerVectors.ToLongVector(snapshot.AttachedRecent))
        .AttachedRecentDates(
            StickerVectors.ToIntVector(snapshot.AttachedRecentDates))
        .Faved(StickerVectors.ToLongVector(snapshot.Faved))
        .FeaturedRead(StickerVectors.ToLongVector(snapshot.FeaturedRead))
        .Build();

    private static long[] Keep(long[] source, HashSet<long> deleted) =>
        source.Where(value => !deleted.Contains(value)).ToArray();

    private static int[] KeepDates(long[] ids, int[] dates,
        HashSet<long> deleted)
    {
        var result = new List<int>();
        for (int i = 0; i < ids.Length; i++)
            if (!deleted.Contains(ids[i]))
                result.Add(i < dates.Length ? dates[i] : 0);
        return result.ToArray();
    }
}
