# Reproducing the official-client demo

The README media is captured from official Telegram applications connected to
a local Ferrite deployment. It is evidence of the interactions shown, not a
claim that every Telegram feature is implemented.

## Recorded inputs

| component | revision |
|---|---|
| Ferrite | `11c0f9ff6745cd9d447ecd28c00254ae4cd1b01f` |
| Telegram for Android | `ddc90f16be1ab952114005347e0102365ba6460b` |
| Telegram-iOS | `762f99c0df24556ea5d44382882fa70ab52b6e2d` |
| API layer | 214 |
| capture date | 2026-08-17 |

The Android client reports version 12.0.1 and the iOS client version 12.0.
Simulator and emulator cameras are unavailable, so call video uses a generated
test pattern. Every README panel is one individual client capture. The linked
MP4 files are continuous, uncut application recordings; none is a montage or a
concatenation. No benchmark should be inferred from playback speed or clip
duration.

| media | SHA-256 |
|---|---|
| `chat-media.png` | `067e609eda2de4044f9fba34db9d910c29fa1e54b312ee82ac01056208fece14` |
| `group-chat.png` | `5ffa25a72c6f714390e17fffcb5e33145c2ff428575c44dc400f111155bdf302` |
| `voice-call.png` | `c0b544b562f3523eb8311d967fde762a094fcbb69b980fc2dfd88f91c415140f` |
| `video-call.png` | `5497090fcdb418a5f45ac7e70e2d6eee3e38bb1f34b28e4087cb989436c65012` |
| `message.mp4` | `c026b84f94980c23bc193981b76d90f0aae4138d5f3b507114deec1fb5ee7496` |
| `voice-call.mp4` | `1de4e4a3a3a0500c990e59fad13ea1233872c43d6db122b93b3468d698b7260e` |
| `video-call.mp4` | `35dce2339b429e9f8cea1b4ea1742b4c3e26b07d7e656a653a8d8bd95d8d4f49` |

## Run it

Install the requirements in the root README and the platform-specific pinned
toolchains in `interop/upstream-clients/PIN`. On the supported macOS host:

```sh
FERRITE_ANDROID_JAVA_HOME=/path/to/jdk-17 \
FERRITE_IOS_BAZEL=/path/to/bazel-8.3.1 \
  ./scripts/ferrite-upstream-apps up --android 1 --ios 1
```

Startup succeeds only after both Ferrite nodes, all dependencies, and exactly
the requested client instances are ready. Each disposable client is authorized
with its assigned test number and imports its peer as a contact. Inspect the
owned resources with:

```sh
./scripts/ferrite-upstream-apps status
```

The applications should show their normal chat list. The conformance-only
loopback bridge can then drive application-owned messaging, group, media and
call APIs. Generated artifacts, the run manifest and logs are under
`.ferrite/upstream-apps/`.

Always tear down the owned run when finished:

```sh
./scripts/ferrite-upstream-apps down
```

This environment uses development credentials and loopback endpoints. Do not
expose it to another machine or use these inputs for a production deployment.
