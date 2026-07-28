# ADR-0035 Increment 2 — Persistent Command Projection

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host production inventory now projects every Command
descriptor beneath its owning instrument.

Each Command snapshot contains exactly the metadata supported by the current
HASE `CommandDescriptor`:

- descriptor path;
- display name; and
- optional description.

No argument or return-value metadata is projected because the current domain
descriptor does not define those fields.

`DesktopRuntimeCommandViewModel` exposes immutable descriptor metadata.
`DesktopRuntimeInstrumentViewModel` owns an observable Commands collection and
reconciles it by ordinal Command path.

During inventory refresh:

- a Command with the same path and unchanged metadata retains its ViewModel
  instance;
- a Command with changed immutable metadata receives a replacement ViewModel;
- missing Commands are removed;
- new Commands are added; and
- descriptor order is restored.

The WPF endpoint details view displays the Command count and read-only Command
cards after the existing Property projection.

Automated tests cover:

- Command metadata projection;
- persistent identity for unchanged descriptors;
- replacement for changed immutable metadata;
- addition, removal, and descriptor ordering;
- required Command paths; and
- required display names.

The operator-operation service introduced by Increment 1 is not invoked. This
increment adds no execution control, argument entry, operation state, Property
write behavior, runtime contract change, transport change, protocol change, or
gRPC change.

