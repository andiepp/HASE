# ADR-0016 – Variant Serialization

- Status: Accepted
- Date: 2026-07-12

---

# Context

HASE Protocol Version 1 transports runtime property values, command arguments and event payloads between different runtime implementations.

The runtime domain model represents engineering values using `object` in order to support different data types while keeping the runtime model simple.

The binary protocol cannot directly serialize CLR objects because:

- the protocol must remain platform independent;
- HASE targets resource-constrained microcontrollers;
- future implementations are expected in languages other than C#;
- CLR serialization formats are unsuitable for long-term protocol compatibility.

A protocol-defined value representation is therefore required.

---

# Decision

Protocol Version 1 introduces a dedicated **Variant** type system.

Every runtime value is encoded as:

```
VariantType
VariantPayload
```

The protocol defines its own type identifiers that are independent of any programming language.

Protocol Version 1 supports:

| Value | Variant Type |
|------:|--------------|
| 0 | Null |
| 1 | Boolean |
| 2 | Int32 |
| 3 | Int64 |
| 4 | Double |
| 5 | String |

Additional variant types may be introduced in future protocol versions while preserving backward compatibility whenever practical.

Unsupported runtime types are rejected during serialization.

---

# Property Values

Runtime property values are represented as:

```
Variant
TimestampUtc
PropertyQuality
```

The timestamp is encoded as a signed 64-bit Unix timestamp in milliseconds (UTC).

Property quality is encoded as a single byte.

---

# Consequences

## Advantages

- Platform independent.
- Compact binary representation.
- Efficient on embedded systems.
- Easy to implement in C, C++, Rust, Python and other languages.
- Independent of CLR implementation details.
- Stable long-term wire format.
- Reusable across all runtime protocol messages.

## Trade-offs

- Runtime implementations must explicitly map native language types to protocol Variant types.
- Unsupported runtime types require explicit serializer extensions before they can be transmitted.

---

# Rationale

Alternative approaches were considered.

## CLR Binary Serialization

Rejected.

Reasons:

- .NET specific.
- Version dependent.
- Unsuitable for embedded targets.
- Not interoperable.

## JSON Values

Rejected.

Reasons:

- Larger payload size.
- Higher parsing cost.
- Loss of compact binary representation.

## Dedicated Variant Type System

Accepted.

A protocol-defined Variant type provides a compact, deterministic and platform-independent representation suitable for both desktop applications and resource-constrained embedded devices.

It also establishes a stable foundation for future protocol versions.