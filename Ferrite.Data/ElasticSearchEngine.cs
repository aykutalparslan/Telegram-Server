// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Elasticsearch.Net;
using Ferrite.Data.Search;
using Nest;

namespace Ferrite.Data;

public class ElasticSearchEngine : ISearchEngine
{
    private const string UsersIndex = "users";
    private const string ChatsIndex = "chats";
    private const string MessagesIndex = "messages";
    private const int DefaultCandidateLimit = 500;
    private readonly ElasticClient _client;

    public ElasticSearchEngine(string url,string username, string password, string fingerprint)
    {
        var uri = new Uri(url);
        var pool = new SingleNodeConnectionPool(uri);
        var connectionSettings = new ConnectionSettings(pool)
            .RequestTimeout(TimeSpan.FromSeconds(5));
        if (!string.IsNullOrWhiteSpace(username))
        {
            connectionSettings = connectionSettings.BasicAuthentication(
                username, password);
        }
        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            connectionSettings = connectionSettings.CertificateFingerprint(
                fingerprint);
        }
        _client = new ElasticClient(connectionSettings);
    }

    public async ValueTask<bool> IndexUser(Search.UserSearchModel user)
    {
        var result = await _client.IndexAsync(user, descriptor => descriptor
            .Index(UsersIndex).Id(user.Id).Refresh(Refresh.WaitFor));
        return result.IsValid;
    }

    public async ValueTask<bool> DeleteUser(long userId)
    {
        var result = await _client.DeleteAsync<UserSearchModel>(userId,
            descriptor => descriptor.Index(UsersIndex).Refresh(Refresh.WaitFor));
        return result.IsValid || result.ServerError?.Status == 404;
    }

    public async ValueTask<List<UserSearchModel>> SearchUser(string q, int limit)
    {
        var result = await _client.SearchAsync<UserSearchModel>(search => search
            .Index(UsersIndex)
            .Size(Math.Min(limit, 50))
            .Query(query => query.Bool(boolean => boolean
                .MinimumShouldMatch(1)
                .Should(
                    clause => clause.Prefix(prefix => prefix
                        .Field(user => user.Username).Value(q)),
                    clause => clause.Prefix(prefix => prefix
                        .Field(user => user.FirstName).Value(q)),
                    clause => clause.Prefix(prefix => prefix
                        .Field(user => user.LastName).Value(q))))));
        return result.IsValid ? result.Documents.ToList() : [];
    }

    public async ValueTask<bool> IndexChat(Search.ChatSearchModel chat)
    {
        var result = await _client.IndexAsync(chat, descriptor => descriptor
            .Index(ChatsIndex).Id(chat.Id).Refresh(Refresh.WaitFor));
        return result.IsValid;
    }

    public async ValueTask<bool> DeleteChat(long chatId)
    {
        var result = await _client.DeleteAsync<ChatSearchModel>(chatId,
            descriptor => descriptor.Index(ChatsIndex).Refresh(Refresh.WaitFor));
        return result.IsValid || result.ServerError?.Status == 404;
    }

    public async ValueTask<List<ChatSearchModel>> SearchChats(string q, int limit)
    {
        var result = await _client.SearchAsync<ChatSearchModel>(search => search
            .Index(ChatsIndex)
            .Size(Math.Min(limit, 50))
            .Query(query => query.Bool(boolean => boolean
                .MinimumShouldMatch(1)
                .Should(
                    clause => clause.Prefix(prefix => prefix
                        .Field(chat => chat.Username).Value(q)),
                    clause => clause.Prefix(prefix => prefix
                        .Field(chat => chat.Title).Value(q))))));
        return result.IsValid ? result.Documents.ToList() : [];
    }

    public async ValueTask<bool> IndexMessage(MessageSearchModel message)
    {
        var result = await _client.IndexAsync(message, descriptor => descriptor
            .Index(MessagesIndex).Id(message.Id).Refresh(Refresh.WaitFor));
        return result.IsValid;
    }

    public async ValueTask<bool> DeleteMessage(string id)
    {
        var result = await _client.DeleteAsync<MessageSearchModel>(id,
            descriptor => descriptor.Index(MessagesIndex).Refresh(Refresh.WaitFor));
        return result.IsValid || result.ServerError?.Status == 404;
    }

    public async ValueTask<List<MessageSearchModel>> SearchMessages(string q)
    {
        var result = await _client.SearchAsync<MessageSearchModel>(search => search
            .Index(MessagesIndex)
            .Size(50)
            .Query(query => query.MatchPhrasePrefix(prefix => prefix
                .Field(message => message.Message).Query(q))));
        return result.IsValid ? result.Documents.ToList() : [];
    }

    public async ValueTask<List<MessageSearchModel>> SearchMessageCandidates(
        MessageCandidateQuery query)
    {
        var result = await _client.SearchAsync<MessageSearchModel>(search => search
            .Index(MessagesIndex)
            .Size(query.Limit > 0 ? query.Limit : DefaultCandidateLimit)
            .Query(root => root.Bool(boolean =>
            {
                var filters = new List<Func<QueryContainerDescriptor<MessageSearchModel>,
                    QueryContainer>>();
                if (query.UserId is { } userId)
                {
                    filters.Add(filter => filter.Term(term => term
                        .Field(message => message.UserId).Value(userId)));
                }
                if (query.PeerType is { } peerType)
                {
                    filters.Add(filter => filter.Term(term => term
                        .Field(message => message.PeerType).Value(peerType)));
                }
                if (query.PeerId is { } peerId)
                {
                    filters.Add(filter => filter.Term(term => term
                        .Field(message => message.PeerId).Value(peerId)));
                }
                if (!string.IsNullOrWhiteSpace(query.Text))
                {
                    filters.Add(filter => filter.MatchPhrasePrefix(prefix => prefix
                        .Field(message => message.Message).Query(query.Text)));
                }
                return boolean.Filter(filters);
            })));
        return result.IsValid ? result.Documents.ToList() : [];
    }
}
