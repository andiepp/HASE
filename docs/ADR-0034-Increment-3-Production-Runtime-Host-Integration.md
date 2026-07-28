# ADR-0034 Increment 3 — Production Runtime-Host Integration

## Status

Implemented for validation.

## Scope

This increment replaces the WPF shell-validation backend with the controlled
ADR-0032 private-network runtime-host composition.

The WPF process now:

- loads the external desktop private-network deployment configuration;
- creates the native-network and compact-serial attachment host;
- attaches the configured ESP32 endpoint;
- discovers, verifies, and explicitly attaches one Arduino Uno;
- creates the northbound snapshot, Property, Command, and observation services;
- starts the mutual-TLS private-network gRPC deployment;
- displays the real runtime-host identity, API version, and configured private-
  network binding; and
- stops and disposes the gRPC deployment, northbound composition, attachment
  inventory, supervisors, sessions, and transports when the application exits.

No private-network address, certificate, password, thumbprint, private key, or
ESP32 address is stored in source control.

## Startup arguments

The executable requires exactly two arguments:

```text
Hase.DesktopHost.App.exe <desktop-private-network.json> <esp32-host>
```

The first argument is the external ADR-0032 desktop deployment file. The second
is the ESP32 host name or IP address.

Startup fails closed when configuration loading, certificate selection,
endpoint attachment, authoritative Arduino verification, two-endpoint
publication, or mutual-TLS hosting fails.

## Binding limitation

This increment activates the established ADR-0032 private-network listener.
A simultaneous loopback listener remains deferred to the dedicated
multi-binding increment. A client running on the same desktop can already use
the configured private-network address without traversing Tailscale, while the
future multi-binding increment will add the explicit `127.0.0.1` endpoint.
