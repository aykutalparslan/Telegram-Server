// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public enum ThemeInputKind
{
    Id,
    Slug,
}

public readonly record struct ThemeInput(ThemeInputKind Kind, long Id,
    long AccessHash, string Slug);

public readonly record struct ThemeDocumentInput(long Id, long AccessHash,
    byte[] FileReference);

public sealed class ThemeSettingsInput : IDisposable
{
    public ThemeSettingsInput(TLBaseTheme baseTheme, bool animated,
        int accentColor, int? outboxAccentColor, int[] messageColors,
        WallpaperInput? wallpaper, TLWallPaperSettings? wallpaperSettings)
    {
        BaseTheme = baseTheme;
        Animated = animated;
        AccentColor = accentColor;
        OutboxAccentColor = outboxAccentColor;
        MessageColors = messageColors;
        Wallpaper = wallpaper;
        WallpaperSettings = wallpaperSettings;
    }

    public TLBaseTheme BaseTheme { get; }
    public bool Animated { get; }
    public int AccentColor { get; }
    public int? OutboxAccentColor { get; }
    public int[] MessageColors { get; }
    public WallpaperInput? Wallpaper { get; }
    public TLWallPaperSettings? WallpaperSettings { get; }

    public void Dispose()
    {
        BaseTheme.Dispose();
        WallpaperSettings?.Dispose();
    }
}

public sealed class ThemeStore
{
    private readonly IDocumentsRepository _documentsRepository;

