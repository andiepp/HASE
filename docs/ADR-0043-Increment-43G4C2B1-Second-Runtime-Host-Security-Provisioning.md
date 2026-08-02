# ADR-0043 — Increment 43G4C2B1 — Second Runtime Host Security Provisioning

## Decision

A distinct second Runtime Host receives newly created server-certificate
material and configuration. Its server private key remains non-exported in the
second PC's `CurrentUser\My` certificate store. Only the public server
certificate is published for later transfer to the laptop.

The enrollment document records an explicitly supplied identity for the
laptop's existing mutual-TLS client certificate. This workflow does not create,
copy, import, or export a laptop client private key.

## Preconditions

Run this workflow on the second PC only, after a Release build. Supply:

- an explicit non-loopback listener address assigned to the second PC;
- an explicit port;
- a new output directory or one without any of the three target files;
- the normalized `x509-sha256:` identity of the laptop's existing client
  certificate;
- the intended client principal and trust-policy identifiers.

## Command

```powershell
& .\tools\Deployment\New-HaseSecondPcRuntimeHostSecurityProvisioning.ps1 `
  -ListenerAddress "<second-PC-private-address>" `
  -Port <port> `
  -OutputDirectory "<fully-qualified-output-directory>" `
  -LaptopClientCredentialId "<existing-laptop-x509-sha256-identity>" `
  -ClientPrincipalId "<client-principal-id>" `
  -TrustPolicyId "<trust-policy-id>"
```

The script refuses an address not assigned to the PC and refuses to overwrite
any target. It stages output, validates the deployment and enrollment documents
with the product's strict readers, requires the referenced server certificate
to have a private key, and verifies that the exported public certificate
matches it. Failed provisioning removes staged files and the newly created
certificate.

## Published result

The output directory contains only:

- `desktop-private-network.json`, for the second Runtime Host;
- `client-enrollments.json`, enrolling the existing laptop credential;
- `runtime-host-server.cer`, the public server certificate for later laptop
  pinning.

The public certificate is the only artifact authorized for transfer in this
increment. 43G4C2B1 does not install or start the Runtime Host, create a Runtime
Host identity, change the laptop, open a firewall port, or bind a listener.

## Safe output

Console output reports readiness and artifact roles. It withholds the listener
address, port, certificate thumbprint and subject, credential identity,
principal, trust policy, and configuration contents.
