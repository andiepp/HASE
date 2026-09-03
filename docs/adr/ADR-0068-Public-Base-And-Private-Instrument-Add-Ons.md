# ADR-0068 — Public Base and Private Instrument Add-Ons

- Status: Accepted; 68A through 68H4 and 68I1 complete; 68I2 to 68I4
  remain
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
the compact-serial endpoint type it speaks to remains. So does the generic
Arduino Uno definition that type is exercised with; the host-side
definition of the Uno Light board itself is the laboratory's and leaves
with the firmware, which 68H2 found had not yet happened.

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

The three composition roots split first, one at a time. Each ships a
different thing, and none could be assumed to behave like the one before
it.

#### Increment 68G1 — The Runtime Host application ships no instrument

Result: complete as `2b72f87`; 7,010 passed, 0 failed, 0 skipped across 36
test projects. The published application composes the two endpoint kinds
that carry no device knowledge; `Hase.DesktopHost.App.Lab` derives from it
and registers the KEL-103 and RF-Lab providers alongside them.

The seam was chosen by experiment. Deriving from a Prism WPF application
across assemblies works, and because the base declares no XAML resources
there is nothing to initialise, so the add-on entry point is an override
and a `Main`. Extracting the WPF shell into a library proved unnecessary.

Removing the six instrument references broke 88 compilations in
`Hase.DesktopHost.Tests`, which is the point of the increment: the base
test project was reaching instrument types transitively through the
application. Six test files moved to the add-on; two used instruments only
as example data and became generic instead of moving.

A latent defect fell out. `ExpectedPublishedEndpointCount` summed native,
compact and KEL-103 and silently omitted RF-Lab. It now counts every
configured endpoint whoever supplies it.

#### Increment 68G2 — The Client application ships no panel

Result: complete as `a7b318d`; 7,011 passed, 0 failed, 0 skipped across 36
test projects. The lightest of the three, because ADR-0067 had already
built the seam: the library owns the panel registry and ships no panel, so
the application only needed its composition made overridable and its single
RF-Lab reference removed. The published Client composes an empty registry
and behaves as one with no panel concept at all.

#### Increment 68G3 — The composition tool edits no instrument

Result: complete as `2966996`; 7,013 passed, 0 failed, 0 skipped across 37
test projects. This root needed a contract rather than an override, because
its operations are commands with different argument shapes and different
reporting. The tool takes `IEndpointProfileOperation` implementations an
entry point supplies.

It surfaced a defect that mattered operationally. The tool refuses to edit
a composition while the Runtime Host is running, but it looked for a
process named `Hase.DesktopHost.App`. Since 68G1 this laboratory's host
runs as `Hase.DesktopHost.App.Lab`, so the guard had quietly stopped
protecting anything. An entry point now contributes the process names its
own host runs under.

#### Increment 68G4 — The base builds and tests without instruments

Result: complete as `24474b1`; the base solution builds with 0 errors and
its own suite passes 5,842 tests across 28 test projects. `HASE.Base.slnx`
is the full solution minus the private laboratory, 62 projects rather than
84, and neither its cold build log nor its test run mentions `Kel103` or
`RfLab` once. The five layering guards prove device-freedom one assembly at
a time; this proves it for the whole base at once.

An instrument is an add-on; the protocol it speaks is not. `Hase.Scpi` and
`Hase.Mcnf` stay in the base exactly as `Hase.Scpi.Kel103` and
`Hase.Mcnf.RfLab` leave it, which is the distinction the project names
already draw.

The base is defined as a subtraction rather than as a second hand-written
list, and a test pins it that way, verified by injecting a project into
`HASE.slnx` alone and confirming it fails. Generating the list caught its
own error first: the exclusion pattern matched `.Lab/` but not
`.Lab.Tests/`, and asserting that no add-on entry remained caught what
counting removals did not.

The base carries a warning baseline of 65 rather than 66; the missing one
belongs to an excluded project.

#### Increment 68G4a — The published Runtime Host names no instrument