    private static readonly Regex SlugPattern = new("^[a-z0-9_]{1,64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly IAccountSettingsRepository _repository;
    private readonly IUnitOfWork _transactions;
    private readonly IUploadService _upload;
    private readonly IPhotoProcessingService _photos;
    private readonly WallpaperStore _wallpapers;
    private readonly IRandomGenerator _random;
    private readonly TimeProvider _time;

    public ThemeStore(IAccountSettingsRepository repository,
        IUnitOfWork transactions, IDocumentsRepository documentsRepository, IUploadService upload,
        IPhotoProcessingService photos, WallpaperStore wallpapers,
        IRandomGenerator random, TimeProvider time)
    {
        _documentsRepository = documentsRepository;

        _repository = repository;
        _transactions = transactions;
        _upload = upload;
        _photos = photos;
        _wallpapers = wallpapers;
        _random = random;
        _time = time;
    }

    public async Task<TLBytes> UploadAsync(TLInputFile file,
        TLInputFile? thumb, string fileName, string mimeType)
    {
        ServiceResult<TLUploadedFileInfo?> saved = await _upload.SaveFile(file);
        if (!saved.Success || saved.Result is null)
            return Error(saved.ErrorMessage.Code, saved.ErrorMessage.Message);
        using TLUploadedFileInfo uploaded = saved.Result.Value;
        byte[]? thumbs = null;
        if (thumb is not null)
        {
            ServiceResult<TLUploadedFileInfo?> thumbSaved =
                await _upload.SaveFile(thumb.Value);
            if (!thumbSaved.Success || thumbSaved.Result is null)
                return Error(thumbSaved.ErrorMessage.Code,
                    thumbSaved.ErrorMessage.Message);
            using TLUploadedFileInfo uploadedThumb = thumbSaved.Result.Value;
            ServiceResult<TLPhoto?> processed =
                await _photos.ProcessPhoto(uploadedThumb);
            if (!processed.Success || processed.Result is null)
                return Error(processed.ErrorMessage.Code,
                    processed.ErrorMessage.Message);
            using TLPhoto photo = processed.Result.Value;
            thumbs = photo.AsPhoto().Sizes.ToReadOnlySpan().ToArray();
        }
        byte[] attributeBytes;
        using (DocumentAttributeFilename attribute = DocumentAttributeFilename
            .Builder().FileName(Encoding.UTF8.GetBytes(fileName)).Build())
        {
            var attributes = new Vector();
            attributes.AppendTLObject(attribute.ToReadOnlySpan());
            attributeBytes = attributes.ToReadOnlySpan().ToArray();
        }
        ServiceResult<TLBytes?> registered = await _upload.RegisterDocument(
            uploaded, Encoding.UTF8.GetBytes(mimeType),
            attributeBytes, thumbs);
        return registered.Success && registered.Result is not null
            ? registered.Result.Value
            : Error(registered.ErrorMessage.Code, registered.ErrorMessage.Message);
    }

    public async Task<TLBytes> CreateAsync(long userId, string slug,
        string title, ThemeDocumentInput? document,
        IReadOnlyList<ThemeSettingsInput> settings)
    {
        if (!ValidTitle(title)) return Invalid();
        long id;
        do id = _random.NextLong() & long.MaxValue;
        while (id == 0 || await ExistsAsync(id));
        slug = Normalize(slug);
        if (slug.Length == 0) slug = $"theme_{id:x}";
        if (!ValidSlug(slug)) return SlugInvalid();
        using TLThemeCatalogState? duplicate = await _repository
            .GetThemeCatalogBySlugAsync(slug);
        if (duplicate is not null) return SlugOccupied();
        using TLDocument? resolvedDocument = ResolveDocument(document);
        if (document is not null && resolvedDocument is null) return Invalid();
        List<TLThemeSettings>? resolvedSettings = await ResolveSettingsAsync(
            userId, settings);
        if (resolvedSettings is null) return Invalid();
        try
        {
            var builder = Theme.Builder().Creator(true).Id(id)
                .AccessHash(_random.NextLong()).Slug(Encoding.UTF8.GetBytes(slug))
                .Title(Encoding.UTF8.GetBytes(title));
            if (resolvedDocument is not null)
                builder = builder.Document(resolvedDocument.Value.AsSpan());
            if (resolvedSettings.Count > 0)
                builder = builder.Settings(ToVector(resolvedSettings));
            Theme theme = builder.Build();
            using TLThemeCatalogState row = ThemeCatalogState.Builder()
                .Theme(theme.ToReadOnlySpan()).OwnerUserId(userId).Date(Now())
                .Build();
            TLBytes result = theme.TLBytes!.Value;
            if (!_repository.PutThemeCatalog(row) ||
                !await _transactions.SaveAsync())
            {
                result.Dispose();
                return Internal();
            }
            return result;
        }
        finally
        {
            Dispose(resolvedSettings);
        }
    }

    public async Task<TLBytes> UpdateAsync(long userId, string format,
        ThemeInput input, string? slug, string? title,
        ThemeDocumentInput? document, IReadOnlyList<ThemeSettingsInput>? settings)
    {
        if (!ValidFormat(format) || title is not null && !ValidTitle(title))
            return Invalid();
        using TLThemeCatalogState? stored = await ResolveAsync(input);
        if (stored is null) return Invalid();
        if (stored.Value.AsThemeCatalogState().OwnerUserId != userId)
            return Error(400, "THEME_NOT_MODIFIED");
        long resolvedId = stored.Value.AsThemeCatalogState().Get_ThemeView()
            .AsTheme().Id;
        string? normalizedSlug = slug is null ? null : Normalize(slug);
        if (normalizedSlug is not null && !ValidSlug(normalizedSlug))
            return SlugInvalid();
        using TLThemeCatalogState? duplicate = normalizedSlug is null ? null
            : await _repository.GetThemeCatalogBySlugAsync(normalizedSlug);
        if (duplicate is not null && duplicate.Value.AsThemeCatalogState()
            .Get_ThemeView().AsTheme().Id != resolvedId)
            return SlugOccupied();
        using TLDocument? resolvedDocument = ResolveDocument(document);
        if (document is not null && resolvedDocument is null) return Invalid();
        List<TLThemeSettings>? resolvedSettings = settings is null ? null
            : await ResolveSettingsAsync(userId, settings);
        if (settings is not null && resolvedSettings is null) return Invalid();
        try
        {
            Theme current = stored.Value.AsThemeCatalogState().Get_ThemeView()
                .AsTheme();
            long currentId = current.Id;
            string oldSlug = Encoding.UTF8.GetString(current.Slug);
            string nextSlug = normalizedSlug ?? oldSlug;
            var builder = Theme.Builder().Creator(true)
                .DefaultProperty(current.DefaultProperty).ForChat(current.ForChat)
                .Id(current.Id).AccessHash(current.AccessHash)
                .Slug(Encoding.UTF8.GetBytes(nextSlug))
                .Title(title is null ? current.Title : Encoding.UTF8.GetBytes(title));
            if (document is not null)
                builder = builder.Document(resolvedDocument!.Value.AsSpan());
            else if (current.Flags[2]) builder = builder.Document(current.Document);
            if (settings is not null && resolvedSettings!.Count > 0)
                builder = builder.Settings(ToVector(resolvedSettings));
            else if (settings is null && current.Flags[3])
                builder = builder.Settings(current.Settings);
            if (current.Flags[6]) builder = builder.Emoticon(current.Emoticon);
            if (current.Flags[4])
                builder = builder.InstallsCount(current.InstallsCount);
            TLTheme updated = builder.Build();
            using TLThemeCatalogState row = ThemeCatalogState.Builder()
                .Theme(updated.AsSpan()).OwnerUserId(userId).Date(Now())
                .Build();
            bool saved = true;
            if (!string.Equals(oldSlug, nextSlug, StringComparison.Ordinal))
                saved &= _repository.DeleteThemeCatalog(currentId, oldSlug);
            saved &= _repository.PutThemeCatalog(row);
            using TLAccountThemeState? account = await _repository
                .GetAccountThemeAsync(userId, currentId);
            if (account is not null)
            {
                var old = account.Value.AsAccountThemeState();
                using TLAccountThemeState accountRow = AccountThemeState.Builder()
                    .Saved(old.Saved).Installed(old.Installed).Dark(old.Dark)
                    .UserId(userId).ThemeId(currentId)
                    .Theme(updated.AsSpan()).Format(old.Format)
                    .Date(Now()).Build();
                saved &= _repository.PutAccountTheme(accountRow);
            }
            TLBytes result = updated;
            if (!saved || !await _transactions.SaveAsync())
            {
                result.Dispose();
                return Internal();
            }
            return result;
        }
        finally
        {
            if (resolvedSettings is not null) Dispose(resolvedSettings);
        }
    }

    public async Task<TLBytes> GetAsync(long userId, string format,
        ThemeInput input)
    {
        if (!ValidFormat(format)) return Invalid();
        using TLThemeCatalogState? row = await ResolveAsync(input);
        if (row is null) return Invalid();
        var state = row.Value.AsThemeCatalogState();
        using TLTheme result = Rebuild(state.Get_ThemeView().AsTheme(),
            state.OwnerUserId == userId);
        return Copy(result.AsSpan());
    }

    public async Task<TLBool> SaveAsync(long userId, ThemeInput input,
        bool unsave)
    {
        using TLThemeCatalogState? row = await ResolveAsync(input);
        if (row is null) return InvalidBool();
        var state = row.Value.AsThemeCatalogState();
        Theme source = state.Get_ThemeView().AsTheme();
        long sourceId = source.Id;
        using TLTheme theme = Rebuild(source, state.OwnerUserId == userId);
        using TLAccountThemeState? current = await _repository
            .GetAccountThemeAsync(userId, sourceId);
        if (unsave && (current is null ||
            !current.Value.AsAccountThemeState().Installed))
        {
            _repository.DeleteAccountTheme(userId, sourceId);
            return await _transactions.SaveAsync() ? True() : InternalBool();
        }
        var builder = AccountThemeState.Builder()
            .Saved(!unsave).Installed(current is not null &&
                current.Value.AsAccountThemeState().Installed)
            .Dark(current is not null && current.Value.AsAccountThemeState().Dark)
            .UserId(userId).ThemeId(sourceId).Theme(theme.AsSpan())
            .Format(current is null ? ReadOnlySpan<byte>.Empty
                : current.Value.AsAccountThemeState().Format).Date(Now());
        using TLAccountThemeState saved = builder.Build();
        return _repository.PutAccountTheme(saved) &&
            await _transactions.SaveAsync() ? True() : InternalBool();
    }

    public async Task<TLBool> InstallAsync(long userId, bool dark,
        ThemeInput? input, string? format)
    {
        if (format is not null && !ValidFormat(format)) return InvalidBool();
        using TLThemeCatalogState? row = input is null ? null
            : await ResolveAsync(input.Value);
        if (input is not null && row is null) return InvalidBool();
        TLTheme? theme = null;
        long targetId = 0;
        if (row is not null)
        {
            var state = row.Value.AsThemeCatalogState();
            Theme source = state.Get_ThemeView().AsTheme();
            targetId = source.Id;
            theme = Rebuild(source, state.OwnerUserId == userId);
        }
        IReadOnlyCollection<TLAccountThemeState> rows = await _repository
            .GetAccountThemesAsync(userId);
        bool success = true;
        try
        {
            foreach (TLAccountThemeState existing in rows)
            {
                var value = existing.AsAccountThemeState();
                if (value.ThemeId == targetId && theme is not null) continue;
                if (!value.Installed) continue;
                if (!value.Saved)
                    success &= _repository.DeleteAccountTheme(userId,
                        value.ThemeId);
                else
                {
                    using TLAccountThemeState cleared = AccountThemeState
                        .Builder().Saved(true).UserId(userId)
                        .ThemeId(value.ThemeId).Theme(value.Theme)
                        .Format(value.Format).Date(Now()).Build();
                    success &= _repository.PutAccountTheme(cleared);
                }
            }
            if (theme is not null)
            {
                using TLAccountThemeState? current = await _repository
                    .GetAccountThemeAsync(userId, targetId);
                using TLAccountThemeState installed = AccountThemeState.Builder()
                    .Saved(current is not null &&
                        current.Value.AsAccountThemeState().Saved)
                    .Installed(true).Dark(dark).UserId(userId).ThemeId(targetId)
                    .Theme(theme.Value.AsSpan())
                    .Format(Encoding.UTF8.GetBytes(format ?? string.Empty))
                    .Date(Now()).Build();
                success &= _repository.PutAccountTheme(installed);
            }
            return success && await _transactions.SaveAsync()
                ? True() : InternalBool();
        }
        finally
        {
            theme?.Dispose();
            Dispose(rows);
        }
    }

    public async Task<TLBytes> GetCatalogueAsync(long userId, string format,
        long requestedHash)
    {
        if (!ValidFormat(format)) return Invalid();
        IReadOnlyCollection<TLThemeCatalogState> catalogue =
            await _repository.GetThemeCatalogAsync();
        IReadOnlyCollection<TLAccountThemeState> account =
            await _repository.GetAccountThemesAsync(userId);
        try
        {
            var globalById = catalogue.ToDictionary(row => row
                .AsThemeCatalogState().Get_ThemeView().AsTheme().Id);
            var accountById = account.Where(row =>
            {
                var value = row.AsAccountThemeState();
                string storedFormat = Encoding.UTF8.GetString(value.Format);
                return storedFormat.Length == 0 || string.Equals(storedFormat,
                    format, StringComparison.OrdinalIgnoreCase);
            }).ToDictionary(row => row.AsAccountThemeState().ThemeId);
            long[] ids = globalById.Keys.Concat(accountById.Keys).Distinct()
                .Order().ToArray();
            long hash = 1;
            var themes = new Vector();
            foreach (long id in ids)
            {
                Theme source;
                bool creator;
                if (accountById.TryGetValue(id, out TLAccountThemeState saved))
                {
                    var state = saved.AsAccountThemeState();
                    source = state.Get_ThemeView().AsTheme();
                    creator = globalById.TryGetValue(id,
                        out TLThemeCatalogState global) && global
                        .AsThemeCatalogState().OwnerUserId == userId;
                    hash = unchecked(hash * 20261 + id * 31 + state.Date +
                        (state.Saved ? 1 : 0) + (state.Installed ? 2 : 0) +
                        (state.Dark ? 4 : 0));
                }
                else
                {
                    var state = globalById[id].AsThemeCatalogState();
                    source = state.Get_ThemeView().AsTheme();
                    creator = state.OwnerUserId == userId;
                    hash = unchecked(hash * 20261 + id * 31 + state.Date);
                }
                using TLTheme theme = Rebuild(source, creator);
                themes.AppendTLObject(theme.AsSpan());
            }
            if (requestedHash != 0 && requestedHash == hash)
                return ThemesNotModified.Builder().Build().TLBytes!.Value;
            return Themes.Builder().Hash(hash).ThemesProperty(themes).Build()
                .TLBytes!.Value;
        }
        finally
        {
            Dispose(catalogue);
            Dispose(account);
        }
    }

    public static bool ValidFormat(string value) =>
        value.Trim().Length is > 0 and <= 64;

    public static bool ValidColor(int value) => value is >= 0 and <= 0xFFFFFF;

    private async ValueTask<List<TLThemeSettings>?> ResolveSettingsAsync(
        long userId, IReadOnlyList<ThemeSettingsInput> inputs)
    {
        var result = new List<TLThemeSettings>(inputs.Count);
        foreach (ThemeSettingsInput input in inputs)
        {
            TLWallPaper? wallpaper = null;
            if (input.Wallpaper is not null && input.WallpaperSettings is not null)
            {
                wallpaper = await _wallpapers.ResolveForThemeAsync(userId,
                    input.Wallpaper.Value, input.WallpaperSettings.Value);
                if (wallpaper is null)
                {
                    Dispose(result);
                    return null;
                }
            }
            try
            {
                var builder = ThemeSettings.Builder()
                    .MessageColorsAnimated(input.Animated)
                    .BaseTheme(input.BaseTheme.AsSpan())
                    .AccentColor(input.AccentColor);
                if (input.OutboxAccentColor.HasValue)
                    builder = builder.OutboxAccentColor(
                        input.OutboxAccentColor.Value);
                if (input.MessageColors.Length > 0)
                {
                    var colors = new VectorOfInt();
                    foreach (int color in input.MessageColors) colors.Append(color);
                    builder = builder.MessageColors(colors);
                }
                if (wallpaper is not null)
                    builder = builder.Wallpaper(wallpaper.Value.AsSpan());
                result.Add(builder.Build());
            }
            finally
            {
                wallpaper?.Dispose();
            }
        }
        return result;
    }

    private TLDocument? ResolveDocument(ThemeDocumentInput? input)
    {
        if (input is null) return null;
        using TLBytes? stored = _documentsRepository
            .GetDocument(input.Value.Id);
        if (stored is null) return null;
        DocumentView view = (DocumentView)stored.Value.AsSpan();
        if (!view.Is(out Document document) ||
            document.AccessHash != input.Value.AccessHash ||
            !document.FileReference.SequenceEqual(input.Value.FileReference))
            return null;
        return document.Clone().Build();
    }

    private async ValueTask<TLThemeCatalogState?> ResolveAsync(ThemeInput input)
    {
        TLThemeCatalogState? row = input.Kind == ThemeInputKind.Slug
            ? await _repository.GetThemeCatalogBySlugAsync(input.Slug)
            : await _repository.GetThemeCatalogAsync(input.Id);
        if (row is null) return null;
        Theme theme = row.Value.AsThemeCatalogState().Get_ThemeView().AsTheme();
        if (input.Kind == ThemeInputKind.Id &&
            theme.AccessHash != input.AccessHash)
        {
            row.Value.Dispose();
            return null;
        }
        return row;
    }

    private async ValueTask<bool> ExistsAsync(long id)
    {
        using TLThemeCatalogState? row = await _repository
            .GetThemeCatalogAsync(id);
        return row is not null;
    }

    private static TLTheme Rebuild(Theme source, bool creator)
    {
        var builder = Theme.Builder().Creator(creator)
            .DefaultProperty(source.DefaultProperty).ForChat(source.ForChat)
            .Id(source.Id).AccessHash(source.AccessHash).Slug(source.Slug)
            .Title(source.Title);
        if (source.Flags[2]) builder = builder.Document(source.Document);
        if (source.Flags[3]) builder = builder.Settings(source.Settings);
        if (source.Flags[6]) builder = builder.Emoticon(source.Emoticon);
        if (source.Flags[4]) builder = builder.InstallsCount(source.InstallsCount);
        return builder.Build();
    }

    private static Vector ToVector(IEnumerable<TLThemeSettings> values)
    {
        var vector = new Vector();
        foreach (TLThemeSettings value in values)
            vector.AppendTLObject(value.AsSpan());
        return vector;
    }

    private static bool ValidSlug(string value) => SlugPattern.IsMatch(value);
    private static bool ValidTitle(string value) =>
        value.Trim().Length is > 0 and <= 128;
    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant();
    private int Now() => (int)_time.GetUtcNow().ToUnixTimeSeconds();

    private static void Dispose(IEnumerable<TLThemeSettings> values)
    {
        foreach (TLThemeSettings value in values) value.Dispose();
    }

    private static void Dispose(IEnumerable<TLThemeCatalogState> values)
    {
        foreach (TLThemeCatalogState value in values) value.Dispose();
    }

    private static void Dispose(IEnumerable<TLAccountThemeState> values)
    {
        foreach (TLAccountThemeState value in values) value.Dispose();
    }

    private static TLBool True() => BoolTrue.Builder().Build();
    private static TLBytes Invalid() => Error(400, "THEME_INVALID");
    private static TLBool InvalidBool() => (TLBool)Invalid();
    private static TLBytes SlugInvalid() => Error(400, "THEME_SLUG_INVALID");
    private static TLBytes SlugOccupied() => Error(400, "THEME_SLUG_OCCUPIED");
    private static TLBytes Internal() => Error(500, "INTERNAL_SERVER_ERROR");
    private static TLBool InternalBool() => (TLBool)Internal();
    private static TLBytes Error(int code, string message) =>
        RpcErrorGenerator.GenerateError(code, Encoding.UTF8.GetBytes(message));
    private static TLBytes Copy(ReadOnlySpan<byte> value)
    {
        byte[] bytes = value.ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
