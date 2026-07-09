# ADR-0003: Hierarchical Runtime Graph

## Status

Accepted

## Context

Engineering systems are naturally hierarchical.

Applications need to navigate between endpoints, instruments, properties,
commands and events.

Future components such as Hase.Studio, diagnostics, logging and gateways
require generic navigation through the runtime model.

## Decision

The runtime is represented as a hierarchical object graph.

Every runtime object implements `IRuntimeNode`.

Each node provides:

- a reference to its parent
- a collection of its children

The runtime hierarchy is:

```
RuntimeContext
    │
    └── RuntimeEndpoint
            │
            └── RuntimeInstrument
                    │
                    ├── RuntimeProperty
                    ├── RuntimeCommand
                    └── RuntimeEvent
```

Navigation is supported in both directions.

## Consequences

Advantages

- generic tree traversal
- simple UI integration
- supports diagnostics and tracing
- enables generic search
- consistent navigation model

Disadvantages

- parent references must be maintained
- runtime construction is slightly more complex

## Related

ADR-0002

RuntimeArchitecture.md