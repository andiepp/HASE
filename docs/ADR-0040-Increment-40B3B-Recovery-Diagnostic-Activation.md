# ADR-0040 Increment 40B3B — Recovery Diagnostic Activation

## Status

Implemented; awaiting solution-wide validation.

## Scope

This increment activates the recovery-scheduling diagnostic decorator in the
two production operational resource graphs:

- native Protocol V1 over framed TCP; and
- Compact Serial Protocol V1.

No supervisor retry loop, reconnect policy, delay schedule, or attachment
lifecycle is changed.

## Composition

`NativeEndpointOperationalResources.CreateNetwork` and
`CompactEndpointOperationalResources.CreateSerial` wrap the supplied
`IRuntimeEndpointReconnectPolicy` with
`RuntimeEndpointReconnectDiagnosticPolicy` immediately before constructing
their supervisor.

The decorator receives:

- the exact supplied reconnect policy;
- `RuntimeEndpoint.Context.Diagnostics`; and
- the authoritative runtime endpoint identity.

It does not receive northbound attachment generation. That generation is owned
later by `RuntimeHostAttachmentProjection` and must not be moved into the
transport graph for diagnostic convenience.

## Native symmetry

`NativeEndpointOperationalResources` now retains its supervisor as an internal
composition-test surface, matching the existing Compact Serial operational
resources. This does not expose the supervisor publicly or change resource
disposal order.

## Behavioral compatibility

- The wrapped policy remains the source of every retry delay.
- The first retry remains immediate.
- Later retries retain the established 1 s, 2 s, 5 s, and capped 10 s delays.
- Default null diagnostics collect nothing.
- Diagnostic sink failures cannot alter recovery scheduling.
- Recovery start and completion remain owned by the shared lifecycle observer.
- Generation-qualified attachment publication and ending remain owned by the
  northbound projection.

No connection address, port, COM name, discovery metadata, certificate
information, configuration path, or free-form status detail enters scheduling
records.

## Verification

Existing native and compact operational-resource composition tests now verify:

- the supervisor owns `RuntimeEndpointReconnectDiagnosticPolicy`;
- the decorator retains the exact supplied policy;
- endpoint identity comes from the runtime endpoint; and
- attachment generation remains absent at the transport boundary.

The focused decorator tests from Increment 40B2 continue to verify delay
preservation, scheduling record contents, null diagnostics, and observer
isolation.
