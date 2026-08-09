// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ferrite.Services.Calls;

namespace Ferrite.GroupCallMedia;

public sealed class MediasoupGroupCallBroadcastPlane : IGroupCallBroadcastPlane
{
    private readonly HttpClient _httpClient;
    private readonly GroupCallMediaWorkerOptions _workerOptions;
    private readonly GroupCallBroadcastOptions _broadcastOptions;

    public MediasoupGroupCallBroadcastPlane(HttpClient httpClient,
        GroupCallMediaWorkerOptions workerOptions,
        GroupCallBroadcastOptions broadcastOptions)
    {
        workerOptions.Validate();
        broadcastOptions.Validate();
        _httpClient = httpClient;
        _workerOptions = workerOptions;
        _broadcastOptions = broadcastOptions;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async ValueTask CreateStreamAsync(long callId, bool rtmpStream,
        CancellationToken cancellationToken = default)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { rtmpStream });
        using var request = Build(HttpMethod.Put, StreamPath(callId), body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async ValueTask<bool> EndStreamAsync(long callId,
        CancellationToken cancellationToken = default)
    {
        using var request = Build(HttpMethod.Delete, StreamPath(callId));
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using JsonDocument document = await ReadJsonAsync(response,
            cancellationToken);
        return ReadBool(document.RootElement, "ended");
    }

    public async ValueTask<GroupCallBroadcastCredentials> GetCredentialsAsync(
        long callId, bool revoke,
        CancellationToken cancellationToken = default)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { revoke });
        using var request = Build(HttpMethod.Post,
            $"{StreamPath(callId)}/credentials", body);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using JsonDocument document = await ReadJsonAsync(response,
            cancellationToken);
        JsonElement root = document.RootElement;
        return new GroupCallBroadcastCredentials(ReadString(root, "url"),
            ReadString(root, "key"), ReadInt32(root, "generation"));
    }

    public async ValueTask<IReadOnlyList<GroupCallBroadcastChannel>>
        GetChannelsAsync(long callId,
            CancellationToken cancellationToken = default)
    {
        using var request = Build(HttpMethod.Get,
            $"{StreamPath(callId)}/channels");
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using JsonDocument document = await ReadJsonAsync(response,
            cancellationToken);
        if (!document.RootElement.TryGetProperty("channels", out var channels) ||
            channels.ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse("broadcast channels are missing");
        }
        var result = new List<GroupCallBroadcastChannel>();
        foreach (JsonElement channel in channels.EnumerateArray())
        {
            result.Add(new GroupCallBroadcastChannel(ReadInt32(channel, "channel"),
                ReadInt32(channel, "scale"),
                ReadInt64(channel, "lastTimestampMs")));
        }
        return result;
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadSegmentAsync(
        GroupCallBroadcastSegmentRequest request,
        CancellationToken cancellationToken = default)
    {
        string query = string.Create(CultureInfo.InvariantCulture,
            $"timestamp={request.TimestampMs}&scale={request.Scale}" +
            $"&channel={request.Channel}&quality={request.VideoQuality}");
        using var httpRequest = Build(HttpMethod.Get,
            $"{StreamPath(request.CallId)}/segments?{query}");
        using var response = await SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        long? contentLength = response.Content.Headers.ContentLength;
        if (contentLength is <= 0 ||
            contentLength > _broadcastOptions.MaxSegmentBytes)
        {
            throw InvalidResponse("broadcast segment length violates the bound");
        }
        byte[] result = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (result.Length == 0 || result.Length > _broadcastOptions.MaxSegmentBytes)
        {
            throw InvalidResponse("broadcast segment body violates the bound");
        }
        return result;
    }

    public async ValueTask<GroupCallBroadcastHealth> HealthAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = Build(HttpMethod.Get, "health");
        using var response = await SendAsync(request, cancellationToken,
            _workerOptions.HealthTimeout);
        await EnsureSuccessAsync(response, cancellationToken);
        using JsonDocument document = await ReadJsonAsync(response,
            cancellationToken);
        if (!document.RootElement.TryGetProperty("broadcast", out var broadcast) ||
            broadcast.ValueKind != JsonValueKind.Object)
        {
            throw InvalidResponse("broadcast health is missing");
        }
        return new GroupCallBroadcastHealth(ReadBool(broadcast, "healthy"),
            ReadInt32(broadcast, "streams"),
            ReadInt32(broadcast, "liveStreams"),
            ReadInt32(broadcast, "segments"),
            ReadInt64(broadcast, "bytes"),
            broadcast.TryGetProperty("ffmpegVersion", out var ffmpeg) &&
            ffmpeg.ValueKind == JsonValueKind.String
                ? ffmpeg.GetString()
                : null);
    }

    private HttpRequestMessage Build(HttpMethod method, string relativePath,
        byte[]? body = null)
    {
        var request = new HttpRequestMessage(method,
            new Uri(_workerOptions.ControlUrl, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            _workerOptions.AuthSecret);
        request.Headers.TryAddWithoutValidation(GroupCallMediaProtocol.ProtocolHeader,
            _workerOptions.ProtocolVersion);
        if (body != null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(
                "application/json") { CharSet = "utf-8" };
        }
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        linked.CancelAfter(timeout ?? _workerOptions.RequestTimeout);
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, linked.Token);
            await response.Content.LoadIntoBufferAsync(
                _broadcastOptions.MaxSegmentBytes, linked.Token);
            return response;
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            response?.Dispose();
            throw new GroupCallBroadcastException(
                GroupCallBroadcastFailureKind.Unavailable,
                "broadcast worker request timed out", e);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or
                                  InvalidOperationException)
        {
            response?.Dispose();
            throw new GroupCallBroadcastException(
                GroupCallBroadcastFailureKind.Unavailable,
                "broadcast worker response is unavailable or exceeds its bound", e);
        }
        catch
        {
            response?.Dispose();
            throw;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        string message;
        try
        {
            using JsonDocument error = await ReadJsonAsync(response,
                cancellationToken);
            message = error.RootElement.TryGetProperty("error", out var value) &&
                      value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "broadcast worker rejected the request"
                : "broadcast worker rejected the request";
        }
        catch (GroupCallBroadcastException e) when (
            e.Kind == GroupCallBroadcastFailureKind.Unavailable)
        {
            message = "broadcast worker rejected the request";
        }

        GroupCallBroadcastFailureKind kind = (int)response.StatusCode == 425
            ? GroupCallBroadcastFailureKind.NotReady
            : response.StatusCode switch
        {
            HttpStatusCode.NotFound => GroupCallBroadcastFailureKind.Expired,
            HttpStatusCode.BadRequest => GroupCallBroadcastFailureKind.Unsupported,
            HttpStatusCode.ServiceUnavailable =>
                GroupCallBroadcastFailureKind.Unavailable,
            _ => GroupCallBroadcastFailureKind.Rejected
        };
        throw new GroupCallBroadcastException(kind, message);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using Stream stream = await response.Content
                .ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream,
                cancellationToken: cancellationToken);
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            throw InvalidResponse("body is not valid bounded JSON");
        }
    }

    private static string StreamPath(long callId)
    {
        if (callId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callId));
        }
        return $"broadcast/{callId}";
    }

    private static bool ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw InvalidResponse($"{name} must be a bool");

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()!
            : throw InvalidResponse($"{name} must be a non-empty string");

    private static int ReadInt32(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out int result)
            ? result
            : throw InvalidResponse($"{name} must be an int32");

    private static long ReadInt64(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt64(out long result)
            ? result
            : throw InvalidResponse($"{name} must be an int64");

    private static GroupCallBroadcastException InvalidResponse(string message) =>
        new(GroupCallBroadcastFailureKind.Unavailable,
            $"invalid broadcast worker response: {message}");
}
