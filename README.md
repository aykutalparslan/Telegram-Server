<p align="center">
  <img src="logo.jpeg" width="160" alt="Ferrite">
</p>

# Ferrite

Ferrite is an implementation of the Telegram server API in C#/.NET. It speaks
MTProto over TCP and WebSocket and implements the Telegram client API at
**layer 214**, including messaging, channels and supergroups, media and file
transfer, secret chats, and voice, video and group calls.

Ferrite is independent software. It is not affiliated with, endorsed by, or
connected to Telegram Messenger Inc.

## Requirements

- .NET SDK **10.0.100** or later (`global.json` pins the feature band)
- Node.js 20+ and Docker, if you need group calls — the media plane is a
  [mediasoup](https://mediasoup.org) worker in `group-call-worker/`

## Build and run

```sh
dotnet build Ferrite.sln
dotnet run --project Ferrite
```

The server listens on port **5222** and advertises **10.0.2.2** by default, which
suits an Android emulator talking to its host. Point a client at your own address
with:

```sh
FERRITE_PUBLIC_ADDRESS=192.0.2.10 FERRITE_PORT=5222 dotnet run --project Ferrite
```

State is written under `data/`. The repository ships **sample** server keys so a
fresh clone runs immediately; delete `default-private.key` and
`default-public.key` before any real deployment so Ferrite generates its own.
Clients need the server's public key and address — see
[docs/installation.md](docs/installation.md).

This runs a single process against local file-backed storage, with no external
dependencies. To bring up the distributed topology instead — Cassandra, Redis,
Kafka, object storage, search, TURN, and the group-call worker — use the .NET
Aspire host:

```sh
dotnet run --project Ferrite.AppHost
```

That graph is a local development environment: it publishes fixed ports, uses
development credentials, and advertises loopback, so it serves clients on the
same machine. See [docs/deployment.md](docs/deployment.md).

## Deployment

`deploy/` holds a Compose definition that builds and runs the server together
with the group-call worker:

```sh
docker compose -f deploy/docker-compose.yml up --build
```

Relayed calls additionally need a TURN server; `deploy/coturn/` contains a
working configuration. See [docs/deployment.md](docs/deployment.md).

## Configuration

Everything is configured through `FERRITE_*` environment variables. Ferrite runs
out of the box on local file-backed storage and needs no external services. It can
also be pointed at Cassandra, Redis, Kafka, S3-compatible object storage, and
Elasticsearch. See [docs/configuration.md](docs/configuration.md).

## Security

Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md).
Please do not open a public issue for a security problem.

## License

Copyright (C) 2022-2026 Aykut Alparslan KOÇ

Ferrite is free software: you may redistribute it and/or modify it under the terms
of the **GNU Affero General Public License, version 3 or later**. It is distributed
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
FITNESS FOR A PARTICULAR PURPOSE. See [LICENSE](LICENSE) for the full text.

`Ferrite.Transport` contains files derived from ASP.NET Core, used under the MIT
license; see [Ferrite.Transport/LICENSE.aspnetcore](Ferrite.Transport/LICENSE.aspnetcore).
