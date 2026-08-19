# ADR-0057 — Client Workspace and Detached Media Presentation

- Status: Accepted and implemented on LTAEP; client validation accepted; three-computer synchronization deferred
- Date: 2026-08-19
- Starting baseline:
  `47199adc5ef93ce41a0b3ea706d6f00ac62a5ded`
- Increment 57A commit:
  `801f3fb834472d30331b937778fd6fe8f9dea8b1`
- Increment 57B implementation commit:
  `71924a773a60473eb21028967b4002920996b7eb`
- Focused LTAEP client validation: 268 passed, 0 failed, 0 skipped
- Three-computer closure: deferred until AEPRAKETE and LABC are available

## Context

The HASE WPF Client accumulated Runtime Host selection, endpoint inventory,
properties, commands, events, diagnostics, and live media in one vertically
stacked presentation. The behavior remained functional, but the presentation
no longer matched the operator workflow after multi-host support and
Runtime-Hosted video/audio were added.

The required Client organization is:

1. Runtime Hosts on the left;
2. endpoints of the selected Runtime Host in the middle;
3. properties and commands of the selected endpoint on the right; and
4. video/audio in a separate window opened from the Client header.

This change is a Client presentation and interaction decision. It does not
change Runtime Host ownership, gRPC contracts, southbound protocols, endpoint
identity, authorization, credentials, media-plane transport, or physical
device behavior.

Initial implementation and validation were intentionally performed on LTAEP
only because AEPRAKETE and LABC were not available. Synchronization of those
computers is therefore a later explicit closure action rather than an implied
part of this decision.

## Decision

### Three-column workspace

The main HASE Client window uses three primary columns.

The left column presents configured Runtime Hosts as operator-action tiles.
Clicking a disconnected tile selects and connects that Runtime Host. Clicking a
connected, connecting, or reconnecting tile selects and disconnects it.
Connection state and selection remain distinct concepts:

- a connected Runtime Host uses a green background; and
- the selected Runtime Host uses an independent emphasized border.

Multiple Runtime Hosts may therefore remain connected while one is selected for
the middle and right workspace.

The middle column presents only endpoints belonging to the selected Runtime
Host. Endpoint identity remains generation-scoped through the existing
attachment model.

The right column presents only the selected endpoint and reuses the existing
Property and Command interaction models. Runtime Host, endpoint, Property, and
Command semantics are not redefined by this ADR.

### Stable selection across projection refresh

Endpoint inventory projection replaces immutable presentation objects as
Runtime Host observations change. Logical endpoint selection therefore cannot
depend on retaining one ViewModel object reference.

The Client retains the selected endpoint by stable attachment key while the
same attachment remains present. A projection refresh resolves that key against
the new endpoint projection. Selection is cleared only when:

- the selected Runtime Host changes; or
- the selected attachment genuinely disappears or is replaced by another
  generation.

A transient WPF `SelectedItem = null` write-back caused by ItemsSource
replacement does not clear a still-valid logical endpoint selection.

### Requested Property edit retention

User-entered requested values are interactive Client state and must not be
silently replaced by periodic Runtime Host projection refreshes while the same
Runtime Host and attachment remain current.

The existing text-property retention model is extended to Boolean property
editors. In particular, a manually changed Arduino Uno LED requested-value
checkbox remains at the requested value until the operator writes it or the
relevant host/attachment context ceases to be current.

Confirmed endpoint values remain authoritative after an operation. The Client
does not treat a requested value as a successful physical change until the
existing write path confirms the result.

### Detached Video/Audio presentation

Live media presentation is removed from the main three-column workspace.

The Client header contains a `Video / Audio` command beside
`Open Diagnostics`. The command is available only when the selected Runtime
Host media model reports usable sources.

The command opens one separately owned WPF media window containing:

- Runtime Host camera refresh and source selection;
- explicit microphone-audio opt-in;
- Start Video and Stop controls;
- media session state and status; and
- the existing receiver-only WebView2 presentation surface.

The window reuses the existing Runtime Host media ViewModel,
`ClientMediaApplicationControlClient`, authenticated control path, WebRTC
presentation boundary, authorization semantics, and media compatibility rules.
It does not introduce a new media protocol or media session model.

Closing the media window does not disconnect a Runtime Host. Reopening the
window reuses the Client-owned media presentation lifecycle. Application exit
closes the media window and disposes the existing media control path.

### Deployment boundary

ADR-0057 does not change Client configuration custody or shortcut semantics.

On LTAEP the existing repository `Publish-HaseClient.ps1` application-update
path was used after validation. It replaced only installed application custody
while preserving:

- `Configuration`;
- `client-runtime-hosts.json`;
- the desktop shortcut target;
- shortcut arguments; and
- shortcut working directory.

