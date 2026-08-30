// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Account;

public enum WallpaperInputKind
{
    Id,
    Slug,
    NoFile,
}

public readonly record struct WallpaperInput(WallpaperInputKind Kind, long Id,
    long AccessHash, string Slug);

public sealed class WallpaperStore
{
    private readonly IAccountSettingsRepository _repository;
    private readonly IUnitOfWork _transactions;
    private readonly IUploadService _upload;
    private readonly TimeProvider _time;

    public WallpaperStore(IAccountSettingsRepository repository,
        IUnitOfWork transactions, IUploadService upload, TimeProvider time)
    {
        _repository = repository;
        _transactions = transactions;
        _upload = upload;
        _time = time;
    }

    public async Task<TLBytes> UploadAsync(long userId, TLInputFile file,
        byte[] mimeType, TLWallPaperSettings settings)
    {
        ServiceResult<TLUploadedFileInfo?> saved = await _upload.SaveFile(file);
        if (!saved.Success || saved.Result is null)
            return RpcErrorGenerator.GenerateError(saved.ErrorMessage.Code,
                Encoding.UTF8.GetBytes(saved.ErrorMessage.Message));
        using TLUploadedFileInfo uploaded = saved.Result.Value;
        var attributes = new Vector();
        ServiceResult<TLBytes?> registered = await _upload.RegisterDocument(
            uploaded, mimeType, attributes.ToReadOnlySpan().ToArray(), null);
        if (!registered.Success || registered.Result is null)
            return RpcErrorGenerator.GenerateError(registered.ErrorMessage.Code,
                Encoding.UTF8.GetBytes(registered.ErrorMessage.Message));
        using TLBytes document = registered.Result.Value;
        var value = (Document)document;
        WallPaper wallpaper = WallPaper.Builder().Creator(true).Id(value.Id)
            .AccessHash(value.AccessHash)
            .Slug(Encoding.UTF8.GetBytes($"wallpaper-{value.Id:x}"))
            .Document(document.AsSpan()).Settings(settings.AsSpan()).Build();
        using TLWallpaperCatalogState row = WallpaperCatalogState.Builder()
            .Wallpaper(wallpaper.ToReadOnlySpan()).OwnerUserId(userId).Date(Now())
            .Build();
        TLBytes result = wallpaper.TLBytes!.Value;
        if (!_repository.PutWallpaperCatalog(row) ||
            !await _transactions.SaveAsync())
        {
            result.Dispose();
            return InternalError();
        }
        return result;
    }

    public async Task<TLBytes> GetAsync(long userId, WallpaperInput input)
    {
        using TLWallPaper? wallpaper = await ResolveAsync(userId, input, null);
        return wallpaper is null ? InvalidError() : Copy(wallpaper.Value.AsSpan());
    }

    public ValueTask<TLWallPaper?> ResolveForThemeAsync(long userId,
        WallpaperInput input, TLWallPaperSettings settings) =>
        ResolveAsync(userId, input, settings);

    public async Task<TLBytes> GetMultiAsync(long userId,
        IReadOnlyList<WallpaperInput> inputs)
    {
        var values = new List<TLWallPaper>();
        try
        {
            foreach (WallpaperInput input in inputs)
            {
                TLWallPaper? wallpaper = await ResolveAsync(userId, input, null);
                if (wallpaper is null) return InvalidError();
                values.Add(wallpaper.Value);
            }
            var vector = new Vector();
            foreach (TLWallPaper wallpaper in values)
                vector.AppendTLObject(wallpaper.AsSpan());
            byte[] bytes = vector.ToReadOnlySpan().ToArray();
            return new TLBytes(bytes, 0, bytes.Length);
        }
        finally
        {
            foreach (TLWallPaper wallpaper in values) wallpaper.Dispose();
        }
    }

