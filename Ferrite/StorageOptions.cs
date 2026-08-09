// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite;

public enum StorageProfile { Local, Distributed }
public enum KeyValueBackend { RocksDb, Cassandra }
public enum EphemeralBackend { InMemory, Redis }
public enum PipeBackend { Local, Redis, Kafka }
public enum ObjectStoreBackend { Local, S3 }
public enum SearchBackend { Lucene, Elasticsearch }
public enum CounterBackend { Faster, Redis }
public enum UpdatesContextBackend { Faster, Redis }

public sealed record StorageOptions
{
    public StorageProfile Profile { get; init; } = StorageProfile.Local;
    public KeyValueBackend? KeyValue { get; init; }
    public EphemeralBackend? Ephemeral { get; init; }
    public PipeBackend? Pipe { get; init; }
    public ObjectStoreBackend? ObjectStore { get; init; }
    public SearchBackend? Search { get; init; }
    public CounterBackend? Counters { get; init; }
    public UpdatesContextBackend? UpdatesContext { get; init; }

    public string[] CassandraHosts { get; init; } = ["127.0.0.1"];
    public int CassandraPort { get; init; } = 9042;
    public string CassandraKeyspace { get; init; } = "ferrite";
    public string RedisConfiguration { get; init; } = "127.0.0.1:6379";
    public string KafkaConfiguration { get; init; } = "127.0.0.1:9092";
    public string S3ServiceUrl { get; init; } = "http://127.0.0.1:9000";
    public string S3AccessKey { get; init; } = "minioadmin";
    public string S3SecretKey { get; init; } = "minioadmin";
    public string ElasticsearchUrl { get; init; } = "http://127.0.0.1:9200";
    public string ElasticsearchUsername { get; init; } = "elastic";
    public string ElasticsearchPassword { get; init; } = "changeme";
    public string ElasticsearchFingerprint { get; init; } = "";

    public static StorageOptions FromEnvironment() =>
        FromEnvironment(Environment.GetEnvironmentVariable);

    internal static StorageOptions FromEnvironment(Func<string, string?> read)
    {
        return new StorageOptions
        {
            Profile = ReadEnum(read, "FERRITE_STORAGE_PROFILE", StorageProfile.Local),
            KeyValue = ReadOptionalEnum<KeyValueBackend>(read,
                "FERRITE_STORAGE_KEY_VALUE"),
            Ephemeral = ReadOptionalEnum<EphemeralBackend>(read,
                "FERRITE_STORAGE_EPHEMERAL"),
            Pipe = ReadOptionalEnum<PipeBackend>(read, "FERRITE_STORAGE_PIPE"),
            ObjectStore = ReadOptionalEnum<ObjectStoreBackend>(read,
                "FERRITE_STORAGE_OBJECT_STORE"),
            Search = ReadOptionalEnum<SearchBackend>(read, "FERRITE_STORAGE_SEARCH"),
            Counters = ReadOptionalEnum<CounterBackend>(read,
                "FERRITE_STORAGE_COUNTERS"),
            UpdatesContext = ReadOptionalEnum<UpdatesContextBackend>(read,
                "FERRITE_STORAGE_UPDATES_CONTEXT"),
            CassandraHosts = (read("FERRITE_CASSANDRA_HOSTS") ?? "127.0.0.1")
                .Split(',', StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries),
            CassandraPort = ReadPort(read, "FERRITE_CASSANDRA_PORT", 9042),
            CassandraKeyspace = read("FERRITE_CASSANDRA_KEYSPACE") ?? "ferrite",
            RedisConfiguration = read("FERRITE_REDIS_CONFIGURATION") ??
                                 "127.0.0.1:6379",
            KafkaConfiguration = read("FERRITE_KAFKA_CONFIGURATION") ??
                                 "127.0.0.1:9092",
            S3ServiceUrl = read("FERRITE_S3_SERVICE_URL") ??
                           "http://127.0.0.1:9000",
            S3AccessKey = read("FERRITE_S3_ACCESS_KEY") ?? "minioadmin",
            S3SecretKey = read("FERRITE_S3_SECRET_KEY") ?? "minioadmin",
            ElasticsearchUrl = read("FERRITE_ELASTICSEARCH_URL") ??
                               "http://127.0.0.1:9200",
            ElasticsearchUsername = read("FERRITE_ELASTICSEARCH_USERNAME") ??
                                    "elastic",
            ElasticsearchPassword = read("FERRITE_ELASTICSEARCH_PASSWORD") ??
                                    "changeme",
            ElasticsearchFingerprint = read("FERRITE_ELASTICSEARCH_FINGERPRINT") ??
                                       "",
        };
    }

