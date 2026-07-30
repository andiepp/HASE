# ADR-0040 Increment 40B3A — Generation-Qualified Attachment Diagnostics

## Status

Implemented; awaiting solution-wide validation.

## Scope

This increment publishes structured operational diagnostics from the shared
northbound attachment projection when an observed attachment is published or
ended.

The projection is the existing authoritative owner of northbound attachment
generation. Diagnostics therefore reuse its committed projection changes and do
not introduce another generation map or move generation ownership into the
transport layer.

## Records

Both records use level `Operational` and category `RuntimeAttachment`.

`AttachmentPublished` contains:

- authoritative endpoint identity; and
- the newly assigned attachment generation.

`AttachmentEnded` contains:

- authoritative endpoint identity; and
- the retired attachment generation.

The same committed inventory entry produces at most one publication record and
one ending record. Repeated or stale inventory notifications are ignored by the
existing projection before diagnostic publication.

An attachment replacement with the same endpoint identity publishes the old
ending before the new publication and uses different generations.

## Safety

Records contain no connection address, port, COM name, discovery metadata,
certificate information, configuration path, or free-form connection-status
detail.

The runtime context's diagnostic publisher isolates sink failures. A diagnostic
observer therefore cannot interrupt projection state changes or existing
projection observers.

## Compatibility

Snapshot-only synchronization continues to update the projection without
inventing historical publication events. Structured attachment diagnostics are
emitted for committed live inventory changes observed by the projection.

No inventory, projection ordering, snapshot, observation, or generation
semantics are changed.

## Verification

Focused tests cover:

- one generation-qualified publication;
- duplicate-publication suppression;
- one ending with the same generation;
- duplicate-ending suppression;
- replacement order and new generation; and
- throwing diagnostic-sink isolation.

Increment 40B3B will activate the recovery scheduling decorator at the native
and Compact Serial attachment composition boundaries.
