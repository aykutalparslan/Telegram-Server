// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ferrite.Services.Calls;

namespace Ferrite.GroupCallMedia;

public sealed class MediasoupGroupCallRecorder : IGroupCallRecorder
{
    private const int MaxControlResponseBytes = 256 * 1024;
    private readonly HttpClient _httpClient;
    private readonly GroupCallMediaWorkerOptions _workerOptions;
    private readonly GroupCallRecordingOptions _recordingOptions;

    public MediasoupGroupCallRecorder(HttpClient httpClient,
        GroupCallMediaWorkerOptions workerOptions,
        GroupCallRecordingOptions recordingOptions)
    {
        workerOptions.Validate();
        recordingOptions.Validate();
        _httpClient = httpClient;
        _workerOptions = workerOptions;
        _recordingOptions = recordingOptions;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async ValueTask StartRecordingAsync(GroupCallRecordingRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            generation = request.Generation,
            startedDate = request.StartedDate,
            initiatingUserId = request.InitiatingUserId,
            title = request.Title,
            video = request.Video,
            portrait = request.Portrait
        });
        using var message = Build(HttpMethod.Put, Path(request.CallId), body);
        using HttpResponseMessage response = await SendBufferedAsync(message,
            _workerOptions.RequestTimeout, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async ValueTask<GroupCallRecordingFile> FinalizeRecordingAsync(
        long callId, int generation,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(callId, generation);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { generation });
        using var message = Build(HttpMethod.Post, $"{Path(callId)}/stop", body);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_recordingOptions.FinalizeTimeout);
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(message,
                HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                await response.Content.LoadIntoBufferAsync(
                    MaxControlResponseBytes, linked.Token);
                await EnsureSuccessAsync(response, linked.Token);
            }

            long length = response.Content.Headers.ContentLength ?? 0;
            if (length <= 0 || length > _recordingOptions.MaxRecordingBytes)
            {
                throw InvalidResponse(
                    "recording Content-Length violates the configured bound");
            }
            string mimeType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (mimeType is not ("video/mp4" or "audio/mp4"))
            {
                throw InvalidResponse("recording MIME type is unsupported");
            }
            string fileName = ReadFileName(response.Content.Headers.ContentDisposition);
            double duration = ReadDoubleHeader(response,
                "X-Ferrite-Recording-Duration");
            int width = ReadIntHeader(response, "X-Ferrite-Recording-Width");
            int height = ReadIntHeader(response, "X-Ferrite-Recording-Height");
            if (duration <= 0 || width < 0 || height < 0 ||
                (mimeType == "video/mp4" && (width == 0 || height == 0)) ||
                (mimeType == "audio/mp4" && (width != 0 || height != 0)))
            {
                throw InvalidResponse("recording media metadata is inconsistent");
            }

