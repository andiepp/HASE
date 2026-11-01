# ADR-0086 — Renewing the Private-Network Validation Certificates

- Status: Closed; Increment 86E documentation closure
- Date: 2026-11-01
- Starting baseline: base `7e30098`; the laboratory add-on at `ba977b8`
  (ADR numbers are shared with the add-on, whose last is ADR-0085)

## Context

ADR-0032 provisions the private-network profile once:
`tools/PrivateNetwork/New-HasePrivateNetworkValidationBundle.ps1` creates
a root (two years), a server certificate and a Laptop client certificate
(90 days each), the Desktop and Laptop configurations and the enrollment
registry. `Install-HasePrivateNetworkValidationClient.ps1` imports the
client credential and the server certificate on the Laptop. No tool
renews either certificate.

Both have now expired for the installed Runtime Host. Read-only on the
Desktop, printing labels and dates only:

- The installed host's server certificate was valid from 2026-07-26 to
  2026-10-24. `RuntimeHostPrivateNetworkServerCertificateValidator`
  checks it at start, so the host faults with "The runtime-host server
  certificate has expired" and publishes no endpoint.
- Its issuer, the root of that bundle, is in the operator's personal
  store with its key and in the trusted root store, valid to 2028-07-26.
- The enrollment registry holds two enrollments: the Laptop validation
  client (`laptop-validation-client`), whose certificate left the Desktop
  with the bundle and was issued for the same 90 days, and
  `hase-python-automation`, re-issued by ADR-0083 under the same root,
  valid to 2027-01-14.
- The host rejects an expired client certificate
  (`RuntimeHostClientCertificateValidator`, `CertificateExpired`) and
  trusts a client through the system chain to the root.
- Three things pin the server certificate by its exact bytes: the
  Laptop's client configuration (`RuntimeHostPinnedServerCertificateValidator`,
  a SHA-256 of the certificate, no date check), the Desktop's Python
  profile and the Laptop's Python profile (the certificate file is the
  gRPC trust anchor, so its expiry also fails the Python channel).
- The Desktop's own Lab Client names only the development loopback host
  and is not affected.
- A second deployment on the Desktop, the repository-built host of the
  ADR-0060 example, has its own root (valid to 2028-08-23), its own
  server certificate and its own Laptop client enrollment, all expiring
  on 2026-11-21.

Read-only on the Laptop: the client certificate its configuration for
the installed host names was valid from 2026-07-26 to 2026-10-24 and has
expired; the pinned server certificate is the expired one; the Python
profile pins the same certificate as a file of the same name as on the
Desktop.

The Laptop's configuration for the MiniPC's host names the same Laptop
client certificate, and the MiniPC's server certificate, pinned by that
configuration and by the Laptop/MiniPC Python profile, expired on
2026-10-31. The Laptop/MiniPC pair is therefore down as well; it is a
deployment of its own on another computer and outside this ADR. Nothing
here changes it: the expired Laptop client certificate stays in the
Laptop's store and its MiniPC configuration is not touched.

The add-on's ADR-0084 records an operator check from the Laptop through
the installed host on 2026-11-01, after both certificates expired. A
host started after 2026-10-24 cannot have published endpoints, and an
expired client certificate cannot have authenticated. The discrepancy is
recorded, not resolved; it does not change the remedy.

## Options

### A. Renew under the existing root (recommended)

A new server certificate and a new Laptop client certificate, both
signed by the existing root, replace the expired ones; the enrollment of
`laptop-validation-client` is replaced, every other enrollment kept; the
three pins are replaced by the new server certificate. The root, the
trust policy, the principal and its grants, the address, the port and
the Python credential stay. A new base tool performs the Desktop side in
one journalled transition with backups; the Laptop side reuses the
existing import and replaces its two pins.

### B. Re-provision with the existing tool

`New-HasePrivateNetworkValidationBundle.ps1` into a new directory: a
third root in the trusted store, new server and client certificates and
a new enrollment registry holding only the Laptop client. The Python
enrollment must be added back by hand, the installed configuration and
the pins copied by hand. No new tooling, more hand edits of
identity-sensitive files, and the old root stays trusted beside the new
one without a purpose.

### C. Server certificate only

Renews only what makes the host fault. It leaves the Laptop client
expired, so the Laptop cannot connect. Rejected unless the Laptop's
preflight shows its client certificate still valid.

## Decision

Option A, decided by the operator on 2026-11-01, with a validity of 90
days for both new certificates (renewal due before 2027-01-30).

### A Desktop renewal tool

