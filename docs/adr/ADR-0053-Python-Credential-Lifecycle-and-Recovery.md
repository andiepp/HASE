# ADR-0053 — Python Credential Lifecycle and Recovery

- Status: Accepted objective; Stage 53B implementation
- Date: 2026-08-11

## Context

ADR-0050 established the external asyncio-native Python Client boundary and
its dedicated mutual-TLS identity. ADR-0051 established revision-locked initial
provisioning, durable five-file publication, interrupted-publication recovery,
private installed automation, and explicit Desktop/MiniPC target selection.
ADR-0052 physically validated repository-backed laboratory examples and closed
at 569 Python and 5,924 .NET tests.

Initial provisioning is intentionally not credential rotation. Its planner
rejects an already-authorized principal and an already-enrolled credential,
while `--allow-replacement` authorizes only replacement of client output files.
Operational use now requires an explicit lifecycle beyond first enrollment
without weakening Runtime Host identity, authorization, mutation safety,
credential custody, or multi-host independence.

## Decision

### Lifecycle ownership

Credential inspection, issuance, rotation, revocation, replacement, and
recovery remain explicit offline deployment operations in the Windows .NET
credential-provisioning boundary. The Python `hase` API does not provision or
rotate credentials, edit deployment configuration, reconnect, retry, fail
over, or select another Runtime Host after authentication failure.

Every lifecycle operation binds exactly one target identifier, Runtime Host
server identity and trusted-server certificate, enrollment registry,
authorization policy, principal, installed profile, current credential, and
replacement credential. Desktop and MiniPC transitions are independent and a
credential is never shared between them.

### Expiry and inspection

The existing one-to-ninety-day issuance range remains unchanged. A selected
credential is `Active` outside its final thirty days, `RotationDue` at thirty
days or less, `Expiring` at seven days or less, `Expired` at or after its exact
`NotAfter` boundary, and `NotYetValid` before `NotBefore`. No state causes an
automatic renewal or rotation.

Lifecycle inspection is offline and read-only. Before returning a time state it
validates the strict profile, certificate/private-key match, client-authentication
usage, selected credential identity, exact credential-to-principal enrollment,
trust-policy identity, exact expected authorization grants, trusted-server
certificate custody, UTC validity, and hashes of all deployment inputs. Missing,
corrupt, substituted, cross-host, or widened state fails with a sanitized code.

### Planned rotation

Planned rotation uses a bounded overlap. A new certificate and private key are
issued with a new credential identity. The enrollment registry temporarily maps
both old and new identities to the same existing principal and trust policy.
The authorization policy remains byte-exact and its effective grant set remains
exactly unchanged.

After the replacement is staged on the Client computer, installed automation
for that target is stopped and the credential/profile custody is atomically
selected. Validation uses a fresh process and channel; a running channel is
never assumed to reload replaced files. Only after the new credential succeeds
is the old enrollment removed. A fresh old-credential connection must then be
rejected before obsolete private-key custody is destroyed.

The target registry normally remains byte-exact because its stable target entry
continues to reference the same profile path. The profile continues to reference
the same host-specific credential paths and trusted-server certificate. No
automatic target selection, discovery, fan-out, or failover is added.

### Authorization and revocation

Rotation changes credential enrollment and Client credential custody, not the
principal or its permissions. The transition records the exact initial grant
set and requires exact equality after every publication boundary. An absent
permission remains absent. For `hase-laptop-python-minipc` the accepted set is
exactly snapshot read, authoritative Property read, observation subscription,
Property write, and Command execution; cached reads and diagnostics remain
absent.

Operational revocation is exact removal of the credential identity from the
Runtime Host enrollment registry. Rotation does not remove the principal's
authorization entry. Existing sessions are stopped or invalidated through the
established Runtime Host restart/reload procedure, and rejection is proved with
a fresh connection.

### Loss, corruption, and compromise

Exact protected rollback material may restore locally corrupt files only while
the credential remains enrolled and every expected hash and security descriptor
matches. A lost private key is never recreated and its credential identity is
never reissued; recovery creates and enrolls a new credential for the same
principal and then revokes the lost identity.

Suspected compromise does not use an overlap period. A revision-locked emergency
transition adds the replacement while removing the compromised identity, then
installs and validates the replacement. Temporary unavailability is preferable
to continued acceptance of a suspected compromised credential.

### Lifecycle transaction and recovery

