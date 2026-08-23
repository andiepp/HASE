# ADR-0062 — Diagnostic Export and Offline Analysis

- Status: Closed; Increment 62G objective closure
- Date: 2026-08-23
- Starting baseline: `67d471e750d63ed50fdd96a456fdd9cccfd9c001`
- Starting subject: `ADR-0061 closing`
- Starting complete Release baseline: 6,464 passed, 0 failed, 0 skipped

## Context

HASE captures structured, sanitized diagnostics in both applications: the
Runtime Host retains a bounded process-local session (2,000 records at the
configured Operational, Protocol, or Bytes level, including bounded
256-byte hexadecimal snapshots), and the Client retains its own bounded
collector. The ADR-0049 remote projection is deliberately live-only. All
of it is volatile: closing the application discards the evidence.

The consequence appeared concretely during the ADR-0061 Example 6
walkthrough: a one-time media incident produced diagnostics that existed
only as long as the Client did, and the investigation had to rely on
operator recollection and reproduction. "Diagnostic Export and Offline
Analysis" has been an accepted, deferred objective since the ADR-0047 era;
this decision takes it up.

## Decision

ADR-0062 adds explicit, operator-controlled diagnostic export from both
applications, one shared versioned export format, and a read-only offline
analysis tool.

### The export document

One export is a UTF-8 JSON Lines document with a strict versioned schema:

- the first line is the envelope: format version 1, the exporting
  application (runtime host or client), the configured capture level, the
  runtime-host identity where the exporter knows one, the export UTC
  timestamp, and the record count;
- every following line is one diagnostic record, complete as captured:
  timestamp, level, category, event name, severity, outcome, direction,
  operation correlation, endpoint and attachment-generation scope,
  instrument and descriptor scope, duration, metadata, and the bounded
  byte snapshot where one was captured.

The export writes exactly what capture retained. Sanitization is
preserved by construction — no secret, credential, address, or unbounded
payload exists in the captured records, so none can exist in an export.
Reading is strict in the established loader style: bounded document size,
exact version, unknown-field rejection, and fail-closed parsing.

### Export from both applications

Each diagnostics window gains an explicit `Export` action beside its
existing controls. Export:

- writes the current retained records — respecting the window is a view:
  the export covers the retained session, not the display filter;
- writes to an operator-chosen destination and never overwrites an
  existing file;
- is manual only: no automatic, scheduled, or shutdown-triggered export;
- changes nothing about capture: levels, bounds, retention, and
  sanitization are untouched.

An export file is operational evidence in the operator's custody. Nothing
is transmitted anywhere by this objective.

### The offline analysis tool

A small console tool — `Hase.Diagnostics.Offline`, a sibling of the
registry tool — operates read-only on export files:

- **validate** — strict parse, envelope/record-count consistency, exit
  code discipline;
- **summarize** — counts by level, category, severity, outcome, event
  name, and endpoint, plus the covered time span;
- **filter** — select by level, category, endpoint, event name, or UTC
  time window, writing the selection as a new valid export document;
- **show** — render one record completely, including the hexadecimal
  byte snapshot and its interpretation-relevant fields.

The tool never connects to a running application, never mutates its
input, and inherits the export's sanitization: it can only show what
capture retained.

## Constraints

- Capture semantics are unchanged: bounds, levels, retention, and every
  sanitization rule stay exactly as they are.
- Export is explicit operator action; the applications never write
  diagnostic files on their own.
- The export format is versioned and immutable once published; evolution
  adds versions.
- The offline tool is read-only over files and has no live attachment of
  any kind.

## Consequences

### Positive

- Diagnostic evidence survives the application session and can be
  studied, compared, and archived by the operator.
- Incidents like the ADR-0061 media case leave a durable, sanitized
  record instead of relying on reproduction.
- One shared format serves the Runtime Host, the Client, and the tool,
  with one strict reader implementation.

### Negative

- Export files are operator-managed artifacts; HASE does not manage their
  retention, protection, or deletion.
- Two windows and a new tool add surface that must track any future
  diagnostic-model changes.

### Neutral

- Runtime, protocol, northbound, and remote-diagnostics contracts are
  unchanged; ADR-0049 streaming remains live-only.

## Increment plan

Each increment is separately approved; code increments run focused suites
first and the complete Release suite after.

### Increment 62A — Decision acceptance

