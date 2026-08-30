// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Ferrite.Core;
using Ferrite.GroupCallMedia;
using Ferrite.Services.Calls;

namespace Ferrite;

public class Program
{
    public static async Task Main(String[] args)
    {
        string publicAddress = Environment.GetEnvironmentVariable(
            "FERRITE_PUBLIC_ADDRESS") ?? "10.0.2.2";
        int port = ReadInt("FERRITE_PORT", 5222);
        string dataPath = Environment.GetEnvironmentVariable("FERRITE_DATA_PATH") ??
                          "data";

        string? controlUrl = Environment.GetEnvironmentVariable(
            "FERRITE_GROUP_CALL_CONTROL_URL");
        string? authSecret = Environment.GetEnvironmentVariable(
            "FERRITE_GROUP_CALL_AUTH_SECRET");
        GroupCallMediaWorkerOptions? groupCallMedia = null;
        if (!string.IsNullOrEmpty(controlUrl) || !string.IsNullOrEmpty(authSecret))
        {
            if (string.IsNullOrEmpty(controlUrl) || string.IsNullOrEmpty(authSecret))
            {
                throw new ArgumentException("FERRITE_GROUP_CALL_CONTROL_URL and " +
                    "FERRITE_GROUP_CALL_AUTH_SECRET must be configured together");
            }
            groupCallMedia = new GroupCallMediaWorkerOptions
            {
                ControlUrl = new Uri(controlUrl, UriKind.Absolute),
                AuthSecret = authSecret,
                RequestTimeout = TimeSpan.FromSeconds(
                    ReadInt("FERRITE_GROUP_CALL_REQUEST_TIMEOUT_SECONDS", 5)),
                HealthTimeout = TimeSpan.FromSeconds(
                    ReadInt("FERRITE_GROUP_CALL_HEALTH_TIMEOUT_SECONDS", 2)),
            };
        }

        var options = new FerriteServerOptions
        {
            PublicAddress = publicAddress,
            Port = port,
            DataPath = dataPath,
            NodeId = ReadGuid("FERRITE_NODE_ID"),
            Storage = StorageOptions.FromEnvironment(),
            CallTurn = ReadCallTurn(Environment.GetEnvironmentVariable),
            CallMedia = ReadCallMedia(Environment.GetEnvironmentVariable),
            GroupCallMediaWorker = groupCallMedia,
            GroupCallVideo = new GroupCallVideoOptions(
                ReadInt("FERRITE_GROUP_CALL_UNMUTED_VIDEO_LIMIT", 30)),
            GroupCallMediaRuntime = new GroupCallMediaRuntimeOptions
            {
                HealthInterval = TimeSpan.FromSeconds(
                    ReadInt("FERRITE_GROUP_CALL_HEALTH_INTERVAL_SECONDS", 5))
            },
            GroupCallRecording = new GroupCallRecordingOptions
            {
                MaxRecordingBytes = ReadLong(
                    "FERRITE_GROUP_CALL_MAX_RECORDING_BYTES", 2_000_000_000),
                FinalizeTimeout = TimeSpan.FromSeconds(ReadInt(
                    "FERRITE_GROUP_CALL_RECORDING_FINALIZE_TIMEOUT_SECONDS", 120)),
                HealthPollInterval = TimeSpan.FromSeconds(ReadInt(
                    "FERRITE_GROUP_CALL_RECORDING_HEALTH_INTERVAL_SECONDS", 5)),
                MaxTitleBytes = ReadInt(
                    "FERRITE_GROUP_CALL_RECORDING_MAX_TITLE_BYTES", 255)
            }
        };
        IFerriteServer ferriteServer = ServerBuilder.BuildServer(options);
        using var stopping = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };
        using PosixSignalRegistration terminate = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                stopping.Cancel();
            });
        try
        {
            await ferriteServer.StartAsync(
                new IPEndPoint(IPAddress.Any, port), stopping.Token);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
        finally
        {
            await ferriteServer.StopAsync(CancellationToken.None);
        }
    }

    internal static CallTurnOptions? ReadCallTurn(Func<string, string?> read)
    {
        if (read("FERRITE_TURN_ENABLED") is not "1")
        {
            return null;
        }
        var options = new CallTurnOptions
        {
            Enabled = true,
            AdvertisedIPv4 = read("FERRITE_TURN_ADVERTISED_IPV4") ?? "",
            AdvertisedIPv6 = read("FERRITE_TURN_ADVERTISED_IPV6") ?? "",
            Port = int.TryParse(read("FERRITE_TURN_PORT"), out int turnPort)
                ? turnPort
                : 3478,
            Realm = read("FERRITE_TURN_REALM") ?? "",
            SharedSecret = read("FERRITE_TURN_SECRET") ?? ""
        };
        if (!options.TryValidate(out string error))
        {
            throw new ArgumentException(error);
        }
        return options;
    }

    internal static CallMediaRelayOptions ReadCallMedia(Func<string, string?> read)
    {
        int bindPort = ReadPort(read, "FERRITE_CALL_RELAY_BIND_PORT", 0);
        var options = new CallMediaRelayOptions
        {
            BindAddress = read("FERRITE_CALL_RELAY_BIND_ADDRESS") ?? "0.0.0.0",
            BindPort = bindPort,
            AdvertisedAddress = read("FERRITE_CALL_RELAY_ADVERTISED_ADDRESS") ?? "",
            AdvertisedPort = ReadPort(read,
                "FERRITE_CALL_RELAY_ADVERTISED_PORT", bindPort)
        };
        CallMediaRelayOptions resolved = options.AdvertisedAddress.Length == 0
            ? options with { AdvertisedAddress = "0.0.0.0" }
            : options;
        if (!resolved.TryValidate(out string error))
        {
            throw new ArgumentException(error);
        }
        return options;
    }

    private static int ReadPort(Func<string, string?> read, string name,
        int fallback)
    {
        string? text = read(name);
        if (string.IsNullOrEmpty(text))
        {
            return fallback;
        }
        if (!int.TryParse(text, out int value) || value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentException($"{name} must be a port number");
        }
        return value;
    }

    private static int ReadInt(string name, int fallback)
    {
        string? text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(text))
        {
            return fallback;
        }
        if (!int.TryParse(text, out int value) || value <= 0)
        {
            throw new ArgumentException($"{name} must be a positive integer");
        }
        return value;
    }

    private static long ReadLong(string name, long fallback)
    {
        string? text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(text))
        {
            return fallback;
        }
        if (!long.TryParse(text, out long value) || value <= 0)
        {
            throw new ArgumentException($"{name} must be a positive integer");
        }
        return value;
    }

    private static Guid? ReadGuid(string name)
    {
        string? text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (!Guid.TryParse(text, out Guid value) || value == Guid.Empty)
        {
            throw new ArgumentException($"{name} must be a non-empty GUID");
        }
        return value;
    }

}
