# Installation

## Prerequisites

- .NET SDK **10.0.100** or later. `global.json` pins the feature band, so a newer
  10.0.x SDK is accepted and an older major is not.
- Node.js 20+ and Docker, only if you need voice/video group calls.

## Build

```sh
git clone https://github.com/aykutalparslan/Ferrite
cd Ferrite
dotnet build Ferrite.sln
```

## Run

```sh
dotnet run --project Ferrite
```

Ferrite listens on TCP port 5222 and serves MTProto over both raw TCP and
WebSocket on that port.

On first start Ferrite assigns itself a node id and creates the `data/` directory.

**Replace the sample server keys before running anything real.** This repository
ships `default-private.key` and `default-public.key` as samples so a fresh clone
runs immediately. They are public, so anyone can impersonate a server that still
uses them. Ferrite loads whatever key files it finds and only generates a new pair
when they are absent, so delete both from the working directory and restart:

```sh
rm default-private.key default-public.key
dotnet run --project Ferrite
```

Then keep the generated `default-private.key` secret and back it up. Clients pin
the matching public key, so replacing it later invalidates every existing
authorization.

## Connecting a client

A client needs two things:

1. **The address and port** Ferrite advertises. This is `FERRITE_PUBLIC_ADDRESS`,
   which defaults to `10.0.2.2` — the host address as seen from an Android
   emulator. Set it to an address your client can actually reach.
2. **The server's RSA public key**, from `default-public.key`.

Official Telegram clients compile in the production keys and datacenter list, so
they must be rebuilt against your key and address to talk to a Ferrite instance.

## Storage

By default Ferrite stores everything under `data/` in the working directory, with
no external dependencies. Override the location with `FERRITE_DATA_PATH`.

To run against Cassandra, Redis, Kafka, S3-compatible object storage, or
Elasticsearch instead, see [configuration.md](configuration.md).

## Group calls

Voice and video calls between two users work with the server alone. **Group**
calls additionally require the mediasoup worker in `group-call-worker/`:

```sh
cd group-call-worker
npm ci
npm start
```

Then start Ferrite with the worker's control endpoint and shared secret:

```sh
FERRITE_GROUP_CALL_CONTROL_URL=http://127.0.0.1:3000 \
FERRITE_GROUP_CALL_AUTH_SECRET=<shared-secret> \
dotnet run --project Ferrite
```

Both variables must be set together; setting only one is a startup error. Without
them Ferrite runs normally and reports group calls as unavailable.

Relayed calls also need a TURN server — see [deployment.md](deployment.md).
