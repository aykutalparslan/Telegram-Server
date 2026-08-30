// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Text.Json;

namespace Ferrite.Services.Calls;

public sealed class GroupCallDataJsonException : Exception
{
    public GroupCallDataJsonException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public static class GroupCallJoinPayloadCodec
{
    public const int MaxPayloadBytes = 8 * 1024;
    public const int MaxDepth = 10;
    public const int MaxFingerprints = 8;
    public const int MaxIceStringLength = 256;
    public const int MaxFingerprintValueLength = 256;
    public const int MaxHashLength = 16;
    public const int MaxSetupLength = 16;
    public const int MaxSourceGroups = 8;
    public const int MaxSourcesPerGroup = 16;
    public const int MaxSemanticsLength = 16;

    private const string ExpectedSetup = "passive";

    public static GroupCallJoinPayload ParseJoinPayload(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length == 0)
        {
            throw new GroupCallDataJsonException("group-call join payload is empty");
        }
        if (utf8Json.Length > MaxPayloadBytes)
        {
            throw new GroupCallDataJsonException(
                $"group-call join payload exceeds {MaxPayloadBytes} bytes");
        }

        try
        {
            var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions
            {
                MaxDepth = MaxDepth,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
            return ParseRoot(ref reader);
        }
        catch (JsonException ex)
        {
            throw new GroupCallDataJsonException("group-call join payload is not valid JSON", ex);
        }
    }

    public static byte[] BuildConnectionParams(GroupCallMediaTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("transport");
            writer.WriteString("ufrag", transport.Ufrag);
            writer.WriteString("pwd", transport.Pwd);

            writer.WriteStartArray("fingerprints");
            foreach (var fingerprint in transport.Fingerprints)
            {
                writer.WriteStartObject();
                writer.WriteString("hash", fingerprint.Hash);
                writer.WriteString("fingerprint", fingerprint.Value);
                writer.WriteString("setup", fingerprint.Setup);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("candidates");
            foreach (var candidate in transport.Candidates)
            {
                writer.WriteStartObject();
                writer.WriteString("port", candidate.Port);
                writer.WriteString("protocol", candidate.Protocol);
                writer.WriteString("network", candidate.Network);
                writer.WriteString("generation", candidate.Generation);
                writer.WriteString("id", candidate.Id);
                writer.WriteString("component", candidate.Component);
                writer.WriteString("foundation", candidate.Foundation);
                writer.WriteString("priority", candidate.Priority);
                writer.WriteString("ip", candidate.Ip);
                writer.WriteString("type", candidate.Type);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();

            if (transport.Video is { } video)
            {
                writer.WriteStartObject("video");
                writer.WriteString("endpoint", video.Endpoint);

                writer.WriteStartArray("server_sources");
                writer.WriteNumberValue(video.ServerSource);
                writer.WriteEndArray();

                writer.WriteStartArray("payload-types");
                foreach (var payloadType in video.PayloadTypes)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("id", payloadType.Id);
                    writer.WriteString("name", payloadType.Name);
                    writer.WriteNumber("clockrate", payloadType.ClockRate);
                    writer.WriteNumber("channels", payloadType.Channels);

                    writer.WriteStartObject("parameters");
                    foreach (var parameter in payloadType.Parameters)
                    {
                        writer.WriteString(parameter.Key, parameter.Value);
                    }
                    writer.WriteEndObject();

                    writer.WriteStartArray("rtcp-fbs");
                    foreach (var feedback in payloadType.FeedbackTypes)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("type", feedback.Type);
                        if (feedback.Subtype is { } subtype)
                        {
                            writer.WriteString("subtype", subtype);
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WriteStartArray("rtp-hdrexts");
                foreach (var extension in video.Extensions)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("id", extension.Id);
                    writer.WriteString("uri", extension.Uri);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static GroupCallJoinPayload ParseRoot(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new GroupCallDataJsonException("group-call join payload must be a JSON object");
        }

        int? source = null;
        string? ufrag = null;
        string? pwd = null;
        List<GroupCallDtlsFingerprint>? fingerprints = null;
        List<GroupCallVideoSourceGroup>? sourceGroups = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            switch (name)
            {
                case "ssrc":
                    RejectDuplicate(source is not null, name);
                    source = ReadSignedSource(ref reader);
                    break;
                case "ufrag":
                    RejectDuplicate(ufrag is not null, name);
                    ufrag = ReadBoundedString(ref reader, MaxIceStringLength, name);
                    break;
                case "pwd":
                    RejectDuplicate(pwd is not null, name);
                    pwd = ReadBoundedString(ref reader, MaxIceStringLength, name);
                    break;
                case "fingerprints":
                    RejectDuplicate(fingerprints is not null, name);
                    fingerprints = ReadFingerprints(ref reader);
                    break;
                case "ssrc-groups":
                    RejectDuplicate(sourceGroups is not null, name);
                    sourceGroups = ReadSourceGroups(ref reader);
                    break;
                default:
                    throw new GroupCallDataJsonException($"unexpected group-call join key '{name}'");
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new GroupCallDataJsonException("group-call join payload is malformed");
        }
        if (reader.Read())
        {
            throw new GroupCallDataJsonException("group-call join payload has trailing content");
        }

        if (source is null || ufrag is null || pwd is null || fingerprints is null)
        {
            throw new GroupCallDataJsonException("group-call join payload is missing a required field");
        }
        if (source.Value == 0)
        {
            throw new GroupCallDataJsonException("group-call join source must be nonzero");
        }
        if (fingerprints.Count == 0)
        {
            throw new GroupCallDataJsonException("group-call join payload requires a fingerprint");
        }

        IReadOnlyList<GroupCallVideoSourceGroup> videoSourceGroups = sourceGroups is null
            ? Array.Empty<GroupCallVideoSourceGroup>()
            : sourceGroups;

        return new GroupCallJoinPayload(source.Value, ufrag, pwd, fingerprints,
            videoSourceGroups);
    }

    private static List<GroupCallDtlsFingerprint> ReadFingerprints(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            throw new GroupCallDataJsonException("fingerprints must be a JSON array");
        }

        var result = new List<GroupCallDtlsFingerprint>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new GroupCallDataJsonException("fingerprint must be a JSON object");
            }
            if (result.Count == MaxFingerprints)
            {
                throw new GroupCallDataJsonException(
                    $"group-call join payload has more than {MaxFingerprints} fingerprints");
            }
            result.Add(ReadFingerprint(ref reader));
        }

        return result;
    }

    private static GroupCallDtlsFingerprint ReadFingerprint(ref Utf8JsonReader reader)
    {
        string? hash = null;
        string? value = null;
        string? setup = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            switch (name)
            {
                case "hash":
                    RejectDuplicate(hash is not null, name);
                    hash = ReadBoundedString(ref reader, MaxHashLength, name);
                    if (!IsAllowedHash(hash))
                    {
                        throw new GroupCallDataJsonException(
                            $"unsupported DTLS fingerprint hash '{hash}'");
                    }
                    break;
                case "fingerprint":
                    RejectDuplicate(value is not null, name);
                    value = ReadBoundedString(ref reader, MaxFingerprintValueLength, name);
                    break;
                case "setup":
                    RejectDuplicate(setup is not null, name);
                    setup = ReadBoundedString(ref reader, MaxSetupLength, name);
                    if (setup != ExpectedSetup)
                    {
                        throw new GroupCallDataJsonException(
                            $"client DTLS setup must be '{ExpectedSetup}' but was '{setup}'");
                    }
                    break;
                default:
                    throw new GroupCallDataJsonException($"unexpected fingerprint key '{name}'");
            }
        }

        if (hash is null || value is null || setup is null)
        {
            throw new GroupCallDataJsonException("fingerprint is missing a required field");
        }
        return new GroupCallDtlsFingerprint(hash, value, setup);
    }

    private static int ReadSignedSource(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
        {
            throw new GroupCallDataJsonException("ssrc must be a JSON number");
        }
        if (!reader.TryGetInt32(out var value))
        {
            throw new GroupCallDataJsonException("ssrc must be a signed 32-bit integer");
        }
        return value;
    }

    private static string ReadBoundedString(ref Utf8JsonReader reader, int maxLength, string name)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
        {
            throw new GroupCallDataJsonException($"'{name}' must be a JSON string");
        }
        var value = reader.GetString()!;
        if (value.Length == 0)
        {
            throw new GroupCallDataJsonException($"'{name}' must not be empty");
        }
        if (value.Length > maxLength)
        {
            throw new GroupCallDataJsonException($"'{name}' exceeds {maxLength} characters");
        }
        return value;
    }

