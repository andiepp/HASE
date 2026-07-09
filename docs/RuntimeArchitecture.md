# HASE Runtime Architecture

The runtime graph mirrors the engineering descriptor graph.

```text
RuntimeContext
└── RuntimeEndpoint
    └── RuntimeInstrument
        ├── RuntimeProperty
        ├── RuntimeCommand
        └── RuntimeEvent

RuntimeProperty
→ RuntimeInstrument
→ RuntimeEndpoint
→ RuntimeContext

