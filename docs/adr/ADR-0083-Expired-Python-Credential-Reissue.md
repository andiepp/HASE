# ADR-0083 — Re-issuing an Expired Python Credential

- Status: Active; Increment 83A next
- Date: 2026-10-16
- Starting baseline: base `d312f31`; the laboratory add-on at `c72451a`
  (ADR numbers are shared with the add-on, whose last is ADR-0082)

## Context

ADR-0053 gave the Python automation credential a lifecycle: inspection
(Active, RotationDue at 30 days, Expiring at 7, Expired, NotYetValid),
planned rotation with an overlap period (`rotate-begin`,
`rotate-finalize`, `rotate-recover`), and a cross-computer rotation for
the Laptop/MiniPC pair. No state renews anything by itself.

A credential that was let expire cannot be brought back with these
operations:

- `rotate-begin` refuses an Expired source
  (`PythonCredentialRotationPreparer.cs:42-53`), under the code
  `rotation-source-revision-mismatch`, which it also uses for a changed
  credential or hash, so the refusal does not say why.
- `provision` refuses a principal that holds grants
  (`principal-already-authorized`) and a credential still enrolled
  (`credential-already-enrolled`)
  (`PythonCredentialProvisioningPlanBuilder.cs:150-164`); it is first
  enrollment only, by design.
- ADR-0053 designs the way out, "Loss, corruption, and compromise": a new
  credential for the same principal, enrolled while the old identity is
  removed in one revision-locked transition without overlap. Stage 53B
  planned that emergency replacement; the objective closed with planned
  rotation and cleanup only, so no verb carries it.

The laboratory meets this now: its `hase-python-automation` credential,
issued on 2026-08-08 for 30 days, expired on 2026-09-07, and Python
automation against the installed Runtime Host answers
`rpc-unauthenticated` from both computers. The only way today is to
remove the enrollment and the six grants by hand and provision again,
which this ADR replaces with an operation of the framework.

An expired credential authenticates nothing, so an overlap period gains
nothing; and a planned rotation must not be bypassed by an operation
meant for credentials already dead.

## Decision

### A re-issue verb

The credential-provisioning operator gains `reissue-expired` and its
recovery `reissue-recover`.

- **Arguments:** those of `rotate-begin`: the publication paths and
  their expected hashes, `signing-root-thumbprint`, `validity-days`,
  `principal-id`, `trust-policy-id`, `expected-grants`,
  `expected-current-credential-id`,
  `expected-trusted-server-certificate-sha256`.
- **Preconditions:** the source credential inspects **Expired**;
  anything else is refused (`reissue-source-not-expired`): an Active,
  RotationDue or Expiring credential takes planned rotation, a
  NotYetValid one is refused as today. Every other lock of
  `rotate-begin` holds unchanged: the current credential ID, every file
  hash, the principal and trust policy, the exact grant set, the trusted
  server certificate.
- **The replacement:** minted by `PythonClientCredentialFactory` as in
  rotation, a new key and certificate (a lost or expired identity is
  never reissued), validity 1 to 90 days, refused if the signing root
  does not cover it.
- **One transition, no overlap:** a journal
  (`python-credential-reissue.transaction.json`) publishes the
  certificate, the key, the profile and the **final** enrollment in
  which the new credential ID replaces the expired one. Grants are read
  and verified, never written: they belong to the principal, which stays.
- **Recovery:** `reissue-recover` completes or rolls back an interrupted
  re-issue from the journal and its stage and backup files, strictly, as
  `rotate-recover` does.
- **Runtime Host:** stopped during the operation, as for every
  credential publication; after its start the new credential is proved
  with a fresh connection.

`rotate-begin` keeps refusing an Expired source, under its own code
`rotation-source-expired` naming `reissue-expired`, so the refusal says
what to do.

### An operator script

`python/hase-client/tools` gains a script that runs `reissue-expired`
for a colocated Runtime Host and its local Python installation: it reads
the paths from the installed host's configuration and the Python
profile, computes the expected hashes, refuses unless
`-ExpectedComputer` names the computer it runs on, and passes the
validity in days (default 90). It prints credential IDs, dates and
states only, never key or certificate content. A matching `Test-*`
verifier checks a completed re-issue the same way.

### A second computer

When another computer uses a copy of the same credential, the re-issue
leaves it holding the expired one. The increment plan settles how the
new material reaches it (83D); a copy of a private key between computers
is recorded and kept to the operator's own custody, as for the
cross-computer rotation's transfer archive.

### What does not change

Planned rotation, its overlap and its finalization; first provisioning;
the cross-computer rotation; the inspector's thresholds; the Python
`hase` API, which neither provisions nor renews.

## Consequences

- An expired credential is brought back by one framework operation
  instead of a hand-edited registry.
- The emergency replacement ADR-0053 designed for loss and compromise
  exists for one case, the expired credential; lost and compromised
  credentials can later use the same transition with their own
  preconditions.
- A re-issue is a short outage: the host is stopped, and a computer
  holding a copy is unauthenticated until it receives the new material.

## Increment plan

### Increment 83A — The verb, the transition, its recovery

The preparer's re-issue variant with its refusals, the single
no-overlap transition in the publisher, `reissue-recover`, the
`rotation-source-expired` code; unit tests for every refusal, every
journal phase and every injected failure. A read-only check of the
laboratory's credential custody (IDs, dates, which computers hold which
credential), with nothing printed beyond IDs and dates. Physical
effects: none.

### Increment 83B — The operator script

The colocated re-issue script and its verifier, with tests against a
temporary installation. Physical effects: none.

### Increment 83C — The Runtime Host's computer

On the Desktop, with the host and the clients stopped: a read-only
preflight, then, with its own approval, re-issue
`hase-python-automation` for 90 days, start the installed host, and read
through Python from the Desktop. Physical effects: the installed host's
enrollment registry and the Desktop's Python credential files; the
journal and backups are kept until a later cleanup approval, and
`reissue-recover` is the recovery boundary.

### Increment 83D — The second computer

The Laptop receives the new material, as 83A's check decides, and reads
through Python.

### Increment 83E — Documentation closure

ADR closed; the base's Roadmap, ProjectStatus and the credential
provisioning guide; the base committed to its public repository and the
laboratory add-on's submodule moved to it.

## Deferred scope

- The same transition for a lost or compromised credential (ADR-0053),
  each with its own preconditions.
- An expiry check in `rotate-cross-computer-begin`: neither it nor the
  Laptop's rotation request looks at the certificate's dates, so an
  expired Laptop credential would pass it today.
- Any automatic renewal or reminder; ADR-0053's states stay advisory.

## Operator decisions (2026-10-16)

1. A base verb that re-issues the expired credential, rather than a
   manual re-provision (2026-10-12 options, chosen on 2026-10-16).
2. Validity 90 days, the factory's maximum.
3. One transition without overlap; the verbs `reissue-expired` and
   `reissue-recover`; the second computer settled in 83D after the
   read-only check of 83A. Approved on 2026-10-16.
