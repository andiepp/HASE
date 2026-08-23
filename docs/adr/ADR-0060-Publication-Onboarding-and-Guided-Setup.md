# ADR-0060 — Publication Onboarding and Guided Setup

- Status: Closed; Increment 60K objective closure
- Date: 2026-08-22
- Starting baseline: `11f9129ce81abaaad2265cbbd166504bae4b33fe`
- Starting subject: `ADR-0059 closing`
- Starting complete Release baseline: 6,391 passed, 0 failed, 0 skipped

## Context

The public GitHub repository `andiepp/HASE` is the authoritative source, but
everything in it is written for the project's own three-computer laboratory.
The documentation set consists of ADRs, physical characterizations, an API
reference, and two tutorials that assume an already-running system. The
deployment tooling under `tools/Deployment` is bound to the AEPRAKETE, LABC,
and LTAEP roles by name. The repository has no top-level Getting Started path,
no LICENSE file, and no route from `git clone` to a running system for anyone
other than the project operator.

A repository without a license grants no usage rights at all. Publication
therefore requires an explicit license before any onboarding documentation is
useful to an external reader.

The dominant obstacle for a new user is the security boundary. Every validated
multi-computer configuration requires mutual TLS with enrolled client
certificates, certificate pinning, and per-machine provisioning that ADR-0032,
ADR-0043, and ADR-0053 perform through bespoke operator scripts. A new user
cannot and should not perform that work on first contact.

Two existing assets remove that obstacle from the first-run experience:

- Phase 7.7 validated the northbound gRPC boundary on loopback-only binding
  before the ADR-0031 security boundary existed, so the architectural seam for
  a single-PC, certificate-free mode already exists; and
- the simulation layer and the opt-in in-process simulation endpoint allow a
  complete live Property, Command, and Event experience with no hardware.

Decisions taken with the operator on 2026-08-22:

- the audience is engineers comfortable with PowerShell and with flashing
  microcontroller firmware;
- distribution is clone-and-build from GitHub only; prebuilt releases,
  versioning, and package-registry publication are out of scope;
- HASE shall be freely available under a permissive open-source license;
- an explicitly labeled certificate-free mode for single-PC loopback use is
  acceptable; and
- the first example uses simulation only, before any hardware.

## Decision

ADR-0060 publishes HASE for external users through a documentation ladder of
runnable examples, a permissive license, an explicitly labeled certificate-free
single-PC development profile, and — for the multi-computer step — guided
provisioning that removes manual certificate work.

### License

HASE is licensed under the MIT License. MIT is the norm of the .NET ecosystem,
imposes the least friction on adoption, and matches the goal that HASE be
freely available. The copyright holder is the project operator. Third-party
component licenses are not changed by this decision.

### Distribution and prerequisites

The supported acquisition path is `git clone` from GitHub followed by
`dotnet build HASE.slnx -c Release`. Visual Studio is not required; any editor
is sufficient. Prerequisites are stated per example, not as one global list:

- Windows 10/11 and the .NET 10 SDK for every example;
- the WebView2 runtime only where the media window is used;
- the Arduino toolchain only for the Arduino Uno example;
- the ESP32 toolchain only for the ESP32 example; and
- Python only for the optional Python client.

### Example ladder

Onboarding documentation is a ladder of examples with strictly increasing
difficulty. Each example states its parts list, prerequisites, complete
copy-and-paste PowerShell blocks, expected output, and troubleshooting, in the
same complete-executable-handoff style used inside the project.

```text
Example 0  Simulation only: one PC, Runtime Host and Client on
           loopback, simulated endpoint, no hardware, no certificates
Example 1  Arduino Uno on the same PC over USB serial; still loopback,
           still no certificates
Example 2  ESP32 in the local network via mDNS discovery and framed
           TCP; Runtime Host and Client still on one PC
Example 3  Client on a second PC: mutual TLS, enrollment, and pinning
           through guided provisioning
Example 4  A second Runtime Host and the multi-host Client
```

Certificates enter the ladder only at Example 3.

### Certificate-free loopback development profile

The Runtime Host and Client gain an explicitly labeled development profile that
binds the northbound gRPC boundary to loopback only, without TLS and without
client-certificate authentication.

Constraints:

- the profile must refuse any non-loopback binding address;
- the profile must be visibly labeled as a development profile in
  configuration and in diagnostics;
