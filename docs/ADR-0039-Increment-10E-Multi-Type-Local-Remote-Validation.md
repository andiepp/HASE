# ADR-0039 Increment 10E — Multi-Type Local/Remote Validation

## Status

Implemented for validation.

## Scope

The existing opt-in `simulation-byte-buffer-validation` endpoint now provides
one deterministic Event for every payload shape supported by ADR-0039:

| Trigger Command | Event | Payload descriptor | Published value |
| --- | --- | --- | --- |
| Emit No-Payload Event | No-Payload Event | none | `null` |
| Emit Boolean Event | Boolean Event | Boolean | `true` |
| Emit Numeric Event | Numeric Event | Numeric temperature in Celsius | `23.5` |
| Emit String Event | String Event | String | `HASE event validation` |
| Emit ByteArray Event | ByteArray Event | ByteArray | bytes `01 AB 00 FF` |

All trigger Commands are parameterless. The simulation executor publishes each
occurrence through the corresponding `RuntimeEvent`. Consequently, the same
occurrence follows the normal Desktop Host observation path and the normal
northbound remote observation path. No validation-only transport or
presentation bypass exists.

The Event occurrence timestamp is supplied by the executor's `TimeProvider` and
is expressed in UTC. An unsupported Command or a trigger Command carrying an
argument fails without publishing an occurrence.

## Automated verification

The simulation tests verify:

- the five parameterless trigger Command descriptors;
- the parameterless, Boolean, Numeric, String, and ByteArray Event descriptors;
- payload display names and Numeric quantity/unit metadata;
- the exact value and UTC timestamp published by every trigger;
- rejection of an Event trigger carrying an argument; and
- delivery of all five occurrences through the normalized runtime-host
  northbound observation stream with authoritative endpoint, attachment
  generation, instrument, and Event identities.

The protocol, gRPC, Laptop Client, Desktop Host, and shared presentation tests
from increments 10A through 10D continue to cover descriptor transport, remote
value normalization, descriptor resolution, and formatted presentation.

## Manual two-application validation

Use builds from the same commit on the Desktop Host and Laptop Client.
Occurrences are transient and are not replayed, so connect the Laptop Client
before executing a trigger.

1. Start the Desktop Host and confirm that
   `simulation-byte-buffer-validation` is published and Ready.
2. Start and connect the Laptop Client.
3. Select `Simulated Property Editor Validation` in both applications.
4. Execute each Event Validation trigger Command once.
5. Confirm that both applications show the same source endpoint, attachment
   generation, instrument, Event path, payload name, and payload text.

Expected payload presentation:

| Event | Desktop Host and Laptop Client text | Diagnostic |
| --- | --- | --- |
| No-Payload Event | `No payload` | none |
| Boolean Event | `True` | none |
| Numeric Event | `23.5` | none |
| String Event | `HASE event validation` | none |
| ByteArray Event | `01AB00FF` | none |

Execute at least one trigger from the Desktop Host and one from the Laptop
Client. Each occurrence must appear in both applications, demonstrating that
Command direction does not change Event publication or presentation.

## Stop condition

Increment 10E is accepted when the complete solution builds, all tests pass,
and all five valid occurrences appear without payload diagnostics in both
applications.

