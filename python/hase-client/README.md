# HASE Python Client

> **New users start here:**
> [Getting Started with HASE — Python API](../../docs/Getting-Started-Python.md)
> introduces the client with runnable examples, and the
> [Python Client API Reference](../../docs/API%20reference/HASE-Python-Client-API-Reference.md)
> documents every public function and model. This file is the engineering
> and provisioning record.

This directory contains the asyncio-native Python Client for the HASE Runtime
Host. The current implementation establishes an isolated package toolchain,
package-internal generated Python bindings, a strict external Runtime Host
profile model, and explicit Windows credential provisioning and recovery. It
also provides hash-bound local wheel distribution, private installed automation
environments, guarded workflows, and explicit Desktop/MiniPC target selection
from the Laptop. Runtime Hosts continue to own every physical connection and
hardware operation.

## Examples — inspect one Runtime Host

ADR-0052 begins with a user-oriented read-only example that consumes only the
public installed `hase-client 0.6.0` API. The example requires the external
Laptop target registry and one exact target ID; it never discovers, defaults,
fans out, fails over, redirects, retries, reconnects, or automatically selects a
Runtime Host.

Run the example with the Python interpreter from an environment where
`hase-client 0.6.0` is installed:

```powershell
python .\examples\inspect_runtime_host.py `
    --registry "<absolute-laptop-target-registry-path>" `
    --target desktop-runtime-host
```

or select `minipc-runtime-host`. The program resolves exactly one target, opens
one mutual-TLS channel, invokes `GetSnapshot` exactly once, closes the channel
deterministically, and prints descriptor inventory only. It does not read a
Property value, write a Property, execute a Command, subscribe to observations
or diagnostics, change authorization, or mutate hardware. Profile paths,
addresses, credential/trust paths and contents, Runtime Host identity,
attachment generation, and instrument serial numbers are not printed.
## Example — authoritative Property read

`examples/read_property.py` demonstrates one explicit authoritative Property read using the installed `hase-client 0.6.0` public API. Supply the external registry and exact target, endpoint, instrument, and Property identifiers. The example obtains one snapshot to bind the current attachment generation and then performs one authoritative read. It never retries, reconnects, falls back to cached data, writes, executes a Command, or subscribes.


## Example — bounded repeated Property sampling

`examples/sample_property.py` extends the single-read example into a bounded measurement session using only the installed `hase-client 0.6.0` public API. It requires explicit target, endpoint, instrument, Property, interval, and count; opens one channel; obtains one snapshot; reuses one attachment generation; and performs sequential authoritative reads on a monotonic schedule.

```powershell
python .\examples\sample_property.py `
    --registry "<absolute-laptop-target-registry-path>" `
    --target minipc-runtime-host `
    --endpoint arduino-uno-01 `
    --instrument arduino-uno-controller-01 `
    --property analog-input-voltage `
    --interval 1.0 `
    --count 5
```

The example stops on the first failed sample and never retries, reconnects, refreshes the snapshot, falls back to cached data, writes, executes a Command, or subscribes. It prints authoritative UTC timestamps, descriptor units, and Property quality to the console only.

## Example — bounded live Runtime Host observation

`examples/observe_runtime_host.py` demonstrates the public installed `hase-client 0.6.0` observation stream without polling. It requires an external target registry, one exact target, and a bounded live-observation count.

For example:

```powershell
python .\examples\observe_runtime_host.py `
    --registry "<absolute-laptop-target-registry-path>" `
    --target minipc-runtime-host `
    --count 1
```

The stream supplies its own initial snapshot, followed by ordered live observations. The initial snapshot does not consume the requested count. The example can present attachment, connection-status, Property-value, and Event observations, but never replays, reconnects, resubscribes, opens diagnostics, reads or writes a Property, or executes a Command.

Physical use with the Laptop MiniPC Python principal is accepted after ADR-0052 Increment 52D2 granted only the distinct `observation.subscribe` permission in addition to its existing snapshot and authoritative-Property-read grants. The resulting principal remains read-only: cached reads, Property writes, Commands, and diagnostics subscription are not authorized. Physical validation used one bounded stream and confirmed MiniPC `Controller/ButtonPressed` Event delivery; periodic Property observations may occur before or between Events, so the requested count bounds the whole Runtime Host observation stream rather than a specific Event kind.

## Example — guarded same-value Property write

`examples/write_same_value_property.py` is the first mutation example. It requires an external target registry, exact target, endpoint, instrument and Property, plus `--confirm-same-value-write`. There is deliberately no value argument: the example reads the current authoritative value and can write only that exact value back once.

```powershell
python .\examples\write_same_value_property.py `
    --registry "<absolute-laptop-target-registry-path>" `
    --target minipc-runtime-host `
    --endpoint arduino-uno-01 `
    --instrument arduino-uno-controller-01 `
    --property built-in-led-state `
    --confirm-same-value-write
```

After one snapshot and one initial authoritative read, a confirmed successful write is followed by one authoritative reconciliation read. Rejected or uncertain mutation outcomes stop immediately; there is no retry, replay, reconnect, second write, snapshot refresh, cached read, Command, observation, or diagnostics operation. Physical MiniPC use remains deferred until a separately approved increment grants the distinct `property.write` permission to the Laptop MiniPC Python principal.

## Example — guarded parameterless Command execution

