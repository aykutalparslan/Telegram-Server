# Deployment

## Local development (.NET Aspire)

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

`FERRITE_APPHOST_RUN_ID` gives the graph's Ferrite data and group-call recording
volumes a normalized run-scoped suffix. The public upstream-app launcher sets it
automatically and retains the exact resulting containers and volumes in its
manifest, allowing `down` to remove only that disposable run.

For anything reachable from other machines, build the images below and run them
under your own orchestration, supplying your own addresses and credentials.

## Building the images

The server image is built from `deploy/Dockerfile.ferrite`, which is the same
image the development graph runs:

```sh
docker build -f deploy/Dockerfile.ferrite -t ferrite .
docker build -t ferrite-group-call-worker group-call-worker/
```

The server needs at minimum a public address, a data path, and — for group calls
— the worker's control URL and their shared secret:

```sh
docker run --rm -p 5222:5222 \
  -e FERRITE_PUBLIC_ADDRESS=203.0.113.10 \
  -e FERRITE_PORT=5222 \
  -e FERRITE_DATA_PATH=/data \
  -e FERRITE_GROUP_CALL_CONTROL_URL=http://group-call-worker:9090/ \
  -e FERRITE_GROUP_CALL_AUTH_SECRET=<shared-secret> \
  -v ferrite-data:/data ferrite
```

The worker needs the same secret, plus the address it advertises for media and
the UDP range it allocates from (`FERRITE_GROUP_CALL_MEDIA_ADVERTISED`,
`FERRITE_GROUP_CALL_RTC_MIN_PORT`, `FERRITE_GROUP_CALL_RTC_MAX_PORT`). Publish
that range and keep the control port private to the server.

Point the server at your storage and messaging backends with the `FERRITE_*`
variables in [configuration.md](configuration.md). Persist the data path on a
volume: it holds the server keys and all durable state, and losing it invalidates
every client authorization.

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
