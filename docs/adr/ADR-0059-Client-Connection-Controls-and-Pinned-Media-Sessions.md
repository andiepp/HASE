# ADR-0059 — Client Connection Controls and Pinned Media Sessions

- Status: Accepted retrospectively, implemented, validated, synchronized,
  deployed, and closed
- Date: 2026-08-22
- Starting baseline: `5205972bcc307b6a5c4d36ab95121bdccf5676c4`
- Starting subject: `CLAUDE.md added`
- Starting complete Release baseline: 6,379 passed, 0 failed, 0 skipped
- Closing complete Release baseline: 6,391 passed, 0 failed, 0 skipped

This ADR records a completed sequence of laptop-client interaction and
reliability corrections as one architectural objective. Each increment was
proposed, implemented, validated, committed, pushed, synchronized, deployed to
the LTAEP laptop client, and physically verified before the next began. The
record is retrospective; the individual increments were approved individually
during implementation.

## Context

Operator experience with the LTAEP laptop client exposed five defects or
interaction problems that the ADR-0057 client workspace and ADR-0058 endpoint
refresh work did not cover:

1. Clicking a runtime-host list entry toggled that host's connection. Selecting
   a host to inspect it therefore connected or disconnected it as a side
   effect.
2. Closing and reopening the detached media window broke `Start Video`
   permanently. The window was destroyed on close while its WebView2
   presentation boundary retained initialization state describing a browser
   host that no longer existed.
3. Closing the client main window while a media session was streaming left the
   client process alive. Shutdown disposal blocked the UI thread while the
   presentation boundary's disposal waited for that same thread.
4. A runtime host that faulted after a successful connection could never be
   reconnected. The profile session controller released its dead session only
   through an explicit disconnect, so a later connect saw a session it
   considered already active and failed until application restart.
5. Selecting another runtime host stopped a running media stream, because the
   media control binding followed the inventory selection unconditionally.

## Decision

### Explicit per-entry connection control

The runtime-host list is a selection list. Clicking an entry changes only the
selection. Each entry carries a `Connect`/`Disconnect` button in its top-right
corner bound to the existing toggle command; the label always names the action
the button performs. Connection state never changes as a side effect of
selection.

### Retained media window

The detached media window is created once and retained for the client
lifetime. An operator close hides the window after stopping any active media
session; reopening shows the same window, so the WebView2 presentation surface
is never re-parented and its boundary state never outlives the browser host.
Application shutdown performs the real close and disposes the WebView2
explicitly.

### Deadlock-free media shutdown

Client shutdown disposes the presentation boundary before stopping the remote
media session. The boundary disposal therefore runs on the UI thread that
`OnExit` blocks, and the remote stop's thread-pool continuation no longer needs
to marshal back to a blocked dispatcher.

### Faulted profile session recovery

`RuntimeHostProfileSessionController.ConnectAsync` releases a retained
faulted session — unsubscribing, disposing, and cancelling it — before
establishing the new session. The duplicate-connection guard continues to
reject a connect while a session is live. A faulted profile therefore recovers
through the ordinary `Connect` action, repeatably, for every consumer of the
coordinator.

### Pinned media sessions

An active media session pins the media control binding to its runtime host.
While a session streams:

- inventory selection changes do not stop the session and do not rebind media;
  the most recent selection is retained as a pending binding;
- the camera inventory and capability watch remain on the pinned host;
- the pinned host's control-session state continues to stop media on
  disconnect, even while another host is selected, because host state
  notifications are delivered for every profile in the multi-host snapshot.

When the session ends through any path — operator stop, media window close,
source loss, or host disconnection — the pending binding is applied, the
camera inventory resets, and the capability watch restarts for the newly bound
host. Selecting the pinned host again cancels the pending change. Without an
active session, media follows the inventory selection exactly as before
ADR-0059.

## Consequences

- Inspecting hosts is safe; connection changes are always explicit operator
  actions on the entry that names them.
- The media window and the client process survive every open, close, reopen,
  and shutdown order, with or without an active stream.
- A runtime host restarted after a fault reconnects without restarting the
  client.
- A running stream survives host selection changes, so the operator can work
  with another host's endpoints while observing live media.
- The `OnExit` shutdown path still blocks the UI thread on asynchronous
  disposal. The deadlock trigger was removed by ordering, not by restructuring
  shutdown; any future disposal work that marshals to the dispatcher would
  reintroduce the risk. This weakness is recorded and deliberately deferred.
- `ConnectSelectedRuntimeHostCommand` and `DisconnectSelectedRuntimeHostCommand`
  remain defined and tested but are bound in no view.

## Implementation record

- `8941f8a04588c8e50651707cf8f3cd2b365e72c7` — selection list and per-entry
  connection control.
- `87bb98772e796983d318e61d7a03aee4cb92d991` — retained media window, hide on
  close, stop on hide.
- `1be42e9b38bd17317be777383f16406b05e8bcaa` — unparent on real close,
  explicit WebView2 disposal.
- `9cbf30323803a0ea7ac42d924cfb785f570a6a0e` — boundary disposal ordered
  before the remote stop.
- `19583960e48acda11e65d5215e74d54b7cd98aaf` — faulted profile session release
  and reconnection.
- `90cc3cd77724124d2b193c82d06f2d2bc50405cd` — pinned media sessions and
  deferred rebinding.

## Validation record

- The faulted-session recovery and the pinned-session behavior are covered by
  focused unit tests written before their fixes; each reproduction failed
  against the prior implementation and passes against the closed one.
- The complete Release suite grew from 6,379 to 6,391 passed with zero failed
  and zero skipped across the objective.
- Every increment was deployed to the installed LTAEP client with
  `Update-HaseClient.ps1`, preserving Runtime Host registry and desktop
  shortcut custody, and physically verified by the operator: repeated media
  window reopening, clean process exit in every close order, repeated
  reconnection after MiniPC Runtime Host restarts, and a stream surviving host
  selection changes.
- The media window controller and application shutdown path in
  `Hase.Client.Wpf.App` remain without a dedicated test project; their fixes
  were verified physically on LTAEP.