`examples/execute_command.py` demonstrates one explicit parameterless Command mutation through the public API. It requires exact target, endpoint, instrument and Command path selection plus `--confirm-command-execution`. A successful run obtains one snapshot and executes the selected Command exactly once. Rejected or uncertain outcomes stop immediately; there is no retry, replay, reconnect, second snapshot, Property write, observation, or diagnostics operation.

Example shape:

```powershell
python .\examples\execute_command.py `
    --registry "<absolute-target-registry-path>" `
    --target minipc-runtime-host `
    --endpoint arduino-uno-01 `
    --instrument arduino-uno-controller-01 `
    --command Led Toggle `
    --confirm-command-execution
```

The exact Command path must match the descriptor published by the selected Runtime Host. Physical use requires the separate `command.execute` authorization established by ADR-0052 Increment 52G.

## Development environment

From this directory in an ordinary PowerShell window:

```powershell
.\tools\Initialize-HasePythonDevelopment.ps1
.\tools\Test-HasePythonDevelopment.ps1
```

Initialization requires 64-bit CPython 3.12 or 3.13 and creates `.venv` only
inside this directory. The virtual environment is excluded from Git and must
not be copied into source or deployment archives.

## Local wheel distribution

Build the versioned wheel into an empty output directory after the complete
regression suites pass:

```powershell
.\tools\Build-HasePythonPackage.ps1 `
    -OutputDirectory "<absolute-empty-output-directory>"
```

The tool builds without an editable installation or dependency payload,
requires the generated message and gRPC modules, rejects credential, profile,
rollback, cache, certificate, key, and repository content, and writes adjacent
sorted package-content and SHA-256 records. The wheel remains local; the tool
does not upload it or install it globally.

With the Runtime Host initially stopped, create the wheel first. Then keep the
laboratory's instruments connected and `Ready` in their safe state, start only
the Desktop Runtime Host, and run:

```powershell
.\tools\Test-HasePythonInstalledPackage.ps1 `
    -PackagePath "<absolute-hase-client-wheel-path>" `
    -ProfilePath "<absolute-published-python-profile-path>"
```

The validator creates a fresh isolated environment, installs only from the
wheel (dependencies may be obtained from the configured package index), clears
source-path injection, proves that the package import belongs to that
environment, checks every declared public export and all seven version-1 RPCs,
and performs exactly one read-only snapshot using the installed package. It
does not mutate hardware, retry, reconnect, change authorization, enable
diagnostics, print deployment values, or retain the validation environment.
Stop the Runtime Host immediately afterward.

## Persistent local automation environment

Install a verified local wheel into a new external automation directory:

```powershell
.\tools\Install-HasePythonAutomation.ps1 `
    -PackagePath "<absolute-hase-client-wheel-path>" `
    -InstallationDirectory "<absolute-new-automation-directory>"
```

The adjacent `<wheel>.sha256` record is mandatory and is verified before the
target directory is created. Installation uses a private virtual environment,
never an editable or global package. The target must not already exist; this
increment does not update or replace installations. A copied launcher and a
non-sensitive manifest record the schema, package version and SHA-256, CPython
version, and UTC installation time. Credentials, profiles, source paths, and
deployment identifiers are neither copied nor recorded.

With the laboratory's instruments in their safe state, the installed HASE
Client stopped, and remote diagnostics disabled, start only the Desktop
Runtime Host and invoke the installed read-only health workflow:

```powershell
& "<absolute-automation-directory>\Invoke-HasePythonAutomation.ps1" `
    -ProfilePath "<absolute-published-python-profile-path>"
```

The launcher uses only its installed private interpreter, clears Python source
path injection, opens one bounded mutual-TLS channel, obtains one version-1
snapshot without retry or reconnection, prints only fixed Boolean outcomes and
bounded inventory counts, and closes deterministically. It never reads or
writes a Property, executes a Command, observes events or diagnostics, changes
authorization, copies credentials, or changes instrument state. Stop the
Runtime Host immediately afterward.

Version 0.7.0 moves the laboratory's confirmed instrument workflows and
physical validations to the laboratory's own package, `hase-lab`, under
ADR-0068 68I2d; this package names no instrument and offers the read-only
workflows above.

## Independent multi-host security readiness

Desktop and MiniPC automation use independent Runtime Host identities,
server-certificate pins, Python client credentials, private keys, enrollment
records, authorization policy state, profiles, channels, attachment
generations, cancellation, and failure lifecycles. They may share the logical
principal `hase-python-automation`, but never a client private key or profile.
No workflow discovers profiles, redirects a target between hosts, fans out,
fails over, reconnects, or retries automatically.

Before provisioning a dedicated MiniPC Python identity, copy the current source
increment to the clean synchronized MiniPC repository, keep its Runtime Host
and installed Client stopped, and run the read-only readiness probe locally:

```powershell
.\tools\Test-HaseMiniPcPythonProvisioningReadiness.ps1 `
    -MiniPcConfigurationPath "<absolute-minipc-private-network-path>" `
    -TrustedServerCertificatePath "<absolute-public-minipc-certificate-path>" `
    -ApplicationProfilePath "<absolute-minipc-application-profile-path>" `
    -ProvisioningDirectory "<absolute-new-minipc-python-security-directory>" `
    -ProfileTemplatePath "<absolute-new-minipc-profile-template-path>" `
    -CertificatePath "<absolute-new-minipc-python-certificate-path>" `
    -PrivateKeyPath "<absolute-new-minipc-python-private-key-path>" `
    -ProfilePath "<absolute-new-minipc-python-profile-path>" `
    -RollbackDirectory "<absolute-new-external-rollback-directory>"
