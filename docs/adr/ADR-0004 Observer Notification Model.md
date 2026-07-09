# ADR-0004: Hierarchical Runtime Notification

## Status

Accepted

## Context

Applications require notification when engineering values change.

Examples include:

- Hase.Studio
- Logging
- Recording
- Alarm processing
- Automation
- Diagnostics

Subscribing individually to every property does not scale for large engineering systems.

## Decision

Property change notifications propagate through the runtime hierarchy.

```
RuntimeProperty
        │
        ▼
RuntimeInstrument
        │
        ▼
RuntimeEndpoint
        │
        ▼
RuntimeContext
```

Each runtime level may observe its children and forward notifications to its own observers.

Applications may subscribe at the level appropriate for their use case.

## Consequences

Advantages

- scalable notification model
- supports local and global observers
- no global event bus required
- naturally follows the runtime hierarchy

Disadvantages

- notifications pass through multiple runtime objects
- runtime objects must maintain observer registrations

## Related

ADR-0003

RuntimeArchitecture.md