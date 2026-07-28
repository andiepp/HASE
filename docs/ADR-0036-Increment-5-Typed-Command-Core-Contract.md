# ADR-0036 Increment 5 — Typed Command Core Contract

## Scope

This increment establishes descriptor semantics for typed Command arguments in
the immutable HASE core model.

It adds:

- `CommandArgumentDescriptor`;
- one required typed argument per typed Command;
- argument display name and optional description;
- argument data described by the existing `DataDescriptor` hierarchy;
- explicit parameterless-versus-typed Command identity; and
- complete compatibility for the existing two-argument `CommandDescriptor`
  constructor.

## Semantics

A `CommandDescriptor` with a null `Argument` is parameterless.

A `CommandDescriptor` created with a `CommandArgumentDescriptor` requires
exactly one argument whose type is described by `CommandArgumentDescriptor.Data`.

The model intentionally does not support:

- optional arguments;
- multiple arguments;
- structured argument lists;
- default argument values; or
- Command result values.

ByteArray arguments use `ByteArrayDataDescriptor` and carry immutable
`ByteArrayValue` instances at execution time.

## Compatibility

The existing constructor remains unchanged:

```csharp
new CommandDescriptor(path, displayName)
```

It continues to create a parameterless Command. All existing endpoint
descriptors therefore retain their current core-model behavior without source
changes.

## Serialization constraint discovered

Protocol Version 1 currently serializes each Command descriptor as:

```text
Path
DisplayName
Optional Description
```

Command descriptors are not individually length-framed inside the instrument
Command collection.

Appending an argument marker is unsafe:

- an older reader can interpret argument bytes as the next collection item;
- a new reader cannot distinguish a missing argument marker from the first
  byte of the next parameterless Command; and
- the last parameterless Command is followed immediately by the Event
  collection count.

This increment therefore does not alter wire serialization. Typed descriptor
serialization requires an explicit versioned or length-delimited descriptor
format decision. It must not be introduced as an unversioned trailing field.

## Excluded work

This increment does not change:

- native or Compact descriptor serialization;
- runtime argument validation;
- Command execution requests;
- normalized northbound services;
- gRPC contracts;
- WPF applications; or
- endpoint firmware.