```

The probe reuses the strict credential-readiness boundary, verifies the chosen
public certificate exactly matches the active MiniPC server credential,
requires enrollment and any configured authorization policy to contain no
existing `hase-python-automation` entry, accepts an active MiniPC profile with
no optional authorization policy, rejects reparse points and retained transaction
artifacts, and requires every planned output to be absent, external, and
distinct. The dedicated Python security directory must also be absent while its
external parent already exists. Certificate, key, profile, template, rollback, enrollment,
authorization, or repository content is never created, copied, edited, or
deleted. Only fixed Boolean outcomes are printed. Credential creation,
publication, authorization, and Runtime Host connection remain deferred.

### Private MiniPC CPython prerequisite

The MiniPC uses a machine-local CPython 3.13.1 runtime and a freshly created
repository-local environment. It never reuses the Desktop `.venv`, invokes a
Python installer, registers Python, changes PATH, creates file associations, or
uses the Visual Studio Python 3.9 runtime. Download the official
`python-3.13.1-amd64.zip` artifact referenced by Python.org's Windows release
metadata, then run the private installer with its explicit external path:

```powershell
.\tools\Install-HaseMiniPcPrivatePython.ps1 `
    -RuntimeArchivePath "<absolute-python-3.13.1-amd64.zip-path>" `
    -InstallationDirectory "<absolute-new-private-runtime-directory>"
```

The tool requires SHA-256
`9877d0d24f7978407bde1b50ab1023b0f5c67ff6c9816b834e5258db1a636249`,
validates CPython 3.13.1 64-bit before publication, creates the local `.venv`,
installs only the pinned development requirements and editable HASE package,
and removes only its own new runtime/environment targets if installation fails.
The repository must be clean and synchronized. Credential and authorization
state is not read or changed.

### Dedicated MiniPC Python client authority

The MiniPC's self-signed Runtime Host server leaf remains unchanged and is
never used to issue client credentials. A separate MiniPC-only client CA may be
created with `New-HaseMiniPcPythonClientAuthority.ps1`. Its RSA-3072 private key
is non-exportable in Current User `My`; only its public certificate is added to
Current User `Root`. Explicit manifest and rollback-evidence paths are required.
The companion `Remove-HaseMiniPcPythonClientAuthority.ps1` removes the two exact
certificate-store records only after both evidence files and the certificate
SHA-256 agree. Authority creation does not create a Python leaf credential or
change enrollment, authorization, profiles, server identity, diagnostics, or
hardware state. MiniPC readiness receives the manifest thumbprint explicitly;
Desktop readiness retains its existing server-issuer selection behavior.

### MiniPC provisioning transaction preparation

`Initialize-HaseMiniPcPythonProvisioningTransaction.ps1` prepares—but does not
publish—the dedicated MiniPC Python transaction. It validates the explicit
client authority, active self-signed server leaf, clean repository, stopped
processes, absent Python targets, and the legacy application profile with no
authorization-policy reference. It creates only an external Python profile
template and a Current-User-only rollback directory. The rollback custody holds
exact enrollment and application-profile copies, candidate policy/profile
documents, and a seven-entry plan covering the new provisioning directory plus
the six publication files. Existing enrolled Client principals receive all six
previously effective non-diagnostic permissions; the new Python principal is
planned with snapshot and authoritative-read only. The companion
`Restore-HaseMiniPcPythonProvisioningPreparation.ps1` removes preparation state
only when every protected source hash remains exact and every publication
target remains absent. Leaf creation and publication remain deferred.

### Atomic MiniPC Python credential publication

`Publish-HaseMiniPcPythonCredential.ps1` consumes the protected 51D2A plan,
requires its recorded commit to remain an ancestor of the clean synchronized
repository, and revalidates every protected source hash and absent target. It
creates private custody, publishes the prepared initial policy, invokes the
reviewed credential operator for the certificate, key, profile, enrollment and
minimal Python grants, and revision-locks, writes, and verifies the application
profile last while the Runtime Host remains stopped.
An external outer journal makes partial completion explicit; failures never
start the Runtime Host and require the companion
`Recover-HaseMiniPcPythonCredentialPublication.ps1`. Recovery composes the
existing five-file operator recovery, restores the exact enrollment and
application-profile evidence, and removes only the new Python outputs, policy,
custody directory, and outer journal. The dedicated authority, preparation
evidence, server identity, diagnostics, and hardware state remain untouched.

### MiniPC authoritative Property validation

After publication and automated regression validation, start only the MiniPC
Runtime Host and first validate one read-only snapshot. Then validate the
second and only other Python grant with:

```powershell
.\tools\Test-HaseMiniPcPythonAuthoritativeProperty.ps1 `
    -ProfilePath "<absolute-minipc-python-profile-path>"
```

