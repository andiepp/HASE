# ADR-0054 Increment 54E2A — Deployment Bundle Preparation

## Status

Approved and implemented for repository validation. Physical firmware upload is
not authorized by this increment.

## Starting authority

- repository: `andiepp/HASE`;
- branch: `main`;
- starting commit: `b27754fe10ca6200af29602f3cd0e98a38afb185`;
- physical preflight evidence SHA-256:
  `4c03abc480ea3f402a92dc6d7645aef2c509cd5074050865d4b80732e20d9c3b`;
- computer: AEPRAKETE;
- approved FQBN: `esp32:esp32:esp32doit-devkit-v1`;
- approved Arduino CLI: 1.3.1;
- approved ESP32 core: 3.3.10;
- approved rollback source commit:
  `96db1799d410eedc82aea82cc3f5b3efa003242c`.

## Goal

Prepare two byte-identified deployment artifact sets without contacting or
changing the physical ESP32:

1. the current ADR-0054 library-based endpoint firmware; and
2. the approved pre-ADR-0054 rollback firmware.

The output makes a later upload recoverable without allowing this increment to
perform that upload.

## Security and custody boundary

`HaseSecrets.h` remains ignored and untracked. Unlike the earlier compilation
validation, deployment compilation must use the real local configuration so
the resulting binaries are deployable. A compiled firmware binary therefore
contains sensitive local configuration even though the source secret file is
not retained with it.

The script consequently separates two roots:

- the **sensitive bundle root** must be a new child of
  `%LOCALAPPDATA%\HASE\Esp32DeploymentBundles`; and
- the **sanitized evidence root** must be new, outside the repository, and must
  not overlap the bundle root.

The sensitive bundle must not be committed, attached to an issue, copied to a
shared package directory, or transferred to another computer. Evidence records
only versions, commit identities, artifact counts, sizes and SHA-256 hashes,
warning counts, and non-mutation outcomes. It contains no application source,
secret source, secret value, COM port, or private-network address.

## Fail-closed preparation contract

`New-HaseEsp32DeploymentBundle.ps1` requires:

- AEPRAKETE exactly;
- a caller-supplied 40-character expected commit;
- exact equality of `HEAD`, `origin/main`, and the supplied commit;
- clean `main`;
- stopped Desktop Runtime Host and Client processes;
- exact approved Arduino CLI binary, CLI version, ESP32 core, and FQBN;
- present, ignored, and untracked local `HaseSecrets.h`;
- the exact 122-path rollback source tree at the approved rollback commit;
- new and non-overlapping output roots.

It copies the exact five current application files and local secret into a
temporary current-source sketch, materializes rollback source into a separate
temporary sketch, and compiles only those temporary sketches into external
build paths. It hashes the retained current and rollback artifacts, creates a
bundle manifest and sanitized evidence, verifies that no `HaseSecrets.h` file
entered either retained root, and verifies that repository status did not
change. Arduino CLI is never given a repository sketch as its compilation
target.

Both temporary current and rollback source receive a temporary copy of
`HaseSecrets.h` only so they can compile. Their common temporary working
directory is removed in `finally` on success or failure. A failed preparation
must stop; any retained partial bundle or evidence is classified before rerun
or removal.

The first physical preparation attempt exposed Arduino CLI 1.3.1 behavior that
also exported build products beneath a directly targeted repository sketch.
Those eleven untracked products were moved intact into current-user sensitive
quarantine. Isolating both sketch targets prevents that side effect rather than
weakening the repository-preservation check.

## Explicit exclusions

54E2A does not:

- accept a COM-port parameter;
- enumerate attached boards;
- invoke Arduino CLI upload;
- instantiate a serial port;
- reset the ESP32;
- start a Runtime Host or Client;
- verify endpoint protocol behavior;
- transfer sensitive compiled binaries.

Those operations require later explicit approval. The physical upload is
54E2B. End-to-end behavior is 54E3.

## Automated validation

The increment adds:

- 14 assessment cases proving all-ready and every fail-closed projection;
- two static security/custody contract tests;
- one Windows PowerShell parser test.

The expected complete .NET total increases from 5,987 to 6,004 tests.

## Operator execution shape

The exact expected commit is supplied after this increment is committed. The
following shows parameter meaning only; values in angle brackets are
documentation placeholders and must not be pasted literally:

```powershell
& .\tools\Arduino\New-HaseEsp32DeploymentBundle.ps1 `
    -RepositoryRoot "H:\Development" `
    -ExpectedCommit "<approved-40-character-commit-hash>" `
    -BundleRoot "<new-current-user-local-sensitive-bundle-root>" `
    -EvidenceRoot "<new-external-sanitized-evidence-root>"
```

The executable handoff supplied after commit uses concrete values and contains
no placeholders.

## Recovery boundary

Compilation and external file creation do not change the physical ESP32, so no
physical rollback is required after a 54E2A failure. Do not rerun immediately.
First classify:

- whether the sensitive bundle root exists and its retained file count;
- whether the evidence root exists and its retained file count;
- whether the temporary working directory remains;
- whether the repository remains clean and exact;
- whether any physical operation occurred (expected false).

Retained artifacts are removed only after explicit cleanup approval.

## Definition of done

54E2A is complete when:

- the six-path repository change is reviewed;
- focused and complete automated tests pass;
- the change is committed and pushed;
- required repositories are synchronized and clean;
- current and rollback firmware compile successfully on AEPRAKETE;
- retained artifact sets and evidence hashes validate independently;
- repository state remains unchanged;
- no upload, serial-port open, reset, or physical mutation occurred.
