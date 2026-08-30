// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace Ferrite.Data.Search;

public class LuceneContext : IDisposable
{
    const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
    private readonly string _path;
    private readonly string _indexPath;
    private readonly FSDirectory _dir;
    private readonly StandardAnalyzer _analyzer;
    private readonly IndexWriterConfig _indexConfig;
    private readonly IndexWriter _writer;

    public LuceneContext(string path)
    {
        _path = path;
        _indexPath = Path.Combine(path, "index");
        _dir = FSDirectory.Open(_indexPath);
        _analyzer = new StandardAnalyzer(AppLuceneVersion);
        _indexConfig = new IndexWriterConfig(AppLuceneVersion, _analyzer);
        _writer = new IndexWriter(_dir, _indexConfig);
    }

    public void Index(string id, Document doc)
    {
        AddField(id, doc, "_id");
        _writer.UpdateDocument(new Term("_id", id), doc);
        _writer.Flush(triggerMerge: false, applyAllDeletes: false);
    }
    public void Delete(string id)
    {
        _writer.DeleteDocuments(new Term("_id", id));
        _writer.Flush(triggerMerge: false, applyAllDeletes: true);
    }

    public IEnumerable<Document> Search(Query q, int topN = 10)
    {
        using var reader = _writer.GetReader(applyAllDeletes: true);
        var searcher = new IndexSearcher(reader);
        var hits = searcher.Search(q, topN).ScoreDocs;

        List<Document> docs = new();
        foreach (var hit in hits)
        {
            var foundDoc = searcher.Doc(hit.Doc);
            if (foundDoc != null) docs.Add(foundDoc);
        }

        return docs;
    }

    public static void AddField(object v, Document doc, string k)
    {
        if (v is int i) doc.Add(new Int32Field(k, i, Field.Store.YES));
        else if (v is long l) doc.Add(new Int64Field(k, l, Field.Store.YES));
        else if (v is float f) doc.Add(new SingleField(k, f, Field.Store.YES));
        else if (v is long d) doc.Add(new DoubleField(k, d, Field.Store.YES));
        else if (v is string s) doc.Add(new TextField(k, s, Field.Store.YES));
    }

    public void Dispose()
    {
        _writer.Dispose();
        _analyzer.Dispose();
        _dir.Dispose();
    }
}