The existing durable file publisher and strict recoverer remain authoritative
for local publication. A lifecycle journal coordinates the Runtime Host and
Client phases using bounded metadata, expected hashes, principal, trust policy,
grant-set hash, and old/new credential identities. It contains no private key,
address, profile, enrollment, or policy content.

Recovery is reversible before old-credential revocation. After revocation it is
forward-only and never silently restores the old credential. Ambiguous phases,
hash mismatches, substituted files, changed grants, or changed server trust
leave all evidence untouched for operator review.

After successful finalization, old enrollment and active old credential files
are absent. Old private-key backups, staging copies, and transfer archives are
destroyed. Retained evidence is non-secret: hashes, validity, principal,
before/after revisions, transaction identifier, validation outcomes, security
evidence, and cleanup result.

## Stage 53A

Stage 53A adds the accepted decision and a separate lifecycle inspection
boundary. It deliberately leaves the initial-provisioning guards unchanged.
The inspector establishes the exact safe input state required by later rotation
planning and publication and performs no write, enrollment change, policy
change, connection, or hardware operation.

Stage 53A closed with 204 focused credential-provisioning tests and 5,940
complete .NET Release tests passing. The implementation was committed, pushed,
and synchronized across all three computers.

## Stage 53B rotation preparation

The first integrated Stage 53B boundary prepares planned rotation entirely in
memory. It reruns the strict lifecycle inspection, locks the profile,
enrollment, policy, and trusted-server revisions, rejects an expired or
not-yet-valid source, and requires a new credential identity backed by a
matching, currently valid certificate and private key.

The preparer returns disposable candidate buffers for the replacement
certificate and key, the byte-exact current profile and authorization policy,
an overlap enrollment containing both old and new identities for the exact
same principal and trust policy, and a final enrollment containing the new but
not the old identity. Both enrollment candidates are loaded through the
authoritative Runtime Host parser and their expected resolutions are proved.
Every candidate buffer, including non-secret configuration evidence, is zeroed
on disposal. No file is staged or published and no Runtime Host is contacted.

The paired durable publisher consumes only that prepared set. `Begin` locks the
exact current certificate, private key, profile, enrollment, authorization
policy, and their protected access control; makes a bounded metadata journal
durable; stages the replacement credential, byte-exact profile, overlap
enrollment, and final enrollment; and publishes the overlap enrollment last.
The authorization policy is verified but never staged or written.

Successful `Begin` intentionally retains the journal, exact old four-file
backups, and final-enrollment stage across the computer boundary. This is the
recoverable validation interval. `Recover` validates every target, stage,
backup, derived path, hash, phase, and policy revision before restoring exact
sources. Ambiguous or substituted evidence is left untouched.

`Finalize` is a separate explicit operation after replacement-client physical
validation. It requires the complete overlap state and unchanged policy,
atomically selects the final enrollment, proves the old identity absent and
the replacement present, records commitment, and removes old credential
backups and all transaction artifacts. It never changes authorization.

The rotation orchestrator composes preparation and durable `Begin` but retains
separate explicit `Finalize` and `Recover` calls. This prevents a successful
local publication from being mistaken for completed cross-computer validation
or authorization to revoke the old credential. Automated fault injection
stops after staging, after each certificate/key/profile/enrollment publication,
and after completed overlap publication; every ordinary failure must restore
all five locked source hashes and leave no transaction artifact.

## Planned implementation stages

1. 53A — accepted decision, lifecycle inspection, expiry classification, and
   exact enrollment/authorization/trust evidence.
2. 53B — rotation planning, overlap preparation/publication, Client selection,
   final revocation, emergency replacement, durable lifecycle recovery, and
   integrated operator commands.
3. 53C — complete automated validation, commit/synchronization, independent
   MiniPC and Desktop read-only physical rotation, old-credential rejection,
   cleanup, documentation reconciliation, and closure.

## Stage 53C operator boundary

Stage 53B closed with 216 focused credential-provisioning and 5,952 complete
.NET Release tests and was synchronized across all three computers. Stage 53C
adds three explicit modes to the existing sanitized Windows provisioning
operator:

- `rotate-begin` issues one bounded replacement credential, re-inspects the
  exact selected deployment, prepares candidates, and publishes the durable
  overlap transaction;
- `rotate-finalize` consumes the exact retained publication inputs only after
  external replacement validation and removes the old enrollment; and
