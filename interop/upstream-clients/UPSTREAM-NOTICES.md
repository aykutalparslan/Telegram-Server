# Upstream application notices

Ferrite's conformance environment downloads upstream repositories at the exact
commits in `PIN`, then applies reviewable patches in disposable or ignored
working directories. Upstream source and application binaries are not vendored
into this repository.

The patches published here are deliberately limited to unattended local
provisioning. Ferrite's private Harness keeps its broader scenario-control
patches outside the public repository. The digest of each private continuation
is recorded in `PIN` only so a Harness run can attest to the complete ordered
series; recording a digest does not distribute or relicense that patch.

## Telegram for Android

- Repository: <https://github.com/DrKLO/Telegram>
- Pinned revision: `ddc90f16be1ab952114005347e0102365ba6460b`
- Upstream license: GNU General Public License, version 2

The public Android provisioning patch remains a modification of Telegram for
Android and retains the upstream copyright and GPL notices. Anyone distributing a patched binary
must satisfy the upstream license, including its corresponding-source duties.

## Telegram-iOS

- Repository: <https://github.com/TelegramMessenger/Telegram-iOS>
- Pinned revision: `762f99c0df24556ea5d44382882fa70ab52b6e2d`
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
