# ADR-0034 Increment 8 — Endpoint Master/Detail Presentation

## Status

Implemented for validation.

## Scope

This increment establishes the endpoint master/detail presentation hierarchy.

The runtime inventory now owns an explicit selected endpoint. Selection:

- defaults to the first published endpoint;
- remains stable while the selected endpoint remains published;
- survives endpoint state updates and collection reordering;
- moves to the first remaining endpoint if the selected endpoint ends; and
- becomes empty when no endpoints are published.

`EndpointDetailsViewModel` observes the selected persistent endpoint view model
and projects its current:

- display name;
- endpoint identity;
- connection state; and
- attachment generation.

The WPF window presents published endpoints as a selectable list beside a
read-only details pane.

Descriptor metadata, instruments, Properties, Commands, and Events are
intentionally excluded from this increment.

No runtime, transport, attachment, supervision, northbound, or gRPC behavior is
changed.