- the secured profiles and their validation remain unchanged; and
- documentation must state plainly that every non-loopback deployment
  requires the existing mutual-TLS boundary.

Network reachability continues to grant no HASE authority; the development
profile is not reachable from the network at all.

### Guided provisioning for the multi-computer step

Example 3 must not require the user to understand certificate authorities,
enrollment files, or pinning. The work proceeds in two stages:

1. a generalized provisioning document with complete copy-and-paste blocks,
   extracted from the ADR-0032/0043 recipes and stripped of machine-specific
   names; then
2. a guided command-line setup wizard that asks which computer it is running
   on, which role it takes, and how the Runtime Host is addressed, and then
   generates the certificate authority, server and client certificates,
   enrollment, trust, and profile files itself, emitting one transfer package
   for the other computer.

The wizard packages existing validated provisioning logic; it introduces no
new cryptography. The ADR-0053 credential-lifecycle machinery (rotation,
revocation, recovery) stays out of the onboarding path entirely.

### Repository front door

The root `README.md` is rewritten for an external first-time reader: what HASE
is, what it runs on, the example ladder, and where the internal engineering
documentation lives. Internal process documents (`AGENTS.md`, ADRs,
characterizations) remain unchanged and are linked, not rewritten.

Where onboarding needs install or publish tooling, neutral parameterized
scripts are added alongside the existing machine-specific ones; the existing
scripts and the validated internal topology remain untouched.

## Consequences

### Positive

- An external engineer has a lawful, documented path from `git clone` to a
  live system in minutes, with no hardware and no certificates.
- Difficulty rises one concept at a time; the security boundary appears only
  when a second computer appears.
- The certificate-free mode is architecturally confined to loopback, so the
  published security model remains honest.
- Guided provisioning reuses validated tooling instead of new mechanisms.

### Negative

- Onboarding documentation must be kept current as the framework evolves;
  each future ADR that changes user-visible behavior inherits a documentation
  obligation.
- The development profile is a deliberate, documented exception to the
  security boundary and must be re-verified whenever hosting composition
  changes.
- Clone-and-build-only distribution requires every user to install the .NET
  SDK and build the solution.

### Neutral

- The internal three-computer process, GitHub-as-authority, and the
  synchronization discipline are unchanged.
- Runtime, protocol, transport, and northbound contracts are unchanged;
  ADR-0060 adds hosting composition, documentation, and tooling only.
- Prebuilt releases, versioning, and package publication remain possible
  later decisions.

## Increment plan

Each increment is separately approved and separately closed. Later increments
are refined when they are reached; scope stated here is the expected shape.

### Increment 60A — Decision acceptance

Goal: record the approved objective, constraints, and increment ladder.

Exact repository scope:

- `docs/adr/ADR-0060-Publication-Onboarding-and-Guided-Setup.md`;
- `docs/ProjectStatus.md`; and
- `docs/Roadmap.md`.

Automated validation is limited to documentation consistency, exact Git scope,
`git diff --check`, and final diff inspection. No .NET build or test is
required because 60A changes no executable or project file.

Physical and deployment effects: none.

Rollback boundary: revert the three documentation edits before commit.

Definition of done: the three documents consistently mark ADR-0060 accepted
and define 60B as the next separately approved increment.

### Increment 60B — License and repository front door

Goal: add the MIT `LICENSE` file and rewrite the root `README.md` for an
external first-time reader. Documentation-only; no build or test required.

Completed result: commit `35232c63e3c392ee71c939380d1740253c2cbe24` adds the
MIT License with copyright holder Dr. Andreas Eppinger and restructures the
README front door around clone-and-build prerequisites and the example
ladder.

### Increment 60C — Certificate-free loopback development profile

Goal: implement and automatically validate the labeled loopback-only,
non-TLS development hosting profile for the Runtime Host and Client,
including refusal of every non-loopback binding. Focused suites first, then
the complete Release suite.

Completed result: Increment 60C1, commit
`416b700e21abde4395fca5a0b5848902424fd3f0`, adds the Runtime Host
development profile (`--development` startup, strict development-loopback
document, loopback-only composition over the existing Phase 7.7 factory,
visible window and diagnostic labeling) at 6,423 complete Release tests.
Increment 60C2, commit `3fb01c1b315c3624ce76ddbfc26461ce59bd2abc`, adds the
Client development configuration, document-kind dispatch, plaintext loopback
session resources, and labeling diagnostics at 6,453 complete Release tests.
Non-loopback refusal is validated at the binding, profile, and document
layers. The secured profiles and their validation are unchanged.

