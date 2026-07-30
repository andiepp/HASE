# ADR-0039 Increment 10D — Desktop Host Event Presentation

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host now preserves complete Event descriptors in its
persistent inventory and presents Event occurrences through the shared
descriptor-driven payload formatter.

For each occurrence, the production backend captures the current published
snapshot and resolves the descriptor by:

- endpoint identity;
- attachment generation;
- instrument identity; and
- Event path.

An occurrence from an earlier attachment generation never uses a descriptor
from a replacement attachment. If the exact descriptor is no longer available,
the occurrence remains visible with fallback metadata and a stable payload
diagnostic.

The Event inventory displays payload name, type, and description. The endpoint
Event history displays Event name and path, payload name and formatted value,
and diagnostics only when the payload is missing, unexpected, mismatched, or
unsupported.

Boolean, Numeric, String, and ByteArray payloads use the shared presentation
policy. The previous generic `ToString()` formatting has been removed.

## Verification

Automated coverage includes complete payload metadata projection, ViewModel
replacement after payload changes, exact attachment-generation resolution,
unknown Event fallback, parameterless Events, every typed payload family,
missing, unexpected, and mismatched payloads, newest-first history, retention,
and consecutive endpoint source identity.

Cross-application multi-type runtime validation remains Increment 10E.
