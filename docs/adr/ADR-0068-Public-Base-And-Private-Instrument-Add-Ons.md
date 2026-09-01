# ADR-0068 — Public Base and Private Instrument Add-Ons

- Status: Accepted; 68A through 68F complete; 68G to 68I remain
- Date: 2026-09-01
- Starting baseline: `e1a5c9328a382b5b7cc01bd37437bc3dd479f50a`
- Starting complete Release baseline: 6,955 passed, 0 failed, 0 skipped

## Context

HASE is to be published. Three parts of it are not: the RF-Lab MCNF
instrument family, the KEL-103 electronic load, and the Arduino Uno Light
endpoint firmware. They are the operator's own laboratory, and publishing
them would publish a bench rather than a framework.

The question is whether HASE can be cut so that the published part is a
framework someone can run, and the unpublished part is an add-on the
operator keeps.

Most of the answer is already built. ADR-0065 established that the Client
knows no device. ADR-0066 kept the MCNF protocol layer free of RF-Lab
knowledge so that other MCNF applications could build on it. ADR-0067
established that the Client library owns a panel registry and ships no
panel, while the application composes the panels it ships. Each of those
decisions was argued on its own merits, and together they have left the
libraries device-free without anyone setting out to split the repository.

Measured rather than assumed: of 75 projects, no library references an
instrument. Exactly four non-instrument projects do, and every one of
them is a composition root or a tool — the Client application, the
Runtime Host application, the endpoint profile tool, and the protocol
explorer.

What is not built is the equivalent seam on the host. The Client has a
registry and composes into it; the Runtime Host has concrete per-device
routers, inventory adapters and definition preflights sitting in its
application, and a composition profile that names each device as a typed
member.

## Decision

### The base is a framework that runs on its own

The published repository contains the libraries, the generic protocols —
native, compact, MCNF and SCPI — the simulation, the Runtime Host and
Client applications, the deployment tooling, and the architecture
decision record set. It composes no instrument.

It is not a library distribution. A published Runtime Host starts,
publishes its simulated byte-buffer endpoint, and a published Client
connects to it and operates that endpoint through the descriptor-driven
interface, with diagnostics and media available. Someone who clones the
base can see what HASE does without owning any hardware, which is the
difference between publishing a framework and publishing a pile of
assemblies.

The two generic endpoint kinds stay, because they carry no instrument
knowledge: native-protocol endpoints over the network and
compact-protocol endpoints over serial. The Uno Light *firmware* leaves;
the compact-serial endpoint type it speaks to remains.

### An add-on supplies its own entry point

The private repository consumes the base and supplies its own composition
root: an application that references the base libraries and its own
instrument projects, and starts the Runtime Host with them registered.
The base's own applications remain device-free.

The alternative considered was runtime discovery, with the published host
loading provider assemblies it finds. It is rejected. It would make a
published application's behaviour depend on what happens to be on disk
beside it, which is a larger security surface for a project intended to
be cloned by strangers, and it would introduce a plugin mechanism that
exists only to serve one private consumer. A private entry point costs a
thin duplicated composition root and keeps the published host's behaviour
fully determined by what it ships.

This mirrors what the Client already does. `Hase.Client.Wpf` owns the
registry and ships no panel; `Hase.Client.Wpf.App` composes the panels it
ships. The add-on composes its own.

### The host gains the registry the client already has

Instrument knowledge moves out of the Runtime Host application and behind
a registry it composes into. Today the application holds five concrete
files naming instruments — inventory adapters, attachment sets and
definition preflights for KEL-103 and RF-Lab — and a router that
dispatches on connection-definition type.

The seam is an endpoint-provider registry: a provider contributes the
definitions it supports, the attachment it creates, and the preflight
that validates it. The base composes providers for the generic and
simulated endpoints. An add-on composes its own alongside them.

This is the ADR-0067 shape applied to the host, and it is worth doing on
its own merits. The instrument attachment router currently grows a branch
per instrument family; a registry replaces that with registration.