### Increment 60D — Getting Started and Example 0

Goal: the prerequisites and build document plus Example 0 (simulation only,
loopback, no certificates), proven by a walkthrough from a fresh clone on one
of the project computers.

Completed result: Increment 60D1, commit
`f5364c0227ad0eddb6f606b471b859239488298d`, publishes
`docs/Getting-Started.md` and links it from the README. The operator then
performed the 60D2 walkthrough as a new user against a fresh clone in
`J:\HASE`, following only the published document. The walkthrough surfaced
two real defects, each fixed as a separately approved corrective increment:

1. 60D2A, commit `6400017da8ae117c1bca8d1383d0d92472685abf` — six
   deployment-script contract tests matched `\n`-only patterns and failed on
   a default Windows checkout with CRLF endings; the test read helpers now
   normalize line endings, satisfying the §5 rule that validation must not
   depend on line layout. The fresh clone then passed the complete suite
   with 6,453 tests, zero failed, zero skipped.
2. 60D2B, commit `275a70ae812a9565844bb4dd103fb532bb98c33d` — the
   runtime-host identity document parser rejected a UTF-8 byte-order mark,
   faulting the host on the guide's hand-authored identity file; reading is
   now byte-order-mark-tolerant like every sibling document loader, while
   serialization stays byte-order-mark-free. The complete Release baseline
   is 6,455 tests.

After the fixes the operator completed Example 0 exactly as documented:
configuration generation, Runtime Host start with the published `Ready`
simulation endpoint, Client connection, Property writes, Command execution,
Event observation, and orderly shutdown — including closing and reopening
both applications. The operator accepted the example as working.

Deferred cosmetic item: the Runtime Host window's `Identity` field shows the
static `hase-desktop-runtime-host` constant instead of the resolved
development identity. Recorded as deferred presentation work; it does not
affect the Client's authoritative identity verification.

### Increment 60E — Example 1, Arduino Uno on one PC

Goal: firmware flashing, USB serial discovery, and compact endpoint
attachment on a single PC, still on the development profile.

Completed result: Increment 60E1, commit
`d8cefc6ea505d50870293e06c84109a653ae51e2`, publishes
`docs/Example-1-Arduino-Uno.md` — parts list, Arduino IDE flashing (the
existing How-To is a source-authoring guide and excludes flashing), optional
D7 push-button wiring, the endpoint-composition and development-profile
configuration block, Refresh-based late attachment, and the interaction and
recovery steps. Every stated value was verified against the tracked
firmware and the composition-file loader.

The operator performed the 60E2 walkthrough in the new-user role against
the fresh clone: flashed the firmware, attached the physical
`arduino-uno-01` endpoint alongside the simulation endpoint, and validated
the LED Property and Command with authoritative readback, the analog
voltage Property, the push-button Event, and the documented recovery
behavior. One upload attempt failed with a bootloader synchronization error
and was classified by the operator as an externally held `RESET` pin from a
wiring test; no repository defect was involved.

Corrective increment 60E2A, commit
`1a0c074c4cd7acca1ad228dbc921fe73e660af8b`, folds the two walkthrough
findings into the document: the analog source is now fully specified (a
10 kOhm potentiometer between `5V` and `GND` with the wiper on `A0`), and
the troubleshooting section covers uploads failing while `RESET` or the
serial pins are wired. The operator accepted Example 1 as working.

### Increment 60F — Example 2, ESP32 in the local network

Goal: ESP32 firmware, mDNS discovery, and native framed-TCP attachment, with
Runtime Host and Client still on one PC.

Completed result: Increment 60F1, commit
`2fa3dfe5d3a6c027cc5ca3295da11385a9d41553`, publishes
`docs/Example-2-ESP32.md` — LAN security note, required BME280 (a failed
sensor initialization deliberately stops the endpoint before network
startup), repository-vendored libraries, wiring, Wi-Fi secrets from the
tracked template, flashing with the validated board and core versions,
mDNS-first address determination, composition, and interaction steps with
the definition's published display names.

