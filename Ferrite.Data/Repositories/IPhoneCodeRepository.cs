// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public interface IPhoneCodeRepository
{
    public void PutPhoneCode(string phoneNumber, string phoneCodeHash, string phoneCode,
        TimeSpan expiresIn);
    public string? GetPhoneCode(string phoneNumber, string phoneCodeHash);
    public bool DeletePhoneCode(string phoneNumber, string phoneCodeHash);
}