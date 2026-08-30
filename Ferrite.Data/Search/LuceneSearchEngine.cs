// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Search;
using Lucene.Net.Analysis.En;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Surround.Parser;
using Lucene.Net.QueryParsers.Surround.Query;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Ferrite.Data.Models;

namespace Ferrite.Data.Search;

public class LuceneSearchEngine : ISearchEngine, IDisposable
{
    private const int DefaultCandidateLimit = 500;
    private readonly string _path;
    private readonly LuceneContext _users;
    private readonly LuceneContext _messages;
    private readonly LuceneContext _chats;

    public LuceneSearchEngine(string path)
    {
        _path = path;
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        _users = new(Path.Combine(path,"lucene-users"));
        _messages = new(Path.Combine(path,"lucene-messages"));
        _chats = new(Path.Combine(path,"lucene-chats"));
    }

    public ValueTask<bool> IndexUser(UserSearchModel user)
    {
        var doc = new Document();
        if (user.Username != null) LuceneContext.AddField(user.Username, doc, "username");
        if (user.Phone != null) LuceneContext.AddField(user.Phone, doc, "phone");
        if (user.FirstName != null) LuceneContext.AddField(user.FirstName, doc, "firstname");
        if (user.LastName != null) LuceneContext.AddField(user.LastName, doc, "lastname");
        _users.Index(user.Id.ToString(), doc);
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> DeleteUser(long userId)
    {
        _users.Delete(userId.ToString());
        return ValueTask.FromResult(true);
    }

    public ValueTask<List<UserSearchModel>> SearchUser(string q, int limit)
    {
        q = q.ToLowerInvariant();
        var query = new BooleanQuery();
        query.Add(new BooleanClause(new PrefixQuery(new Term("username", q)), Occur.SHOULD));
        query.Add(new BooleanClause(new PrefixQuery(new Term("firstname", q)), Occur.SHOULD));
        query.Add(new BooleanClause(new PrefixQuery(new Term("lastname", q)), Occur.SHOULD));
        var docs = _users.Search(query, int.Min(limit, 50));
        List<UserSearchModel> results = new();
        foreach (var d in docs)
        {
            UserSearchModel m = new UserSearchModel(
                long.Parse(d.GetField("_id").GetStringValue()),
                d.GetField("username") != null ? d.GetField("username").GetStringValue() : null,
                d.GetField("firstname") != null ? d.GetField("firstname").GetStringValue() : null,
                d.GetField("lastname") != null ? d.GetField("lastname").GetStringValue() : null,
                d.GetField("phone") != null ? d.GetField("phone").GetStringValue() : null);
            results.Add(m);
        }

        return ValueTask.FromResult(results);
    }

    public ValueTask<bool> IndexChat(ChatSearchModel chat)
    {
        var doc = new Document();
        if (chat.Username != null) LuceneContext.AddField(chat.Username, doc, "username");
        LuceneContext.AddField(chat.Title, doc, "title");
        _chats.Index(chat.Id.ToString(), doc);
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> DeleteChat(long chatId)
    {
        _chats.Delete(chatId.ToString());
        return ValueTask.FromResult(true);
    }

    public ValueTask<List<ChatSearchModel>> SearchChats(string q, int limit)
    {
        q = q.ToLowerInvariant();
        var query = new BooleanQuery();
        query.Add(new BooleanClause(new PrefixQuery(new Term("username", q)), Occur.SHOULD));
        query.Add(new BooleanClause(new PrefixQuery(new Term("title", q)), Occur.SHOULD));
        var docs = _chats.Search(query, int.Min(limit, 50));
        List<ChatSearchModel> results = new();
        foreach (var d in docs)
        {
            ChatSearchModel m = new ChatSearchModel(
                long.Parse(d.GetField("_id").GetStringValue()),
                d.GetField("username") != null ? d.GetField("username").GetStringValue() : null,
                d.GetField("title") != null ? d.GetField("title").GetStringValue() : "");
            results.Add(m);
        }

        return ValueTask.FromResult(results);
    }

    public ValueTask<bool> IndexMessage(MessageSearchModel message)
    {
        var doc = new Document();
        LuceneContext.AddField(message.Message, doc, "message");
        LuceneContext.AddField(message.Date, doc, "date");
        LuceneContext.AddField(message.FromId, doc, "fromid");
        LuceneContext.AddField(message.FromType, doc, "fromtype");
        LuceneContext.AddField(message.MessageId, doc, "messageid");
        LuceneContext.AddField(message.PeerId, doc, "peerid");
        LuceneContext.AddField(message.PeerType, doc, "peertype");
        LuceneContext.AddField(message.UserId, doc, "userid");
        if(message.TopMessageId != null) LuceneContext.AddField(message.TopMessageId, doc, "topmessageid");
        
        _messages.Index(message.Id, doc);
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> DeleteMessage(string id)
    {
        _messages.Delete(id);
        return ValueTask.FromResult(true);
    }

    public ValueTask<List<MessageSearchModel>> SearchMessages(string q)
    {
        var query = new MultiPhraseQuery();
        string[] terms = q.Split(" ");
        foreach (var t in terms)
        {
            query.Add(new Term("message", t));
        }

        return ValueTask.FromResult(Read(_messages.Search(query, 50)));
    }

    public ValueTask<List<MessageSearchModel>> SearchMessageCandidates(
        MessageCandidateQuery query)
    {
        var boolean = new BooleanQuery();
        if (query.UserId is { } userId)
        {
            boolean.Add(NumericRangeQuery.NewInt64Range("userid", userId, userId,
                true, true), Occur.MUST);
        }
        if (query.PeerType is { } peerType)
        {
            boolean.Add(NumericRangeQuery.NewInt32Range("peertype", peerType,
                peerType, true, true), Occur.MUST);
        }
        if (query.PeerId is { } peerId)
        {
            boolean.Add(NumericRangeQuery.NewInt64Range("peerid", peerId, peerId,
                true, true), Occur.MUST);
        }
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var phrase = new MultiPhraseQuery();
            foreach (string term in query.Text.Split(' ',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                phrase.Add(new Term("message", term));
            }
            boolean.Add(phrase, Occur.MUST);
        }

        int limit = query.Limit > 0 ? query.Limit : DefaultCandidateLimit;
        Lucene.Net.Search.Query lucene = boolean.Clauses.Count > 0
            ? boolean
            : new MatchAllDocsQuery();
        return ValueTask.FromResult(Read(_messages.Search(lucene, limit)));
    }

    private static List<MessageSearchModel> Read(IEnumerable<Document> docs)
    {
        List<MessageSearchModel> results = new();
        foreach (var d in docs)
        {
            MessageSearchModel m = new MessageSearchModel(
                d.GetField("_id").GetStringValue(),
                (long)d.GetField("userid").GetInt64Value(),
                (int)d.GetField("fromtype").GetInt32Value(),
                (long)d.GetField("fromid").GetInt64Value(),
                (int)d.GetField("peertype").GetInt32Value(),
                (long)d.GetField("peerid").GetInt64Value(),
                (int)d.GetField("messageid").GetInt32Value(),
                d.GetField("topmessageid") != null ? (int)d.GetField("topmessageid").GetInt32Value() : null,
            d.GetField("message").GetStringValue(),
                (int)d.GetField("date").GetInt32Value());
            results.Add(m);
        }
        return results;
    }

    public void Dispose()
    {
        _users.Dispose();
        _messages.Dispose();
        _chats.Dispose();
    }
}
