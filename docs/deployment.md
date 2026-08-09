# Deployment

## Local development graph (.NET Aspire)

`Ferrite.AppHost` brings up the whole distributed topology in one command:

```sh
dotnet run --project Ferrite.AppHost
```

It provisions Cassandra, Redis, Kafka, MinIO, Elasticsearch, coturn, and the
group-call worker as containers, then starts **two** Ferrite nodes against them
with `FERRITE_STORAGE_PROFILE=Distributed`.

This is a development environment, not a deployment target. It publishes fixed
host ports, ships development credentials, and advertises `127.0.0.1`, so it only
serves clients on the same machine. Set `FERRITE_APPHOST_SERVICES_ONLY=1` to start
the backing services without the Ferrite nodes, which is useful when you want to
run the server from your editor against the same dependencies.

For anything reachable from other machines, use Compose below or your own
orchestration, and supply your own addresses and credentials.

## Compose

`deploy/docker-compose.yml` builds the server image from `deploy/Dockerfile.ferrite`
and runs it alongside the group-call worker built from `group-call-worker/`.

```sh
FERRITE_PUBLIC_ADDRESS=203.0.113.10 \
FERRITE_GROUP_CALL_MEDIA_ADVERTISED=203.0.113.10 \
FERRITE_GROUP_CALL_AUTH_SECRET=<shared-secret> \
docker compose -f deploy/docker-compose.yml up --build
```

Validate the definition without starting anything:

```sh
docker compose -f deploy/docker-compose.yml config --quiet
```

Persist `data/` on a volume. It holds the server keys and all durable state;
losing it invalidates every client authorization.

## TURN

`deploy/coturn/` runs a stock coturn image with a working configuration.

```sh
docker compose -f deploy/coturn/docker-compose.yml up
```

The static-auth secret in `deploy/coturn/turnserver.dev.conf` must match
`FERRITE_TURN_SECRET`. Behind NAT, set `external-ip` in that file or relayed
candidates will advertise an unreachable address.

## Networking

Media is UDP and is unforgiving about addresses:

- Publish UDP ports explicitly with the `/udp` suffix, and bind each published
  port to the address clients are told to dial rather than `0.0.0.0`. A client
  discards datagrams arriving from an unexpected source address, which looks like
  silence rather than an error.
- Clients must dial an IP address; `localhost` resolution is not reliable across
  container boundaries.
- Docker Desktop for macOS drops inbound UDP datagrams above roughly 1472 bytes on
  a published port. ICE completes on small packets while the DTLS handshake times
  out. Run the media worker on the host for local media testing on macOS; Linux
  hosts are unaffected.

Open TCP `FERRITE_PORT` for clients, the TURN port, and the configured RTC and
RTMP UDP ranges.

## Multiple nodes

Give every node a distinct `FERRITE_NODE_ID` and point them at shared backends:
Cassandra for durable rows, Redis for ephemeral state, Kafka for the inter-node
pipe, and S3-compatible storage for media. See
[configuration.md](configuration.md). Nodes coordinate only through those
backends, so all of them must be reachable from every node.
