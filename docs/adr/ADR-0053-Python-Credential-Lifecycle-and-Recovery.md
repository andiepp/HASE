# ADR-0053 — Python Credential Lifecycle and Recovery

- Status: Accepted objective; Stage 53A implementation
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

## Planned implementation stages

1. 53A — accepted decision, lifecycle inspection, expiry classification, and
   exact enrollment/authorization/trust evidence.
2. 53B — rotation planning, overlap preparation/publication, Client selection,
   final revocation, emergency replacement, durable lifecycle recovery, and
   integrated operator commands.
3. 53C — complete automated validation, commit/synchronization, independent
   MiniPC and Desktop read-only physical rotation, old-credential rejection,
   cleanup, documentation reconciliation, and closure.

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
