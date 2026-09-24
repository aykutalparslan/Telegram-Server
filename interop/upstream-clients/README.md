# Official upstream application conformance inputs

`PIN` fixes the Android and iOS repositories, commits, layer, source roots, and
supported toolchains used by Ferrite's full-client conformance environment.
`LAYER` is the layer the pinned applications announce, which is Ferrite's base
layer.
Upstream sources and build products belong under the ignored
`.ferrite/upstream-apps/work/` directory; they are never vendored into Ferrite
or modified in place.

The public entry point is:

```sh
./scripts/ferrite-upstream-apps up --android 0 --ios 0
./scripts/ferrite-upstream-apps status
./scripts/ferrite-upstream-apps down
```

The zero/zero form starts the production-shaped two-node AppHost deployment and
records every owned process, container, volume, endpoint, log, source commit,
and requested client count in a run manifest under `.ferrite/upstream-apps/`.
`status` and `down` act only on resources in that manifest.

Positive Android and iOS counts use the reviewed public provisioning patch,
build helper, launch helper, and loopback token-authenticated provisioning
endpoint recorded in `PIN`. The public patch can report provisioning state and
accept the one-time verification code; it contains no messaging, group, call,
account, media, network-fault, or scenario-observation commands. Those test-only
controls live in a separate private Harness patch series.

The launcher treats a missing or mismatched patch or application digest as a
preflight error rather than silently starting fewer applications, substituting
a lower-level probe, or accepting an unpinned binary. Builds write `Telegram.apk`
and `Telegram.ipa` below `.ferrite/upstream-apps/artifacts/`. `up` reuses a
staged APK only while its digest matches `ANDROID_APK_SHA256`. The IPA embeds
its workspace path, so no IPA digest is pinned; `up` reuses a staged IPA only
while its version matches `IOS_CANDIDATE_VERSION`, and the iOS launch helper
refuses any other version. Anything else is rebuilt.

When `PIN` moves to a new commit, a work checkout under
`.ferrite/upstream-apps/work/` that still carries the previous patch is refused
rather than discarded. Move it aside and
the next build clones the pinned commit again.

The promoted iOS input can be inspected without Xcode, Bazel, network
access, or a Simulator:

```sh
./interop/upstream-clients/scripts/build-ios --metadata-only
./interop/upstream-clients/scripts/launch-ios metadata
```

Both commands verify the patch against `PIN`; neither launches a client. A real
build needs the Bazel release pinned in `IOS_CANDIDATE_BAZEL`, which it downloads itself: `provision-bazel`
fetches the release asset named in `PIN`, checks it against the pinned SHA-256,
and caches it under `.ferrite/upstream-apps/toolchains/`. The helper rejects a
different Bazel, Xcode, patch, source commit, layer, configuration, or output
bundle and writes the successful Simulator IPA under
`.ferrite/upstream-apps/artifacts/ios/`.

The pinned Android build requires the Android platform, build-tools and NDK
revisions named by `ANDROID_COMPILE_SDK`, `ANDROID_BUILD_TOOLS` and
`ANDROID_NDK`, command-line/platform/emulator tools, and a system image and AVD
for `ANDROID_EMULATOR_API`. The build initializes the upstream native
submodules itself. It does not require a JDK on the host: `provision-jdk`
downloads the Temurin build named by `ANDROID_JDK_VERSION`, which the APK
digest was produced by, verifies it
against `PIN`, and caches it under `.ferrite/upstream-apps/toolchains/`. A host
JDK is not substituted for it, because a host JDK is not the build the digests
came from. On macOS the launcher discovers the normal `~/Library/Android/sdk`
location even when `ANDROID_SDK_ROOT` and `ANDROID_HOME` are unset. Newer SDK or NDK packages do not replace the pinned
side-by-side revisions. The current resource floor is 4 logical CPUs, 8 GiB
RAM, and 20 GiB free disk for server-only, plus 1 CPU, 2 GiB RAM, and 5 GiB
disk for each requested application after the first two CPUs.

The pinned iOS build requires macOS, Xcode 26.3, and at least one available
iPhone Simulator device. The Xcode Metal Toolchain component must be installed.
Bazel is provisioned rather than installed, and a newer Bazel is never
substituted for the pinned one. The pinned Bazel digest covers Darwin/arm64; on
any other host `provision-bazel` stops and asks for `FERRITE_IOS_BAZEL`.
Simulator builds use the committed Bzlmod lockfile, disable provisioning
profiles and extensions, and never require a private signing repository. All
positive counts also require the reviewed public provisioning patch/build/launch
boundary described above.

Examples (the launcher rejects an incomplete toolchain before creating any
resource):

```sh
# Android only.
./scripts/ferrite-upstream-apps up --android 2 --ios 0

# iOS only.
./scripts/ferrite-upstream-apps up --android 0 --ios 2

# Mixed runs on the supported macOS host.
./scripts/ferrite-upstream-apps up --android 1 --ios 1
./scripts/ferrite-upstream-apps up --android 1 --ios 2
./scripts/ferrite-upstream-apps up --android 2 --ios 2
```

Every application in a mixed run authorizes on its own and imports every other
application in the same run as a contact, across platforms. `up` fails rather
than returning a run in which any requested application is unauthorized or
missing a peer.

`FERRITE_ANDROID_JAVA_HOME` and `FERRITE_IOS_BAZEL` still override the
provisioned toolchains when a host has to supply its own. Do not set them merely
to make `up` succeed: they mask a public command that cannot provision itself.

The iOS launch helper records each created Simulator immediately under
`.ferrite/upstream-apps/ios/session.json`. `ferrite-upstream-apps down` asks it
to remove only devices whose recorded id, UDID, generated name, and bridge token
match the owning run manifest. If a partial launch needs inspection, use
`./interop/upstream-clients/scripts/launch-ios status`; cleanup refuses a token
belonging to another run.

`status` prints the manifest-owned server and client state. Logs and generated
artifacts live below `.ferrite/upstream-apps/`; inspect them before cleanup when
a launch fails. Re-running `down` is safe: it asks each platform helper to stop
only the recorded run token, then removes only the server processes, containers
and volumes named by the manifest. If startup fails partway through, run
`./scripts/ferrite-upstream-apps down` before retrying. A foreign or stale
client session is reported and is never adopted.

The upstream source, patch and license boundaries are recorded in
[UPSTREAM-NOTICES.md](UPSTREAM-NOTICES.md).

This launcher is for disposable local conformance data. It uses the development
keys and loopback endpoints in this repository and must not be made reachable
from another machine.
