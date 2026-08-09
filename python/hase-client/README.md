# HASE Python Client

This directory contains the asyncio-native Python Client for the HASE Runtime
Host. The current implementation establishes an isolated package toolchain,
package-internal generated Python bindings, a strict external Runtime Host
profile model, and explicit Windows credential provisioning and recovery. It
does not yet open a Runtime Host connection or invoke hardware.

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
KEL-103 connected and `Ready` in CC/OFF state with the external laboratory
supply output OFF, start only the Desktop Runtime Host, and run:

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

With the KEL-103 in CC/OFF state, the laboratory supply output OFF, the
installed HASE Client stopped, and remote diagnostics disabled, start only the
Desktop Runtime Host and invoke the installed read-only health workflow:

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

### Guarded same-value KEL-103 Property workflow

Version 0.3.0 adds one explicitly confirmed mutation workflow to a newly
installed persistent automation environment:

```powershell
& "<absolute-automation-directory>\Invoke-HasePythonAutomation.ps1" `
    -Workflow Kel103SameValuePropertyWrite `
    -ProfilePath "<absolute-published-python-profile-path>" `
    -ConfirmSameValueWrite
```

The launcher rejects the workflow unless the dedicated confirmation switch is
present, and passes a second fixed confirmation token to the package-internal
module. The workflow obtains one current snapshot, resolves exactly one ready
`electronic-load-01`, authoritatively verifies CC mode and input OFF, reads
`target-current`, writes that exact numeric value once, requires the returned
confirmation to match, and performs one authoritative reconciliation read.
Values, identities, generations, addresses, paths, and raw diagnostics are
withheld.

No failure path retries, replays, reconnects, falls back to cached data, or
issues a second write. An uncertain outcome stops immediately and requires
operator reconciliation outside the workflow. The workflow never changes a
setpoint, selects a mode, activates the input, or executes a Command. The
retained ADR-0050 authorization already includes `property.write`; physical use
must not modify the authorization policy or application profile.

### Guarded same-state KEL-103 Command workflow

Version 0.4.0 adds one explicitly confirmed parameterless Command workflow to
a newly installed persistent automation environment:

```powershell
& "<absolute-automation-directory>\Invoke-HasePythonAutomation.ps1" `
    -Workflow Kel103SameStateCcCommand `
    -ProfilePath "<absolute-published-python-profile-path>" `
    -ConfirmSameStateCommand
```

The launcher rejects missing, unrelated, or crossed confirmation switches
before inspecting the profile and passes a second fixed confirmation token to
the package-internal module. The workflow obtains one current snapshot,
resolves exactly one ready `electronic-load-01`, requires exactly one
parameterless `Mode/SelectConstantCurrent` descriptor, authoritatively verifies
CC mode and input OFF, executes that Command once, and authoritatively requires
the resulting state to remain exactly CC/OFF.

No failure path retries, replays, reconnects, writes a Property, or issues a
second Command. An uncertain outcome stops immediately; because the initial and
intended state are identical, a later CC/OFF read cannot prove whether an
uncertain Command executed. Values, identities, generations, addresses, paths,
and raw diagnostics are withheld. The workflow never selects CV, CR, CW, or
SHORT and never activates or deactivates the input. The retained ADR-0050
authorization already includes `command.execute`; physical use must not modify
the authorization policy or application profile.

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
    -AuthorizationPolicyPath "<absolute-minipc-authorization-policy-path>" `
    -ProvisioningDirectory "<absolute-existing-minipc-python-security-directory>" `
    -ProfileTemplatePath "<absolute-new-minipc-profile-template-path>" `
    -CertificatePath "<absolute-new-minipc-python-certificate-path>" `
    -PrivateKeyPath "<absolute-new-minipc-python-private-key-path>" `
    -ProfilePath "<absolute-new-minipc-python-profile-path>" `
    -RollbackDirectory "<absolute-new-external-rollback-directory>"
```

The probe reuses the strict credential-readiness boundary, verifies the chosen
public certificate exactly matches the active MiniPC server credential,
requires both enrollment and authorization state to contain no existing
`hase-python-automation` entry, rejects reparse points and retained transaction
artifacts, and requires every planned output to be absent, external, and
distinct. Certificate, key, profile, template, rollback, enrollment,
authorization, or repository content is never created, copied, edited, or
deleted. Only fixed Boolean outcomes are printed. Credential creation,
publication, authorization, and Runtime Host connection remain deferred.

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

After all automated validation succeeds with the Runtime Host stopped, one
authoritative measured-voltage read can be physically validated on the Desktop:

```powershell
.\tools\Test-HasePythonPhysicalAuthoritativeProperty.ps1 `
    -ProfilePath "<absolute-published-python-profile-path>"
```

Keep the KEL-103 connected and `Ready` in CC/OFF state with the external
laboratory supply output OFF. Start only the Desktop Runtime Host immediately
before the check and stop it immediately afterward; the Laptop and installed
HASE Client remain uninvolved. The tool gets one snapshot, resolves exactly one
current `electronic-load-01`/`measured-voltage` target with readable access,
performs exactly one authoritative read, requires a finite `GOOD` numeric result
with a UTC timestamp, and closes exactly once including failure paths. It prints
only seven fixed Boolean outcomes and withholds all identities, generations,
measurement values, timestamps, paths, addresses, and credential information.
It never retries, reconnects, reads cached data, writes, executes a Command, or
changes KEL-103 state.

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

`Test-HasePythonPhysicalPropertyWrite.ps1` requires exactly one ready KEL-103,
authoritatively verifies CC/OFF, reads `target-current`, writes
that same numeric value exactly once, verifies the returned confirmation, and
performs one authoritative reconciliation read. It never changes the setpoint,
activates the load, selects a mode, executes a Command, retries, reconnects, or
replays a mutation. Any uncertain outcome stops the run without another write.

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

The package currently provides:

- distribution name `hase-client` and import namespace `hase`;
- reproducible version-1 protobuf and gRPC bindings;
- byte-exact freshness and descriptor-level parity validation;
- an immutable strict external Runtime Host profile model; and
- an asyncio mutual-TLS channel lifecycle with bounded readiness; and
- immutable Runtime Host snapshot models with strict transport projection; and
- one bounded, non-retrying asynchronous Runtime Host snapshot operation; and
- immutable authoritative Property-operation models and strict projection; and
- one bounded, non-retrying authoritative Property-read operation; and
- strict mutation values and explicit uncertain-outcome semantics; and
- dedicated-Python-identity provisioning, durable five-file publication, and
  an explicit operator and interrupted-publication recovery boundaries.

Observations require a later approved increment.

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
