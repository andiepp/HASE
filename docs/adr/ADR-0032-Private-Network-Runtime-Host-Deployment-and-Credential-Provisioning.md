# ADR-0032 - Private-Network Runtime-Host Deployment and Credential Provisioning

- Status: Accepted
- Date: 2026-07-26

---

# Context

ADR-0031 defines the HASE runtime host as the complete northbound remote API
security boundary.

C-029 through C-034 implement and validate:

- operation-based authorization;
- X.509 client-certificate validation;
- mutual-TLS client authentication;
- stable HASE client-principal resolution;
- HTTPS and HTTP/2 Kestrel integration;
- authenticated snapshot, Property, Command, and observation RPCs;
- rejection before application-service invocation;
- physical ESP32 Property and observation access;
- physical Arduino Command execution.

The verified baseline before this ADR is:

```text
Commit eb8abeb60440861f2c71d7a0033af78980607728
2,850 automated tests pass
.NET solution builds
C-034 authenticated physical observation validation is complete
The secured gRPC host remains restricted to loopback
```

The next validation topology uses two Windows 11 computers joined to the same
private routed network:

```text
Laptop test client
    -> private routed network
    -> desktop runtime-host HTTPS/HTTP/2 listener
    -> runtime-host-owned Arduino USB connection
    -> runtime-host-owned ESP32 network connection
```

The desktop remains the sole owner of both physical endpoint lifecycles. The
laptop is an operational northbound client only.

The deployment must not place a private-network address, certificate, private
key, password, certificate thumbprint, enrollment, or machine-specific path in
source control, committed documentation, test fixtures, packaged implementation
archives, or ordinary console output.

This ADR does not approve unrestricted production or Internet exposure.
ADR-0031 audit, authorization, resource-governance, revocation, rotation, and
operational-hardening requirements remain authoritative.

---

# Decision

HASE will support a controlled private-network runtime-host validation profile
for desktop-to-laptop operational testing.

The profile uses:

- one explicitly configured non-loopback IP address;
- one explicitly configured fixed TCP port;
- HTTPS with HTTP/2 only;
- TLS 1.2 or TLS 1.3;
- a server certificate installed outside the repository;
- mandatory client-certificate presentation;
- system X.509 trust for client certificates;
- explicit HASE enrollment of each accepted client credential;
- exact client-side pinning of the runtime-host server certificate;
- normal TLS server IP-identity validation in addition to pinning;
- external versioned JSON configuration files;
- operating-system certificate stores for private-key custody.

Network reachability does not grant HASE authority.

Private-network membership, including membership in a Tailscale network, is
neither a HASE client identity nor an authorization grant.

## Validation profile classification

The profile is approved for controlled operational validation on a private
routed network.

It is not yet the ADR-0031 production secured-remote profile because the
following production requirements remain incomplete:

- complete security audit delivery and retention;
- host-wide and per-principal resource governance;
- failed-authentication rate governance;
- complete authorization-policy deployment configuration;
- active-session termination after credential or policy revocation;
- operational certificate rotation and recovery automation;
- hardened service installation and update procedures.

The validation profile must therefore not:

- bind to an Internet-facing address;
- bind to a wildcard address;
- be advertised as production-ready;
- replace application authentication with network membership;
- add a cleartext fallback listener;
- add remote endpoint lifecycle administration.

## Explicit listener binding

The desktop listener is configured with one literal IP address and one fixed
nonzero port.

Rejected listener configurations include:

- IPv4 wildcard;
- IPv6 wildcard;
- IPv4 loopback;
- IPv6 loopback;
- port zero;
- invalid ports;
- hostnames requiring runtime resolution;
- implicit binding to every interface.

The binding address is deployment data. It is not embedded in source,
documentation, fixtures, packaged archives, or application defaults.

Changing the selected private-network interface or address requires an explicit
configuration change and runtime-host restart.

## Server credential

The runtime-host server certificate is installed in an operating-system X.509
certificate store.

Deployment configuration identifies it by:

```text
Store name
Store location
Certificate thumbprint
```

The configuration contains no certificate bytes and no private key.

Before listener creation, HASE requires:

- one exact certificate-store match;
- an accessible private key;
- a current validity interval;
- server-authentication Enhanced Key Usage when EKU is present;
- an IP Subject Alternative Name matching the configured listener address.

