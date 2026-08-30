// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;
using TLPhoto = Ferrite.TL.baseLayer.TLPhoto;

namespace Ferrite.Services.Media;

public interface IPhotoProcessingService
{
    ValueTask<ServiceResult<TLPhoto?>> ProcessPhoto(TLUploadedFileInfo file);
}