The operator performed the 60F2 walkthrough in the new-user role on
physical ESP32/BME280 hardware. Two corrective increments followed:

1. 60F2A, commit `150198393af437b2abaf3382e71a9d3cb3c5b509` — the library
   setup assumed the default Documents sketchbook; the operator's
   per-project library workflow motivated the better flow now documented:
   one local, git-ignored copy of the shared `HaseEsp32Endpoint` library
   beside the vendored Adafruit libraries, with the sketchbook pointed at
   `HaseEndpoint`. The tracked single source under `libraries/` remains
   authoritative per ADR-0054.
2. 60F2B, commit `a04614d0a9e73617941c93696f51722616f9b58c` — the start
   section repeats the Runtime Host and Client launch blocks
   copy-and-paste-ready instead of referring back to Example 0.

The operator validated the complete example: firmware compilation and
upload, the documented serial startup sequence, native attachment through
the mDNS name `doit-esp32-devkitc-v4-01.local` (the documented default
route, confirmed working), live BME280 Properties, the status-LED Property
and Command, the GPIO 17 Button Pressed Event, and recovery. The operator
accepted Example 2 as working.

### Increment 60G — Generalized provisioning documentation

Goal: the neutral multi-computer provisioning document and any neutral
install/publish scripts Example 3 requires.

Completed result: commit `df0bd7071d661cc1e20d8aa7022e5c048a073531`
publishes `docs/Provisioning-Two-Computers.md`. The inspection established
that no new scripts are required: the parameterized ADR-0032 bundle and
client-install scripts under `tools/PrivateNetwork` are already machine
neutral and physically validated. The document explains the security model
(mutual TLS, byte-exact pinning, certificate-to-principal enrollment,
principal-to-grant authorization, with the development-grade caveats
stated), wraps the two scripts with placeholder-based steps for two
arbitrary PCs, and adds what they do not produce: the host identity,
authorization policy (six operational grants; remote diagnostics
deliberately absent), endpoint composition, installation profile, the
client registry with the expected-identity check, and the Windows Firewall
inbound rule no prior document covered. Every field name and grant string
was verified against the strict loaders, all blocks pass the §5 parser,
and the hand-authored documents were schema-validated.

The planned single-machine dry-run was consciously skipped as redundant:
the provisioning scripts are unchanged since their ADR-0032 physical
validation and later ADR-0043/ADR-0053 exercise, and the genuinely new
composition receives its end-to-end proof in the Example 3 walkthrough
(Increment 60I).

### Increment 60H — Guided setup wizard

Goal: the command-line wizard that generates certificate authority,
certificates, enrollment, trust, and profile files and one transfer package,
built from the existing validated provisioning logic, with focused automated
tests for parsing, success, rejection, failure, and recovery paths.

Completed result: commit `206f735f85e7992695ec24b8f2a461dba7b58247` adds
`tools/Setup/Start-HaseSetup.ps1` with a host role and a client role
selected by parameter set. All cryptography stays with the validated
ADR-0032 provisioning scripts; the wizard refuses pre-existing targets
before creating anything, authors the identity, authorization policy,
endpoint composition, installation profile, and client registry, and adds
the non-secret `client-handoff.json` that makes the transfer package
self-sufficient. Eight focused tests execute both roles non-interactively
with a stubbed provisioning step and validate the authored documents
through the real application parsers; the complete Release baseline is
6,463 tests at the 66-warning baseline. Physical proof followed in the
Example 3 walkthrough under Increment 60I.

### Increment 60I — Example 3, Client on a second PC

Goal: the second-PC example using the wizard, physically validated on the
project computers acting as stand-ins for a new user's machines.

Completed result: Increment 60I1, commit
`b0a0a5ca67ee711117d4aeffd7c400c6d1025e6b`, publishes
`docs/Example-3-Client-on-a-Second-PC.md` around the wizard and the
provisioning reference. The operator performed the 60I2 walkthrough on two
computers in the new-user role — the wizard's first execution with real
certificates on both roles. The secured session succeeded end to end: the
wizard provisioned the host, the four-file transfer package installed the
client credential and authored the registry, and after the firewall rule
existed the first `Connect` established the mutual-TLS session with the
Arduino and simulation endpoints served across the network. The operator
accepted Example 3 as working and praised the walkthrough as a
demonstration of real networking obstacles.

