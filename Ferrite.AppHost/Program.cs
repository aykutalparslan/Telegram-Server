// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = DistributedApplication.CreateBuilder(args);

// Development-only secrets for loopback containers. Never deployment credentials.
const string turnSecret = "ferrite-dev-turn-secret";
const string groupCallSecret = "ferrite-dev-groupcall-secret";

builder.Services.AddHealthChecks()
    .AddCheck("cassandra-cql", () => CqlHealth("127.0.0.1", 19042))
    .AddCheck("redis-tcp", () => TcpHealth("127.0.0.1", 16379))
    .AddCheck("kafka-tcp", () => TcpHealth("127.0.0.1", 19092))
    .AddCheck("coturn-tcp", () => TcpHealth("127.0.0.1", 3478))
    .AddCheck("group-call-worker-http",
        () => WorkerHealth("http://127.0.0.1:9090/health", groupCallSecret));

var cassandra = builder.AddContainer("cassandra", "cassandra", "5.0.8")
    .WithEnvironment("MAX_HEAP_SIZE", "512M")
    .WithEnvironment("HEAP_NEWSIZE", "100M")
    .WithEndpoint(targetPort: 9042, port: 19042, name: "cql",
        isProxied: false)
    .WithHealthCheck("cassandra-cql");

var redis = builder.AddContainer("redis", "redis", "7.4-alpine")
    .WithEndpoint(targetPort: 6379, port: 16379, name: "tcp",
        isProxied: false)
    .WithHealthCheck("redis-tcp");

var kafka = builder.AddContainer("kafka", "apache/kafka", "4.1.1")
    .WithEnvironment("KAFKA_NODE_ID", "1")
    .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
    // Two client listeners, because a broker advertises one address per
    // listener and host and container clients need different ones. A client
    // inside a container that bootstraps on the PLAINTEXT listener is told to
    // reconnect to 127.0.0.1:19092, which is that container itself, so it can
    // never reach the broker. DOCKER advertises an address that resolves from
    // inside a container instead.
    .WithEnvironment("KAFKA_LISTENERS",
        "PLAINTEXT://:9092,DOCKER://:9094,CONTROLLER://:9093")
    .WithEnvironment("KAFKA_ADVERTISED_LISTENERS",
        "PLAINTEXT://127.0.0.1:19092,DOCKER://host.docker.internal:19094")
    .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
    .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT")
    .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP",
        "CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT,DOCKER:PLAINTEXT")
    .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:9093")
    .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
    .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
    .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
    .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
    .WithEndpoint(targetPort: 9092, port: 19092, name: "tcp",
        isProxied: false)
    .WithEndpoint(targetPort: 9094, port: 19094, name: "tcp-docker",
        isProxied: false)
    .WithHealthCheck("kafka-tcp");

var minio = builder.AddContainer("minio", "minio/minio",
        "RELEASE.2025-04-22T22-12-26Z")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEndpoint(targetPort: 9000, port: 19000, scheme: "http", name: "s3",
        isProxied: false)
    .WithEndpoint(targetPort: 9001, port: 19001, scheme: "http", name: "console",
        isProxied: false)
    .WithHttpHealthCheck("/minio/health/ready", endpointName: "s3");

var elasticsearch = builder.AddContainer("elasticsearch",
        "docker.elastic.co/elasticsearch/elasticsearch", "7.17.29")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("xpack.security.enabled", "false")
    .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
    .WithEndpoint(targetPort: 9200, port: 19200, name: "http", scheme: "http",
        isProxied: false)
    .WithHttpHealthCheck("/_cluster/health", endpointName: "http");

var coturn = builder.AddContainer("coturn", "coturn/coturn", "4.6")
    .WithBindMount("../deploy/coturn/turnserver.dev.conf",
        "/etc/coturn/turnserver.conf", isReadOnly: true)
    .WithArgs("-c", "/etc/coturn/turnserver.conf",
        $"--static-auth-secret={turnSecret}")
    .WithEndpoint(targetPort: 3478, port: 3478, name: "turn-tcp",
        isProxied: false)
    .WithEndpoint(targetPort: 3478, port: 3478, name: "turn-udp",
        isProxied: false, protocol: ProtocolType.Udp)
    // Aspire endpoints are per-port, so the relay range is published with a raw
    // container runtime argument instead of 41 endpoint declarations.
    .WithContainerRuntimeArgs("-p", "49160-49200:49160-49200/udp")
    .WithHealthCheck("coturn-tcp");