Common-name fallback and wildcard identity matching are not accepted for the
private-network listener.

Missing, ambiguous, expired, not-yet-valid, mismatched, or unusable server
credentials reject deployment.

## Client credential

The laptop client certificate and its private key are installed in an
operating-system X.509 certificate store.

The laptop configuration identifies the certificate by store name, store
location, and thumbprint.

The laptop client:

- requires an accessible client private key;
- presents the selected certificate explicitly;
- uses TLS 1.2 or TLS 1.3;
- connects only to an absolute HTTPS URI using a literal IP address;
- rejects user information, paths, queries, and fragments in the address.

Private-key bytes are never serialized into HASE configuration.

## Client trust and enrollment

Client acceptance has two independent requirements:

1. the certificate must pass configured X.509 trust validation;
2. its derived HASE credential identity must exist in the enrollment registry.

The credential identity is:

```text
x509-sha256:<lowercase SHA-256 hash of complete DER certificate>
```

The version 1 enrollment document contains only:

```text
Format version
Credential identifier
Stable HASE client-principal identifier
Trust-policy identifier
```

It contains no certificate bytes, private key, password, network address,
physical endpoint identity, or authorization grant.

An unknown but otherwise trusted certificate does not authenticate.

Credential rotation may map a new credential identity to the same stable HASE
client principal. Duplicate credential identities are invalid.

## Server trust on the laptop

The laptop loads one externally provisioned public server certificate and pins
its SHA-256 certificate hash.

Exact pinning may replace platform chain acceptance for this controlled
validation profile, but it does not replace TLS server identity validation.

The laptop rejects:

- a missing server certificate;
- a different server certificate;
- a server IP-identity mismatch;
- a certificate unavailable error;
- cleartext transport.

No certificate-validation bypass is permitted.

## External configuration

Desktop and laptop configuration use separate bounded, versioned JSON files.

The desktop configuration references:

- explicit listener IP address;
- fixed port;
- server-certificate store name;
- server-certificate store location;
- server-certificate thumbprint;
- fully qualified client-enrollment file path.

The laptop configuration references:

- absolute HTTPS URI using the desktop listener IP address and port;
- client-certificate store name;
- client-certificate store location;
- client-certificate thumbprint;
- pinned server-certificate store name;
- pinned server-certificate store location;
- pinned server-certificate thumbprint.

Configuration rules are:

- maximum document size is 64 KiB;
- UTF-8 with or without a byte-order mark is accepted;
- the format version is mandatory;
- unknown fields are rejected;
- incomplete documents are rejected;
- enum values are case-sensitive;
- referenced paths are fully qualified;
- configuration failure is fatal;
- no insecure fallback is attempted.

Machine-specific configuration remains outside the repository and outside
downloadable implementation archives.

## Enrollment provisioning

HASE may derive and atomically create an enrollment document from an already
provisioned public client certificate.

The enrollment provisioner:

- derives the SHA-256 credential identity;
- writes public enrollment metadata only;
- never exports a private key;
- never overwrites an existing target;
- publishes atomically;
- removes temporary state after failure or cancellation.

Certificate issuance, private-key creation, certificate-store installation, and
secure transfer of a client credential remain operating-system provisioning
operations.

HASE does not become a general-purpose certificate authority.

Operational provisioning procedures must:

- create server and client credentials outside source control;
- protect private keys with operating-system access controls;
- transfer client credentials through an approved secure channel;
- avoid command-line passwords when the operating-system tooling provides a
  secure prompt or protected secret input;
- remove intermediate export files after successful installation;
- verify intended EKUs and server IP Subject Alternative Names;
- retain recovery material only under an explicitly approved protection policy.

## Lifecycle ownership

The desktop runtime host remains the sole owner of:

- Arduino discovery, selection, attachment, supervision, and detachment;
- ESP32 attachment, synchronization, supervision, and detachment;
- native and compact transport connections;
- endpoint Property cache;
- Command routing;
- Event observation;
- endpoint publication.

The laptop receives no API for endpoint discovery, attachment, replacement,
detachment, or recovery control.

## Validation client

The laptop validation application will use the existing version 1 generated
gRPC client.

The controlled physical validation will prove, incrementally:

1. authenticated snapshot access;
2. authoritative ESP32 Property access;
3. authenticated Arduino Command execution with authoritative confirmation;
4. authenticated observation of physical endpoint lifecycle, Property changes,
   and Events;
5. orderly client, stream, host, and physical endpoint shutdown.

No address, thumbprint, credential identifier, private key, password, or
machine-specific path is written to ordinary validation output.

---

# Consequences

## Positive

- The intended desktop-to-laptop topology can be validated without transferring
  endpoint lifecycle ownership.
- The existing C-029 through C-034 authentication boundary is reused.
- Tailscale or another routed private network supplies reachability only.
- Listener expansion is explicit and fail closed.
- Private keys remain under operating-system custody.
- Machine-specific configuration remains outside source control.
- Client identity remains stable across planned credential rotation.
- The client verifies both the exact server credential and its listener IP
  identity.
- The same normalized API remains independent of Arduino and ESP32 transports.

## Negative

- Initial provisioning requires operating-system certificate operations on both
  computers.
- Exact server-certificate pinning requires explicit laptop configuration
  update during server-certificate rotation.
- The first validation profile requires runtime restart for configuration,
  enrollment, or credential changes.
- The profile is not yet production-ready under all ADR-0031 requirements.
- A private routed network remains an additional operational dependency.

## Neutral

- No protobuf contract changes are introduced.
- No normalized northbound application-service changes are introduced.
- No physical endpoint firmware changes are introduced.
- No remote endpoint lifecycle administration is introduced.
- No Tailscale API integration or host discovery is introduced.
- No private-network address becomes a HASE identity.

---

# Rejected alternatives

## Trust private-network membership alone

Rejected because network membership is not a stable HASE principal, explicit
credential enrollment, authorization policy, or audit identity.

## Bind to every interface

Rejected because wildcard binding expands the exposure boundary silently.

## Use cleartext HTTP/2 inside the private network

Rejected because ADR-0031 requires encryption and authenticated clients for
every non-loopback connection.

## Disable server-certificate validation on the laptop

Rejected because it permits server impersonation and removes the authenticated
server identity.

## Store certificate files and passwords beside configuration

Rejected because ordinary configuration storage is not approved private-key
custody.

## Embed deployment values in Protocol Explorer defaults

Rejected because machine-specific addresses, credential identifiers, and paths
must not enter source, fixtures, documentation, output, or packaged archives.

## Transfer physical endpoint ownership to the laptop

Rejected because ADR-0019 assigns physical endpoint lifecycle ownership to the
runtime host.

---

# Result

ADR-0032 approves implementation and controlled physical validation of an
explicit private-network runtime-host listener and a separately configured
laptop client using mutual TLS and external credential provisioning.

It does not approve unrestricted production or Internet exposure.

Production promotion remains blocked until the remaining ADR-0031 audit,
resource-governance, authorization-deployment, revocation, rotation, and
operational-hardening requirements are separately completed and verified.

## Implemented and validated

The approved profile was implemented and physically validated between two
Windows 11 computers on a private routed network.

The completed validation proves:

- explicit non-loopback, non-wildcard, fixed-port HTTPS/HTTP/2 binding;
- externally provisioned server and client credentials held in operating-system
  certificate stores;
- client trust through exact server-certificate pinning plus normal TLS
  listener-identity validation;
- system-trusted and explicitly enrolled client-certificate authentication;
- one desktop-owned attachment inventory containing the physical native-network
  and compact-serial endpoints;
- authenticated laptop snapshot access to both published endpoints;
- authenticated authoritative Property reads for both endpoint families;
- authenticated compact Command execution, authoritative confirmation, and
  restoration of the original physical state;
- authenticated Property and physical Event observation with strictly
  increasing stream sequences;
- explicit stream cancellation and orderly client, host, and physical endpoint
  shutdown;
- removal of transferable private-key files after certificate installation.

The verified implementation baseline is:

```text
3,029 automated tests pass
.NET solution builds
Self-contained Windows x64 Protocol Explorer publish succeeds
Controlled desktop-to-laptop physical validation succeeds
```

No private-network address, certificate thumbprint, credential identifier,
private key, password, or machine-specific configuration path is recorded in
the repository or ordinary validation output.
