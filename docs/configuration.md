# Configuration

Ferrite is configured entirely through environment variables. Every setting has a
working default; an unset variable is never an error except where noted.

## Server

| Variable | Default | Meaning |
| --- | --- | --- |
| `FERRITE_PUBLIC_ADDRESS` | `10.0.2.2` | address advertised to clients in the datacenter list |
| `FERRITE_PORT` | `5222` | TCP port for MTProto and WebSocket |
| `FERRITE_DATA_PATH` | `data` | root directory for local state |
| `FERRITE_NODE_ID` | `node.guid` | stable node GUID; set it explicitly when running more than one node |

`FERRITE_PUBLIC_ADDRESS` must be an address the client can reach. It is what
Ferrite puts in its datacenter list, not what it binds to, so a wrong value fails
only after the client reconnects to the advertised address.

Unset, `FERRITE_NODE_ID` falls back to `node.guid` in the working directory and
generates one there when it is absent. The repository ships a sample `node.guid`
that the build copies into the output directory and the server image, so every
node started from that image shares an id until this variable is set.

## Storage

`FERRITE_STORAGE_PROFILE` selects the overall backend set and defaults to `Local`,
which keeps everything on disk under `FERRITE_DATA_PATH` and requires no external
service. The per-concern variables below override individual pieces of the
profile.

| Variable | Purpose |
| --- | --- |
| `FERRITE_STORAGE_KEY_VALUE` | durable rows |
| `FERRITE_STORAGE_EPHEMERAL` | short-lived state such as sessions |
| `FERRITE_STORAGE_PIPE` | inter-node messaging |
| `FERRITE_STORAGE_OBJECT_STORE` | uploaded files and media |
| `FERRITE_STORAGE_SEARCH` | message search index |
| `FERRITE_STORAGE_COUNTERS` | monotonic counters |
| `FERRITE_STORAGE_UPDATES_CONTEXT` | update-box state |

### External backends

These apply only when the corresponding backend is selected.

| Variable | Default |
| --- | --- |
| `FERRITE_CASSANDRA_HOSTS` | `127.0.0.1` (comma-separated) |
| `FERRITE_CASSANDRA_PORT` | `9042` |
| `FERRITE_CASSANDRA_KEYSPACE` | `ferrite` |
| `FERRITE_REDIS_CONFIGURATION` | `127.0.0.1:6379` |
| `FERRITE_KAFKA_CONFIGURATION` | `127.0.0.1:9092` |
| `FERRITE_S3_SERVICE_URL` | `http://127.0.0.1:9000` |
| `FERRITE_S3_ACCESS_KEY` | `minioadmin` |
| `FERRITE_S3_SECRET_KEY` | `minioadmin` |
| `FERRITE_ELASTICSEARCH_URL` | — |
| `FERRITE_ELASTICSEARCH_USERNAME` / `_PASSWORD` / `_FINGERPRINT` | — |

Change the default S3 credentials before exposing an instance.

## Calls

### One-to-one calls

| Variable | Default | Meaning |
| --- | --- | --- |
| `FERRITE_CALL_RELAY_BIND_ADDRESS` / `_PORT` | — | address the built-in call reflector binds |
| `FERRITE_CALL_RELAY_ADVERTISED_ADDRESS` / `_PORT` | — | address given to clients |

The advertised address must be the one clients dial. A client discards relay
datagrams whose source address differs from the address it was told to use, so a
mismatch produces a silent media failure rather than an error.

### TURN

| Variable | Default |
| --- | --- |
| `FERRITE_TURN_ENABLED` | off |
| `FERRITE_TURN_SECRET` | — |
| `FERRITE_TURN_REALM`, `FERRITE_TURN_PORT` | — |
| `FERRITE_TURN_ADVERTISED_IPV4` / `_IPV6` | — |

`FERRITE_TURN_SECRET` must match the static-auth secret in the TURN server's
configuration.

### Group calls

| Variable | Default | Meaning |
| --- | --- | --- |
| `FERRITE_GROUP_CALL_CONTROL_URL` | — | mediasoup worker control endpoint |
| `FERRITE_GROUP_CALL_AUTH_SECRET` | — | shared secret for that endpoint |
| `FERRITE_GROUP_CALL_MEDIA_BIND` | — | address the worker binds for RTC |
| `FERRITE_GROUP_CALL_MEDIA_ADVERTISED` | — | address announced in ICE candidates |
| `FERRITE_GROUP_CALL_RTC_MIN_PORT` / `_MAX_PORT` | — | RTC port range |
| `FERRITE_GROUP_CALL_UNMUTED_VIDEO_LIMIT` | `30` | concurrent unmuted video senders |
| `FERRITE_GROUP_CALL_REQUEST_TIMEOUT_SECONDS` | `5` | worker request timeout |
| `FERRITE_GROUP_CALL_HEALTH_INTERVAL_SECONDS` | `5` | worker health poll interval |
| `FERRITE_GROUP_CALL_HEALTH_TIMEOUT_SECONDS` | `2` | worker health timeout |

`FERRITE_GROUP_CALL_CONTROL_URL` and `FERRITE_GROUP_CALL_AUTH_SECRET` must be set
together or startup fails. Binding `0.0.0.0` while announcing a specific address
leaves ICE unable to complete, so keep the bind and advertised addresses
consistent.

Recording, live-stream segments, and RTMP ingest have their own
`FERRITE_GROUP_CALL_RECORDING_*`, `FERRITE_GROUP_CALL_SEGMENT_*`, and
`FERRITE_GROUP_CALL_RTMP_*` variables, plus per-room and per-recording resource
caps under `FERRITE_GROUP_CALL_MAX_*`. The worker reads them; see
`group-call-worker/src/server.mjs`.