    private static List<GroupCallVideoSourceGroup> ReadSourceGroups(
        ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            throw new GroupCallDataJsonException("'ssrc-groups' must be a JSON array");
        }

        var result = new List<GroupCallVideoSourceGroup>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new GroupCallDataJsonException("source group must be a JSON object");
            }
            if (result.Count == MaxSourceGroups)
            {
                throw new GroupCallDataJsonException(
                    $"group-call join payload has more than {MaxSourceGroups} source groups");
            }
            result.Add(ReadSourceGroup(ref reader));
        }

        return result;
    }

    private static GroupCallVideoSourceGroup ReadSourceGroup(ref Utf8JsonReader reader)
    {
        string? semantics = null;
        List<int>? sources = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            switch (name)
            {
                case "semantics":
                    RejectDuplicate(semantics is not null, name);
                    semantics = ReadBoundedString(ref reader, MaxSemanticsLength, name);
                    if (!IsAllowedSemantics(semantics))
                    {
                        throw new GroupCallDataJsonException(
                            $"unsupported source group semantics '{semantics}'");
                    }
                    break;
                case "sources":
                    RejectDuplicate(sources is not null, name);
                    sources = ReadSources(ref reader);
                    break;
                default:
                    throw new GroupCallDataJsonException(
                        $"unexpected source group key '{name}'");
            }
        }

        if (semantics is null || sources is null)
        {
            throw new GroupCallDataJsonException("source group is missing a required field");
        }
        if (sources.Count == 0)
        {
            throw new GroupCallDataJsonException("source group requires at least one source");
        }

        return new GroupCallVideoSourceGroup(semantics, sources);
    }

    private static List<int> ReadSources(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            throw new GroupCallDataJsonException("'sources' must be a JSON array");
        }

        var result = new List<int>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number ||
                !reader.TryGetInt32(out var value))
            {
                throw new GroupCallDataJsonException(
                    "source must be a signed 32-bit integer");
            }
            if (value == 0)
            {
                throw new GroupCallDataJsonException("source must be nonzero");
            }
            if (result.Count == MaxSourcesPerGroup)
            {
                throw new GroupCallDataJsonException(
                    $"source group has more than {MaxSourcesPerGroup} sources");
            }
            result.Add(value);
        }

        return result;
    }

    private static bool IsAllowedSemantics(string semantics) => semantics switch
    {
        "SIM" or "FID" or "FEC-FR" => true,
        _ => false,
    };

    private static void RejectDuplicate(bool alreadySeen, string name)
    {
        if (alreadySeen)
        {
            throw new GroupCallDataJsonException($"duplicate group-call join key '{name}'");
        }
    }

    private static bool IsAllowedHash(string hash) => hash switch
    {
        "sha-1" or "sha-224" or "sha-256" or "sha-384" or "sha-512" => true,
        _ => false,
    };
}
