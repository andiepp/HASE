# ADR-0057 — Client Workspace and Detached Media Presentation

- Status: Accepted, implemented, validated, synchronized, and closed
- Date: 2026-08-19
- Starting baseline: `47199adc5ef93ce41a0b3ea706d6f00ac62a5ded`
- Increment 57A commit: `801f3fb834472d30331b937778fd6fe8f9dea8b1`
- Increment 57B implementation commit: `71924a773a60473eb21028967b4002920996b7eb`
- Increment 57C documentation/synchronization checkpoint:
  `496b3a316c9ab92fb80f37235efa800c0675893b`
- Focused LTAEP Client validation: 268 passed, 0 failed, 0 skipped
- Closure complete Release validation: 6,369 passed, 0 failed, 0 skipped
- Three-computer synchronization: AEPRAKETE, LABC, and LTAEP synchronized

## Decision

The HASE Client main window uses three columns: Runtime Hosts on the left,
endpoints of the selected Runtime Host in the middle, and Properties/Commands
of the selected endpoint on the right. Runtime Host tiles perform
connect/disconnect, use green for connected state, and retain an independent
selected-host indication.

Endpoint selection is retained by stable attachment identity across immutable
projection refreshes. User-entered requested Property values are interactive
Client state: Boolean requested values, including the Arduino Uno LED checkbox,
are retained across same-host, same-attachment refreshes until Write or context
loss. Confirmed endpoint values remain authoritative.

Live media is removed from the main workspace. `Video / Audio` opens a separate
WPF window containing camera refresh/selection, explicit optional audio,
Start/Stop, media state/status, and the existing receiver-only WebView2
presentation surface. The existing authenticated media control path, WebRTC
boundary, authorization semantics, and one-session ownership remain unchanged.

ADR-0057 changes Client presentation and interaction state only. It does not
change Runtime Host ownership, gRPC contracts, southbound protocols, endpoint
identity, credentials, authorization, or media-plane transport.

## Increment history

### 57A — Three-column workspace

57A implemented the three-column layout and Runtime Host action tiles. Physical
LTAEP validation exposed endpoint selection disappearing after projection
refresh. 57A3 retained selection by attachment identity. Focused Client
validation passed 266 tests. The accepted implementation is commit
`801f3fb834472d30331b937778fd6fe8f9dea8b1`.

### 57B — Detached Video/Audio window

57B moved media controls and presentation into a separate window while reusing
the existing media implementation. Physical LTAEP validation then exposed
Boolean requested Property values being overwritten by periodic refresh.
57B1 retained those values; 57B2 corrected only the regression-test fixture.

Final focused Client validation passed 268/268. The installed shortcut Client
was republished with configuration and shortcut custody preserved. The
three-column layout, detached media window, endpoint selection, requested-value
retention, and endpoint-confirmed Arduino Uno LED write were accepted. The
implementation is commit `71924a773a60473eb21028967b4002920996b7eb`.

### 57C — Documentation and synchronization checkpoint

57C documented the accepted LTAEP state at
`496b3a316c9ab92fb80f37235efa800c0675893b`. AEPRAKETE and LABC later
fast-forwarded from `47199adc5ef93ce41a0b3ea706d6f00ac62a5ded` to that
checkpoint. LTAEP was already there. All three repositories were clean and
matched `origin/main`; synchronization performed no deployment or physical
mutation.

### 57D — Closure documentation

Before 57D, the complete Release suite was run on AEPRAKETE against exact clean
checkpoint `496b3a316c9ab92fb80f37235efa800c0675893b` using `HASE.slnx`.

```text
6,369 total
6,369 passed
0 failed
0 skipped
duration: 22.1 seconds
build successful: 24.4 seconds
```

The exit code was zero. `HEAD` and `origin/main` remained exact and the
repository remained clean. No deployment or physical mutation occurred.

Earlier LTAEP-only complete validation had exposed one transient Northbound
failure and two CRLF-sensitive DesktopHost failures. The Northbound test passed
on isolated rerun, and the DesktopHost failures did not reproduce in the
authoritative AEPRAKETE closure regression. No unrelated test change is part of
ADR-0057.

## Closure

ADR-0057 is accepted, implemented, validated, synchronized, and closed at the
6,369-test complete Release baseline.

The three-column workspace and detached Video/Audio window are the authoritative
HASE Client presentation model.

## Deferred scope

Recording, snapshots, PTZ, talkback, Client-originated capture, dynamic
microphone discovery, multiple media viewers or simultaneous sessions,
STUN/TURN, public relay, cloud media processing, non-Windows capture, new
Runtime Host media contracts, and remote device management remain outside this
ADR.

Diagnostic Export and Offline Analysis remains accepted but deferred.
