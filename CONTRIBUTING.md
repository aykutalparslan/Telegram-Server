# Contributing to Ferrite

Ferrite welcomes focused fixes, protocol compatibility work, tests, and
documentation improvements.

## Development setup

Install the .NET SDK selected by `global.json`, Docker, Python 3, and Node.js
20 or later. Then build and run the non-destructive server-only graph:

```sh
dotnet build Ferrite.sln
./scripts/ferrite-upstream-apps up --android 0 --ios 0
./scripts/ferrite-upstream-apps status
./scripts/ferrite-upstream-apps down
```

`Ferrite.sln` carries the server projects only; the conformance suite that
exercises them lives outside this repository. Verify a change against a runnable
graph and say in the pull request exactly what you ran and what it reported.

Official-client work additionally requires the exact public inputs and
toolchains documented in `interop/upstream-clients/README.md`. Do not commit
upstream checkouts, generated build products, private keys, or run data.

## Changes

Keep a change scoped and add a regression test that proves the externally
visible behavior. For protocol changes, preserve the declared layer and verify
constructor ids, flags, actor perspective, error behavior, state transition,
and peer-visible updates. Avoid assertions that merely prove a request was
accepted when the test name claims stored or delivered state.

Before opening a pull request, build the affected projects and exercise the
changed behavior against a running graph. Explain anything that cannot be run in
the pull request environment. Never put secrets, production credentials, or
Telegram user data in an issue, test, log, or fixture.

By contributing, you agree that your contribution is provided under this
repository's AGPL-3.0-or-later license. Retain applicable third-party notices.
Security reports follow `SECURITY.md`, not the public issue tracker.
