// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Data.Models;

namespace Ferrite.Data.Repositories;

public interface IPrivacyRulesRepository
{
    public bool PutPrivacyRules(long userId, InputPrivacyKey key, Vector rules);
    public ValueTask<ICollection<TLPrivacyRule>> GetPrivacyRulesAsync(long userId, InputPrivacyKey key);
    public bool DeletePrivacyRules(long userId, InputPrivacyKey key);
    public bool DeletePrivacyRules(long userId);
}