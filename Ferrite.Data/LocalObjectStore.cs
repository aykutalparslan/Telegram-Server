// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using FASTER.core;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data;

public sealed class LocalObjectStore : IObjectStore, IAsyncDisposable
{
    private readonly string _parentDir;
    private readonly string _smallFilesDir;
    private readonly string _bigFilesDir;
    private FasterContext<ObjectId, ObjectMetadata> _metadataStore;
    private readonly ClientSession<ObjectId, ObjectMetadata, ObjectMetadata, ObjectMetadata, Empty, 
        IFunctions<ObjectId, ObjectMetadata, ObjectMetadata, ObjectMetadata, Empty>> _session;

    public LocalObjectStore(string path)
    {
        _parentDir = path;
        _smallFilesDir = Path.Combine(_parentDir, "small");
        _bigFilesDir = Path.Combine(_parentDir, "big");
        _metadataStore = new FasterContext<ObjectId, ObjectMetadata>(path+"-faster-object-metadata");
        _session = _metadataStore.Store.NewSession(new SimpleFunctions<ObjectId, ObjectMetadata>());
        if (!Directory.Exists(_parentDir)) Directory.CreateDirectory(_parentDir);
        if (!Directory.Exists(_smallFilesDir)) Directory.CreateDirectory(_smallFilesDir);
        if (!Directory.Exists(_bigFilesDir)) Directory.CreateDirectory(_bigFilesDir);
    }
    public async ValueTask<bool> SaveFilePart(long fileId, int filePart, Stream data)
    {
        ObjectId key = new (fileId, filePart);
        ObjectMetadata metadata = new (fileId, filePart, 
            (int)data.Length, DateTimeOffset.Now, false);
        return await SaveFile(data, key, metadata);
    }

    private async Task<bool> SaveFile(Stream data, ObjectId key, ObjectMetadata metadata)
    {
        _session.Upsert(key, metadata);
        var filePath = GetFilePath(metadata);
        if (File.Exists(filePath)) File.Delete(filePath);
        await using var fileStream = File.Create(filePath);
        await data.CopyToAsync(fileStream);
        fileStream.Close();
        await _session.WaitForCommitAsync();
        return true;
    }

    private string GetFilePath(ObjectMetadata metadata)
    {
        string folderName = Path.Combine(metadata.IsBig ? _bigFilesDir : _smallFilesDir,
            metadata.Timestamp.Year +
            metadata.Timestamp.Month.ToString("00") + metadata.Timestamp.Day.ToString("00"));
        if (!Directory.Exists(folderName)) Directory.CreateDirectory(folderName);
        string fileName = metadata.FileId.ToString("X") + "-" + metadata.PartNum.ToString("X");
        var filePath = Path.Combine(folderName, fileName);
        return filePath;
    }

    public async ValueTask<bool> SaveBigFilePart(long fileId, int filePart, int fileTotalParts, Stream data)
    {
        ObjectId key = new (fileId, filePart);
        ObjectMetadata metadata = new (fileId, filePart, 
            (int)data.Length, DateTimeOffset.Now, true);
        return await SaveFile(data, key, metadata);
    }

    public ValueTask<Stream> GetFilePart(long fileId, int filePart)
    {
        return ValueTask.FromResult(GetFileStream(fileId, filePart));
    }

    public ValueTask<Stream> GetBigFilePart(long fileId, int filePart)
    {
        return ValueTask.FromResult(GetFileStream(fileId, filePart));
    }

    public IFileOwner GetFileOwner(TLUploadedFileInfo fileInfo, long offset,
        int limit, long reqMsgId, byte[] headerBytes)
    {
        return new LocalFileOwner(fileInfo, this, offset, limit, reqMsgId, headerBytes);
    }

    private Stream GetFileStream(long fileId, int filePart)
    {
        ObjectId key = new(fileId, filePart);
        _session.Read(key, out var metadata);
        var path = GetFilePath(metadata);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async ValueTask DisposeAsync()
    {
        _session.Dispose();
        await _metadataStore.DisposeAsync();
    }
}
