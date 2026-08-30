// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IChatRepository
{
    public bool PutChat(TLChat chat);
    public ValueTask<TLChat?> GetChatAsync(long chatId);
    public bool DeleteChat(long chatId);
    public bool PutFullInfo(TLChatFullInfo fullInfo);
    public ValueTask<TLChatFullInfo?> GetFullInfoAsync(long chatId);
    public bool DeleteFullInfo(long chatId);
    public bool PutUsername(string username, long chatId);
    public long? GetChatIdByUsername(string username);
    public bool DeleteUsername(string username);
}