A process-only PowerShell execution-policy bypass was used to invoke the
repository script; no machine- or user-scope execution-policy change was made.

AEPRAKETE and LABC were not modified during ADR-0057 implementation or
validation.

## Increment history

### Increment 57A — Three-column Client workspace

57A introduced:

- Runtime Host action tiles;
- independent connected and selected presentation;
- selected-host endpoint projection;
- selected-endpoint Property and Command workspace;
- removal of separate Connect/Disconnect buttons; and
- removal of the embedded media presentation from the visible main workspace.

A visual validation exposed endpoint selection disappearing after projection
refresh. Increment 57A3 corrected selection retention by stable attachment
identity.

Focused `Hase.Client.Wpf.Tests` validation passed 266 tests with zero failures
after the correction. The three-column UI was then physically inspected on
LTAEP and accepted.

Commit:
`801f3fb834472d30331b937778fd6fe8f9dea8b1`
(`Add three-column client workspace`).

### Increment 57B — Detached Video/Audio window

57B added the separate media-window controller, WPF media window, header
command, and Client application composition while retaining the existing media
control and presentation implementation.

Focused validation initially passed 267 tests. Installed LTAEP publication then
preserved configuration and shortcut custody and the shortcut-launched Client
showed the accepted new presentation.

Manual Arduino Uno validation exposed a separate interactive-state defect:
changing a Boolean `Requested value` checkbox was overwritten by the next
Runtime Host snapshot before Write could be pressed.

Increment 57B1 retained Boolean requested values across same-host,
same-attachment snapshot refresh. Increment 57B2 corrected only the new
regression-test fixture so it used an actual read/write Boolean property.

Final focused Client validation passed:

```text
268 total
268 passed
0 failed
0 skipped
```

The installed shortcut Client was republished and manually validated. The
three-column layout, detached Video/Audio window, endpoint selection,
Boolean requested-value retention, and endpoint-confirmed Arduino Uno LED write
all behaved as intended.

Commit:
`71924a773a60473eb21028967b4002920996b7eb`
(`Add detached client media window`).

## Complete-suite validation note

ADR-0057 does not claim a new all-project green test baseline yet.

During 57A, one complete Release run reached 6,367 discovered tests with 6,364
passing and three failures outside the four-file 57A change scope. The
Northbound concurrency failure passed on isolated rerun. The remaining two
DesktopHost failures were reproduced and classified as line-ending-sensitive
tests: the unchanged Windows working copy contained CRLF while the tests
searched for an LF-only installer delimiter.

The authoritative pre-ADR-0057 fully green complete-suite baseline therefore
remains ADR-0056's 6,362 tests until a later closure validation establishes a
new complete green count.

No DesktopHost test correction is included in ADR-0057.

## Validation status

Accepted LTAEP evidence:

- repository baseline exact and clean before each controlled source
  application;
- 57A focused Client suite: 266 passed;
- 57B final focused Client suite: 268 passed;
- `git diff --check` passed at each accepted source boundary;
- three-column Client layout visually accepted;
- endpoint selection stable across refresh;
- detached Video/Audio presentation visually accepted;
- installed shortcut Client validated after application-only publication;
- Client configuration and shortcut custody preserved;
- Boolean Arduino Uno requested-value edit retained across periodic refresh;
- endpoint-confirmed LED write succeeded; and
- no AEPRAKETE or LABC modification was performed.

## Current synchronization state

At the 57B implementation commit:

- LTAEP is clean and synchronized with `origin/main`;
- the installed LTAEP Client contains the accepted ADR-0057 implementation;
- AEPRAKETE and LABC have not yet been synchronized to ADR-0057;
- no Runtime Host application update is required by the Client-only source
  changes themselves; and
- final synchronization and any multi-computer regression proof remain
  explicitly deferred until those machines are available.

## Definition of done

ADR-0057 may be closed only after a separately approved finalization step:

1. documentation is accepted and committed;
2. a new complete Release regression status is recorded without hiding any
   unrelated baseline defect;
3. AEPRAKETE and LABC repositories are synchronized to the accepted commit when
   available;
4. LTAEP remains clean and synchronized;
5. any required installed Client consistency check is completed; and
6. Project Status, Roadmap, and this ADR agree on the final closure state.

Until then, ADR-0057 is implemented and accepted on LTAEP but not three-computer
closed.

## Deferred scope

This ADR does not add recording, snapshots, PTZ, talkback, Client-originated
capture, dynamic microphone discovery, multiple media viewers, multiple
simultaneous sessions, STUN/TURN, public relay, cloud media processing,
non-Windows capture, new Runtime Host media contracts, or remote device
management.

Diagnostic Export and Offline Analysis remains accepted but deferred.
