# Upstream application notices

Ferrite's conformance environment downloads upstream repositories at the exact
commits in `PIN`, then applies reviewable patches in disposable or ignored
working directories. Upstream source and application binaries are not vendored
into this repository.

The patches published here are deliberately limited to unattended local
provisioning. Ferrite's private Harness keeps its broader scenario-control
patches outside the public repository.

## Telegram for Android

- Repository: <https://github.com/DrKLO/Telegram>
- Pinned revision: `62b56a07ca7e30e39f7fd00a6728d6bbd716ca1c`
- Upstream license: GNU General Public License, version 2

The public Android provisioning patch remains a modification of Telegram for
Android and retains the upstream copyright and GPL notices. Anyone distributing a patched binary
must satisfy the upstream license, including its corresponding-source duties.

## Telegram-iOS

- Repository: <https://github.com/TelegramMessenger/Telegram-iOS>
- Pinned revision: `ab836dabd2816682bf4c7ef7dec5539a8ac04ebb`
- Upstream notices: the repository's `LICENSE`, `README.md`, and third-party
  dependency notices at that revision

The public iOS provisioning patch does not replace or narrow those notices. A distributor must
review the upstream repository and every bundled dependency's terms for the
artifact it produces.

## Ferrite boundary

Ferrite is independent software licensed under AGPL-3.0-or-later. Applying a
client patch does not relicense the client, and Ferrite's license does not
replace either client's upstream terms. The client names and marks belong to
their respective owners; Ferrite is not affiliated with or endorsed by
Telegram Messenger Inc.
