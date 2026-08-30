// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.photos;
using TLPhotosPhoto = Ferrite.TL.baseLayer.photos.TLPhoto;

namespace Ferrite.Services.Handlers.PhotoMethods;

public sealed class UploadContactProfilePhotoHandler
{
    [TLFunction(Constructors.baseLayer_UploadContactProfilePhoto)]
    public ValueTask<TLPhotosPhoto> Handle(long authKeyId, TLBytes q)
    {
        TLPhotosPhoto error = (TLPhotosPhoto)RpcErrorGenerator.GenerateError(
            ErrorMessages.UserIdInvalid.Code,
            Encoding.UTF8.GetBytes(ErrorMessages.UserIdInvalid.Message));
        return ValueTask.FromResult(error);
    }
}
