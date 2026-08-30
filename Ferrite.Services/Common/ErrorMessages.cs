// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext;

namespace Ferrite.Services.Common;

public class ErrorMessages
{
    public static readonly ErrorMessage None = new ErrorMessage(0, string.Empty);
    public static readonly ErrorMessage PhoneNumberOccupied = new ErrorMessage(400, "PHONE_NUMBER_OCCUPIED");
    public static readonly ErrorMessage FreshChangePhoneForbidden = new ErrorMessage(406, "FRESH_CHANGE_PHONE_FORBIDDEN");
    public static readonly ErrorMessage PhoneNumberBanned = new ErrorMessage(400, "PHONE_NUMBER_BANNED");
    public static readonly ErrorMessage PhoneNumberInvalid = new ErrorMessage(406, "PHONE_NUMBER_INVALID");
    public static readonly ErrorMessage HashInvalid = new ErrorMessage(400, "HASH_INVALID");
    public static readonly ErrorMessage FreshResetAuthorizationForbidden = new ErrorMessage(406, "FRESH_RESET_AUTHORISATION_FORBIDDEN");
    public static readonly ErrorMessage UserIdInvalid = new ErrorMessage(400, "USER_ID_INVALID");
    public static readonly ErrorMessage PhotoFileMissing = new ErrorMessage(400, "PHOTO_FILE_MISSING");
    public static readonly ErrorMessage FilePartsInvalid  = new ErrorMessage(400, "FILE_PARTS_INVALID");
    public static readonly ErrorMessage FilePartInvalid = new ErrorMessage(400, "FILE_PART_INVALID");
    public static readonly ErrorMessage FilePartEmpty = new ErrorMessage(400, "FILE_PART_EMPTY");
    public static readonly ErrorMessage FilePartTooBig = new ErrorMessage(400, "FILE_PART_TOO_BIG");
    public static readonly ErrorMessage FilePartSizeInvalid = new ErrorMessage(400, "FILE_PART_SIZE_INVALID");
    public static readonly ErrorMessage FilePartSizeChanged = new ErrorMessage(400, "FILE_PART_SIZE_CHANGED");
    public static readonly ErrorMessage Md5ChecksumInvalid = new ErrorMessage(400, "MD5_CHECKSUM_INVALID");
    public static ErrorMessage FilePartMissing(int partNum) => new ErrorMessage(400, $"FILE_PART_{partNum}_MISSING");
    public static readonly ErrorMessage InternalServerError = new ErrorMessage(500, "INTERNAL_SERVER_ERROR");
    public static readonly ErrorMessage PhotoFileTooBig   = new ErrorMessage(400, "PHOTO_FILE_TOO_BIG");
    public static readonly ErrorMessage PhotoFileInvalid = new ErrorMessage(400, "PHOTO_FILE_INVALID");
    public static readonly ErrorMessage PhotoIdInvalid = new ErrorMessage(400, "PHOTO_ID_INVALID");
    public static readonly ErrorMessage PhotoInvalid = new ErrorMessage(400, "PHOTO_INVALID");
    public static readonly ErrorMessage VideoFileInvalid = new ErrorMessage(400, "VIDEO_FILE_INVALID");
    public static readonly ErrorMessage MediaInvalid = new ErrorMessage(400, "MEDIA_INVALID");
    public static readonly ErrorMessage MediaEmpty = new ErrorMessage(400, "MEDIA_EMPTY");
    public static readonly ErrorMessage MultiMediaTooLong = new ErrorMessage(400, "MULTI_MEDIA_TOO_LONG");
    public static readonly ErrorMessage ChatSendPhotosForbidden = new ErrorMessage(400, "CHAT_SEND_PHOTOS_FORBIDDEN");
    public static readonly ErrorMessage ChatSendDocsForbidden = new ErrorMessage(400, "CHAT_SEND_DOCS_FORBIDDEN");
    public static readonly ErrorMessage LocationInvalid = new ErrorMessage(400, "LOCATION_INVALID");
    public static readonly ErrorMessage FileReferenceExpired = new ErrorMessage(400, "FILE_REFERENCE_EXPIRED");
    public static readonly ErrorMessage OffsetInvalid = new ErrorMessage(400, "OFFSET_INVALID");
    public static readonly ErrorMessage LimitInvalid = new ErrorMessage(400, "LIMIT_INVALID");
    public static readonly ErrorMessage TimeTooBig = new ErrorMessage(400, "TIME_TOO_BIG");
    public static readonly ErrorMessage GroupCallJoinMissing =
        new ErrorMessage(400, "GROUPCALL_JOIN_MISSING");
    public static readonly ErrorMessage FileIdInvalid = new ErrorMessage(400, "FILE_ID_INVALID");
    public static readonly ErrorMessage EncryptedFileAssociationsTooMuch =
        new ErrorMessage(400, "ENCRYPTED_FILE_ASSOCIATIONS_TOO_MUCH");
    public static readonly ErrorMessage PeerIdInvalid = new ErrorMessage(400, "PEER_ID_INVALID");
    public static readonly ErrorMessage InvalidAuthKey = new ErrorMessage(400, "INVALID_AUTH_KEY");
    public static readonly ErrorMessage UsernameInvalid = new ErrorMessage(400, "USERNAME_INVALID");
}
