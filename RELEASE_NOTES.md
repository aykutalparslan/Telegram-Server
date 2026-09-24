# Release notes

## Unreleased — multi-layer interoperability

- Uses layer 229 as the base and accepts exactly layers 150, 214–225, and
  227–229, with per-recipient conversion for RPC results and pushed updates.
- Upgrades requests through consecutive published layers before constructor-only
  dispatch. Upgrade and downgrade converters share a conversion context and live
  together by semantic concern.
- Derives layer negotiation and conversion routes from the embedded schema catalog,
  with one base-layer setting and deterministic generation of historical TL inputs.
- Documents a reproducible two-node local graph with Cassandra, Redis, Kafka,
  MinIO, Elasticsearch, TURN, and the group-call worker.
- Publishes pinned, digest-checked official Android and iOS conformance inputs
  plus manifest-owned build, launch, status, recovery, and cleanup helpers.
- Expands layer-214 compatibility across messaging, groups and channels, media,
  contacts, scheduled messages, and private voice/video call signaling.
- Keeps intentionally disabled namespaces explicit: bots, payments, stories,
  premium, smsjobs, and fragment return `METHOD_DISABLED`.
- Adds exact official-client capture provenance and upstream license notices.

These notes describe the current development branch and are not a tagged
release. Exact supported-operation counts are generated from the schema and are
reported in the root README. [API layer structure](docs/api-layers.md) describes
the schema and converter changes needed to add a layer.