The tool gets one snapshot, resolves exactly one current Ready
`arduino-uno-controller-01`/`analog-input-voltage` descriptor with readable
numeric access and a declared range, performs exactly one authoritative read,
and requires a finite `GOOD` result with an exact UTC timestamp inside that
range. It prints only fixed Boolean outcomes and withholds the voltage,
identities, attachment generation, timestamp, address, paths, and credentials.
It never retries, reconnects, reads cached data, writes, executes a Command,
subscribes, changes authorization, or changes Arduino state.

### Installed MiniPC authoritative Property workflow

Version 0.5.0 packages the same read-only boundary for a fresh persistent
automation installation. After building, transferring, hash-verifying, and
installing the wheel outside the repository, invoke only its copied launcher:

```powershell
& "<absolute-minipc-automation-directory>\Invoke-HasePythonAutomation.ps1" `
    -Workflow MiniPcAuthoritativePropertyRead `
    -ProfilePath "<absolute-minipc-python-profile-path>"
```

The launcher clears `PYTHONPATH`, changes its working directory to the external
installation, and invokes only the package installed in that environment. The
workflow gets one snapshot, resolves the current Ready Arduino A0 numeric
descriptor, performs exactly one authoritative read, validates its `GOOD` UTC
result against the descriptor range, and closes the channel. It needs only the
two published MiniPC Python permissions. No confirmation switch is accepted or
required because the workflow cannot mutate state. It never retries,
reconnects, reads cached data, writes, executes a Command, subscribes, enables
diagnostics, or changes authorization.

## Laptop automation target registry

Version 0.6.0 adds an explicit two-target registry for Python automation that
runs beside—but independently from—the installed WPF Client. The registry is
external JSON configuration and contains no endpoint address, certificate, or
private-key value itself. It maps the two exact approved target IDs to two
strict external Runtime Host profiles:

```json
{
  "formatVersion": 1,
  "targets": [
    {
      "targetId": "desktop-runtime-host",
      "displayName": "Desktop Runtime Host",
      "profilePath": "<absolute-desktop-python-profile-path>"
    },
    {
      "targetId": "minipc-runtime-host",
      "displayName": "MiniPC Runtime Host",
      "profilePath": "<absolute-minipc-python-profile-path>"
    }
  ]
}
```

The loader requires exactly these two IDs, unique absolute profile paths,
distinct Runtime Host addresses, and six distinct certificate/key custody
paths across the profiles. Registry and profile paths must be regular files,
must not traverse reparse points, and can be excluded from repository and
installation roots by the caller. Both profiles are loaded strictly without
reading credential bytes. Unknown fields, implicit targets, shared profiles,
shared credentials, shared server pins, and automatic target selection are
rejected.

An installed launcher accepts either the existing direct `-ProfilePath` or the
paired `-TargetRegistryPath` and `-TargetId`; supplying both modes, neither
mode, or an incomplete registry mode is rejected. For example:

```powershell
& "<absolute-laptop-automation-directory>\Invoke-HasePythonAutomation.ps1" `
    -Workflow Health `
    -TargetRegistryPath "<absolute-laptop-target-registry-path>" `
    -TargetId "desktop-runtime-host"
```

Target resolution runs only through the installed private interpreter with
`PYTHONPATH` cleared. It selects one profile and one workflow; it never
discovers, fans out, fails over, retries, or redirects between hosts.

Before Laptop credential provisioning or physical connection, validate a
prepared registry and external installation custody with:

```powershell
.\tools\Test-HaseClientPythonAutomationReadiness.ps1 `
    -TargetRegistryPath "<absolute-laptop-target-registry-path>" `
    -InstallationDirectory "<absolute-laptop-automation-directory>"