// The same image deploy/docker-compose.yml builds. That compose file's
// own ferrite service is deliberately not carried over: the test fixture runs the
// server in-process.
var groupCallWorker = builder.AddDockerfile("group-call-worker",
        "../group-call-worker")
    .WithEnvironment("FERRITE_GROUP_CALL_AUTH_SECRET", groupCallSecret)
    .WithEnvironment("FERRITE_GROUP_CALL_CONTROL_BIND", "0.0.0.0")
    .WithEnvironment("FERRITE_GROUP_CALL_CONTROL_PORT", "9090")
    .WithEnvironment("FERRITE_GROUP_CALL_MEDIA_BIND", "0.0.0.0")
    .WithEnvironment("FERRITE_GROUP_CALL_MEDIA_ADVERTISED", "127.0.0.1")
    .WithEnvironment("FERRITE_GROUP_CALL_RTC_MIN_PORT", "40000")
    .WithEnvironment("FERRITE_GROUP_CALL_RTC_MAX_PORT", "40100")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_ROOMS", "100")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_PARTICIPANTS_PER_ROOM", "1000")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_CONTROL_BODY_BYTES", "262144")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_EVENT_CLIENTS", "32")
    .WithEnvironment("FERRITE_GROUP_CALL_RTMP_BIND", "0.0.0.0")
    .WithEnvironment("FERRITE_GROUP_CALL_RTMP_ADVERTISED", "127.0.0.1")
    .WithEnvironment("FERRITE_GROUP_CALL_RTMP_MIN_PORT", "19350")
    .WithEnvironment("FERRITE_GROUP_CALL_RTMP_MAX_PORT", "19449")
    .WithEnvironment("FERRITE_GROUP_CALL_RTP_TAP_ADDRESS", "127.0.0.1")
    .WithEnvironment("FERRITE_GROUP_CALL_RTP_TAP_MIN_PORT", "50000")
    .WithEnvironment("FERRITE_GROUP_CALL_RTP_TAP_MAX_PORT", "50199")
    .WithEnvironment("FERRITE_GROUP_CALL_SEGMENT_PATH", "/segments")
    .WithEnvironment("FERRITE_GROUP_CALL_SEGMENT_RETENTION_MS", "300000")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_SEGMENTS_PER_CALL", "4096")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_SEGMENT_BYTES_PER_CALL", "536870912")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_SEGMENT_BYTES", "1048576")
    .WithEnvironment("FERRITE_GROUP_CALL_RECORDING_PATH", "/recordings")
    .WithEnvironment("FERRITE_GROUP_CALL_RECORDING_TAP_MIN_PORT", "50200")
    .WithEnvironment("FERRITE_GROUP_CALL_RECORDING_TAP_MAX_PORT", "50399")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_RECORDINGS", "16")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_RECORDING_BYTES", "2000000000")
    .WithEnvironment("FERRITE_GROUP_CALL_MAX_RECORDING_DURATION_MS", "14400000")
    .WithEnvironment("FERRITE_GROUP_CALL_RECORDING_SOURCE_WAIT_MS", "10000")
    .WithEnvironment("FERRITE_GROUP_CALL_RECORDING_STOP_TIMEOUT_MS", "10000")
    .WithEndpoint(targetPort: 9090, port: 9090, scheme: "http", name: "control",
        isProxied: false)
    .WithEndpoint(targetPort: 19350, port: 19350, name: "rtmp", isProxied: false)
    .WithContainerRuntimeArgs("-p", "40000-40100:40000-40100/udp")
    .WithContainerRuntimeArgs("--tmpfs",
        "/segments:size=1g,mode=0700,uid=1000,gid=1000")
    .WithVolume("ferrite-group-call-recordings", "/recordings")
    .WithHealthCheck("group-call-worker-http");

if (Environment.GetEnvironmentVariable("FERRITE_APPHOST_SERVICES_ONLY") != "1")
{
    string repositoryRoot = Path.GetFullPath("..", builder.AppHostDirectory);
    AddFerriteNode("ferrite-a", repositoryRoot, 52222, 19400,
        "00000000-0000-0000-0000-000000000001");
    AddFerriteNode("ferrite-b", repositoryRoot, 52223, 19401,
        "00000000-0000-0000-0000-000000000002");
}

builder.Build().Run();

