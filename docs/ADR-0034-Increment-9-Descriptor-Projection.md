# ADR-0034 Increment 9 — Descriptor Projection

## Status

Implemented for validation.

## Scope

This increment projects authoritative endpoint descriptor information into the
Desktop Runtime Host operator UI.

The inventory snapshot now carries the endpoint description and instrument
descriptor metadata. Each instrument projection contains its identity, name,
kind, manufacturer, model, serial number, firmware version, hardware revision,
and description.

The existing four-argument `DesktopRuntimeEndpointSnapshot` constructor remains
source-compatible. Properties, Commands, Events, and live values remain outside
this increment.

No transport, attachment, supervision, protocol, northbound, or gRPC behavior
is changed.
