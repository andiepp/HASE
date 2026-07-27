# Private-Network Credential Provisioning

## Purpose

This procedure provisions the controlled ADR-0032 desktop-to-laptop validation
profile on Windows 11.

It creates deployment-specific credentials and configuration only when the
operator runs the scripts locally.

The repository and downloadable implementation archives contain:

- no private-network address;
- no certificate;
- no private key;
- no password;
- no certificate thumbprint;
- no HASE credential identifier;
- no machine-specific configuration path.

Do not commit or publish any generated bundle file.

## Prerequisites

- The desktop and laptop can already reach each other through the selected
  private routed network.
- The operator has selected one explicit desktop address and one fixed port.
- The repository is built from the accepted ADR-0032 baseline.
- PowerShell execution policy permits locally reviewed scripts.
- The operator has a secure method to transfer one password-protected client
  credential from desktop to laptop.

The selected address must not be a wildcard, loopback, or Internet-facing
listener.

## Files created at runtime

The desktop provisioning script creates:

```text
desktop-private-network.json
laptop-private-network.json
client-enrollments.json
laptop-client.pfx
runtime-host-server.cer
```

Only these files are transferred to the laptop:

```text
laptop-private-network.json
laptop-client.pfx
runtime-host-server.cer
```

The desktop retains:

```text
desktop-private-network.json
client-enrollments.json
```

The generated directory must remain outside the repository.

## Desktop provisioning

Open PowerShell as the same Windows user that will run Protocol Explorer.

Run:

```powershell
& "<repository>\tools\PrivateNetwork\New-HasePrivateNetworkValidationBundle.ps1" `
    -ListenerAddress <desktop-private-address> `
    -Port <fixed-port> `
    -OutputDirectory "<external-protected-directory>"
```

Supply the client-transfer password through the secure prompt. Do not put the
password on the command line or in a configuration file.

The script:

- creates a deployment-specific root certificate;
- installs the root certificate in the desktop Current User root store;
- creates and installs the runtime-host server certificate in the desktop
  Current User personal store;
- creates a client-authentication certificate;
- exports the client credential once as a password-protected PKCS#12 file;
- exports the public runtime-host server certificate;
- creates the desktop and laptop external configuration files;
- creates the HASE client-enrollment document;
- removes the client private key from the desktop Current User personal store;
- prints no address, path, thumbprint, credential identifier, principal, or
  password.

The server certificate contains the selected listener address as an IP Subject
Alternative Name.

## Secure transfer

Transfer only the three laptop files through the approved private transfer
channel.

Do not:

- email the client credential;
- place it in source control;
- place it in an ordinary shared folder;
- copy its password beside it;
- upload it to chat;
- retain unnecessary transfer copies.

Communicate the transfer password through a separate approved channel.

## Laptop installation

Open PowerShell as the same Windows user that will run Protocol Explorer.

Run:

```powershell
& "<repository>\tools\PrivateNetwork\Install-HasePrivateNetworkValidationClient.ps1" `
    -BundleDirectory "<external-protected-directory>"
```

Supply the transfer password through the secure prompt.

The script:

- imports the client certificate and private key into the laptop Current User
  personal store;
- marks the imported client private key non-exportable;
- imports the public server certificate into the laptop Current User trusted
  people store;
- verifies both imports against the external laptop configuration;
- prints no address, path, thumbprint, credential identifier, or password.

After the laptop installation succeeds and has been verified, securely delete
the transferred PKCS#12 file and any unnecessary transfer copy.

## Desktop smoke host

Run Protocol Explorer on the desktop:

```powershell
HASE.ProtocolExplorer private-network-host `
    "<external-protected-directory>\desktop-private-network.json"
```

Expected safe output identifies:

- HTTPS / HTTP/2 gRPC;
- mutual TLS;
- withheld listener configuration;
- zero published endpoints;
- readiness for Ctrl+C shutdown.

Framework listener logging is suppressed so the configured address is not
printed.

## Laptop smoke client

Run Protocol Explorer on the laptop:

```powershell
HASE.ProtocolExplorer private-network-client `
    "<external-protected-directory>\laptop-private-network.json"
```

Expected safe output identifies:

- HTTPS / HTTP/2 gRPC;
- mutual TLS;
- withheld remote configuration;
- the non-sensitive runtime-host identifier;
- API version 1.0;
- zero published endpoints;
- successful authenticated snapshot completion.

## Failure behavior

Provisioning and startup fail closed when:

- a generated target already exists;
- the listener address or port is invalid;
- the server certificate does not identify the listener address;
- a required certificate is absent or ambiguous;
- the client private key is unavailable;
- the enrollment file is absent, malformed, empty, or inconsistent;
- a presented certificate is untrusted or unenrolled;
- the server certificate differs from the laptop pin;
- TLS reports a server identity mismatch;
- any configuration file is missing, malformed, oversized, incomplete, or
  contains unknown fields.

No failure enables cleartext, wildcard binding, unauthenticated access, or
certificate-validation bypass.

## Scope

This workflow supports controlled private-network validation only.

It is not a production certificate authority or a production deployment
procedure.

ADR-0031 production promotion remains blocked by the separately tracked audit,
resource-governance, authorization-deployment, revocation, rotation, recovery,
service-installation, and operational-hardening requirements.