Result: complete as `3fd2e8e`; 7,020 passed, 0 failed, 0 skipped across 37
test projects. 68G4 found this by inventory rather than by failure, and it
was structural rather than the two strings it first appeared to be. The
command view model held the mode and input label tables, the command path
`ShortCircuit.Activate` and a SHORT-specific validation sentence; the
instrument view model held the order CC, CV, CR, CW, SHORT and offered the
selector only to a device matching those five labels exactly; the view held
a safety warning naming the device. Two of those carry no instrument name,
so the first count understated what was there.

No design was invented. 68E built `CommandPresentation` for exactly this
and the Client already consumes it, so this mirrors the Client: the label
comes from `ShortLabel`, the grouping from `SelectionGroupId`, and the
confirmation from `RequiresExplicitConfirmation`. Three couplings were
dropped as device assumptions rather than reproduced: a selection is
offered when it has at least two choices instead of exactly five, commands
declaring different selections are no longer merged, and a confirmation no
longer requires the instrument to also declare input controls.

The guard is a new kind, because the existing five could not have caught
this. They compare assembly references, and every one of them passed while
a KEL-103 safety warning sat in the shipped user interface: a reference
guard cannot see a string. The sixth reads the published application's own
source and fails on any instrument name, verified by planting one.

The cold build reports 64 warnings rather than the 66 baseline, because two
of the baseline's warnings were nullable-assertion warnings inside the test
block this increment replaced and were not reintroduced.

#### Increment 68G5 — Documentation closure before the base is run

Result: complete as `c1c9ab9`. Records 68G1 through 68G4a across this ADR,
Project Status and the Roadmap, before the physical run, as 68D2 did before
the migration.

#### Increment 68G6 — The base is run

Result: complete. A Runtime Host and a Client, both built from
`HASE.Base.slnx`, ran on AEPRAKETE against each other on loopback. The host
published one endpoint, `simulation-byte-buffer-validation`, and reported
itself as the certificate-free development composition. The Client
connected to it, read a property and executed a command, and offered no
instrument panel while connected.

The proof is in what was built rather than in what was used. The two
application output directories carried 40 and 39 assemblies and not one
matching `Kel103`, `RfLab` or `Mcnf`, so the instrument code was not merely
unexercised; it was never compiled. The build went to an isolated artifacts
directory, leaving the repository output trees and every installed
application untouched.

No hardware, certificate or existing configuration took part. The
development profile is loopback-only and certificate-free, and it accepts
the byte-buffer simulation without an endpoint composition, so the base
published a real endpoint with no instrument and no device attached. The
run used its own configuration directory and referenced the existing
development identity read-only; minting one would have been a credential
operation and was out of scope.

The Client refused the host on the first attempt, and was right to. Its
registry named an expected runtime-host identity taken from a constant that
the shell only displays, rather than the authoritative one in the identity
file, and the Client reported an identity mismatch instead of attaching.
The check earned its place: a profile pointing at an unintended host fails
closed and says why. The missing panel counts as evidence only from the
second attempt, because a Client that never connects shows no panel either
way.

#### Increment 68G7 — Documentation closure for the run

Result: complete. Records the run and closes 68G, which is now built,
tested and run as this increment defined it.

Two things 68G left for 68H. The publish scripts built fixed projects and
could not produce the `.Lab` variants, so republishing an installed
application would have installed a host with no instruments and a Client
with no panel; 68H1 closed that. And definition version 5 remains in
service while version 6, which declares the presentation the Runtime Host
now reads, exists unused; until it is in service the KEL-103 modes, input
controls and SHORT activation are offered as ordinary command entries
rather than as dedicated controls, with every command still executable.

### Increment 68H1 — The publish path can install an add-on application

