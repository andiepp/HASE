# ADR-0036 Increment 7 — Runtime Command Argument Validation

## Scope

This increment makes the runtime host authoritative for typed Command argument
validation.

Validation occurs after generation, instrument, and Command resolution and
before the attachment-bound endpoint operation is invoked.

## Validation rules

| Command descriptor | Supplied argument | Outcome |
| --- | --- | --- |
| Parameterless | null | Accepted |
| Parameterless | non-null | Rejected |
| Typed | null | Rejected |
| Boolean | `bool` | Accepted |
| String | `string` | Accepted |
| Numeric | `int`, `long`, or `double` | Accepted |
| ByteArray | `ByteArrayValue` | Accepted |
| Any typed descriptor | Other value type | Rejected |

Numeric values remain in their supplied CLR representation. The validator does
not convert between `int`, `long`, and `double`.

String parsing, numeric parsing, Boolean parsing, Base64 decoding, hexadecimal
decoding, and raw `byte[]` conversion are not performed.

## Execution ordering

The runtime execution sequence is:

```text
Resolve current attachment generation
    → Resolve instrument
    → Resolve Command
    → Validate argument against authoritative Command descriptor
    → Invoke attachment-bound Command operation once
```

An invalid argument returns `ArgumentNotSupported` and never reaches the
endpoint adapter.

## Compatibility

Existing parameterless Commands continue to execute successfully with null.

Earlier tests that passed a non-null value to a parameterless test Command now
describe that Command with a required Numeric argument. This preserves the
original pass-through assertion under the authoritative typed contract.

## Excluded work

This increment does not add:

- descriptor range validation for Numeric arguments;
- ByteArray maximum-length constraints in the core descriptor;
- normalized remote-contract changes;
- gRPC mapping;
- client-side argument editors;
- simulated typed-Command behavior; or
- physical endpoint firmware changes.