The walkthrough surfaced documentation gaps, no wizard or application
defects; the wizard failed closed correctly when the transferred files
were misplaced. Corrective increments:

1. 60I2A, commit `0e07e2739b2027db0b012e6cdee53c5225c03527` — an explicit
   "Set up HASE on both PCs" step (the client-PC clone-and-build was
   previously only a prerequisite bullet), the listener address hoisted
   into an explained variable, and per-step PC attribution.
2. 60I2B, commit `e6bc9a255ce1c9e9b7f9bff14db066ab9a47186d` — the
   execution-policy note, transfer-path exactness, an elevation-verified
   firewall step with the warning that an access-denied attempt creates no
   rule, the Public network-category trap, symptom-based
   `Test-NetConnection` troubleshooting distinguishing reachability from
   security refusal, and the wizard's printed firewall step naming the
   elevated-terminal route.

### Increment 60J — Example 4, second Runtime Host

Goal: the multi-host example, largely reusing Example 3 provisioning.

Completed result: after Increment 60I4, commit
`0e59678b238c5712df820ce9686167354ddd0bd3`, moved Examples 1 through 3
into `docs/examples/`, Increment 60J1, commit
`3ec8bac16303d7f0c0d033f721a5ae3cdec497e1`, publishes
`docs/examples/Example-4-Second-Runtime-Host.md` — the second host through
a repeated wizard run with distinct identity, profile, and port; the
transfer into a second folder; and the ADR-0043-validated
`Hase.Client.RegistryTool` merging the second host into the existing
client registry.

The operator performed the 60J2 walkthrough with host 2 on the MiniPC.
One startup fault was classified as designed behavior, not a defect: two
HASE-flashed boards matched the composition's VID/PID filter, and the host
refused to guess. With exactly one board attached — an original Arduino
Uno reporting `PID 0x0001`, adapted in the composition — host 2 published
its Arduino and simulation endpoints, the client connected both hosts over
mutual TLS, host 1 gained its ESP32 through its own secured composition,
and the operator accepted Example 4 completely with all endpoints on both
hosts.

Corrective commit `c9b10daec501791e392d6d70be366e28b0a59bfe` (60J2A and
60J2B) folds the findings in: execution-policy lines inlined in every
wizard block, the explicit host-address instruction, the
composition-as-template rules (one matching board, `PID 0x0001` originals,
per-host composition files), four new troubleshooting entries including
the exact multiple-board refusal text, and the original-Uno note in
Example 1.

### Increment 60K — Closure

Goal: reconcile this ADR, Project Status, and Roadmap with the final
baselines. Documentation-only.

Completed result: ADR-0060 closes with every planned increment complete.
Delivered: the MIT license and external-reader README; the certificate-free
loopback development profile for Runtime Host and Client; the five-example
onboarding ladder (simulation, Arduino Uno, ESP32, secured second-PC
client, multi-host), each proven by an operator walkthrough in the
new-user role; the Two-Computer Provisioning reference; and the guided
setup wizard. The walkthroughs drove ten corrective increments, fixing two
real code defects (line-ending-brittle contract tests, byte-order-mark
intolerance in identity reading) and hardening every document against the
obstacles a real user meets. A fresh clone passes the complete Release
suite; the final automated baseline is 6,463 tests, zero failed, zero
skipped, at the 66-warning cold-build baseline.

Remaining deferred scope, unchanged from the decision plus walkthrough
observations: prebuilt releases and installers, package-registry
publication, a graphical configuration tool, non-Windows hosts,
contribution governance and continuous integration, the Runtime Host
window's static `Identity` display, and the impossibility of a
simulation-only secured host (the endpoint composition requires at least
one endpoint).

ADR-0060 is closed.

## Deferred scope

- prebuilt GitHub releases, version numbering, and update channels;
- NuGet, PyPI, or other package-registry publication;
- a graphical configuration tool inside the Runtime Host;
- runtime editing or persistence of endpoint composition;
- non-Windows Runtime Host or Client;
- Linux USB serial discovery;
- contribution governance, CONTRIBUTING guidelines, issue templates, and
  continuous integration;
- community support channels; and
- credential lifecycle (rotation, revocation, recovery) in the onboarding
  path.
