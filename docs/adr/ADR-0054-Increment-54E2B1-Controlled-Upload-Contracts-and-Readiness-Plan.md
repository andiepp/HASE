# ADR-0054 Increment 54E2B1 — Controlled Upload Contracts and Readiness Plan

## Status

Approved and implemented for repository validation. This increment does not
authorize or perform a physical firmware upload.

## Starting authority

- repository: `andiepp/HASE`;
- branch: `main`;
- starting commit: `9ed25b01c909c5341ca1e859fe494248f502d1b4`;
- computer: AEPRAKETE;
- approved FQBN: `esp32:esp32:esp32doit-devkit-v1`;
- approved Arduino CLI: 1.3.1;
- approved ESP32 core: 3.3.10;
- selected physical port: COM6;
- selected physical identity: `VID_10C4&PID_EA60`;
- bundle source commit: `9ed25b01c909c5341ca1e859fe494248f502d1b4`;
- bundle manifest SHA-256:
  `adbba82b7c889a77f2a40291f1b940e2381fff543ac65945ec2d1c31eb939f47`;
- preparation evidence SHA-256:
  `cc7f892edb5210dc6ceac782e7363fcd2478e4b1099a8b136ee04dfc99c9ea9a`.

The sensitive 54E2A bundle remains only in current-user custody below
`%LOCALAPPDATA%\HASE\Esp32DeploymentBundles`. It must not be transferred,
attached, or committed.

## Goal

Define the exact boundary for a later one-shot physical upload and create a
sanitized, hash-bound readiness plan without opening COM6 or changing the
ESP32. The plan binds the later operation to:

- an exact committed repository state;
- the exact earlier bundle source commit;
- the exact bundle manifest and preparation evidence;
- byte-identified current and rollback artifact sets;
- COM6 and its exact vendor/product identity; and
- the approved toolchain and FQBN.

## Read-only readiness planner

`New-HaseEsp32ControlledUploadReadinessPlan.ps1` fails closed unless all of
the following are true:

- it runs on AEPRAKETE;
- `HEAD` and `origin/main` equal the caller-supplied 54E2B1 commit;
- the branch is clean `main`;
- Desktop Runtime Host, Client, and Arduino IDE processes are stopped;
- the embedded Arduino CLI binary, version, ESP32 core, and FQBN are exact;
- the sensitive bundle remains below current-user local HASE custody;
- the bundle manifest and sanitized preparation evidence hashes and semantics
  are exact;
- all six current and six rollback artifacts remain byte-exact;
- COM6 is present exactly once with `VID_10C4&PID_EA60` and status `OK`;
- the new readiness-plan root is outside the repository and does not exist.

The planner uses read-only PnP inspection. It does not invoke upload, create a
serial-port object, reset the board, compile firmware, retry, roll back, or
otherwise mutate the physical device. Its retained JSON plan contains only
identities, versions, hashes, sizes, names, and non-mutation outcomes. It
contains no source, secret value, private-network address, or compiled binary.

## Dormant controlled-upload executor

`Invoke-HaseEsp32ControlledUpload.ps1` is added and parser-tested in this
increment but must not be run during 54E2B1. A later 54E2B2 approval and a
separate executable handoff are required.

Before any future physical mutation, the executor repeats the complete
repository, process, toolchain, bundle, evidence, readiness-plan, artifact,
and device checks. It refuses an existing evidence root and creates sanitized
begin evidence before invoking the uploader.

The executor contains exactly one permitted Arduino CLI upload invocation:

- target: the `Current` artifact directory only;
- FQBN: `esp32:esp32:esp32doit-devkit-v1`;
- port: the approved COM port only;
- invocation limit: one;
- automatic retry: prohibited;
- automatic rollback: prohibited.

After a successful uploader exit, it waits for at most 30 seconds for the same
COM port and vendor/product identity to return. It then records sanitized
result evidence. It does not start a Runtime Host or Client and does not claim
endpoint behavior validation.

## Failure and recovery boundary

Any failure before the single upload invocation is a closed, non-mutating
readiness failure. No cleanup or retry is automatic.

Once the uploader is invoked, a nonzero exit is classified as an uncertain
physical outcome. The operator must stop. The script records the uncertainty
and does not retry or roll back. A successful uploader exit followed by failure
of the exact device identity to return is also a stop condition. Rollback, if
later required, needs an independent diagnosis and explicit approval.

## Automated validation

The increment adds:

- 15 assessment cases proving all-ready and every fail-closed projection;
- three static planner/executor contract tests; and
- two Windows PowerShell parser cases.

The expected complete .NET total increases from 6,004 to 6,024 tests.

## Explicit exclusions

54E2B1 does not:

- run either new PowerShell script as part of repository validation;
- upload firmware;
- open COM6;
- reset or enumerate the board through Arduino CLI;
- start the Runtime Host or Client;
- perform endpoint protocol or application validation;
- transfer the sensitive deployment bundle;
- authorize a retry or rollback.

## Definition of done

54E2B1 is complete when:

- the seven-path repository change is reviewed;
- Release build, focused tests, Windows parser cases, and complete tests pass;
- the change is committed and pushed by the operator;
- the repository is synchronized and clean;
- the read-only planner creates one independently validated readiness plan on
  AEPRAKETE;
- repository and physical state remain unchanged; and
- no firmware upload, serial-port open, reset, retry, or rollback occurred.
