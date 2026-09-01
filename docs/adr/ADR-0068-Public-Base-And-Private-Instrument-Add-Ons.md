# ADR-0068 — Public Base and Private Instrument Add-Ons

- Status: Proposed
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

Proposed, not approved. Each is a separate stop point.

### Increment 68A — The endpoint-provider registry

Introduce the registry and move the base's own endpoint kinds behind it,
with the instrument families still composed directly. No behaviour
changes; the router keeps working while the seam appears beneath it.

### Increment 68B — The instruments move behind the registry

RF-Lab and KEL-103 are composed as providers rather than named in the
Runtime Host application. The five instrument-named files leave the
application. Still one repository, still one solution.

### Increment 68C — The composition profile opens

The profile becomes an open collection keyed by provider. The reader
accepts both shapes. Nothing is written in the new shape yet.

### Increment 68D — Migration

The existing composition files are migrated on each computer, with
backups, and each is verified to publish the same endpoints as before.
Physical, separately approved, one computer at a time.

### Increment 68E — Device knowledge leaves the client library

The KEL-103 labels move out of `CommandInventoryItemViewModel`.

### Increment 68F — The protocol explorer splits

Instrument characterization separates from generic exploration.

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