```

The readiness tool loads both profiles without reading credential contents,
excludes the repository and installed automation roots, prints only fixed
Boolean outcomes, and never opens a channel or performs a Runtime Host RPC.
Credential creation, publication, import, and physical validation are separate
explicit transactions described below.

### Laptop-to-MiniPC credential readiness

The Laptop uses a new `hase-laptop-python-minipc` principal and never reuses
the MiniPC-local `hase-python-automation` credential, either WPF Client
credential, or any Desktop Python credential. Before credential creation, run
the paired read-only readiness tools with every path explicit.

On the stopped MiniPC, invoke
`Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1` with its active private
network configuration and application profile, the dedicated client-authority
manifest and rollback evidence, and new external staging, certificate, key,
profile, transfer-archive, and rollback targets. The tool verifies the exact
non-exported authority custody, absence of the Laptop principal and grants,
and preservation of the MiniPC-local Python enrollment with exactly snapshot
and authoritative-read access. All planned targets must be absent, distinct,
outside the repository, and free of reparse-point traversal.

On the stopped Laptop, invoke
`Test-HaseClientMiniPcPythonCredentialReadiness.ps1` with its existing Desktop
Python profile and new external paths for the 0.6.0 installation, MiniPC
credential custody, certificate, key, profile, two-target registry, incoming
transfer archive, and rollback directory. The existing Desktop profile is
loaded strictly without reading credential bytes. Planned MiniPC custody must
be absent, distinct from the Desktop profile and installation, and outside the
repository.

Both tools require a clean synchronized repository and stopped Runtime Host
and WPF Client processes. They print only fixed Boolean outcomes and never
create a certificate, profile, registry, archive, directory, rollback record,
enrollment, or grant; they never open a channel or operate hardware. Issuance,
transactional publication, transfer, and Laptop installation remain deferred.

After both readiness checks pass, the MiniPC-only
`Initialize-HaseMiniPcLaptopPythonCredentialTransaction.ps1` tool prepares the
Laptop transaction without issuing or publishing a credential. It requires
machine `LABC`, reuses the strict MiniPC readiness boundary, verifies the
dedicated non-exportable authority and active public server certificate, and
records a Current-User-only profile template plus exact enrollment,
authorization-policy, and application-profile originals. Its transaction plan
revision-locks five absent publication targets and all three active files,
identifies only `hase-laptop-python-minipc`, and records exactly snapshot and
authoritative-read grants. The staging directory, credential files, transfer
archive, enrollment, authorization policy, and application profile remain
unchanged. The paired restore tool removes preparation evidence only while all
revision-locked active and absent publication state still matches the plan.

`Publish-HaseMiniPcLaptopPythonCredential.ps1` consumes that plan through a
dedicated `provision-laptop-minipc` operator command. The existing local
`provision` command remains fixed to `hase-python-automation`; the new command
is fixed to `hase-laptop-python-minipc`. The operator publishes one enrollment
and exactly snapshot and authoritative-read grants against the active locked
files. A private outer journal makes incomplete outcomes recovery-only.

After credential publication, the tool rewrites only the unpublished transfer
profile to four explicit Laptop-local certificate, key, profile, and MiniPC
server-trust paths. It records hashes and destination paths in a transfer
manifest and creates a Current-User-only archive containing only the client
certificate chain, private key, Runtime Host profile, and manifest. Runtime
Host and Client processes remain stopped. The paired recovery tool restores
all three active files exactly, removes credential and archive outputs, and
retains the original 51E2B1 preparation evidence. Laptop installation,
connection, RPCs, and hardware operations remain outside publication and are
performed only by a later explicit installed-workflow validation.

On Laptop `LTAEP`, `Import-HaseClientMiniPcPythonCredential.ps1` validates the
transferred archive in place before extraction: exactly four root entries,
strict manifest identity and destination paths, and SHA-256 for all credential
payloads. It extracts only into a private rollback-owned stage, validates the
certificate/key pair, then publishes protected MiniPC custody and an exact
Desktop/MiniPC two-target registry. Both profiles are loaded strictly without
opening a channel, and the Desktop profile remains byte- and ACL-exact. The
incoming archive is removed only after registry validation; secured import
evidence is retained. A sanitized phase journal makes incomplete import
explicit-recovery-only. The paired recovery tool removes only the new MiniPC
custody and registry while refusing any changed Desktop profile. Package
installation, channel creation, RPCs, and hardware operations remain outside
import and require a later explicit installed-workflow invocation.

The publication journal records sanitized post-credential phases for custody
creation, profile rewriting, published-scope validation, manifest creation,
and archive creation. A failure reports only that phase and remains explicit-
recovery-only. Transfer packaging binds an explicit four-file input list and
verifies that a non-empty archive exists before committing the outer journal.
The operator-created profile retains and verifies its protected ACL. The newly
created manifest and the archive outside staging each receive and verify an
explicit Current-User-only protected ACL.

If Laptop readiness finds a Desktop Python profile whose three custody paths
still refer to the MiniPC user root, use the paired Laptop-only custody repair
and restore tools. Repair requires machine `LTAEP`, exact stale-root evidence,
a locally matching certificate/key pair, distinct Desktop and MiniPC server
certificates, a clean synchronized repository, and a new external rollback
file. It changes only the three profile paths, preserves the address, records
exact original bytes, SHA-256, and security metadata, and verifies the strict
profile without opening a channel. A retry after a safe pre-publication failure
accepts retained rollback evidence only when its purpose, target, original
bytes, and SHA-256 match the still-unchanged profile exactly. The corrected
profile is committed in place, its ACL must remain unchanged, and all three
corrected paths are verified explicitly. Restore accepts only matching evidence.

### Accepted Laptop two-target installation

The accepted `hase-client 0.6.0` wheel has SHA-256
`4abd42ccb529703560c3f8e400b50c9cd290d319671ed9040d13ac131eb4df2c`.
It was hash-verified and installed into a fresh private Laptop automation
environment. The external two-target registry and both strict profiles remain
outside that installation.

Physical closure selected `desktop-runtime-host` and `minipc-runtime-host`
individually from the installed launcher. Both Health workflows received valid
snapshots with every expected endpoint Ready. One installed
`MiniPcAuthoritativePropertyRead` resolved and authoritatively read Arduino A0.
Every channel closed cleanly, all workflows succeeded, and all nine protected
Laptop registry, profile, credential, key, and trust files remained unchanged.

After validation the transferred Laptop wheel and hash copies, the MiniPC
credential transfer archive, and the duplicate MiniPC staging custody were
removed. The installed environment, target registry, Laptop credential
custody, rollback evidence, MiniPC preparation evidence, and MiniPC profile
template remain. Runtime Hosts, WPF Client, and installed automation were
stopped; the laboratory's instruments remained in their safe state.

## Generated Runtime Host contract

The only authoritative protobuf source is:

```text
src\Hase.Runtime.Remote.Grpc.Contracts\Protos\runtime_host_remote_api_v1.proto
```

Generate package-internal Python bindings and verify freshness:

```powershell
.\tools\Generate-HasePythonContracts.ps1
.\tools\Test-HasePythonContractsCurrent.ps1
```

Generated modules remain internal. Descriptor-level parity tests lock the
reviewed version-1 wire shape.

## Strict external profile

`load_runtime_host_profile` reads one versioned JSON profile by absolute path.
It requires an explicit HTTPS IP address and absolute paths to three distinct
external files: the Client certificate chain, Client private key, and exact
trusted-server certificate. Increment 50C1 checks file custody only and does
not parse credential bytes.

## Mutual-TLS channel

`open_runtime_host_channel` creates one asyncio gRPC channel from a validated
profile. Credential reads are bounded, the exact trusted Runtime Host
certificate is supplied as the channel trust anchor, TLS authority is never
overridden, readiness has an explicit timeout, and opening is never retried.
The returned `RuntimeHostChannel` is an async context manager with deterministic
concurrent and repeated close behavior. Channel failures expose only sanitized
codes and never include credential bytes or deployment paths. Increment 50D1
does not invoke an RPC or connect during automated validation.

After automated validation succeeds with the Runtime Host stopped, one
explicit Desktop-only physical handshake can be performed with:

```powershell
.\tools\Test-HasePythonPhysicalChannel.ps1 `
    -ProfilePath "<absolute-published-python-profile-path>"
```