Not in the original plan. 68G left an add-on that could be built and run
but not installed, and 68H would have produced a repository whose
applications the tooling could not deploy. Scoping found the debt wider
than the two publish scripts: the installed application's identity was the
base executable name across the deployment surface, 46 occurrences in 28
files with no abstraction anywhere. The cheap answer, giving the `.Lab`
projects the base assembly name so every script kept working, was checked
and rejected: a `.Lab` project references the base application project, so
both assemblies would carry one name in one output directory. The add-on
applications keep their own names, and the tooling learns to read them.

#### Increment 68H1a — Publication names the application it publishes

Result: complete as `beb4248`; 7,030 passed, 0 failed, 0 skipped across 37
test projects. Both publish scripts take the application project and
derive every executable name from it, defaulting to the project this
repository ships, and the installation records which application it holds
in `installed-application.json`. The base ships the generic default; a
composition root that ships instruments names its own project. That is
the seam this objective has now used six times.

The record is written after the installed executable is verified and
before the previous application is discarded, so it can never outlive what
it describes. All four callers invoke the publishers with a named
installation directory, so the optional parameter cannot shift a
positional binding, and the base project's derived legacy WebView2 path
resolves to the identical string.

Validated by publishing. Five publications into throwaway directories,
base host, base host again, add-on host, base client, add-on client: every
recorded executable existed in its installation, the base installations
carried no instrument assembly, the add-on host carried all four KEL-103
and all four RF-Lab assemblies, the second host publication took the update
path, and none left a backup behind. The resolver was extracted from the
script's syntax tree and exercised: the default, a relative add-on project,
a project outside the repository, and a file of the wrong kind.

#### Increment 68H1b — An update updates the application that is installed

Result: complete as `ea6efcd`; 7,040 passed, 0 failed, 0 skipped across 37
test projects. Both updaters read the record and fall back to the shipped
application, which is what every installation predating the record holds.
Publication records the project as well as the executable, and an update
republishes that project.

The project had to be recorded, and the reason is the point. The updaters
call the publisher: with only the executable recorded, updating an add-on
installation would have verified the add-on executable and then
republished the base into it. It also closed the defect 68G3 found, again:
the Client updater refused to run while `Hase.Client.Wpf.App` was up, so on
an add-on installation the guard protected nothing and the update would
have proceeded underneath a running client. The guard now follows the
record.

Scoping narrowed it. The plan said the C# installation plans would read the
record; four of the six have no production caller and the two that do
belong to the onboarding audit and the guided installer, outside this
scope. The C# is untouched.

The reader was extracted and exercised on four inputs, two throwaway
publications showed the record upgraded in place and written identically
from both scripts, and a real record fed back through the reader named an
executable and a project that both exist. The updaters themselves were not
run: each hard-codes its installation directory, so exercising them end to
end would mutate a live installation. The first real exercise is a
separately approved update.

#### Increment 68H1c — Documentation closure

Result: complete as `fb0d88c`. Records 68H1a and 68H1b across this ADR,
Project Status and the Roadmap.

### Increment 68H2 — Uno Light leaves the base host

Not in the original plan. Scoping 68H found that the published Runtime
Host built its compact-definition repository inline from three factories,
one of which was the Uno Light sensor board, an Arduino Uno carrying an
AS7331 UV sensor and an AS7343 spectral sensor, and registered it
unconditionally. This ADR names the Uno Light firmware as private; the
host-side definition of that board is device knowledge of the same
laboratory, and 68H could not claim the base composes the instruments as
before while the base still owned one of them.

Result: complete as `b26ea55`; 7,042 passed, 0 failed, 0 skipped across 37
test projects. The base solution builds with 0 errors and its suite passes
5,854 tests across 28 projects, and neither log mentions any instrument or
the board.

The seam is the shape of 68G1 exactly: a `CreateCompactDefinitions`
virtual on the base application, a static default of the generic Arduino
Uno definition in both versions, a three-argument backend constructor, and
`LabApp` overriding to add the board. The backend composes its definition
repository rather than enumerating it. The factory and its test moved to
the Lab projects, recorded as renames at 99 percent similarity; the move
diff is the namespace and using lines and nothing else.