Exact repository scope: this ADR, `docs/ProjectStatus.md`, and
`docs/Roadmap.md`. Documentation-only; validation is consistency, exact
Git scope, `git diff --check`, and diff inspection. Physical effects:
none. Rollback: revert before commit.

### Increment 62B — Export format, writer, and strict reader

The shared document model: envelope and record serialization for both
applications' record types, the writer, the strict bounded reader, and
focused tests covering round-trips, sanitization preservation, version
and unknown-field rejection, and truncated-document behavior.

Completed result: the new `Hase.Diagnostics.Export` project carries the
neutral model with construction-enforced invariants (including the
256-byte snapshot bound and truncation consistency), the host and client
mappers, the atomic temp-and-rename writer that refuses existing targets,
and the strict 16 MB-bounded reader with unknown-field rejection and
CRLF tolerance. Twenty-two focused tests cover round-trips of both
applications' records and eleven distinct rejection paths.

### Increment 62C — Runtime Host diagnostics export

The `Export` action in the Runtime Host diagnostics window over the
retained session, with focused view-model and file-behavior tests
(operator-chosen path, refusal to overwrite, complete-session export
independent of the display filter).

Completed result: the host diagnostics window exports the fresh retained
session — independent of the display filter and of presentation pause —
through an operator save dialog, reentrancy-guarded, with a status line
naming only the record count and file name. Seven focused tests prove
filter and pause independence, overwrite refusal, cancellation, and the
empty session.

### Increment 62D — Client diagnostics export

The same action in the Client diagnostics window over the Client's
retained records, with the corresponding focused tests.

Completed result: the Client window exports its complete retained
session past the level, category, and Runtime Host display filters and
past the pause watermark, carrying per-record session context. Seven
focused tests mirror the host set and additionally prove session-context
round-trip and Runtime Host filter independence.

### Increment 62E — Offline analysis tool

`Hase.Diagnostics.Offline` with validate, summarize, filter, and show,
including exit-code discipline and focused tests over authored and
exported documents.

Completed result: the tool ships as a thin console shim over a testable
command class. Exit codes are 0 for success, 1 for an invalid document or
processing failure, and 2 for usage errors; errors go to stderr. The
filter command writes its selection as a new valid export document
through the never-overwriting writer and preserves the original sequence
numbers so filtered evidence stays traceable to the source capture.
Sixteen focused tests cover exported documents of both kinds and
hand-authored JSON Lines, including every rejection and usage path.

### Increment 62F — Operator validation

Capture a live session on real hardware, export from both applications,
and exercise the tool offline — the operator walkthrough in the
established style, with corrective sub-increments as findings arise.

Completed result: the operator validated the full chain on the live
secured pair — the Runtime Host export on AEPRAKETE (32 records over
three physical and simulated endpoints) and the Client export on LTAEP
(692 records at Bytes capture, including one real Warning and one real
Failed outcome). Both windows exported their complete retained sessions
independent of display filters and pause, both refused overwrites, and
validate, summarize, filter, and show worked on both exports, with
overwrite refusal, tamper rejection, missing-sequence, missing-file, and
usage errors all exiting under the documented codes.

Two corrective sub-increments arose. 62F1: both view-models took an
optional `Func<DateTimeOffset>` clock, and the DryIoc container injects a
wrapper delegate for such parameters that fails with a
NullReferenceException on invocation — the first Export click in the
production Runtime Host failed; the parameter became the DI-safe
`IDiagnosticExportClock` interface, the failure mode proven by a
container reproduction. The process lesson: view-model tests that
construct directly never exercise the container-resolution path. 62F2:
the tool's overwrite refusal was reworded from the misleading
invalid-document prefix to "The filter output could not be written".

### Increment 62G — Objective closure

Reconcile this ADR, Project Status, and Roadmap. Documentation-only.

Completed result: ADR-0062 closes with every increment complete.
Diagnostic evidence now survives the application session: one shared
strict format, explicit never-overwriting Export in both diagnostics
windows, and read-only offline analysis. The final automated baseline is
6,516 tests, zero failed, zero skipped, across 28 test projects.

ADR-0062 is closed.

## Deferred scope

- Automatic, scheduled, or shutdown-triggered export;
- export transmission over any network channel;
- persistence of the ADR-0049 remote diagnostic stream;
- retention management, encryption, or signing of export files;
- analysis attached to a running application;
- graphical offline analysis.