The tool loads the strict external profile, opens one bounded mutual-TLS
channel, waits for HTTP/2 readiness, and closes it exactly once. It prints only
four fixed Boolean outcomes. It does not invoke an RPC, retry, reconnect,
operate hardware, modify deployment state, or print deployment values. Start
only the Desktop Runtime Host immediately before this physical check and stop
it again immediately afterward.

## Runtime Host snapshot model

`project_runtime_host_snapshot` converts the package-internal generated
`GetSnapshotResponse` into a deeply immutable public model. Runtime Host API
version, endpoint and attachment identity, connection state and UTC timestamp,
and the complete ordered Instrument, Property, Command, Event, and data
descriptor graphs are preserved. Optional transport fields remain distinct
from present empty strings. Authoritatively absent connection timestamps,
numeric ranges and resolutions, Command arguments, and Event payloads are
preserved as `None`. Unspecified enums, incomplete required nested messages,
unknown data kinds, invalid numeric descriptors, and inconsistent endpoint
identity are rejected with sanitized error codes.

Increment 50D3A defines and tests only this transport-independent projection.
It does not invoke `GetSnapshot`, start the Runtime Host, connect, retry,
reconnect, read a Property, or interact with hardware.

`RuntimeHostClient.get_snapshot` now invokes exactly one bounded asynchronous
`GetSnapshot` operation over a caller-owned `RuntimeHostChannel` and returns
that immutable model. Generated request, response, and stub objects remain
internal. Caller cancellation propagates, while authentication, authorization,
deadline, availability, cancellation-status, and unexpected RPC failures map
to stable sanitized codes. The operation never retries or reconnects and does
not close the caller's channel. Increment 50D3B1 validates this behavior only
with isolated fakes; it does not connect to a Runtime Host or operate hardware.

After all automated validation succeeds with the Runtime Host stopped, the
authorized snapshot boundary can be physically validated once on the Desktop:

```powershell
.\tools\Test-HasePythonPhysicalSnapshot.ps1 `
    -ProfilePath "<absolute-published-python-profile-path>"
