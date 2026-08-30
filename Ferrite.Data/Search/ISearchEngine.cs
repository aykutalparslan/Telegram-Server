// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Search;

namespace Ferrite.Data.Search;

public interface ISearchEngine
{
    public ValueTask<bool> IndexUser(Search.UserSearchModel user);
    public ValueTask<bool> DeleteUser(long userId);
    public ValueTask<List<UserSearchModel>> SearchUser(string q, int limit);
    public ValueTask<bool> IndexChat(Search.ChatSearchModel chat);
    public ValueTask<bool> DeleteChat(long chatId);
    public ValueTask<List<ChatSearchModel>> SearchChats(string q, int limit);
    public ValueTask<bool> IndexMessage(MessageSearchModel message);
    public ValueTask<bool> DeleteMessage(string id);
    public ValueTask<List<MessageSearchModel>> SearchMessages(string q);

    public ValueTask<List<MessageSearchModel>> SearchMessageCandidates(
        MessageCandidateQuery query);
}