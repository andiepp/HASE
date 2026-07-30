# ADR-0039 Increment 10C — Laptop Client Event Presentation

## Status

Completed.

## Scope

The WPF Laptop Client now presents Event payloads from the authoritative Event
descriptor.

For each remote occurrence, the client:

1. resolves the attachment-specific instrument and Event descriptor;
2. normalizes the remote value union to a supported core value;
3. formats the value with the shared `EventPayloadFormatter`; and
4. captures payload metadata, formatted text, and formatting status in the
   immutable occurrence ViewModel.

The Live Events presentation shows the payload display name and formatted
value. Optional payload descriptions are shown when present. Diagnostic status
is shown only for missing, unexpected, mismatched, or unsupported payloads.

Boolean, Numeric, String, and ByteArray payloads are supported. ByteArrays use
uppercase hexadecimal text without separators.

## Verification

Automated coverage includes normalization of every remote value kind,
parameterless Events, every typed payload kind, payload metadata, missing
payloads, unexpected payloads, type mismatches, and diagnostic visibility.

This increment changed the Laptop Client only. Desktop Runtime Host Event
presentation was completed in Increment 10D.
