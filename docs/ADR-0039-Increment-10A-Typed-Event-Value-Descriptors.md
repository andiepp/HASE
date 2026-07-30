# ADR-0039 Increment 10A — Typed Event Value Descriptors

## Status

Completed.

## Scope

An Event descriptor may now declare zero or one typed value.

`EventPayloadDescriptor` contains:

- a display name;
- an optional description; and
- one existing `DataDescriptor`.

Events without values retain their existing constructor and semantics. Typed
values use the established Boolean, Numeric, String, and ByteArray data
descriptors.

## Protocol compatibility

The Protocol V1 base Event descriptor encoding is unchanged. Typed Event value
metadata is carried in endpoint descriptor extension type `0x02`. Readers that
do not understand this extension may skip it and retain the untyped Event
descriptor.

The remote gRPC contract adds an optional Event payload descriptor at field 4.
Both host and client descriptor mappers preserve the metadata and data
descriptor.

## Verification

Automated coverage includes:

- Events with and without value descriptors;
- constructor validation;
- ByteArray Protocol V1 round trips;
- duplicate and unknown-instrument extension rejection;
- host-side gRPC mapping; and
- client-side snapshot mapping.

This increment changed descriptor contracts and mappings only.
Descriptor-driven formatting and application presentation were completed in
Increments 10B through 10D.