`tools/PrivateNetwork/Start-HasePrivateNetworkValidationRenewal.ps1`
with `-ExpectedComputer`, every Desktop configuration that names the
certificate (the installed one and the bundle's copy), the existing
Laptop configuration (the template of the new one), the pinned
certificate files to replace, a new external output directory,
`-ValidityDays` (default 90) and `-PreflightOnly`.

- **Preflight, read-only:** the computer; no HASE host or client process
  (`Hase.DesktopHost.App*`, `Hase.Client.Wpf.App*`); the configurations
  parse and agree; the configured server certificate is found; exactly
  one certificate of its issuer's name, with its key, in the same store
  is its issuer; that root is in the trusted root store, its key is
  accessible and it covers the validity; the enrollment registry holds
  the client principal exactly once; the Laptop configuration names the
  binding and pins the current server certificate; every pinned file is
  byte-identical to it; the output directory does not exist.
- **Renewal:** both certificates are issued in memory under the root
  (RSA 2048, SHA-256; the server certificate with the binding address as
  its IP Subject Alternative Name and server authentication, the client
  certificate with client authentication; both with key identifiers).
  The client certificate is written once to a password-protected
  PKCS#12 in the output directory's `Laptop` set beside
  `runtime-host-server.cer` and `laptop-private-network.json`; it never
  enters a store on the Desktop. The new server certificate enters the
  configured store with a non-exportable persisted key. A journal,
  `private-network-renewal.transaction.json` in the output directory,
  records every target with its original and candidate hash; the
  candidates are staged first, every original is kept as
  `<target>.renewal-<id>.backup`, then the configurations (new
  thumbprint), the enrollment registry (the principal's new credential
  ID, every other enrollment unchanged) and the pins are replaced, each
  verified by hash. A failure while publishing restores every original
  and removes the added certificate. The verifier then runs.
- **Verifier:** `Test-HasePrivateNetworkValidationRenewal.ps1`,
  read-only: the configured server certificate current, with its key,
  for server authentication, identifying the binding address (read from
  the certificate's DER, not from localized text); the principal
  enrolled once; every pin equal to the certificate; with the journal,
  every target its candidate and the renewed certificate and credential
  the ones in place.
- **Recovery:** `Restore-HasePrivateNetworkValidationRenewal.ps1`
  reads the journal of either computer. `-Mode Rollback` restores every
  target to its original and removes a certificate the renewal added;
  `-Mode Cleanup`, separately approved, removes the backups, the staged
  candidates and the transfer PKCS#12 of a published renewal. Both
  refuse a target that is neither its original nor its candidate.

The expired certificates stay in the stores; nothing refers to them
once the renewal is published, and the Laptop's MiniPC configuration
still names the expired client certificate.

The tools print states, dates and the transaction ID only: no
thumbprint, address, credential ID or key material.

### The Laptop

`Install-HasePrivateNetworkValidationClient.ps1` imports the new client
credential and server certificate as today.
`Install-HasePrivateNetworkValidationRenewal.ps1` with
`-ExpectedComputer` then points the client configuration named by the
client's runtime-host registry at them and replaces the Python
profile's pinned server certificate, under the same journal, backups
and recovery. Its preflight requires the bundle to name the same host
address as the current configuration, both new certificates imported
(the client one with its key and current), the pins to be the current
server certificate and a new custody directory.

### What does not change

The root, the trust policy, principals, grants and the authorization
policy; the Python credential and ADR-0053/0083; the provisioning tool;
the host's validation, which keeps refusing an expired certificate.

## Consequences

- An expired private-network deployment is renewed by a tool rather than
  by hand, and the renewal leaves the Python credential intact.
- The renewal is an outage of the host until the Laptop holds the new
  material; the Python automation on the Desktop works again as soon as
  the host starts.
- The example deployment, expiring on 2026-11-21, can use the same tool
  as its own operation.

## Increment plan

### Increment 86A — The tools

The Desktop renewal, its verifier and recovery, the Laptop installation;
tests: Windows PowerShell parses every script; preflight against a
temporary deployment (expired and valid) changes nothing; a renewal of
a temporary deployment under a temporary root passes the verifier, keeps
other enrollments and replaces every pin; refusals (wrong computer,
running process, foreign pin, root not covering, existing journal);
an injected failure rolls back; recovery restores the originals.
Physical effects: none.

Result 86A: complete on 2026-11-01, `beb337b`, from `7e30098`. Five scripts in
`tools/PrivateNetwork`: the shared `HasePrivateNetworkRenewal.Common.ps1`
and `Start-`, `Test-`, `Install-` and
`Restore-HasePrivateNetworkValidationRenewal.ps1`; the provisioning
procedure gains a Renewal section. `PrivateNetworkRenewalToolTests`
(21 tests) runs the tools under Windows PowerShell 5.1 against a
temporary deployment whose certificates live in two temporary,
separately named stores of the current user, removed afterwards with
their keys: every script parses; the preflight of an expired deployment
is ready and changes no file and no store; it refuses a foreign pin, a
root that does not cover the validity, a missing principal, an existing
output directory and a Laptop configuration naming another address; it
refuses another computer; a renewal is read back by the host's
deployment parser and server-certificate validator, the enrollment
registry (the new client resolves to the Laptop principal, the Python
client still to its own, the old client to none) and the client's
configuration parser, with every original kept as a backup; a failure
while replacing the last target restores every original and removes the
added certificate; recovery rolls a published renewal back and cleans
one up, refusing a changed target; the verifier rejects the expired
deployment; the Laptop installation refuses before the import and then
points the configuration and the pin at the renewed certificates. The
tools' output is checked to hold no thumbprint and no address. Complete
Release suite 5,873 passed, 0 failed, 0 skipped across 28 test
assemblies; cold build 0 errors, 63 warnings.

### Increment 86B — Synchronization

The base checkouts of the Desktop and the Laptop level with the 86A
commit. Physical effects: none.

Result 86B: complete on 2026-11-01. The Desktop's base checkout was
fast-forwarded to `7e30098` before 86A and stood at `beb337b` after the
push; the Laptop's, at `8133f34`, was synchronized to `beb337b` with
`Sync-HaseRepository.ps1`, clean.

### Increment 86C — The Desktop

Preflight, then, separately approved, the renewal with the host
stopped; the verifier; the installed host started and observed Ready
with its endpoints; a Python Health read on the Desktop. The Laptop set
goes to the operator's removable drive.

Result 86C: complete on 2026-11-01. The preflight, run read-only, was
ready: the server certificate Expired (2026-07-26 to 2026-10-24), the
root valid to 2028-07-26, trusted, its key accessible; two enrollments,
the Laptop client enrolled once; both Desktop configurations, the
Laptop template and the Python pin naming the expired certificate. A
first attempt passed the two configuration paths through
`powershell -File`, which hands `a,b` to the script as one string; it
stopped at the path check before reading anything, and a classification
found nothing created. The operator then ran the renewal through
`-Command` and chose the transfer password: transaction `d994f9d2...`,
both certificates valid from 2026-11-01 to 2027-01-30, four targets
published with their backups, the verifier accepting; an independent
run of the verifier agreed, and the Python enrollment and certificate
were found unchanged. The installed Runtime Host, started from its
shortcut, showed Ready with its endpoints; the Desktop's Python
automation (0.4.0, Health) read it: two endpoints, both Ready, two
instruments, 65 properties, 24 commands. The Laptop set went to the
operator's removable drive with hashes compared; the copy block first
refused because an empty card-reader slot counted as a second removable
drive, and counts only volumes with media since.

### Increment 86D — The Laptop

The import and the pin replacement, separately approved; the installed
Lab Client connects to the installed host; a Python Health read from the
Laptop. The transferred PKCS#12 is removed from the drive.

Result 86D: complete on 2026-11-01. On the Laptop the transfer's hashes
matched the Desktop's; `Install-HasePrivateNetworkValidationClient.ps1`
imported the new client credential and server certificate; the
installation preflight was ready, and the installation (transaction
`6400cb27...`) pointed the installed Lab Client's configuration at the
new certificates and replaced the Python profile's pin, keeping both
originals, verified. The Laptop's Python automation (0.6.0, Health) read
the installed host: two endpoints Ready, 65 properties, 24 commands. The
operator observed the installed Lab Client connect to the Desktop's
installed host. The Laptop's configuration for the MiniPC still names
the expired client certificate, which stays in its store; that pair's
renewal is outside this ADR.

### Increment 86E — Closure

The ADR, Project Status and Roadmap; the add-on's operating notes and
submodule pointer. The removal of the retained backups, staged files and
transfer PKCS#12 is a separate approval.

Result 86E: the objective closed on 2026-11-01 at `beb337b`. The same
day, approved by the operator, `Restore-HasePrivateNetworkValidationRenewal.ps1
-Mode Cleanup` removed the retained files from both journals: on the
Desktop the four backups, the four staged candidates and the transfer
PKCS#12, the verifier still accepting the renewal; on the Laptop the
two backups and two staged candidates; the transfer copy on the
removable drive was deleted by the Laptop's cleanup block after its
contents were checked. The journals and the Laptop set's public files
remain, and the expired certificates stay in the stores. The next
renewal is due before 2027-01-30; the example deployment's certificates
expire on 2026-11-21. The ADR-0084 discrepancy recorded in the context
remains unresolved.
