// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Calls;

public readonly record struct GroupCallRecordingDocument(long FileId,
    byte[] MediaBytes);

public interface IGroupCallRecordingDelivery
{
    ValueTask<GroupCallRecordingDocument> ImportAsync(GroupCallRecordingFile file,
        CancellationToken cancellationToken = default);

    Task<StoredMessageWrite> StoreAsync(long userId,
        GroupCallRecordingDocument document, string title, int date);

    Task PublishAsync(long userId, StoredMessageWrite write);
}

public sealed class GroupCallRecordingDelivery : IGroupCallRecordingDelivery
{
    private const int BigFileThreshold = 10 * 1024 * 1024;

    private readonly IUploadService _upload;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageStore _messages;
    private readonly UpdateFanout _fanout;
    private readonly IRandomGenerator _random;
    private readonly GroupCallRecordingOptions _options;

    public GroupCallRecordingDelivery(IUploadService upload, IUnitOfWork unitOfWork,
        MessageStore messages, UpdateFanout fanout, IRandomGenerator random,
        GroupCallRecordingOptions options)
    {
        options.Validate();
        _upload = upload;
        _unitOfWork = unitOfWork;
        _messages = messages;
        _fanout = fanout;
        _random = random;
        _options = options;
    }

    public async ValueTask<GroupCallRecordingDocument> ImportAsync(
        GroupCallRecordingFile file, CancellationToken cancellationToken = default)
    {
        if (file.ContentLength > _options.MaxRecordingBytes ||
            file.ContentLength > GroupCallRecordingOptions.UploadMaximumBytes)
        {
            throw Failure("recording exceeds the configured upload limit");
        }

        int parts = checked((int)((file.ContentLength + UploadService.MaxPartSize - 1) /
                                  UploadService.MaxPartSize));
        if (parts is < 1 or > UploadService.MaxFileParts)
        {
            throw Failure("recording has an invalid upload part count");
        }

        long fileId = NextFileId();
        bool big = file.ContentLength > BigFileThreshold;
        long remaining = file.ContentLength;
        for (int part = 0; part < parts; part++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = checked((int)Math.Min(remaining, UploadService.MaxPartSize));
            byte[] bytes = new byte[length];
            await ReadExactlyAsync(file.Content, bytes, cancellationToken);
            using var stream = new MemoryStream(bytes, writable: false);
            ServiceResult<bool> saved = big
                ? await _upload.SaveBigFilePart(fileId, part, parts, stream)
                : await _upload.SaveFilePart(fileId, part, stream);
            if (!saved.Success || saved.Result != true)
            {
                throw Failure($"recording upload part {part} failed: " +
                              saved.ErrorMessage.Message);
            }
            remaining -= length;
        }

        byte[] extra = new byte[1];
        if (await file.Content.ReadAsync(extra.AsMemory(), cancellationToken) != 0)
        {
            throw Failure("recording response exceeded its declared content length");
        }

        TLInputFile input = big
            ? InputFileBig.Builder().Id(fileId).Parts(parts)
                .Name(System.Text.Encoding.UTF8.GetBytes(file.FileName)).Build()
            : InputFile.Builder().Id(fileId).Parts(parts)
                .Name(System.Text.Encoding.UTF8.GetBytes(file.FileName))
                .Md5Checksum([]).Build();
        ServiceResult<Ferrite.TL.baseLayer.dto.TLUploadedFileInfo?> finalized;
        using (input)
        {
            finalized = await _upload.SaveFile(input);
        }
        if (!finalized.Success || finalized.Result == null)
        {
            throw Failure("recording upload finalization failed: " +
                          finalized.ErrorMessage.Message);
        }

        using var uploaded = finalized.Result.Value;
        byte[] attributes = BuildAttributes(file);
        ServiceResult<TLBytes?> registered = await _upload.RegisterDocument(uploaded,
            Encoding.UTF8.GetBytes(file.MimeType), attributes, null);
        if (!registered.Success || registered.Result == null)
        {
            throw Failure("recording document registration failed: " +
                          registered.ErrorMessage.Message);
        }

        using TLBytes document = registered.Result.Value;
        using TLMessageMedia media = MessageMediaDocument.Builder()
            .Document(document.AsSpan())
            .Build();
        return new GroupCallRecordingDocument(fileId, media.AsSpan().ToArray());
    }

    public Task<StoredMessageWrite> StoreAsync(long userId,
        GroupCallRecordingDocument document, string title, int date)
        => _messages.PutSelfMediaMessageAsync(userId, document.MediaBytes,
            Encoding.UTF8.GetBytes(title), date);

    public Task PublishAsync(long userId, StoredMessageWrite write) =>
        _fanout.EnqueueNewMessageAsync(userId, write.Bytes, write.Pts);

    private long NextFileId()
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            long id = _random.NextLong() & long.MaxValue;
            if (id != 0)
            {
                return id;
            }
        }
        throw Failure("could not allocate a recording file id");
    }

    private static byte[] BuildAttributes(GroupCallRecordingFile file)
    {
        var attributes = new Vector();
        using (TLDocumentAttribute filename = DocumentAttributeFilename.Builder()
                   .FileName(Encoding.UTF8.GetBytes(file.FileName))
                   .Build())
        {
            attributes.AppendTLObject(filename.AsSpan());
        }

        if (file.Width > 0 && file.Height > 0)
        {
            using TLDocumentAttribute video = DocumentAttributeVideo.Builder()
                .SupportsStreaming(true)
                .Duration(file.DurationSeconds)
                .W(file.Width)
                .H(file.Height)
                .Build();
            attributes.AppendTLObject(video.AsSpan());
        }
        else
        {
            int duration = file.DurationSeconds >= int.MaxValue
                ? int.MaxValue
                : Math.Max(0, (int)Math.Ceiling(file.DurationSeconds));
            using TLDocumentAttribute audio = DocumentAttributeAudio.Builder()
                .Duration(duration)
                .Build();
            attributes.AppendTLObject(audio.AsSpan());
        }

        return attributes.ToReadOnlySpan().ToArray();
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < destination.Length)
        {
            int read = await stream.ReadAsync(destination[offset..], cancellationToken);
            if (read == 0)
            {
                throw Failure("recording response ended before its declared content length");
            }
            offset += read;
        }
    }

    private static GroupCallRecordingException Failure(string message) =>
        new(GroupCallRecordingFailureKind.InvalidResponse, message);
}