```

Start only the Desktop Runtime Host immediately before this check and stop it
again immediately afterward. The Laptop and installed HASE Client remain
uninvolved. The tool loads the profile, opens one bounded channel, invokes
`GetSnapshot` exactly once, verifies the immutable snapshot and supported API
major version, and closes exactly once including failure paths. It prints only
six fixed Boolean outcomes and never prints inventory, deployment, profile,
path, or credential values. It does not retry, reconnect, read a Property,
modify deployment state, or operate hardware.

## Authoritative Property model

`PropertyTarget`, `PropertyValue`, and `PropertyOperationResult` define the
immutable public boundary for a later authoritative Property operation.
Projection preserves Boolean, string, finite numeric, byte-array, and absent
values; exact UTC timestamps; quality; every version-1 operation status; and
the presence distinction of optional diagnostic text. Byte arrays are detached
immutable `bytes`. Successful results must contain exactly one confirmed value
and no diagnostic, while failed results must not contain a confirmed value.
Malformed identities, timestamps, enums, numeric values, and result shapes are
rejected with sanitized codes.

Increment 50D4A defines and tests only these transport-independent models. It
does not invoke an RPC, open a channel, start the Runtime Host, retry, reconnect,
read or write a Property, or interact with hardware.

`RuntimeHostClient.read_authoritative_property` accepts one validated immutable
target, encodes its four identity fields exactly, invokes
`ReadAuthoritativeProperty` exactly once with a bounded timeout, and returns the
immutable normalized result. Generated transport objects remain internal.
Caller cancellation propagates; authorization and transport failures use the
same stable sanitized codes as the snapshot operation. It never retries,
reconnects, closes the caller-owned channel, or falls back to cached data.
Increment 50D4B1 validates this behavior only with isolated fakes and performs
no physical read or hardware interaction.

The physical validation of one authoritative read against a real instrument
lives with the laboratory's package since ADR-0068 68I2d; this package
validates the operation with isolated fakes only.

## Mutation safety foundation

`normalize_mutation_value` accepts only Boolean, string, immutable byte-array,
and finite numeric values. Integers are converted to the version-1 wire
`double` only when the conversion is exact. Absent values, mutable byte arrays,
unsupported types, non-finite numbers, overflow, and lossy integers fail before
any transport object or RPC exists. The exact generated `RemoteValue` encoder
remains package-internal.

`RuntimeHostMutationError` classifies mutation failures as `NOT_SENT`,
`REJECTED`, or `OUTCOME_UNCERTAIN`, exposes only a stable sanitized code, and
never permits automatic retry. An uncertain outcome must be reconciled by an
authoritative read or explicit operator action before another mutation is
considered. Increment 50E1A defines only these value and failure semantics; it
does not invoke `WriteProperty` or `ExecuteCommand`, change authorization,
connect, or operate hardware.

The package-internal mutation transport boundary invokes one prepared call
exactly once. A failure constructing that call is `NOT_SENT`; explicit server
authentication, authorization, validation, precondition, target, range, or
unsupported-operation statuses are `REJECTED`; cancellation and all ambiguous
post-invocation transport failures are `OUTCOME_UNCERTAIN`. Exception details
remain sanitized and no classification permits automatic retry. Increment
50E1B still exposes no public mutation operation and invokes neither
`WriteProperty` nor `ExecuteCommand`.

Returned `WriteProperty` results also have package-internal mutation semantics.
A valid success preserves its confirmed authoritative value. Attachment, target,
write-access, value, endpoint-unavailable, and endpoint-rejected results are
sanitized `REJECTED` failures. Endpoint failure and timeout are
`OUTCOME_UNCERTAIN`. A malformed result, unspecified status, or the impossible
write result `READ_NOT_SUPPORTED` is also uncertain because it cannot prove
execution or non-execution. Raw Runtime Host diagnostics are never exposed by
mutation exceptions. Increment 50E2A adds only this projector and does not make
`WriteProperty` publicly callable.

`RuntimeHostClient.write_property` is the public version-1 Property mutation
boundary. It validates the target, value, and bounded timeout before transport;
constructs one exact `WritePropertyRequest`; invokes `WriteProperty` exactly
once; and returns only a valid confirmed authoritative success. Rejection and
uncertain-outcome failures retain the mutation classifications above, expose no
raw diagnostic, and never permit automatic retry, replay, or reconnection. The
caller must reconcile an uncertain outcome with an authoritative read or
explicit operator action before considering another write. Increment 50E2B
does not add authorization or perform a physical write.

## Authorized physical Property-write validation

`Enable-HasePythonPropertyWrite.ps1` performs one fixed-purpose authorization
transaction for the existing `hase-python-automation` principal. It requires
the exact read-only grant set, locks the input policy revision, appends only
`property.write`, and activates that policy in the legacy Desktop application
profile while keeping remote diagnostics disabled. Both input revisions are
locked, both candidates are staged before publication, the profile is
published last, file security is preserved, and exact rollback files are
retained for both inputs. It never changes credential or enrollment files and
cannot grant Command, observation, or diagnostic permissions.

The physical validation of one same-value write against a real instrument
lives with the laboratory's package since ADR-0068 68I2d.

## Credential-provisioning readiness

The installed WPF Client private key is intentionally non-exportable and must
not be reused for Python. Python automation requires a separate Client identity
issued and enrolled through an explicit later provisioning increment.

Before that increment, run the read-only Windows readiness probe against the
retained external Desktop validation configuration:

```powershell
.\tools\Test-HasePythonCredentialProvisioningReadiness.ps1 `
    -DesktopConfigurationPath "<external desktop-private-network.json>"
```

The probe verifies the configured server credential, unique signing root and
private signing-key availability, matching Current User trust anchor, and
strict enrollment-document custody. It prints only fixed Boolean results. It
does not inspect the WPF private key, create or export a credential, modify an
enrollment, print deployment values, connect to the Runtime Host, or perform a
hardware operation.

The configuration, enrollment, profile, and every credential remain external
deployment state. They must not be committed, included in archives, printed,
or logged.

The .NET provisioning boundary can now prepare a revision-locked candidate set
entirely in memory after an approved plan has been created. It revalidates the
dedicated credential, signing root, source revisions, enrollment, authorization
policy, and Python profile before returning five disposable candidate buffers.
This preparation step does not publish files, create backups or a journal,
modify certificate stores, or connect to a Runtime Host.

An approved prepared set can now be published through a durable five-file
transaction. A bounded metadata-only journal is made durable before staging,
then updated after the candidate files have been staged and flushed and after
every publication boundary. Authorization is published last. Ordinary failures
restore the exact pre-transaction files and security metadata. A retained
journal represents an interrupted or committed-cleanup case; another
publication remains blocked until the explicit recovery boundary validates the
expected five target paths and all retained hashes. Uncommitted transactions
are rolled back; committed transactions retain their published candidates and
complete cleanup. Ambiguous, corrupt, substituted, or hash-mismatched evidence
is left untouched for operator review.

## Credential-provisioning operator

The Windows-only .NET operator composes the reviewed planning, preparation,
publication, and recovery boundaries without exposing credential or deployment
contents. Every path and deployment identifier is explicit; no security path is
discovered or defaulted. Successful provisioning reports only the plan and
transaction identifiers, replacement status, and a fixed withholding marker.
Failures report only a sanitized error code.

