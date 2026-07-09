# ADR-0001: Immutable Engineering Contracts

## Status

Accepted

## Context

HASE models engineering systems such as instruments, endpoints, properties,
commands and events.

These objects describe the engineering capabilities of a system.

They are shared by discovery, runtime, diagnostics, SDK and Studio.

Changing these objects during runtime would make it difficult to reason about
the system and would complicate caching, testing and synchronization.

## Decision

All engineering contracts in Hase.Core are immutable.

Examples include:

- EndpointDescriptor
- InstrumentDescriptor
- PropertyDescriptor
- CommandDescriptor
- EventDescriptor
- DataDescriptor
- Quantity
- Unit

Changes to the live engineering system are represented by runtime objects
instead of modifying descriptors.

## Consequences

Advantages

- thread-safe
- easy to cache
- deterministic
- easy to serialize
- simple equality
- easy testing

Disadvantages

- changes require creating new descriptor instances
- runtime state must be stored separately

## Related

Architecture.md

RuntimeArchitecture.md