void AddFerriteNode(string name, string repositoryRoot, int port, int relayPort,
    string nodeId)
{
    // Ferrite is a server component, so it runs as a container like every other
    // server in the graph. The backing services publish fixed host ports rather
    // than being addressed by container name, so the node reaches them back
    // through the host gateway; FERRITE_PUBLIC_ADDRESS stays loopback because
    // that is what clients outside the container dial.
    const string host = "host.docker.internal";
    builder.AddDockerfile(name, repositoryRoot,
            "deploy/Dockerfile.ferrite")
        .WithEnvironment("FERRITE_PUBLIC_ADDRESS", "127.0.0.1")
        .WithEnvironment("FERRITE_PORT", port.ToString())
        .WithEnvironment("FERRITE_NODE_ID", nodeId)
        .WithEnvironment("FERRITE_DATA_PATH", "/data")
        .WithEnvironment("FERRITE_STORAGE_PROFILE", "Distributed")
        .WithEnvironment("FERRITE_CASSANDRA_HOSTS", host)
        .WithEnvironment("FERRITE_CASSANDRA_PORT", "19042")
        .WithEnvironment("FERRITE_REDIS_CONFIGURATION", $"{host}:16379")
        // The DOCKER listener, not the host one: see the Kafka registration.
        .WithEnvironment("FERRITE_KAFKA_CONFIGURATION", $"{host}:19094")
        .WithEnvironment("FERRITE_S3_SERVICE_URL", $"http://{host}:19000")
        .WithEnvironment("FERRITE_S3_ACCESS_KEY", "minioadmin")
        .WithEnvironment("FERRITE_S3_SECRET_KEY", "minioadmin")
        .WithEnvironment("FERRITE_ELASTICSEARCH_URL", $"http://{host}:19200")
        .WithEnvironment("FERRITE_ELASTICSEARCH_USERNAME", "")
        .WithEnvironment("FERRITE_ELASTICSEARCH_PASSWORD", "")
        .WithEnvironment("FERRITE_TURN_ENABLED", "1")
        .WithEnvironment("FERRITE_TURN_ADVERTISED_IPV4", "127.0.0.1")
        .WithEnvironment("FERRITE_TURN_PORT", "3478")
        .WithEnvironment("FERRITE_TURN_SECRET", turnSecret)
        .WithEnvironment("FERRITE_TURN_REALM", "ferrite.local")
        // The reflector's development default binds an ephemeral port, which a
        // container can never publish: the runtime has to know the number
        // before the process picks one. Bind every interface inside the
        // container and advertise the loopback address host clients dial.
        .WithEnvironment("FERRITE_CALL_RELAY_BIND_ADDRESS", "0.0.0.0")
        .WithEnvironment("FERRITE_CALL_RELAY_BIND_PORT", relayPort.ToString())
        .WithEnvironment("FERRITE_CALL_RELAY_ADVERTISED_ADDRESS", "127.0.0.1")
        .WithEndpoint(targetPort: port, port: port, scheme: "tcp",
            name: "mtproto", isProxied: false)
        .WithEndpoint(targetPort: relayPort, port: relayPort, name: "call-relay",
            isProxied: false, protocol: ProtocolType.Udp)
        // Linux hosts do not resolve host.docker.internal on their own.
        .WithContainerRuntimeArgs("--add-host",
            "host.docker.internal:host-gateway")
        .WithVolume($"ferrite-apphost-data-{name}", "/data")
        .WaitFor(cassandra)
        .WaitFor(redis)
        .WaitFor(kafka)
        .WaitFor(minio)
        .WaitFor(elasticsearch)
        .WaitFor(coturn)
        .WaitFor(groupCallWorker);
}

// The worker rejects unauthenticated probes, so its health check carries the
// same bearer token and protocol header the adapter uses.
static HealthCheckResult WorkerHealth(string url, string secret)
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("authorization", "Bearer " + secret);
        request.Headers.TryAddWithoutValidation("x-ferrite-groupcall-protocol", "1");
        using HttpResponseMessage response = client.Send(request);
        return response.IsSuccessStatusCode
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"status {(int)response.StatusCode}");
    }
    catch (Exception exception)
    {
        return HealthCheckResult.Unhealthy(exception.Message);
    }
}

static HealthCheckResult TcpHealth(string host, int port)
{
    try
    {
        using var client = new TcpClient();
        client.Connect(host, port);
        return client.Connected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
    catch (Exception exception)
    {
        return HealthCheckResult.Unhealthy(exception.Message);
    }
}

static HealthCheckResult CqlHealth(string host, int port)
{
    try
    {
        using var client = new TcpClient
        {
            ReceiveTimeout = 1_000,
            SendTimeout = 1_000
        };
        client.Connect(host, port);
        using NetworkStream stream = client.GetStream();

        // Native protocol v4 OPTIONS frame. A Docker port mapping can accept TCP
        // before Cassandra is ready, so require a real SUPPORTED response.
        ReadOnlySpan<byte> options = [0x04, 0, 0, 0, 0x05, 0, 0, 0, 0];
        stream.Write(options);
        Span<byte> responseHeader = stackalloc byte[9];
        stream.ReadExactly(responseHeader);
        return responseHeader[0] == 0x84 && responseHeader[4] == 0x06
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Unexpected CQL OPTIONS response");
    }
    catch (Exception exception)
    {
        return HealthCheckResult.Unhealthy(exception.Message);
    }
}
