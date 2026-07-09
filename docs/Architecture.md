# HASE Architecture

HASE is a platform for self-describing engineering systems.

## Layers

### Hase.Core

Defines immutable engineering contracts:

- Endpoints
- Instruments
- Properties
- Commands
- Events
- Data descriptors
- Units and quantities

### Hase.Runtime

Creates a live runtime graph from descriptors.

Runtime objects are mutable and represent the current application view of the engineering system.

### Services

Services operate on the runtime graph.

Examples:

- Discovery
- Polling
- Recording
- Replay
- Diagnostics

### Future Layers

- Hase.Transport
- Hase.Gateway
- Hase.Diagnostics
- Hase.Studio
- Hase.SDK