// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ferrite.Services.Calls;
using Ferrite.Utils;

namespace Ferrite.GroupCallMedia;

public sealed class MediasoupGroupCallMediaPlane : IGroupCallMediaPlane
{
    private const int MaxParticipants = 10_000;

    private readonly HttpClient _httpClient;
    private readonly GroupCallMediaWorkerOptions _options;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly List<Action<GroupCallMediaDisconnectEvent>> _subscribers = new();
    private readonly List<Action<GroupCallMediaSourcesChangedEvent>>
        _sourcesChangedSubscribers = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private CancellationTokenSource? _stopping;
    private Task? _eventLoop;

    public MediasoupGroupCallMediaPlane(HttpClient httpClient,
        GroupCallMediaWorkerOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        options.Validate();
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_stopping != null)
            {
                return;
            }
            _stopping = new CancellationTokenSource();
            _eventLoop = ReadEventsLoopAsync(_stopping.Token);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_stopping == null)
            {
                return;
            }
            _stopping.Cancel();
            if (_eventLoop != null)
            {
                try
                {
                    await _eventLoop.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }
            }
            _eventLoop = null;
            _stopping.Dispose();
            _stopping = null;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask CreateRoomAsync(long callId, CancellationToken cancellationToken = default) =>
        await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Put, RoomPath(callId));
            using var response = await SendAsync(request, _options.RequestTimeout, ct);
            EnsureSuccess(response, "createRoom");
            return true;
        }, cancellationToken);

    public async ValueTask<bool> EndRoomAsync(long callId, CancellationToken cancellationToken = default) =>
        await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Delete, RoomPath(callId));
            using var response = await SendAsync(request, _options.RequestTimeout, ct);
            EnsureSuccess(response, "endRoom");
            using var document = await ReadJsonAsync(response, ct);
            return ReadBool(document.RootElement, "ended", "endRoom");
        }, cancellationToken);

    public async ValueTask<GroupCallMediaJoinResult> JoinAsync(GroupCallMediaJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = Build(HttpMethod.Post,
            ParticipantPath(request.CallId, request.ParticipantId),
            BuildJoinBody(request.Payload));
        using var response = await SendAsync(httpRequest, _options.RequestTimeout, cancellationToken);
        EnsureSuccess(response, "join");
        using var document = await ReadJsonAsync(response, cancellationToken);
        return ParseJoinResult(document.RootElement);
    }

    public async ValueTask<bool> LeaveAsync(long callId, string participantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(participantId);
        return await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Delete, ParticipantPath(callId, participantId));
            using var response = await SendAsync(request, _options.RequestTimeout, ct);
            EnsureSuccess(response, "leave");
            using var document = await ReadJsonAsync(response, ct);
            return ReadBool(document.RootElement, "left", "leave");
        }, cancellationToken);
    }

    public async ValueTask<GroupCallMediaJoinResult> JoinPresentationAsync(
        GroupCallMediaJoinRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = Build(HttpMethod.Post,
            ParticipantPath(request.CallId, request.ParticipantId) + "/presentation",
            BuildJoinBody(request.Payload));
        using var response = await SendAsync(httpRequest, _options.RequestTimeout, cancellationToken);
        EnsureSuccess(response, "joinPresentation");
        using var document = await ReadJsonAsync(response, cancellationToken);
        return ParseJoinResult(document.RootElement);
    }

    public async ValueTask<bool> LeavePresentationAsync(long callId, string participantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(participantId);
        return await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Delete,
                ParticipantPath(callId, participantId) + "/presentation");
            using var response = await SendAsync(request, _options.RequestTimeout, ct);
            EnsureSuccess(response, "leavePresentation");
            using var document = await ReadJsonAsync(response, ct);
            return ReadBool(document.RootElement, "left", "leavePresentation");
        }, cancellationToken);
    }

    public async ValueTask SetVideoPausedAsync(long callId, string participantId, bool paused,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(participantId);
        await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Post,
                ParticipantPath(callId, participantId) + "/video-paused",
                BuildVideoPausedBody(paused));
            using var response = await SendAsync(request, _options.RequestTimeout, ct);
            EnsureSuccess(response, "videoPaused");
            return true;
        }, cancellationToken);
    }

    public async ValueTask SetIngressMuteAsync(long callId, string participantId, bool muted,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(participantId);
        await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Post,
                ParticipantPath(callId, participantId) + "/mute", BuildMuteBody(muted));
            using var response = await SendAsync(request, _options.RequestTimeout, ct);
            EnsureSuccess(response, "mute");
            return true;
        }, cancellationToken);
    }

    public async ValueTask<bool> IsAliveAsync(long callId, string participantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(participantId);
        return await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Get,
                ParticipantPath(callId, participantId) + "/liveness");
            using var response = await SendAsync(request, _options.RequestTimeout, ct);
            EnsureSuccess(response, "liveness");
            using var document = await ReadJsonAsync(response, ct);
            return ReadBool(document.RootElement, "alive", "liveness");
        }, cancellationToken);
    }

    public async ValueTask<GroupCallMediaHealth> HealthAsync(CancellationToken cancellationToken = default) =>
        await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Get, "health");
            using var response = await SendAsync(request, _options.HealthTimeout, ct);
            EnsureSuccess(response, "health");
            using var document = await ReadJsonAsync(response, ct);
            var root = document.RootElement;
            string instanceId = ReadRequiredString(root, "instanceId");
            string protocolVersion = ReadRequiredString(root, "protocolVersion");
            string workerVersion = ReadRequiredString(root, "workerVersion");
            if (protocolVersion != _options.ProtocolVersion ||
                workerVersion != _options.WorkerVersion)
            {
                throw new GroupCallMediaException(
                    GroupCallMediaFailureKind.Unavailable,
                    $"group-call media worker version mismatch: protocol " +
                    $"{protocolVersion}, worker {workerVersion}");
            }
            return new GroupCallMediaHealth(
                ReadBool(root, "healthy", "health"),
                ReadInt32(root, "rooms", "health"), instanceId, workerVersion);
        }, cancellationToken);

    public IDisposable Subscribe(Action<GroupCallMediaDisconnectEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            _subscribers.Add(handler);
        }
        return new Subscription(this, handler);
    }

    public IDisposable SubscribeSourcesChanged(
        Action<GroupCallMediaSourcesChangedEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            _sourcesChangedSubscribers.Add(handler);
        }
        return new SourcesChangedSubscription(this, handler);
    }

    public async ValueTask<IReadOnlyDictionary<string,
        IReadOnlyDictionary<string, GroupCallViewerSources>>> ReadViewerSourcesAsync(
        long callId, CancellationToken cancellationToken = default) =>
        await ExecuteIdempotentAsync(async ct =>
        {
            using var request = Build(HttpMethod.Get,
                $"rooms/{callId}/viewer-media");
            using var response = await SendAsync(request, _options.RequestTimeout, ct);
            EnsureSuccess(response, "viewer-media");
            using var document = await ReadJsonAsync(response, ct);
            return ParseViewerSources(document.RootElement);
        }, cancellationToken);

    internal void DispatchSourcesChanged(GroupCallMediaSourcesChangedEvent changed)
    {
        Action<GroupCallMediaSourcesChangedEvent>[] handlers;
        lock (_gate)
        {
            handlers = _sourcesChangedSubscribers.ToArray();
        }
        foreach (var handler in handlers)
        {
            handler(changed);
        }
    }

    internal void DispatchDisconnect(GroupCallMediaDisconnectEvent disconnect)
    {
        Action<GroupCallMediaDisconnectEvent>[] handlers;
        lock (_gate)
        {
            handlers = _subscribers.ToArray();
        }
        foreach (var handler in handlers)
        {
            handler(disconnect);
        }
    }

    private async Task ReadEventsLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var request = Build(HttpMethod.Get, "events");
                using HttpResponseMessage response = await _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                EnsureSuccess(response, "events");
                await using Stream stream = await response.Content
                    .ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false, bufferSize: 4096,
                    leaveOpen: false);
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(cancellationToken);
                    if (line == null)
                    {
                        break;
                    }
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    if (Encoding.UTF8.GetByteCount(line) > _options.MaxEventBytes)
                    {
                        throw new GroupCallMediaException(
                            GroupCallMediaFailureKind.Unavailable,
                            "group-call media worker event exceeded the configured bound");
                    }
                    DispatchEvent(line);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e) when (e is HttpRequestException or IOException or
                                      GroupCallMediaException or JsonException)
            {
                _logger.Warning(e,
                    "group-call media event stream disconnected; reconnecting");
            }

            if (_options.EventReconnectBackoff > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(_options.EventReconnectBackoff,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    internal void DispatchEvent(string line)
    {
        if (IsSourcesChangedReason(ReadEventReason(line)))
        {
            DispatchSourcesChanged(ParseSourcesChangedEvent(line));
            return;
        }
        DispatchDisconnect(ParseDisconnectEvent(line));
    }

    private static string ReadEventReason(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return ReadRequiredString(document.RootElement, "reason");
    }

    internal static bool IsSourcesChangedReason(string reason) =>
        reason is "sources_changed" or "video_codec_corrected";

    internal static GroupCallMediaSourcesChangedEvent ParseSourcesChangedEvent(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new GroupCallMediaSourcesChangedEvent(
            ReadInt64(root, "callId", "events"),
            ReadRequiredString(root, "participantId"),
            ReadRequiredString(root, "reason"));
    }

    internal static GroupCallMediaDisconnectEvent ParseDisconnectEvent(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        long callId = ReadInt64(root, "callId", "events");
        string participantId = ReadRequiredString(root, "participantId");
        string reasonText = ReadRequiredString(root, "reason");
        GroupCallMediaDisconnectReason reason = reasonText switch
        {
            "transport_closed" => GroupCallMediaDisconnectReason.TransportClosed,
            "worker_died" => GroupCallMediaDisconnectReason.WorkerDied,
            _ => throw new GroupCallMediaException(
                GroupCallMediaFailureKind.Unavailable,
                $"group-call media worker returned unknown event reason '{reasonText}'"),
        };
        return new GroupCallMediaDisconnectEvent(callId, participantId, reason);
    }

    private async Task<T> ExecuteIdempotentAsync<T>(Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (GroupCallMediaException ex) when (
                (ex.Kind is GroupCallMediaFailureKind.Unavailable or GroupCallMediaFailureKind.Timeout) &&
                attempt < _options.MaxRetries)
            {
                attempt++;
                _logger.Warning(ex,
                    $"group-call media worker {ex.Kind}; retry {attempt}/{_options.MaxRetries}");
                if (_options.RetryBackoff > TimeSpan.Zero)
                {
                    await Task.Delay(_options.RetryBackoff, cancellationToken);
                }
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GroupCallMediaException(GroupCallMediaFailureKind.Timeout,
                "group-call media worker request timed out");
        }
        catch (HttpRequestException ex)
        {
            throw new GroupCallMediaException(GroupCallMediaFailureKind.Unavailable,
                "group-call media worker is unreachable", ex);
        }
    }

    private HttpRequestMessage Build(HttpMethod method, string relativePath, byte[]? body = null)
    {
        var request = new HttpRequestMessage(method, new Uri(_options.ControlUrl, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AuthSecret);
        request.Headers.TryAddWithoutValidation(
            GroupCallMediaProtocol.ProtocolHeader, _options.ProtocolVersion);
        if (body is not null)
        {
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
        }
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var kind = response.StatusCode switch
        {
            HttpStatusCode.Conflict => GroupCallMediaFailureKind.Conflict,
            HttpStatusCode.BadRequest => GroupCallMediaFailureKind.Rejected,
            HttpStatusCode.UnprocessableEntity => GroupCallMediaFailureKind.Rejected,
            HttpStatusCode.RequestTimeout => GroupCallMediaFailureKind.Timeout,
            _ => GroupCallMediaFailureKind.Unavailable,
        };
        throw new GroupCallMediaException(kind,
            $"group-call media worker {operation} failed with {(int)response.StatusCode}");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException ex)
        {
            throw new GroupCallMediaException(GroupCallMediaFailureKind.Unavailable,
                "group-call media worker returned invalid JSON", ex);
        }
    }

    private static GroupCallMediaJoinResult ParseJoinResult(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("connection", out var connection) ||
            connection.ValueKind != JsonValueKind.Object ||
            !connection.TryGetProperty("transport", out var transportElement) ||
            transportElement.ValueKind != JsonValueKind.Object)
        {
            throw Unmappable("missing connection transport");
        }

        GroupCallVideoAnswer? video = connection.TryGetProperty("video",
            out JsonElement videoElement)
            ? ParseVideoAnswer(videoElement)
            : null;
        var transport = ParseTransport(transportElement, video);
        var canonicalSource = ReinterpretSource(
            ReadInt64(root, "canonicalSource", "join"), "canonicalSource");
        var viewerSources = ParseViewerSources(root);
        return new GroupCallMediaJoinResult(transport, canonicalSource, viewerSources);
    }

    private static GroupCallMediaTransport ParseTransport(JsonElement transport,
        GroupCallVideoAnswer? video)
    {
        var ufrag = ReadRequiredString(transport, "ufrag");
        var pwd = ReadRequiredString(transport, "pwd");

        if (!transport.TryGetProperty("fingerprints", out var fingerprintsElement) ||
            fingerprintsElement.ValueKind != JsonValueKind.Array ||
            fingerprintsElement.GetArrayLength() == 0)
        {
            throw Unmappable("transport has no fingerprints");
        }
        var fingerprints = new List<GroupCallDtlsFingerprint>();
        foreach (var fingerprint in fingerprintsElement.EnumerateArray())
        {
            var setup = ReadRequiredString(fingerprint, "setup");
            if (setup != "active")
            {
                throw Unmappable($"worker fingerprint setup must be 'active' but was '{setup}'");
            }
            fingerprints.Add(new GroupCallDtlsFingerprint(
                ReadRequiredString(fingerprint, "hash"),
                ReadRequiredString(fingerprint, "fingerprint"),
                setup));
        }

        if (!transport.TryGetProperty("candidates", out var candidatesElement) ||
            candidatesElement.ValueKind != JsonValueKind.Array ||
            candidatesElement.GetArrayLength() == 0)
        {
            throw Unmappable("transport has no candidates");
        }
        var candidates = new List<GroupCallIceCandidate>();
        foreach (var candidate in candidatesElement.EnumerateArray())
        {
            candidates.Add(new GroupCallIceCandidate(
                ReadRequiredString(candidate, "port"),
                ReadRequiredString(candidate, "protocol"),
                ReadRequiredString(candidate, "network"),
                ReadRequiredString(candidate, "generation"),
                ReadRequiredString(candidate, "id"),
                ReadRequiredString(candidate, "component"),
                ReadRequiredString(candidate, "foundation"),
                ReadRequiredString(candidate, "priority"),
                ReadRequiredString(candidate, "ip"),
                ReadRequiredString(candidate, "type")));
        }

        return new GroupCallMediaTransport(ufrag, pwd, fingerprints, candidates, video);
    }

    private static GroupCallVideoAnswer ParseVideoAnswer(JsonElement video)
    {
        if (video.ValueKind != JsonValueKind.Object)
        {
            throw Unmappable("connection video must be an object");
        }
        string endpoint = ReadRequiredString(video, "endpoint");
        if (!video.TryGetProperty("server_sources", out JsonElement serverSources) ||
            serverSources.ValueKind != JsonValueKind.Array ||
            serverSources.GetArrayLength() == 0)
        {
            throw Unmappable("connection video has no server_sources");
        }
        if (!serverSources[0].TryGetInt64(out long serverSourceValue))
        {
            throw Unmappable("connection video server_source must be an integer");
        }
        int serverSource = ReinterpretSource(serverSourceValue, "server_source");

        if (!video.TryGetProperty("payload-types", out JsonElement payloadTypes) ||
            payloadTypes.ValueKind != JsonValueKind.Array)
        {
            throw Unmappable("connection video has no payload-types");
        }
        var codecs = new List<GroupCallVideoPayloadType>();
        foreach (JsonElement payloadType in payloadTypes.EnumerateArray())
        {
            var feedback = new List<GroupCallRtcpFeedback>();
            if (payloadType.TryGetProperty("rtcp-fbs", out JsonElement feedbackItems) &&
                feedbackItems.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in feedbackItems.EnumerateArray())
                {
                    feedback.Add(new GroupCallRtcpFeedback(
                        ReadRequiredString(item, "type"),
                        item.TryGetProperty("subtype", out JsonElement subtype) &&
                        subtype.ValueKind == JsonValueKind.String
                            ? subtype.GetString()
                            : null));
                }
            }

            var parameters = new List<KeyValuePair<string, string>>();
            if (payloadType.TryGetProperty("parameters", out JsonElement parameterItems) &&
                parameterItems.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty parameter in parameterItems.EnumerateObject())
                {
                    if (parameter.Value.ValueKind != JsonValueKind.String)
                    {
                        throw Unmappable("video payload parameters must be strings");
                    }
                    parameters.Add(new KeyValuePair<string, string>(parameter.Name,
                        parameter.Value.GetString()!));
                }
            }
            codecs.Add(new GroupCallVideoPayloadType(
                ReadInt32(payloadType, "id", "join"),
                ReadRequiredString(payloadType, "name"),
                ReadInt32(payloadType, "clockrate", "join"),
                ReadInt32(payloadType, "channels", "join"),
                feedback, parameters));
        }

        if (!video.TryGetProperty("rtp-hdrexts", out JsonElement extensionItems) ||
            extensionItems.ValueKind != JsonValueKind.Array)
        {
            throw Unmappable("connection video has no rtp-hdrexts");
        }
        var extensions = new List<GroupCallRtpExtension>();
        foreach (JsonElement extension in extensionItems.EnumerateArray())
        {
            extensions.Add(new GroupCallRtpExtension(
                ReadInt32(extension, "id", "join"),
                ReadRequiredString(extension, "uri")));
        }
        return new GroupCallVideoAnswer(endpoint, serverSource, codecs, extensions);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, GroupCallViewerSources>>
        ParseViewerSources(JsonElement root)
    {
        if (root.TryGetProperty("viewerMedia", out var viewerMedia))
        {
            return ParseViewerMap(viewerMedia, "viewerMedia", ParseViewerSourcesEntry);
        }
        if (root.TryGetProperty("viewerSources", out var viewerSources))
        {
            return ParseViewerMap(viewerSources, "viewerSources", ParseAudioOnlyEntry);
        }
        return new Dictionary<string, IReadOnlyDictionary<string, GroupCallViewerSources>>();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, GroupCallViewerSources>>
        ParseViewerMap(JsonElement map, string field,
            Func<JsonElement, GroupCallViewerSources> parseEntry)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, GroupCallViewerSources>>();
        if (map.ValueKind != JsonValueKind.Object)
        {
            throw Unmappable($"{field} must be an object");
        }
        foreach (var viewer in map.EnumerateObject())
        {
            if (viewer.Value.ValueKind != JsonValueKind.Object)
            {
                throw Unmappable($"{field} entry must be an object");
            }
            if (result.Count >= MaxParticipants)
            {
                throw Unmappable($"{field} exceeds participant bound");
            }
            var inner = new Dictionary<string, GroupCallViewerSources>();
            foreach (var producer in viewer.Value.EnumerateObject())
            {
                inner[producer.Name] = parseEntry(producer.Value);
            }
            result[viewer.Name] = inner;
        }
        return result;
    }

    private static GroupCallViewerSources ParseAudioOnlyEntry(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var raw))
        {
            throw Unmappable("viewerSources value must be a number");
        }
        return new GroupCallViewerSources(ReinterpretSource(raw, "viewerSource"), null, null);
    }

    private static GroupCallViewerSources ParseViewerSourcesEntry(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Unmappable("viewerMedia value must be an object");
        }
        var audioSource = ReinterpretSource(
            ReadInt64(value, "audioSource", "join"), "audioSource");
        return new GroupCallViewerSources(
            audioSource,
            ParseParticipantVideo(value, "video", audioSource),
            ParseParticipantVideo(value, "presentation", audioSource));
    }

    private static GroupCallParticipantVideoSources? ParseParticipantVideo(JsonElement parent,
        string name, int audioSource)
    {
        if (!parent.TryGetProperty(name, out var video) ||
            video.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (video.ValueKind != JsonValueKind.Object)
        {
            throw Unmappable($"'{name}' must be an object or null");
        }

        var endpoint = ReadRequiredString(video, "endpoint");
        if (!video.TryGetProperty("sourceGroups", out var groupsElement) ||
            groupsElement.ValueKind != JsonValueKind.Array ||
            groupsElement.GetArrayLength() == 0)
        {
            throw Unmappable($"'{name}' has no source groups");
        }

        var groups = new List<GroupCallVideoSourceGroup>();
        foreach (var group in groupsElement.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Object)
            {
                throw Unmappable("source group must be an object");
            }
            var semantics = ReadRequiredString(group, "semantics");
            if (!group.TryGetProperty("sources", out var sourcesElement) ||
                sourcesElement.ValueKind != JsonValueKind.Array ||
                sourcesElement.GetArrayLength() == 0)
            {
                throw Unmappable("source group has no sources");
            }
            var sources = new List<int>();
            foreach (var source in sourcesElement.EnumerateArray())
            {
                if (source.ValueKind != JsonValueKind.Number ||
                    !source.TryGetInt64(out var raw))
                {
                    throw Unmappable("source must be a number");
                }
                sources.Add(ReinterpretSource(raw, "videoSource"));
            }
            groups.Add(new GroupCallVideoSourceGroup(semantics, sources));
        }

        var paused = video.TryGetProperty("paused", out var pausedElement) &&
            pausedElement.ValueKind == JsonValueKind.True;
        return new GroupCallParticipantVideoSources(endpoint, groups, audioSource, paused);
    }

    private static string ReadRequiredString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            throw Unmappable($"missing string '{name}'");
        }
        var text = value.GetString()!;
        if (text.Length == 0)
        {
            throw Unmappable($"'{name}' must not be empty");
        }
        return text;
    }

    private static bool ReadBool(JsonElement parent, string name, string operation)
    {
        if (!parent.TryGetProperty(name, out var value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            throw new GroupCallMediaException(GroupCallMediaFailureKind.Unavailable,
                $"group-call media worker {operation} response missing bool '{name}'");
        }
        return value.GetBoolean();
    }

    private static int ReadInt32(JsonElement parent, string name, string operation)
    {
        if (!parent.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            throw new GroupCallMediaException(GroupCallMediaFailureKind.Unavailable,
                $"group-call media worker {operation} response missing int '{name}'");
        }
        return result;
    }

    private static long ReadInt64(JsonElement parent, string name, string operation)
    {
        if (!parent.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result))
        {
            throw new GroupCallMediaException(GroupCallMediaFailureKind.Unavailable,
                $"group-call media worker {operation} response missing number '{name}'");
        }
        return result;
    }

    private static int ReinterpretSource(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw Unmappable($"'{name}' is out of the uint32 SSRC range");
        }
        return unchecked((int)(uint)value);
    }

    private static GroupCallMediaException Unmappable(string reason) =>
        new(GroupCallMediaFailureKind.Unavailable,
            $"group-call media worker returned an unmappable response: {reason}");

    private static byte[] BuildJoinBody(GroupCallJoinPayload payload)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("payload");
            writer.WriteNumber("ssrc", payload.Source);
            writer.WriteString("ufrag", payload.Ufrag);
            writer.WriteString("pwd", payload.Pwd);
            writer.WriteStartArray("fingerprints");
            foreach (var fingerprint in payload.Fingerprints)
            {
                writer.WriteStartObject();
                writer.WriteString("hash", fingerprint.Hash);
                writer.WriteString("fingerprint", fingerprint.Value);
                writer.WriteString("setup", fingerprint.Setup);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            if (payload.VideoSourceGroups.Count > 0)
            {
                writer.WriteStartArray("ssrc-groups");
                foreach (GroupCallVideoSourceGroup group in payload.VideoSourceGroups)
                {
                    writer.WriteStartObject();
                    writer.WriteString("semantics", group.Semantics);
                    writer.WriteStartArray("sources");
                    foreach (int source in group.Sources)
                    {
                        writer.WriteNumberValue(source);
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] BuildMuteBody(bool muted)
    {
        var buffer = new ArrayBufferWriter<byte>(32);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("muted", muted);
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] BuildVideoPausedBody(bool paused)
    {
        var buffer = new ArrayBufferWriter<byte>(32);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("paused", paused);
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static string RoomPath(long callId) =>
        "rooms/" + callId.ToString(CultureInfo.InvariantCulture);

    private static string ParticipantPath(long callId, string participantId) =>
        RoomPath(callId) + "/participants/" + Uri.EscapeDataString(participantId);

    private sealed class Subscription : IDisposable
    {
        private readonly MediasoupGroupCallMediaPlane _owner;
        private Action<GroupCallMediaDisconnectEvent>? _handler;

        public Subscription(MediasoupGroupCallMediaPlane owner,
            Action<GroupCallMediaDisconnectEvent> handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Dispose()
        {
            var handler = _handler;
            if (handler is null)
            {
                return;
            }
            _handler = null;
            lock (_owner._gate)
            {
                _owner._subscribers.Remove(handler);
            }
        }
    }

    private sealed class SourcesChangedSubscription : IDisposable
    {
        private readonly MediasoupGroupCallMediaPlane _owner;
        private Action<GroupCallMediaSourcesChangedEvent>? _handler;

        public SourcesChangedSubscription(MediasoupGroupCallMediaPlane owner,
            Action<GroupCallMediaSourcesChangedEvent> handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Dispose()
        {
            var handler = _handler;
            if (handler is null)
            {
                return;
            }
            _handler = null;
            lock (_owner._gate)
            {
                _owner._sourcesChangedSubscribers.Remove(handler);
            }
        }
    }
}
