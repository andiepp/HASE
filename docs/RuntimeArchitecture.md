# HASE Runtime Architecture

## Purpose

The runtime represents the live engineering system seen by one application.

It is created from immutable engineering contracts defined in Hase.Core.

---

# Runtime Graph

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

Each runtime object references its immutable descriptor.

---

# RuntimeContext

The RuntimeContext is the root object for one application.

Responsibilities:

- owns all runtime endpoints
- receives notifications
- provides lookup
- acts as the root of the runtime tree

A RuntimeContext belongs to exactly one application.

---

# RuntimeEndpoint

Represents one engineering endpoint.

Responsibilities:

- references an EndpointDescriptor
- owns RuntimeInstrument objects
- forwards notifications

An endpoint may contain zero or more instruments.

Gateway-only endpoints are therefore supported.

---

# RuntimeInstrument

Represents one instrument.

Responsibilities:

- references an InstrumentDescriptor
- creates runtime objects from the InstrumentInterface
- updates property values
- forwards notifications

The RuntimeInstrument is constructed automatically from its descriptor.

---

# RuntimeProperty

Represents one live engineering property.

Responsibilities:

- references a PropertyDescriptor
- stores the latest PropertyValue
- notifies observers when the value changes

A RuntimeProperty is initially created without a value.

---

# RuntimeCommand

Represents one executable command.

Currently it references the corresponding CommandDescriptor.

Execution behavior will be added later.

---

# RuntimeEvent

Represents one engineering event.

Currently it references the corresponding EventDescriptor.

Runtime event processing will be added later.

---

# Notification Flow

Property updates propagate through the runtime hierarchy.

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

Applications may subscribe at any level of the hierarchy.

---

# Runtime Tree

All runtime objects implement IRuntimeNode.

This allows generic traversal of the runtime graph independent of the concrete object types.

---

# Services

The runtime itself performs no discovery or communication.

Those responsibilities belong to services.

Examples:

- Discovery
- Polling
- Recording
- Replay

Services modify the runtime graph but do not modify engineering contracts.


Use your repository’s existing ADR directory and naming style if it differs from `docs/adr`.

---

## Simulation

HASE simulation models physical processes independently of HASE runtime objects.

The simulation subsystem is not a separate HASE runtime mode. It is a source of instrument behavior that can later be exposed through the same runtime interfaces as physical instruments.

The conceptual flow is:

---

ValueGenerator
      |
      v
Simulation
      |
      v
Physical state
      |
      v
Simulated instrument
      |
      v
HASE runtime