# ADR-0043 — Increment 43G4C2C4C — MiniPC Inbound Client Trust

## Discussion

The laptop certificate selected by the MiniPC Client profile matched the
MiniPC enrollment exactly, but mutual-TLS authentication still failed. The
production Runtime Host deliberately composes provisioned enrollment with
system certificate trust. The MiniPC had the credential enrollment but not the
laptop Client's public certificate in its CurrentUser trust store.

This increment completes reciprocal trust without exporting a private key or
changing any authoritative identity.

## Implement now

The laptop wrapper exports the configured Client certificate as a public-only
`.cer` file and verifies that the Client configuration and private-key custody
remain unchanged. The MiniPC wrapper calculates the certificate credential
identity, requires an exact existing enrollment match, and imports the public
certificate into `CurrentUser\TrustedPeople`.

The MiniPC installation is idempotent for an identical existing certificate,
rejects ambiguous or conflicting state, verifies configuration and enrollment
hash preservation, and removes a newly imported certificate if a later
postcondition fails.

Seven automated cases cover the all-ready assessment and fail-closed behavior
for each readiness input. Expected full-suite result: **4,403 passed, 0
failed**.

## Backlog

- connect the laptop Client to the MiniPC Runtime Host;
- validate remote property, command, Event, and diagnostics behavior;
- validate simultaneous Desktop and MiniPC Runtime Hosts.

## Stop point

Stop after both wrappers report success. Restart the MiniPC Runtime Host, then
retry the laptop Client connection before committing.