### The composition profile stops naming instruments

`DesktopRuntimeHostEndpointCompositionProfile` carries a typed member per
endpoint kind, two of them instrument-specific, and is serialised to
`desktop-runtime-endpoints.json`. A closed set in a base library cannot
survive the split.

It becomes an open collection keyed by a provider identifier, so that a
profile names what provides an endpoint rather than requiring the base to
know every kind that might exist.

This changes a file format that exists on all three computers. It is the
only part of this objective with a physical consequence, and it is
handled as such: a reader that accepts both shapes, a migration that
rewrites in place with a timestamped backup, and no removal of the old
reader until every computer is confirmed migrated.

### Device knowledge leaves the client library

`CommandInventoryItemViewModel` carries KEL-103 command-path-to-label
dictionaries. They are inert without the instrument, but they are device
knowledge in the component whose stated principle is that it has none,
and they would publish an instrument's naming in a repository that does
not ship it. The labels move to where the instrument is described.

### The protocol explorer splits

Forty of its 153 files are KEL-103 characterization. The generic
protocol-exploration surface stays public; the instrument
characterization scenarios move to the add-on.

### What does not change

- The northbound API, the descriptor model, the authorization model, and
  every protocol.
- How instruments work. An add-on's instruments behave exactly as they do
  now; they are composed from a different entry point.

## Consequences

### Positive

- The published repository is a framework that runs, demonstrates itself
  without hardware, and contains no part of a private laboratory.
- The host acquires the registry the client already has, which removes a
  per-instrument branch from the attachment router.
- The base is forced to stay device-free, because a leak breaks a build
  rather than merely offending a principle.

### Negative

- Two repositories to keep in step, and a version relationship between
  them where there was none.
- A duplicated composition root, thin but real, in the add-on.
- A configuration file format migration on three computers, which is the
  only physical risk in the objective.

### Neutral

- History is not carried across. The published repository begins with a
  fresh initial commit, so the split need not be reconstructed from the
  existing history.

## Increment plan

Each is a separate stop point. The first four are complete and pushed.
68D1 was not in the original plan: 68C deliberately left nothing able to
write the new shape, so the migration had to be built before there was a
migration to run.

### Increment 68A — The endpoint-provider registry

Introduce the registry and move the base's own endpoint kinds behind it,
with the instrument families still composed directly. No behaviour
changes; the router keeps working while the seam appears beneath it.

Result: complete as `3972e7b`; 6,971 passed, 0 failed, 0 skipped across 34
test projects. The contract and registry live in the `Hase.DesktopHost`
library, which registers no provider; the native-network and
compact-serial providers live in the application, mirroring ADR-0067. The
byte-buffer simulation deliberately did not move: it attaches even when no
composition is configured, it is not a refresh target, and routing it
through the startup coordinator would have turned a failed simulation
attach from a startup failure into a tolerated one.

### Increment 68B — The instruments move behind the registry

RF-Lab and KEL-103 are composed as providers rather than named in the
Runtime Host application. The five instrument-named files leave the
application. Still one repository, still one solution.

Result: complete as `8a0bdb4`; 6,976 passed, 0 failed, 0 skipped across 34
test projects. The five files could not go into the instrument hosting
libraries: those pin an assertion that they reference no
`Hase.DesktopHost`, no Grpc and no Wpf, and the complete suite caught the
attempt in four failures. They landed in two new projects,
`Hase.Scpi.Kel103.DesktopHost` and `Hase.Mcnf.RfLab.DesktopHost`, which
are what 68H moves to the add-on repository.

Two seams from 68A changed under the weight of real providers. Resolution
now runs before the attachment host exists, so an attachment receives the
inventory as a parameter rather than capturing it, which keeps a
definition preflight failing before any runtime resource is created. And a
provider is asked for its attachment service only when it resolved at
least one endpoint, preserving the existing assertion that no KEL-103
service is constructed when no KEL-103 endpoint is configured.

### Increment 68C — The composition profile opens

