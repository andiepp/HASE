# HASE Architecture

## Purpose

HASE (Hardware Abstraction System for Engineering) is a platform for building engineering applications around self-describing hardware.

The primary design goal is to separate the engineering model from runtime execution, communication transports and user interface concerns.

---

# Layered Architecture

HASE is organized into independent layers.

```
Application
        │
        ▼
Services
        │
        ▼
Runtime
        │
        ▼
Core
```

Each layer depends only on the layer below it.

---

# Hase.Core

Hase.Core defines the immutable engineering model.

It contains no runtime state and no communication logic.

Examples:

- Endpoints
- Instruments
- Properties
- Commands
- Events
- Data descriptors
- Quantities
- Units

Objects in Hase.Core are engineering contracts.

---

# Hase.Runtime

Hase.Runtime creates a live runtime graph from engineering contracts.

Runtime objects are mutable.

They represent the current application view of an engineering system.

The runtime graph mirrors the engineering model.

---

# Services

Services perform work on the runtime graph.

Examples include:

- Discovery
- Polling
- Recording
- Replay
- Diagnostics

Services modify the runtime graph but never the engineering contracts.

---

# Future Layers

The current architecture anticipates additional layers.

Examples:

- Hase.Transport
- Hase.Gateway
- Hase.Diagnostics
- Hase.Studio
- Hase.SDK

These layers are intentionally independent from Hase.Core.

---

# Design Principles

## Immutable engineering contracts

Engineering contracts are immutable.

A contract describes what a system is capable of.

---

## Mutable runtime

Runtime objects represent the current live state of an engineering system.

---

## Separation of concerns

Engineering contracts never contain:

- communication
- transport
- polling
- user interface
- runtime state

---

## Transport independence

The engineering model is independent of Serial, TCP/IP, BLE, MQTT or future transports.

---

## Descriptor-driven runtime

The runtime graph is automatically constructed from engineering descriptors.

---

## Services operate on the runtime

Behavior is implemented by services.

Runtime objects remain lightweight representations of the engineering system.

### Simulation independence

Physical-process simulations are independent of HASE runtime objects, 
descriptors, transports, and protocols.

Simulated instruments adapt physical simulation state to the normal HASE runtime model. 
Applications should therefore interact with physical and simulated instruments 
through the same interfaces.

## Protocol message model

Protocol communication consists of immutable protocol messages.

The protocol defines four semantic message categories:

- Request
- Response
- Notification
- Stream

Responses complete Requests.

Notifications are asynchronous.

Streams represent large or sequential data transfers.

The message model is independent of transport, framing, and serialization.

See ADR-0010.