    public ResolvedStorageOptions Resolve()
    {
        bool distributed = Profile == StorageProfile.Distributed;
        return new ResolvedStorageOptions(
            KeyValue ?? (distributed ? KeyValueBackend.Cassandra : KeyValueBackend.RocksDb),
            Ephemeral ?? (distributed ? EphemeralBackend.Redis : EphemeralBackend.InMemory),
            Pipe ?? (distributed ? PipeBackend.Kafka : PipeBackend.Local),
            ObjectStore ?? (distributed ? ObjectStoreBackend.S3 : ObjectStoreBackend.Local),
            Search ?? (distributed ? SearchBackend.Elasticsearch : SearchBackend.Lucene),
            Counters ?? (distributed ? CounterBackend.Redis : CounterBackend.Faster),
            UpdatesContext ?? (distributed ? UpdatesContextBackend.Redis : UpdatesContextBackend.Faster));
    }

    public bool TryValidate(out string error)
    {
        ResolvedStorageOptions value = Resolve();
        if (value.KeyValue == KeyValueBackend.Cassandra &&
            (CassandraHosts.Length == 0 || CassandraHosts.Any(string.IsNullOrWhiteSpace)))
        {
            error = "Cassandra storage requires at least one host";
            return false;
        }
        if (value.KeyValue == KeyValueBackend.Cassandra &&
            string.IsNullOrWhiteSpace(CassandraKeyspace))
        {
            error = "Cassandra storage requires a keyspace";
            return false;
        }
        if (value.KeyValue == KeyValueBackend.Cassandra &&
            CassandraPort is <= 0 or > 65535)
        {
            error = "Cassandra storage requires a valid port";
            return false;
        }
        if ((value.Ephemeral == EphemeralBackend.Redis ||
             value.Pipe == PipeBackend.Redis || value.Counters == CounterBackend.Redis ||
             value.UpdatesContext == UpdatesContextBackend.Redis) &&
            string.IsNullOrWhiteSpace(RedisConfiguration))
        {
            error = "Redis-backed capabilities require a Redis configuration";
            return false;
        }
        if (value.Pipe == PipeBackend.Kafka && string.IsNullOrWhiteSpace(KafkaConfiguration))
        {
            error = "Kafka pipe requires a bootstrap-server configuration";
            return false;
        }
        if (value.ObjectStore == ObjectStoreBackend.S3 &&
            (string.IsNullOrWhiteSpace(S3ServiceUrl) ||
             string.IsNullOrWhiteSpace(S3AccessKey) ||
             string.IsNullOrWhiteSpace(S3SecretKey)))
        {
            error = "S3 object storage requires URL and credentials";
            return false;
        }
        if (value.Search == SearchBackend.Elasticsearch &&
            string.IsNullOrWhiteSpace(ElasticsearchUrl))
        {
            error = "Elasticsearch requires a URL";
            return false;
        }
        error = "";
        return true;
    }

    private static int ReadPort(Func<string, string?> read, string name,
        int fallback)
    {
        string? text = read(name);
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        if (!int.TryParse(text, out int value) || value is <= 0 or > 65535)
        {
            throw new ArgumentException($"{name} must be a valid TCP port");
        }
        return value;
    }

    private static T ReadEnum<T>(Func<string, string?> read, string name,
        T fallback) where T : struct, Enum =>
        ReadOptionalEnum<T>(read, name) ?? fallback;

    private static T? ReadOptionalEnum<T>(Func<string, string?> read,
        string name) where T : struct, Enum
    {
        string? text = read(name);
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (Enum.TryParse(text, ignoreCase: true, out T value) &&
            Enum.IsDefined(value))
        {
            return value;
        }
        throw new ArgumentException($"{name} has unsupported value '{text}'");
    }
}

public readonly record struct ResolvedStorageOptions(
    KeyValueBackend KeyValue,
    EphemeralBackend Ephemeral,
    PipeBackend Pipe,
    ObjectStoreBackend ObjectStore,
    SearchBackend Search,
    CounterBackend Counters,
    UpdatesContextBackend UpdatesContext);