The profile becomes an open collection keyed by provider. The reader
accepts both shapes. Nothing is written in the new shape yet.

Result: complete as `6b4ffd8`; 6,988 passed, 0 failed, 0 skipped across 34
test projects. An endpoint is an entry: a provider identifier, an expected
identity, and settings carried as text. The four typed views remain as a
transitional convenience, projected back out of the entries. The writer
still emits version 1, but from the entries rather than the typed views,
and refuses a provider the closed format cannot name.

That refusal was only half the guard. Every edit had rebuilt the
composition from the typed views, so an endpoint from an unknown provider
was already gone before the writer saw it, and the test written to prove
the refusal passed because the edit had silently succeeded. The edits now
rebuild from entries and preserve what they do not understand.

### Increment 68D1 — The migration the physical step will run

The capability 68D needs: the composition carries the format version it
was read in, an edit writes that version back, and one migrate operation
changes it. That rule is what makes a version 2 writer safe to deploy
ahead of the migration, because an ordinary edit cannot rewrite an
unmigrated host's composition into a shape its own host cannot read. A
read-only preflight reports what a migration would change, and the profile
tool gains both operations.

Result: complete as `10e993f`; 6,996 passed, 0 failed, 0 skipped across 34
test projects. No installed file was touched. The preflight reports
provider identifiers and a count of settings, never their values, because
a composition names serial targets and a preflight exists to be pasted
into a handoff.

One test was removed rather than repaired: 68C's assertion that a foreign
provider blocks an edit is the rule this increment lifts. Three tests
replace it.

### Increment 68D2 — Documentation closure before the migration

Result: complete as `6823cea`. Recorded 68A through 68D1 across this ADR,
Project Status and the Roadmap, so that the written state matched the
pushed state before anything physical happened. It ran before 68D rather
than after it, which is why it is numbered here.

### Increment 68D — Migration

The existing composition files are migrated on each computer, with
backups, and each is verified to publish the same endpoints as before.
Physical, separately approved, one installation at a time.

The order within an installation is fixed by an asymmetry. A host built
before 68D1 cannot read a version 2 composition; a host built from it
reads both. So the host is republished first and its composition migrated
second, and a rollback of the host alone after migrating is unsafe — the
pair comes back together, composition first.

How many installations exist is not assumed. The composition path comes
from `endpointCompositionFilePath` in each installation profile, and a
read-only discovery phase enumerates them and runs the preflight against
each before anything is written.

Result: complete. Discovery found five compositions across three
computers: three on AEPRAKETE, two on LABC, and none on LTAEP, which is
client-only and had nothing to migrate. **All five are migrated, and four
are verified by publication.** Each retains a backup proven byte-identical
to the file it replaced.

The republish-then-migrate order held, and discovery changed what it
applied to. Neither multi-host computer has one application per
configuration: each has a single installed application serving several,
so republishing it covers every configuration on that computer at once.
AEPRAKETE's `Development` needed no republish at all, because it runs from
the repository build rather than an installation. The applications on
AEPRAKETE and LABC were republished from `6823cea` before either
computer's compositions were touched.

The one composition not verified by publication is AEPRAKETE's `Secured`.
Its host cannot bind: the private-network port sits inside the machine's
Windows dynamic port range, where Hyper-V holds a reservation covering it,
so the host faults on a socket permission error and publishes nothing.
That predates this objective, proven by restoring the version 1 backup and
observing the identical fault. It was migrated anyway, on the operator's
instruction and on file-level verification alone, and is recorded as
migrated with publication outstanding.

Two operational findings worth keeping. A first start can omit an endpoint
that a restart then publishes; it happened twice, once where the device
had enumerated late, and neither was caused by the migration. And the
publish tooling does not retain the previous application — it moves it
aside as a transaction, restoring on failure and deleting on success — so
an application rollback means republishing from an earlier commit, while
composition backups are retained.

### Increment 68D3 — Documentation closure for the migration

Result: complete. Records what the migration did, what discovery changed
about the plan, and the one composition left unverified.