The guard learned the device it was blind to. Every base assembly guard
passed while the board's definition sat in the published host, as they had
passed over the KEL-103 warning in 68G4a, because a reference guard cannot
see a name and the source guard sees only the names it is given. It now
refuses `UnoLight`, `Uno Light`, `AS7331` and `AS7343`, verified by
planting a comment naming the board in `App.xaml.cs` and confirming the
guard fails naming that file. The list of names is only as complete as the
last inventory, which is a thing for 68I to remember.

Two composition assertions mirror 68G1's and look definitions up through
the real repository by reference rather than matching strings: the base
resolves `arduino-uno-validation` in both versions and does not resolve
`arduino-uno-light`; the Lab resolves all three.

Two things went wrong on the way and are recorded because they will recur.
The moved test compares the board against the generic Uno definition,
which stays internal to the base, so the base had to grant the Lab tests
`InternalsVisibleTo`, the mechanism it already grants its own tests. That
alone did not compile: the namespace rewrite had also dropped a using, and
the compiler said the name was not found rather than inaccessible, which
is what pointed at the using.

Consequence for AEPRAKETE, stated before it bites. Its Development
composition contains `arduino-uno-light`, so from this commit only the Lab
host publishes that endpoint; a base host started against that
composition reports it unresolved. Nothing changes on disk until a
republish, and 68H1's record means an update rebuilds whichever
application is installed, but the Development host is now the one place
where which host is running matters.

The base solution's cold warning count was not re-measured; its build
ran incrementally after the cold full build. The 65 measured at 68G4
stands as the base's cold figure until it is.

#### Increment 68H2a — Documentation closure

Result: complete as `e7185f8`. Records 68H2 across this ADR, Project
Status and the Roadmap.

### Increment 68H — The add-on repository

The private repository is created with its own entry point, consuming the
base, and is proven to compose and operate the instruments as before.

Result: complete as two commits, `6b120cd` in `andiepp/HASE-Lab` and
`e7345c7` in this repository, in that order, so that at no moment did the
laboratory exist in neither.

The add-on consumes the base as a Git submodule at `base/`, pinned to an
exact commit, which was chosen over a sibling checkout by convention and
over NuGet packages. A sibling checkout pins nothing and fails with
twenty-two confusing errors from a wrong parent directory; packaging is
the proper long-term shape and the largest build-out, because the base has
no packaging, versioning or feed at all. The submodule pins the exact base
commit the add-on was proven against, which is what §1 asks of any
starting state.

The move was a copy with one mechanical rewrite. Twenty-two projects and
the Uno Light firmware, 284 files, arrived byte-identical except for 41
project references in 13 project files, each changed from a sibling path
in the base to the same path through the submodule. Add-on projects
referencing add-on projects were untouched, and no reference of any other
kind climbed out of a project, which was checked rather than assumed. The
add-on solution lists its 22 projects and nothing of the base, which is
reached through the references.

Proven from nothing local. A fresh clone of the add-on with its submodule
resolved the base at the pinned commit, built with 0 errors, and passed
1,190 tests across 9 projects: the 1,188 that partition the full suite
exactly against the base's 5,854 at the time, plus two mirror-image
guards pinning that the Lab host references both instrument families and
derives from the published application rather than copying it. The
add-on's 12 warnings are all base warnings seen through the submodule; the
add-on projects compile with none.

The base then shed the laboratory: 22 project directories and the
firmware removed, `HASE.slnx` reduced to the 62 projects that remain, and
`HASE.Base.slnx` removed as redundant, because with nothing to subtract
the base solution is the solution. The subtraction guard inverted rather
than died; it now pins that the solution names no project of the
laboratory. `CLAUDE.md` is true again, six stale figures corrected to
measured ones, among them an expected suite total from before this
objective began. The base keeps its `InternalsVisibleTo` grant to the
add-on's host tests, which compile against this base through the
submodule and need it. Complete suite 5,853 passed, 0 failed, 0 skipped
across 28 projects; cold build 63 warnings, the baseline from here.

