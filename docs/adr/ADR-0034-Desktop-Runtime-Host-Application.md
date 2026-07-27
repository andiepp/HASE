# ADR-0034 — Desktop Runtime Host Application

## Status

Accepted

## Context

The HASE runtime host has been validated as the sole owner of physical ESP32 and
Arduino endpoint connections. It publishes the normalized runtime model through
the authenticated northbound gRPC API and can serve a Windows laptop client over
a controlled private network.

The current executable runtime-host scenarios are hosted by the Protocol
Explorer. That remains suitable for focused capability validation, but it is not
an operator application for routine desktop use.

The desktop PC should run a dedicated application that starts and stops the
existing runtime-host composition and displays its operational state. The same
PC must also be able to run a HASE client through loopback while a laptop client
connects through the configured private-network interface.

The desktop application must not duplicate protocol, transport, attachment,
supervision, recovery, inventory, or northbound API behavior.

## Decision

HASE will introduce a Windows WPF Desktop Runtime Host Application.

The application will:

- compose and own the existing runtime-host services;
- retain exclusive runtime-host ownership of every physical endpoint connection;
- start the runtime when the desktop application starts;
- stop the runtime orderly when the desktop application exits;
- display runtime-host status and published endpoint status;
- expose the existing versioned northbound gRPC API;
- support authenticated local clients through loopback;
- support authenticated remote clients through an explicitly configured private-
  network binding; and
- support local and remote clients concurrently without sharing physical endpoint
  ownership.

Local and remote clients use the same northbound API and mutual-TLS security
boundary. No unauthenticated local-only API path will be introduced.

The application is split into two projects:

- `Hase.DesktopHost` contains presentation-neutral lifecycle coordination, state
  projection, and testable view-model-facing contracts.
- `Hase.DesktopHost.App` contains the WPF executable, views, resources,
  environment-specific configuration loading, and application composition.

Runtime, transport, protocol, and northbound projects must not reference WPF or
the desktop-host projects.

## Runtime lifecycle model

The desktop application projects these process-level states:

- `Stopped`
- `Starting`
- `Running`
- `Stopping`
- `Faulted`

The lifecycle coordinator serializes start and stop operations. A startup or
shutdown exception is preserved as the last error and transitions the projected
status to `Faulted`.

For the initial implementation, the WPF window and runtime share one lifetime.
Closing the application stops the runtime. System-tray behavior and an
independent window/runtime lifetime are deferred.

## Endpoint state model

The desktop application observes the authoritative runtime-host inventory. It
must not infer, simulate, or independently manage endpoint connection state.

Initial endpoint presentation will include:

- authoritative endpoint identity;
- attachment generation;
- display name;
- connection state; and
- endpoint family or transport description where available.

## Binding and configuration model

The runtime host may listen concurrently on:

- IPv4 loopback for clients on the desktop PC; and
- an explicitly configured private-network address for authorized remote clients.

Addresses, certificate paths, certificate passwords, private keys, and other
environment-specific secrets remain outside source control. The desktop host
will reuse the established ADR-0032 deployment and credential-provisioning
principles rather than define a parallel security model.

## Consequences

### Positive

- The existing runtime remains reusable by console, desktop, service, Linux, and
  test hosts.
- Physical endpoints continue to have exactly one runtime owner.
- Local and remote applications exercise the same public API.
- The operator UI can evolve without introducing dependencies into the runtime.
- Lifecycle behavior can be tested without starting WPF or physical hardware.

### Negative

- Desktop application composition must adapt the existing runtime-host startup
  path instead of copying Protocol Explorer code.
- WPF application shutdown must await orderly runtime shutdown.
- Multi-binding certificate validation and configuration require explicit
  environment-specific setup.

## Deferred work

- production runtime-host composition adapter;
- endpoint inventory and connection-state projection;
- loopback plus private-network multi-binding validation;
- simultaneous desktop and laptop client validation;
- live event log;
- system-tray support;
- independent window and runtime lifetimes;
- diagnostics and administration views;
- endpoint attachment and detachment controls; and
- certificate and configuration management UI.

## Initial implementation increment

The first increment introduces:

- this accepted ADR;
- the `Hase.DesktopHost` project;
- the `Hase.DesktopHost.Tests` project;
- the desktop runtime-host lifecycle contract;
- a presentation-neutral backend adapter boundary;
- serialized lifecycle coordination;
- status-change notification;
- startup and shutdown error projection; and
- automated lifecycle tests.

It does not yet introduce WPF, physical endpoint composition, Kestrel hosting, or
changes to existing runtime and client behavior.


## WPF shell increment

The second increment introduces:

- the `Hase.DesktopHost.App` Windows WPF executable project;
- Prism application composition and dependency injection;
- a desktop-host main window;
- presentation-neutral runtime status projection through
  `DesktopRuntimeHostViewModel`;
- explicit shell information that distinguishes the validation backend from the
  future production runtime composition;
- WPF application startup and orderly shutdown through the lifecycle coordinator;
  and
- automated view-model and shell-backend tests.

This increment intentionally uses a no-op shell validation backend. It does not
yet own physical endpoint connections, start Kestrel, load certificates, or expose
the northbound API. The window therefore labels bindings and runtime identity as
not configured rather than presenting simulated production state.
