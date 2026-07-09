# ADR-0002: Descriptor-Driven Runtime

## Status

Accepted

## Context

Engineering contracts describe the capabilities of an engineering system.

Applications, however, require mutable runtime objects representing the
current live state of the system.

The runtime must always remain consistent with the engineering contracts.

Manual construction of runtime objects would duplicate knowledge already
contained in the descriptors and increase the risk of inconsistencies.

## Decision

The runtime graph is automatically constructed from engineering descriptors.

The engineering descriptors remain immutable.

The runtime graph mirrors the descriptor hierarchy.

```
EndpointDescriptor
        │
        ▼
RuntimeEndpoint
        │
        ▼
RuntimeInstrument
        │
        ▼
RuntimeProperty
```

Runtime objects always reference their corresponding descriptors.

## Consequences

Advantages

- single source of truth
- automatic runtime construction
- consistent runtime graph
- transport independent
- easy testing
- easy discovery integration

Disadvantages

- runtime objects cannot exist without descriptors
- descriptor quality directly affects runtime quality

## Related

ADR-0001

Architecture.md

RuntimeArchitecture.md