The first construction run reported success while three of its five steps
had failed, because a failure inside a redirected brace group does not
propagate. The second run gated each step on a measured count, and the
clone-proof exists because a push and a working clean machine are
different claims.

What 68H left open, in the add-on. The submodule pin was one commit
behind the base; it has been advanced three times since, each a small
add-on commit the add-on's README documents, validated by building and
testing against the new pin, and it now equals the base's head. The
add-on repository carries GPL-3, GitHub's form default, against an MIT
base; a private repository makes it legally unremarkable, but it was a
form choice rather than a decision. And an add-on application can be
published and updated but not yet installed: the guided installers create
base installations only, and an add-on installation needs its own
configuration and identity. That is 68H4.

#### Increment 68H3 — Documentation closure for the add-on repository

Result: complete. Records 68H across this ADR, Project Status and the
Roadmap, and renumbers the installation increment proposed as 68H3 to
68H4, since the closure takes the number.

### Increment 68H4 — An add-on installation

Not in the original plan; the gap 68H1 left. As proposed, an add-on
installation would need its own configuration and identity, and the first
real update of a live installation with the record-reading update path
had not yet run.

Result: complete. The prediction was wrong in a useful way. The real
installations on AEPRAKETE already had configuration, identity and a
composition naming all four provider families; what they lacked was that
their application was the base one, and three things that nobody had seen
stood between them and the Lab one. Each became an increment.

#### Increment 68H4a — Publication from the containing repository

Result: complete as `3a41d05`; 5,857 passed, 0 failed, 0 skipped across 28
test projects. 68H1a's resolver refused any project outside the base
repository, and from inside the submodule every add-on project is outside
it; its own proof had run while the add-on projects still lived in the
base. The boundary is now the outermost repository that contains this one,
found through Git: `rev-parse --show-superproject-working-tree` names the
containing repository from inside the submodule and nothing from the base.
A relative project is looked for in this repository first and the
containing one second, and the record says which root it was found under.
Proven from both roots on real records, and the record fed back through the
updater's reader and the publisher's resolver from inside the submodule.

#### Increment 68H4a2 — An installation can be told what it should hold

Result: complete as `5fe00c7`; 5,863 passed, 0 failed, 0 skipped across 28
test projects. Both real installations predated the record and both
updaters took no parameters, so an update would have fallen back to the
shipped project and republished the base into them. Worse, the updaters'
contract, replace the application and preserve everything else, verified
the shortcut against the installed executable before publishing and
required that same executable and an unchanged shortcut afterwards; the
first time an installation changes to an application of another name, that
fails after the publisher has already succeeded, leaving the new
application behind a shortcut pointing at a file that no longer exists.
Both updaters take an optional `-ApplicationProject`, what this
installation should hold from now on. The checks before publication run
against what is installed now; the check after runs against what the
publisher recorded; if the executable's name changed, the shortcut is
re-pointed, target and icon only, and verified to have changed in nothing
else. Proven on a synthetic installation with a genuine shortcut for the
changed-name and unchanged-name paths.

The add-on's pin advanced to each base commit as it landed, `e89e33a` to
`3a41d05` and `a461612` to `5fe00c7`, each validated by a cold build and the
add-on's 1,190 tests against the new pin, and the second proven with the
submodule's own publishers producing the Lab host and Lab client into
throwaway directories with records reading `superproject`.

#### Increment 68H4b — The live installations take the Lab applications

Result: complete, on AEPRAKETE, one installation at a time, each with an
independent snapshot before and after. The Runtime Host first:
`HASE\RuntimeHost` holds `Hase.DesktopHost.App.Lab.exe` with the eight
instrument assemblies as add-on dependencies and a record reading
`superproject`; the three configuration hashes and the identity hash are
byte-identical before and after; the desktop shortcut is re-pointed with
its argument unchanged; WebView2 custody preserved; the publisher's backup
removed on success. `Secured` shares that application directory and so
holds the Lab host too. Then the Client, the same way: `HASE\Client` holds
`Hase.Client.Wpf.App.Lab.exe` with its two, the registry hash unchanged,
the shortcut re-pointed. The Client needed one preparation: its desktop
shortcut was named `HASE Dev Client.lnk` where the updater requires `HASE
Client.lnk`, and was renamed, byte-equivalent in target, argument and
working directory. Nothing else on the machine and nothing in any
repository changed. These were the first two executions of the
record-reading and told-once update paths on live installations, and both
matched the synthetic exercise line for line.