    public async Task<TLBytes> GetCatalogueAsync(long userId,
        long requestedHash)
    {
        IReadOnlyCollection<TLWallpaperCatalogState> catalogue =
            await _repository.GetWallpaperCatalogAsync();
        IReadOnlyCollection<TLAccountWallpaperState> account =
            await _repository.GetAccountWallpapersAsync(userId);
        try
        {
            var globalById = catalogue.ToDictionary(row => WallpaperId(
                row.AsWallpaperCatalogState().Get_WallpaperView()));
            var accountById = account.ToDictionary(row =>
                row.AsAccountWallpaperState().WallpaperId);
            long[] ids = globalById.Keys.Concat(accountById.Keys).Distinct()
                .Order().ToArray();
            long hash = 1;
            var wallpapers = new Vector();
            foreach (long id in ids)
            {
                if (accountById.TryGetValue(id, out TLAccountWallpaperState state))
                {
                    var row = state.AsAccountWallpaperState();
                    hash = unchecked(hash * 20261 + id * 31 + row.Date +
                        (row.Saved ? 1 : 0) + (row.Installed ? 2 : 0));
                    wallpapers.AppendTLObject(row.Wallpaper);
                }
                else
                {
                    var row = globalById[id].AsWallpaperCatalogState();
                    hash = unchecked(hash * 20261 + id * 31 + row.Date);
                    wallpapers.AppendTLObject(row.Wallpaper);
                }
            }
            if (requestedHash != 0 && requestedHash == hash)
                return WallPapersNotModified.Builder().Build().TLBytes!.Value;
            return WallPapers.Builder().Hash(hash).WallpapersProperty(wallpapers).Build()
                .TLBytes!.Value;
        }
        finally
        {
            Dispose(catalogue);
            Dispose(account);
        }
    }

    public async Task<TLBool> SaveAsync(long userId, WallpaperInput input,
        bool unsave, TLWallPaperSettings settings)
    {
        using TLWallPaper? wallpaper = await ResolveAsync(userId, input, settings);
        if (wallpaper is null) return InvalidBool();
        long id = WallpaperId((WallPaperView)wallpaper.Value.AsSpan());
        if (unsave)
        {
            _repository.DeleteAccountWallpaper(userId, id);
            return await _transactions.SaveAsync()
                ? BoolTrue.Builder().Build() : InternalBool();
        }
        using TLAccountWallpaperState? existing = await _repository
            .GetAccountWallpaperAsync(userId, id);
        using TLAccountWallpaperState row = AccountWallpaperState.Builder()
            .Saved(true).Installed(existing is not null &&
                existing.Value.AsAccountWallpaperState().Installed)
            .UserId(userId).WallpaperId(id).Wallpaper(wallpaper.Value.AsSpan())
            .Date(Now()).Build();
        return _repository.PutAccountWallpaper(row) &&
               await _transactions.SaveAsync()
            ? BoolTrue.Builder().Build() : InternalBool();
    }

    public async Task<TLBool> InstallAsync(long userId, WallpaperInput input,
        TLWallPaperSettings settings)
    {
        using TLWallPaper? wallpaper = await ResolveAsync(userId, input, settings);
        if (wallpaper is null) return InvalidBool();
        long targetId = WallpaperId((WallPaperView)wallpaper.Value.AsSpan());
        IReadOnlyCollection<TLAccountWallpaperState> rows = await _repository
            .GetAccountWallpapersAsync(userId);
        bool success = true;
        try
        {
            foreach (TLAccountWallpaperState existing in rows)
            {
                var value = existing.AsAccountWallpaperState();
                if (value.WallpaperId == targetId) continue;
                using TLAccountWallpaperState cleared = AccountWallpaperState
                    .Builder().Saved(value.Saved).UserId(userId)
                    .WallpaperId(value.WallpaperId).Wallpaper(value.Wallpaper)
                    .Date(Now()).Build();
                success &= _repository.PutAccountWallpaper(cleared);
            }
            using TLAccountWallpaperState? current = await _repository
                .GetAccountWallpaperAsync(userId, targetId);
            using TLAccountWallpaperState installed = AccountWallpaperState
                .Builder().Saved(current is not null &&
                    current.Value.AsAccountWallpaperState().Saved)
                .Installed(true).UserId(userId).WallpaperId(targetId)
                .Wallpaper(wallpaper.Value.AsSpan()).Date(Now()).Build();
            success &= _repository.PutAccountWallpaper(installed);
            return success && await _transactions.SaveAsync()
                ? BoolTrue.Builder().Build() : InternalBool();
        }
        finally
        {
            Dispose(rows);
        }
    }