            Stream stream = await response.Content.ReadAsStreamAsync(linked.Token);
            HttpResponseMessage ownedResponse = response;
            CancellationTokenSource ownedCancellation = linked;
            response = null;
            linked = null!;
            return new GroupCallRecordingFile(stream, length, fileName, mimeType,
                duration, width, height, () =>
                {
                    ownedResponse.Dispose();
                    ownedCancellation.Dispose();
                    return ValueTask.CompletedTask;
                });
        }
        catch (OperationCanceledException e) when (
            !cancellationToken.IsCancellationRequested)
        {
            throw new GroupCallRecordingException(
                GroupCallRecordingFailureKind.Unavailable,
                "recording worker finalization timed out", e);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or
                                  InvalidOperationException)
        {
            throw new GroupCallRecordingException(
                GroupCallRecordingFailureKind.Unavailable,
                "recording worker finalization is unavailable", e);
        }
        finally
        {
            response?.Dispose();
            linked?.Dispose();
        }
    }

    public async ValueTask AcknowledgeRecordingAsync(long callId, int generation,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(callId, generation);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { generation });
        using var message = Build(HttpMethod.Post, $"{Path(callId)}/ack", body);
        using HttpResponseMessage response = await SendBufferedAsync(message,
            _workerOptions.RequestTimeout, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async ValueTask<bool> CancelRecordingAsync(long callId, int generation,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(callId, generation);
        string query = string.Create(CultureInfo.InvariantCulture,
            $"{Path(callId)}?generation={generation}");
        using var message = Build(HttpMethod.Delete, query);
        using HttpResponseMessage response = await SendBufferedAsync(message,
            _workerOptions.RequestTimeout, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken);
        return ReadBool(document.RootElement, "cancelled");
    }

    public async ValueTask<GroupCallRecordingHealth> HealthAsync(
        CancellationToken cancellationToken = default)
    {
        using var message = Build(HttpMethod.Get, "health");
        using HttpResponseMessage response = await SendBufferedAsync(message,
            _workerOptions.HealthTimeout, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken);
        if (!document.RootElement.TryGetProperty("recording", out JsonElement root) ||
            root.ValueKind != JsonValueKind.Object)
        {
            throw InvalidResponse("recording health is missing");
        }
        return new GroupCallRecordingHealth(ReadBool(root, "healthy"),
            ReadInt32(root, "activeRecordings"),
            ReadInt32(root, "finalizedRecordings"),
            ReadInt64(root, "bytes"),
            root.TryGetProperty("ffmpegVersion", out JsonElement ffmpeg) &&
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

    private async Task<HttpResponseMessage> SendBufferedAsync(
        HttpRequestMessage request, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        linked.CancelAfter(timeout);
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, linked.Token);
            await response.Content.LoadIntoBufferAsync(MaxControlResponseBytes,
                linked.Token);
            return response;
        }
        catch (OperationCanceledException e) when (
            !cancellationToken.IsCancellationRequested)
        {
            response?.Dispose();
            throw new GroupCallRecordingException(
                GroupCallRecordingFailureKind.Unavailable,
                "recording worker request timed out", e);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or
                                  InvalidOperationException)
        {
            response?.Dispose();
            throw new GroupCallRecordingException(
                GroupCallRecordingFailureKind.Unavailable,
                "recording worker response is unavailable or exceeds its bound", e);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        string message = "recording worker rejected the request";
        try
        {
            using JsonDocument error = await ReadJsonAsync(response,
                cancellationToken);
            if (error.RootElement.TryGetProperty("error", out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
            {
                message = value.GetString() ?? message;
            }
        }
        catch (GroupCallRecordingException)
        {
        }

        GroupCallRecordingFailureKind kind = response.StatusCode switch
        {
            HttpStatusCode.NotFound => GroupCallRecordingFailureKind.NotFound,
            HttpStatusCode.Conflict => GroupCallRecordingFailureKind.Conflict,
            HttpStatusCode.RequestEntityTooLarge =>
                GroupCallRecordingFailureKind.LimitExceeded,
            HttpStatusCode.ServiceUnavailable =>
                GroupCallRecordingFailureKind.Unavailable,
            _ when (int)response.StatusCode == 425 =>
                GroupCallRecordingFailureKind.Rejected,
            _ => GroupCallRecordingFailureKind.Rejected,
        };
        throw new GroupCallRecordingException(kind, message);
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

    private static string ReadFileName(ContentDispositionHeaderValue? disposition)
    {
        string raw = disposition?.FileNameStar ?? disposition?.FileName ?? "";
        string value = raw.Trim('"');
        if (value.Length is < 1 or > 255 ||
            System.IO.Path.GetFileName(value) != value ||
            value.Any(char.IsControl))
        {
            throw InvalidResponse("recording filename is invalid");
        }
        return value;
    }

    private static double ReadDoubleHeader(HttpResponseMessage response,
        string name) => response.Headers.TryGetValues(name, out var values) &&
        double.TryParse(values.SingleOrDefault(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double result) &&
        double.IsFinite(result)
            ? result
            : throw InvalidResponse($"{name} must be a finite double");

    private static int ReadIntHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) &&
        int.TryParse(values.SingleOrDefault(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int result)
            ? result
            : throw InvalidResponse($"{name} must be an int32");

    private static bool ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw InvalidResponse($"{name} must be a bool");

    private static int ReadInt32(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.TryGetInt32(out int result)
            ? result
            : throw InvalidResponse($"{name} must be an int32");

    private static long ReadInt64(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.TryGetInt64(out long result)
            ? result
            : throw InvalidResponse($"{name} must be an int64");

    private static string Path(long callId)
    {
        if (callId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callId));
        }
        return $"recordings/{callId}";
    }

    private static void ValidateIdentity(long callId, int generation)
    {
        if (callId <= 0 || generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callId));
        }
    }

    private static void ValidateRequest(GroupCallRecordingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentity(request.CallId, request.Generation);
        if (request.StartedDate <= 0 || request.InitiatingUserId <= 0 ||
            request.Portrait && !request.Video)
        {
            throw new ArgumentException("recording request is invalid");
        }
    }

    private static GroupCallRecordingException InvalidResponse(string message) =>
        new(GroupCallRecordingFailureKind.InvalidResponse,
            $"invalid recording worker response: {message}");
}