- `rotate-recover` consumes those same exact inputs to restore the pre-rotation
  state while finalization has not committed.

`rotate-begin` requires explicit paths and lowercase SHA-256 revisions for the
current certificate, private key, profile, enrollment, authorization policy,
and trusted-server certificate, plus the current credential ID, principal,
trust-policy ID, exact comma-separated grants, signing-root thumbprint, and
validity. No path, address, credential value, hash input, principal, trust
policy, or grant is printed. Finalize is never implied by successful Begin.

### Cross-computer custody correction

Physical Stage 53C preflight proved that the installed Laptop credential and
the MiniPC enrollment and signing authority deliberately occupy different
computers. The local four-file publisher remains valid for colocated
deployments, but it is not used to copy or reconstruct the Laptop's old private
key on the MiniPC.

The cross-computer Begin boundary consumes a protected metadata-only Laptop
request. It validates the exact profile template, current enrollment, exact
five-grant authorization, Runtime Host identity evidence, trust policy,
signing authority, and source revisions. It publishes only overlap enrollment
on the MiniPC and creates a protected four-entry archive containing the new
certificate, new private key, unchanged profile, and strict manifest.
Finalization and recovery remain later explicit operations.

## Consequences

- Initial provisioning remains narrow and cannot be misused as rotation.
- Expiry becomes visible before it causes an unexplained authentication outage.
- Rotation preserves the principal, exact permissions, Runtime Host identity,
  and target selection.
- Planned overlap is reversible until explicit old-credential revocation.
- Compromise recovery favors revocation over availability.
- Successful rotation leaves no usable obsolete private-key rollback path.
- Python mutation no-retry, no-replay, and uncertain-outcome semantics remain
  unchanged.

### Increment 53C2A3 â€” protected Laptop replacement cutover

Increment 53C2A3 adds the Laptop-side cutover half of the cross-computer
custody bridge. The operation accepts only the replacement archive issued by
the durable MiniPC Begin transaction. Before mutation it validates the exact
four-entry archive shape, strict manifest purpose and principal, every payload
hash, the old and replacement certificate identities, and unchanged installed
certificate and private-key paths.

The operation is pinned to LTAEP, a synchronized clean repository, stopped
Runtime Host and Client processes, direct non-reparse custody, and a protected
directory outside the repository. It imports the archive byte-exact, records a
durable journal, retains protected byte-exact copies of the old certificate,
private key, and profile, preserves their access descriptors, installs all
three replacements, and verifies every installed hash. A handled failure after
installation begins restores all three originals and their access descriptors;
an incomplete rollback is reported as requiring operator recovery.

The independent verifier proves the durable installed phase, protected archive
hash, exact installed files, and retained old-credential rollback. Neither
tool accepts or mutates the MiniPC enrollment or authorization policy. The
MiniPC old-plus-replacement overlap remains until replacement connectivity is
physically validated and a later explicit finalization is approved. Physical
cutover and finalization remain pending.

#### Increment 53C2A3A â€” Windows ACL descriptor correction

The first physical 53C2A3 attempt validated the replacement archive and entered
installation, but Windows rejected applying a full security descriptor as an
Access-only descriptor. Automatic rollback restored all three old files
byte-exact. Independent inspection confirmed every installed ACL remained
protected, owned by the current user, and limited to one explicit allow with no
deny. The MiniPC overlap, authorization, repository, and both stopped-process
states remained unchanged.

Increment 53C2A3A captures only the Access section used by the existing
Access-only restoration call. Regression coverage requires three explicit
Access-only captures and the existing Access-only restore. The failed protected
transaction is retained separately as recovery evidence; physical cutover must
start from a new custody directory.

#### Increment 53C2A3B â€” direct Windows ACL object preservation

The corrected Access-only SDDL attempt also failed at the common ACL
application boundary. Automatic content rollback again restored all three old
files byte-exact, and independent inspection again confirmed protected
current-user ACLs with one explicit allow and no deny. Downloads replacement
custody, both protected failed transactions, MiniPC overlap, authorization,
repository, and stopped-process state remained intact.

Increment 53C2A3B removes security-descriptor serialization from the live
transaction. It retains each original `FileSecurity` object and reapplies that
same object after installation or rollback. Failure output adds only exception
type and numeric HResult for the primary and rollback boundaries; paths and
credential values remain withheld. Regression coverage requires three direct
ACL captures, two direct reapplications, no SDDL conversion, and sanitized
failure classification.
