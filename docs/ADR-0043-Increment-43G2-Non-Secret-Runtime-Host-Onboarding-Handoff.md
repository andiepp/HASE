# ADR-0043 — Increment 43G2 — Non-Secret Runtime Host Onboarding Handoff

## Decision

After the 43G1 strict installation audit succeeds, an operator may explicitly
create one versioned Runtime Host onboarding handoff. Version 1 contains only:

```json
{
  "formatVersion": 1,
  "runtimeHostId": "<authoritative-runtime-host-id>"
}
```

The handoff identifies the expected Runtime Host for a later Client profile. It
is not a credential, certificate, authorization grant, network configuration,
or client profile.

## Creation

Close the Desktop Runtime Host, build Release, choose a new fully qualified
destination, and run from the repository root:

```powershell
& .\tools\Deployment\New-HaseRuntimeHostOnboardingHandoff.ps1 `
  -DestinationPath "<new-fully-qualified-handoff-path>"
```

Creation refuses an existing destination. It writes a same-directory temporary
candidate, reloads it through the strict reader, flushes it, and publishes it
without overwrite. Failure removes only its unpublished temporary candidate.

## Transfer custody

Transfer the handoff through an approved channel and compare the authoritative
Runtime Host ID at both ends. The receiving Client still requires its own
private-network configuration, certificate custody, client enrollment, local
profile ID, display name, and explicit registry administration.

Do not add actual deployment handoffs to source control. Although the artifact
is non-secret, it is installation-specific operational data.

## Boundaries

The handoff contains no private address, hostname, certificate thumbprint,
credential, enrollment content, endpoint configuration, client-local profile
ID, or display name. Creation changes no installed configuration and performs
no enrollment, connection, discovery, or endpoint lifecycle operation.