### Increment 68E — Device knowledge leaves the client library

The KEL-103 labels move out of `CommandInventoryItemViewModel`.

Result: complete as `753ba0d`; 7,006 passed, 0 failed, 0 skipped across 34
test projects. The library held six things rather than the two this plan
named, and the last three became visible only once the first three were
gone: the two label dictionaries, the hardcoded `ShortCircuit.Activate`
path, the instrument's own SCPI vocabulary in `NormalizeOperatingMode`,
the hardcoded `Operating.Mode` property path in two separate places, and
the arrays that imposed both an order and a completeness requirement.

`CommandPresentation` follows `PropertyPresentation` exactly: relationship
and the instrument's own naming, not appearance. A command may declare its
short label, the selection it belongs to, the property reporting which
member is in effect, and the value that property reads when this member
is. `RequiresExplicitConfirmation` sits beside it rather than inside it,
being a statement about severity rather than presentation. Both cross the
northbound API as additive fields, and the pinned contract test failed
when they landed, which is what it is for.

`Kel103DeclaredControlDefinition` is version 6, version 5 plus the
declarations; version 5 is untouched and a test pins that it declares
none. The `SHORt` and `SHORT` spellings differ only in case, so comparing
the declared value without case sensitivity reproduces the removed
normalizer exactly.

Two rules changed, both consequences of removing the device knowledge
rather than side effects of it. A selection is offered at whatever size
the instrument declares, because a presentation layer cannot know what
complete means for an instrument it has never seen. And the declared order
is honoured, because the client no longer has an order of its own. For the
KEL-103 both are invisible: version 6 declares all five members in the
original order.

Two things this increment did not do. The Runtime Host application holds
the same block; it is a composition root, which this ADR accepts, but it
is still device naming that would be published. And nothing uses version 6
yet: putting it into service needs a tool operation, a republish, and a
composition edit, which is a physical increment of its own.

### Increment 68F — The protocol explorer splits

Instrument characterization separates from generic exploration.

Result: complete as `60bce77`; 7,008 passed, 0 failed, 0 skipped across 35
test projects. The measurement held exactly: `ScpiCharacterization` was 31
files and entirely KEL-103, and 8 of the 77 scenarios were as well. Those
39 files and their 28 tests moved to `HASE.ProtocolExplorer.Kel103` and its
own test project, recorded as renames at full similarity, so no
characterization logic changed. The base explorer keeps 105 source files
and no longer references `Hase.Scpi.Kel103`.

The seam is the one this objective has now used three times.
`Program.cs` was already a composition root handing a scenario list to a
runner, so the work was extracting an application that runs the generic
scenarios plus any an entry point composes into it, and giving the add-on
its own entry point. The published program is three lines; the add-on's
supplies its eight scenarios and their usage lines.

The public surface was deliberately kept narrow. Exposing the explorer host
cascaded into the protocol client and the trace generator, which would have
widened the published API for no benefit, because the characterization
scenarios take no constructor arguments. Only the two scenario contracts
are public.

A layering test pins the result: the published assembly may reference
nothing matching `Kel103`, `RfLab` or `Mcnf`, and no scenario type may
carry an instrument name.

The characterization commands now run from a differently named executable,
with the same arguments and output.

### Increment 68G — The base is proven device-free

The instrument projects are removed from the base solution and the base
is built, tested and run: a Runtime Host publishing its simulated
endpoint, a Client operating it. This is the increment that proves the
claim rather than asserting it.

### Increment 68H — The add-on repository

The private repository is created with its own entry point, consuming the
base, and is proven to compose and operate the instruments as before.

### Increment 68I — Publication

Separately approved, and the only irreversible step.

## Deferred scope

- Versioned package releases of the base. A submodule or path reference
  is sufficient until someone other than the operator consumes it.
- Runtime provider discovery, rejected above; it remains available should
  a second private consumer ever appear.
- Licence, contribution guidance and any published-repository hygiene,
  which belong to the publication increment rather than the split.