Run the operator from the repository root with `dotnet run --project
src/Hase.Python.CredentialProvisioning.Operator -c Release --`, followed by one
of these operations:

```text
provision --signing-root-thumbprint <value> --trust-policy-id <value> --source-profile <absolute-path> --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --validity-days <1-90> [--allow-replacement]

recover --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path>
```

`--allow-replacement` authorizes replacement only for that invocation. A
retained journal blocks another publication until the exact five targets are
supplied to the explicit `recover` operation. The operator does not connect to
a Runtime Host or modify a certificate store.

Before physical publication, run
`tools\Initialize-HasePythonPhysicalProvisioningValidation.ps1` with explicit
absolute paths for the retained Desktop configuration, an existing public
Runtime Host certificate, authorization policy, provisioning directory, three
Python output files, a new external Python profile-template path, and a new
rollback directory outside both the repository and provisioning directory. The
installed Client and its configuration are not required. The template path
must not already exist and
must remain outside the repository. The script creates that template from only
the authoritative Desktop Runtime Host binding, the explicitly selected public
trusted-server certificate path, and the planned new Python certificate and
private-key output paths. It rejects a public certificate that differs from
the active Runtime Host certificate or contains a private key. It never opens,
exports, copies, or references the WPF Client certificate or private key.
The script requires the Runtime Host and Client to be stopped and the default
branch to be clean and synchronized. It reuses the strict readiness probe,
rejects transaction artifacts and reparse points, and creates a Current-User-
only rollback capture containing exact content, SHA-256, security descriptors,
and the withheld operator input document. Only fixed Boolean readiness results
are displayed. The rollback directory is external security state and must not
be committed, copied into a source archive, or displayed.

## Current scope

The package (version 0.6.0) currently provides:

- distribution name `hase-client` and import namespace `hase`;
- reproducible version-1 protobuf and gRPC bindings with byte-exact
  freshness and descriptor-level parity validation;
- an immutable strict external Runtime Host profile model and the exact
  two-target Laptop automation registry;
- an asyncio mutual-TLS channel lifecycle with bounded readiness;
- immutable snapshot, Property, Command, observation, and diagnostic
  models with strict transport projection;
- bounded, non-retrying operations for snapshot, authoritative and cached
  Property reads, Property writes, and Command execution, with strict
  mutation values and explicit uncertain-outcome semantics;
- one caller-owned live observation stream and one caller-owned
  authorized diagnostic stream, both strictly ordered and never
  resubscribed; and
- dedicated-Python-identity provisioning, durable five-file publication,
  and explicit operator and interrupted-publication recovery boundaries.

The complete public API is documented in the
[Python Client API Reference](../../docs/API%20reference/HASE-Python-Client-API-Reference.md).

## Command execution

`RuntimeHostClient.execute_command` accepts an immutable `CommandTarget` and
an optional typed argument. It invokes `ExecuteCommand` exactly once with a
bounded timeout. It never retries, replays, reconnects, or exposes remote
diagnostic text. Returned rejections are explicit; transport loss, timeout,
cancellation, endpoint failure, and malformed results are classified as an
uncertain mutation outcome.

Command execution is independently authorized with `command.execute`. The
`Enable-HasePythonCommandExecution.ps1` operator tool revision-locks the active
policy, appends only that grant to the exact Python principal, publishes by
replacement, preserves effective access control, and retains the exact prior
policy as rollback evidence.

## Observation streaming

`RuntimeHostClient.observe()` returns one caller-owned async stream. Its first
item is a typed authoritative snapshot boundary; subsequent items are typed,
strictly contiguous attachment, connection, Property, or event observations.
Malformed messages, repeated or missing snapshots, sequence gaps, and sanitized
RPC failures terminate the stream. Closing or cancelling the iterator cancels
that subscription; the client never reconnects, replays, or resubscribes.

Live observations require the independent `observation.subscribe` permission.
`Enable-HasePythonObservation.ps1` revision-locks the active policy, appends only
that grant to the exact Python principal, preserves effective access control,
and retains the exact previous policy as rollback evidence. Diagnostic streams
remain outside this increment and remote diagnostics remain disabled.

## Cached Property reads

`RuntimeHostClient.read_cached_property()` reads exactly one Runtime Host cache
entry without contacting the endpoint and without authoritative fallback. The
typed result preserves target, descriptor, connection state, value, timestamp,
and quality. It requires the independent `property.cached.read` permission.

## Diagnostic observation

`RuntimeHostClient.observe_diagnostics()` opens exactly one caller-owned,
authorized diagnostic stream. Immutable records preserve stream and source
sequence, Runtime Host and endpoint/generation scope, level, category,
severity, direction, operation correlation, duration, outcome, details, and
captured byte fragments with their original length and truncation state.
Malformed records, gaps, sanitized authorization failures, and cancellation
terminate that stream. The client never retries, reconnects, resubscribes,
replays, or synthesizes a diagnostic record.

Remote diagnostics require both the independent `diagnostics.subscribe` grant
and explicit Runtime Host profile enablement. The paired 50I enable and restore
tools revision-lock both files, preserve their access control, retain exact
rollback bytes, and return the validated installation to disabled state after
the bounded physical validation.
