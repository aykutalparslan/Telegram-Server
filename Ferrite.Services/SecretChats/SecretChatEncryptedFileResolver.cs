// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats;

public enum SecretChatEncryptedFileInputKind
{
    Invalid,
    Empty,
    Uploaded,
    BigUploaded,
    Existing
}

public readonly record struct SecretChatEncryptedFileInput(
    SecretChatEncryptedFileInputKind Kind, long Id, long AccessHash, int Parts,
    string? Md5Checksum, int KeyFingerprint);

public sealed class SecretChatEncryptedFileResolver
{
    private readonly IFileInfoRepository _fileInfoRepository;
    private readonly ISecretChatsRepository _secretChatsRepository;

    private const int GateCount = 256;
    private const int FileIdAttempts = 16;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUploadService _upload;
    private readonly IRandomGenerator _random;
    private readonly SecretChatLimits _limits;
    private readonly ILogger _log;
    private readonly SecretChatTelemetry? _telemetry;
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, GateCount)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public SecretChatEncryptedFileResolver(IUnitOfWork unitOfWork, IFileInfoRepository fileInfoRepository, ISecretChatsRepository secretChatsRepository,
        IUploadService upload, IRandomGenerator random, SecretChatLimits limits,
        ILogger log, SecretChatTelemetry? telemetry = null)
    {
        _fileInfoRepository = fileInfoRepository;
        _secretChatsRepository = secretChatsRepository;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.MaxEncryptedFileAssociations);
        _unitOfWork = unitOfWork;
        _upload = upload;
        _random = random;
        _limits = limits;
        _log = log;
        _telemetry = telemetry;
    }

    public static SecretChatEncryptedFileInput Parse(InputEncryptedFileView file)
    {
        if (file.Is(out InputEncryptedFileUploaded uploaded))
        {
            return new SecretChatEncryptedFileInput(
                SecretChatEncryptedFileInputKind.Uploaded, uploaded.Id, 0,
                uploaded.Parts, Encoding.UTF8.GetString(uploaded.Md5Checksum),
                uploaded.KeyFingerprint);
        }
        if (file.Is(out InputEncryptedFileBigUploaded bigUploaded))
        {
            return new SecretChatEncryptedFileInput(
                SecretChatEncryptedFileInputKind.BigUploaded, bigUploaded.Id, 0,
                bigUploaded.Parts, null, bigUploaded.KeyFingerprint);
        }
        if (file.Is(out InputEncryptedFile existing))
        {
            return new SecretChatEncryptedFileInput(
                SecretChatEncryptedFileInputKind.Existing, existing.Id,
                existing.AccessHash, 0, null, 0);
        }
        if (file.Is(out InputEncryptedFileEmpty _))
        {
            return new SecretChatEncryptedFileInput(
                SecretChatEncryptedFileInputKind.Empty, 0, 0, 0, null, 0);
        }
        return new SecretChatEncryptedFileInput(
            SecretChatEncryptedFileInputKind.Invalid, 0, 0, 0, null, 0);
    }

    public async ValueTask<ServiceResult<TLDto.TLSecretChatEncryptedFile?>>
        ResolveAsync(int chatId, SecretChatEncryptedFileInput input, int date,
            CancellationToken cancellationToken = default)
    {
        _log.Debug($"🔐 ResolveEncryptedFile chat:{chatId} kind:{input.Kind} " +
                   $"id:{input.Id} parts:{input.Parts}");
        if (input.Kind is SecretChatEncryptedFileInputKind.Empty or
            SecretChatEncryptedFileInputKind.Invalid)
        {
            _telemetry?.Rejection("encrypted_file_resolve", 0, chatId,
                "file_id_invalid");
            return Error(ErrorMessages.FileIdInvalid);
        }

        SemaphoreSlim gate = GetGate(input.Id);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatEncryptedFile? file;
            if (input.Kind == SecretChatEncryptedFileInputKind.Existing)
            {
                file = await _secretChatsRepository.GetEncryptedFileAsync(
                    input.Id, input.AccessHash, cancellationToken);
            }
            else
            {
                ServiceResult<TLDto.TLSecretChatEncryptedFile?> uploaded =
                    await ResolveUploadedAsync(input, date, cancellationToken);
                if (!uploaded.Success)
                {
                    return uploaded;
                }
                file = uploaded.Result;
            }
            if (file is null)
            {
                _log.Debug($"🔐 ResolveEncryptedFile missing id:{input.Id}");
                _telemetry?.Rejection("encrypted_file_resolve", 0, chatId,
                    "file_missing");
                return Error(ErrorMessages.FileIdInvalid);
            }

            TLDto.SecretChatEncryptedFile row = file.Value.AsSecretChatEncryptedFile();
            if ((input.Kind is SecretChatEncryptedFileInputKind.Uploaded or
                    SecretChatEncryptedFileInputKind.BigUploaded) &&
                row.KeyFingerprint != input.KeyFingerprint)
            {
                file.Value.Dispose();
                return Error(ErrorMessages.FileIdInvalid);
            }
            long storedFileId = row.FileId;
            long storedUploadFileId = row.UploadFileId;
            long storedSize = row.Size;

            using TLDto.TLSecretChatEncryptedFileAssociation association =
                TLDto.SecretChatEncryptedFileAssociation.Builder()
                    .FileId(storedFileId)
                    .AccessHash(row.AccessHash)
                    .ChatId(chatId)
                    .Date(date)
                    .Build();
            SecretChatFileAssociationStatus associationStatus = await _secretChatsRepository.TryAssociateEncryptedFileAsync(association,
                    _limits.MaxEncryptedFileAssociations, cancellationToken);
            if (associationStatus == SecretChatFileAssociationStatus.LimitExceeded)
            {
                file.Value.Dispose();
                _telemetry?.Rejection("encrypted_file_associate", 0, chatId,
                    "association_limit_exceeded");
                return Error(ErrorMessages.EncryptedFileAssociationsTooMuch);
            }
            _log.Debug($"🔐 ResolveEncryptedFile ready chat:{chatId} " +
                       $"file:{storedFileId} upload:{storedUploadFileId} " +
                       $"size:{storedSize}");
            return new ServiceResult<TLDto.TLSecretChatEncryptedFile?>(file, true,
                ErrorMessages.None);
        }
        finally
        {
            gate.Release();
        }
    }

    public static TLEncryptedFile BuildWireFile(
        TLDto.TLSecretChatEncryptedFile stored)
    {
        TLDto.SecretChatEncryptedFile row = stored.AsSecretChatEncryptedFile();
        return EncryptedFile.Builder()
            .Id(row.FileId)
            .AccessHash(row.AccessHash)
            .Size(row.Size)
            .DcId(row.DcId)
            .KeyFingerprint(row.KeyFingerprint)
            .Build();
    }

    private async ValueTask<ServiceResult<TLDto.TLSecretChatEncryptedFile?>>
        ResolveUploadedAsync(
        SecretChatEncryptedFileInput input, int date,
        CancellationToken cancellationToken)
    {
        TLDto.TLSecretChatEncryptedFile? existing = await _secretChatsRepository.GetEncryptedFileByUploadIdAsync(input.Id,
                cancellationToken);
        if (existing is not null)
        {
            _log.Debug($"🔐 FinalizeEncryptedFile reuse upload:{input.Id}");
            return new ServiceResult<TLDto.TLSecretChatEncryptedFile?>(existing,
                true, ErrorMessages.None);
        }

        bool isBigFile = input.Kind == SecretChatEncryptedFileInputKind.BigUploaded;
        TLInputFile upload = isBigFile
            ? InputFileBig.Builder().Id(input.Id).Parts(input.Parts).Name([]).Build()
            : InputFile.Builder().Id(input.Id).Parts(input.Parts).Name([])
                .Md5Checksum(System.Text.Encoding.UTF8.GetBytes(
                    input.Md5Checksum ?? string.Empty)).Build();
        ServiceResult<TLDto.TLUploadedFileInfo?> finalized;
        using (upload)
        {
            finalized = await _upload.SaveEncryptedFile(upload);
        }
        if (!finalized.Success)
        {
            _log.Debug($"🔐 FinalizeEncryptedFile rejected upload:{input.Id} " +
                       $"error:{finalized.ErrorMessage.Message}");
            return new ServiceResult<TLDto.TLSecretChatEncryptedFile?>(null,
                false, finalized.ErrorMessage);
        }
        if (finalized.Result is null)
        {
            _log.Debug($"🔐 FinalizeEncryptedFile empty result upload:{input.Id}");
            return Error(ErrorMessages.InternalServerError);
        }

        using TLDto.TLUploadedFileInfo uploaded = finalized.Result.Value;
        long size = GetUploadSize(input.Id, input.Parts, isBigFile);
        if (size < 0)
        {
            return Error(ErrorMessages.FilePartsInvalid);
        }

        long? fileId = await AllocateFileIdAsync(cancellationToken);
        if (fileId is null)
        {
            return Error(ErrorMessages.InternalServerError);
        }
        using TLDto.TLSecretChatEncryptedFile file =
            TLDto.SecretChatEncryptedFile.Builder()
                .FileId(fileId.Value)
                .AccessHash(_random.NextLong())
                .UploadFileId(input.Id)
                .Size(size)
                .DcId(MediaDefaults.DcId)
                .KeyFingerprint(input.KeyFingerprint)
                .Date(date)
                .Build();
        if (!await _secretChatsRepository.PutEncryptedFileAsync(file,
                cancellationToken))
        {
            return Error(ErrorMessages.InternalServerError);
        }
        _log.Debug($"🔐 FinalizeEncryptedFile stored upload:{input.Id} " +
                   $"file:{fileId.Value} size:{size} big:{isBigFile}");
        byte[] bytes = file.AsSpan().ToArray();
        return new ServiceResult<TLDto.TLSecretChatEncryptedFile?>(
            new TLDto.TLSecretChatEncryptedFile(bytes, 0, bytes.Length), true,
            ErrorMessages.None);
    }

    private long GetUploadSize(long uploadFileId, int parts, bool isBigFile)
    {
        IReadOnlyCollection<TLDto.TLFilePart> storedParts = isBigFile
            ? _fileInfoRepository.GetBigFileParts(uploadFileId)
            : _fileInfoRepository.GetFileParts(uploadFileId);
        long size = 0;
        HashSet<int> partNumbers = new();
        bool invalid = false;
        foreach (TLDto.TLFilePart value in storedParts)
        {
            using (value)
            {
                TLDto.FilePart part = value.AsFilePart();
                if (part.PartNum < 0 || part.PartNum >= parts ||
                    !partNumbers.Add(part.PartNum))
                {
                    invalid = true;
                    continue;
                }
                size = checked(size + part.PartSize);
            }
        }
        return !invalid && partNumbers.Count == parts ? size : -1;
    }

    private async ValueTask<long?> AllocateFileIdAsync(
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < FileIdAttempts; attempt++)
        {
            long candidate = _random.NextLong();
            if (candidate == 0)
            {
                continue;
            }
            TLDto.TLSecretChatEncryptedFile? collision = await _secretChatsRepository.GetEncryptedFileByIdAsync(candidate,
                    cancellationToken);
            if (collision is null)
            {
                return candidate;
            }
            collision.Value.Dispose();
        }
        return null;
    }

    private SemaphoreSlim GetGate(long fileId)
    {
        ulong key = unchecked((ulong)fileId);
        return _gates[(int)(key % (uint)_gates.Length)];
    }

    private static ServiceResult<TLDto.TLSecretChatEncryptedFile?> Error(
        ErrorMessage error) => new(null, false, error);
}