Each installation's `Application` directory now holds two executables,
the Lab one and the base one. The base one is a published dependency, not
a leftover: the Lab application references the base application project,
both carry one publish timestamp, and the throwaway publish, which had no
old installation to inherit from, holds the same pair. The record and the
shortcut both name the Lab one.

What stays deferred. A guided installer that creates an add-on
installation from nothing, and RF-Lab operations in the composition tool
alongside the KEL-103 ones, remain unbuilt, and unneeded while every
installation on the estate already exists. What stays open: the installed
Lab applications have not yet been started and observed publishing and
operating, which is a physical step; the `HASE Dev Host` shortcut still
targets the base repository's build, which since 68H2 is a base host; and
the add-on's GPL-3 licence against an MIT base remains GitHub's form
default rather than a decision.

#### Increment 68H4c — Documentation closure

Result: complete. Records 68H4a, 68H4a2 and 68H4b across this ADR, Project
Status and the Roadmap, and closes 68H4.

### Increment 68I — Publication

Separately approved. Planned as the only irreversible step, and scoping
found that the step had already been taken: `andiepp/HASE` has been
public since its creation on 2026-07-08, under the MIT licence and front
door ADR-0060 delivered on 2026-08-22, and every commit of this
objective, the ones that moved the laboratory out included, was pushed to
it. Nothing about the tip can undo that, and nothing needed to: the
history holds no credential material, no key body and no address of the
estate, which was verified before anything else. Publication is therefore
not an act but a state, and 68I is declaring the base in its intended
form, what the deferred scope below calls licence, contribution guidance
and published-repository hygiene. Four sub-increments, each a decision
made before it became a task: the base's worked example is base-only
(68I1); the legacy typed projections of the two laboratory families leave
the base library (68I2); the operating documents become generic, with the
estate-specific ones moving to the add-on (68I3); and a tagged release
closes the ADR (68I4). Versioned packages and the Python package on PyPI
stay deferred as before.

#### Increment 68I1 — The front door tells the truth

Result: complete. Two public documents had become unfollowable: Example 5
told a reader to start the development host from the repository root and
attach a KEL-103 the repository no longer contains, and the SCPI
authoring guide used the KEL-103 projects as its worked reference, citing
`src/Hase.Scpi.Kel103` and its two siblings six times. The README
described the Uno Light endpoint under validated hardware and linked
Example 5 and ADR-0063 from its documentation list, and Example 4 pointed
onward to Example 5.

Example 5 and the KEL-103 characterization report moved to the add-on's
`docs`, verbatim under a note saying where they came from, their two links
re-pointed through the submodule. The guide was rewritten around what the
base ships: the text session and serial byte stream of `Hase.Scpi` and
`Hase.Scpi.Serial`, and the seams an instrument family implements,
`IDesktopRuntimeHostEndpointProvider` and the application's
`CreateEndpointProviders` override. The KEL-103 remains its case study
where a rule needs a concrete instance, cited as the record in ADR-0044
through ADR-0049 rather than as code to read alongside. The README
presents the two validated boards, the ladder and Example 6; the
laboratory's history, ADR-0061 and ADR-0063 included, is untouched. No
code changed; the previous suite result stands.

## Deferred scope

- Versioned package releases of the base. A submodule or path reference
  is sufficient until someone other than the operator consumes it.
- Runtime provider discovery, rejected above; it remains available should
  a second private consumer ever appear.
- Licence, contribution guidance and any published-repository hygiene,
  which belong to the publication increment rather than the split.
