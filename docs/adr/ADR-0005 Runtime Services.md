# ADR-0005: Runtime Services

## Status

Accepted

## Context

The runtime graph represents the current state of an engineering system.

Operations such as discovery, polling, recording and replay require active
behavior but should not become responsibilities of the runtime objects.

Embedding such behavior into runtime objects would violate the Single
Responsibility Principle and make testing more difficult.

## Decision

Behavior is implemented by services operating on the runtime graph.

Typical services include:

- Discovery
- Polling
- Subscription
- Recording
- Replay
- Diagnostics

Services may create, modify and observe runtime objects but never modify
engineering contracts.

The runtime graph remains a lightweight representation of the engineering
system.

## Consequences

Advantages

- clear separation of responsibilities
- independent service implementations
- easier unit and integration testing
- transport-independent runtime model
- services can be replaced or extended

Disadvantages

- additional service layer
- more classes than embedding behavior directly into runtime objects

## Related

ADR-0002

ADR-0003

Architecture.md

RuntimeArchitecture.md