    public async Task<TLBool> ResetAsync(long userId) =>
        _repository.DeleteAccountWallpapers(userId) &&
        await _transactions.SaveAsync()
            ? BoolTrue.Builder().Build() : InternalBool();

    public static bool IsSettingsValid(WallPaperSettings settings)
    {
        static bool Color(int value) => value is >= 0 and <= 0xFFFFFF;
        if (settings.Flags[0] && !Color(settings.BackgroundColor)) return false;
        if (settings.Flags[4] && (!Color(settings.SecondBackgroundColor) ||
            settings.Rotation is < 0 or >= 360)) return false;
        if (settings.Flags[5] && !Color(settings.ThirdBackgroundColor)) return false;
        if (settings.Flags[6] && !Color(settings.FourthBackgroundColor)) return false;
        return !settings.Flags[3] || settings.Intensity is >= -100 and <= 100;
    }

    private async ValueTask<TLWallPaper?> ResolveAsync(long userId,
        WallpaperInput input, TLWallPaperSettings? settings)
    {
        using TLAccountWallpaperState? account = input.Kind ==
            WallpaperInputKind.Slug ? null : await _repository
            .GetAccountWallpaperAsync(userId, input.Id);
        if (account is not null)
        {
            TLWallPaper value = Clone(account.Value.AsAccountWallpaperState()
                .Get_WallpaperView(), settings);
            if (input.Kind != WallpaperInputKind.Id ||
                value.Type != TLWallPaper.WallPaperType.WallPaper ||
                value.AsWallPaper().AccessHash == input.AccessHash) return value;
            value.Dispose();
            return null;
        }

        using TLWallpaperCatalogState? catalogue = input.Kind switch
        {
            WallpaperInputKind.Slug => await _repository
                .GetWallpaperCatalogBySlugAsync(input.Slug),
            WallpaperInputKind.Id => await _repository
                .GetWallpaperCatalogAsync(input.Id),
            _ => null,
        };
        if (catalogue is not null)
        {
            TLWallPaper value = Clone(catalogue.Value.AsWallpaperCatalogState()
                .Get_WallpaperView(), settings);
            if (input.Kind != WallpaperInputKind.Id ||
                value.AsWallPaper().AccessHash == input.AccessHash) return value;
            value.Dispose();
            return null;
        }
        if (input.Kind != WallpaperInputKind.NoFile) return null;
        var builder = WallPaperNoFile.Builder().Id(input.Id);
        if (settings is not null) builder = builder.Settings(settings.Value.AsSpan());
        return builder.Build();
    }

    private static TLWallPaper Clone(WallPaperView wallpaper,
        TLWallPaperSettings? settings)
    {
        if (wallpaper.Is(out WallPaper file))
        {
            var builder = file.Clone();
            if (settings is not null) builder = builder.Settings(settings.Value.AsSpan());
            return builder.Build();
        }
        var noFileBuilder = wallpaper.AsWallPaperNoFile().Clone();
        if (settings is not null)
            noFileBuilder = noFileBuilder.Settings(settings.Value.AsSpan());
        return noFileBuilder.Build();
    }

    private static long WallpaperId(WallPaperView wallpaper) =>
        wallpaper.Is(out WallPaper value) ? value.Id
            : wallpaper.AsWallPaperNoFile().Id;

    private int Now() => (int)_time.GetUtcNow().ToUnixTimeSeconds();

    private static void Dispose(IEnumerable<TLWallpaperCatalogState> rows)
    {
        foreach (TLWallpaperCatalogState row in rows) row.Dispose();
    }

    private static void Dispose(IEnumerable<TLAccountWallpaperState> rows)
    {
        foreach (TLAccountWallpaperState row in rows) row.Dispose();
    }

    private static TLBytes InvalidError() =>
        RpcErrorGenerator.GenerateError(400, "WALLPAPER_INVALID"u8);
    private static TLBool InvalidBool() => (TLBool)InvalidError();
    private static TLBytes InternalError() =>
        RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);
    private static TLBool InternalBool() => (TLBool)InternalError();
    private static TLBytes Copy(ReadOnlySpan<byte> value)
    {
        byte[] bytes = value.ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
