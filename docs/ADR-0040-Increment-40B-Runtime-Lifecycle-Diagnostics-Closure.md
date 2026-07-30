# ADR-0040 Increment 40B — Runtime Lifecycle Diagnostics Closure

## Status

Completed.

## Baseline

Increment 40B is complete at 3,799 passing automated tests.

## Delivered increments

### 40B1 — shared runtime lifecycle diagnostics

The runtime context publishes endpoint inventory changes and installs one
diagnostic observer for every published runtime endpoint.

Operational records cover:

- endpoint publication and removal;
- attachment start and ready;
- connection-state transitions;
- synchronization start and completion; and
- recovery start and completion.

Free-form `EndpointConnectionStatus.Detail` is not copied.

### 40B2 — recovery scheduling diagnostics

`RuntimeEndpointReconnectDiagnosticPolicy` decorates any existing reconnect
policy and publishes `RecoveryScheduled` without altering the selected delay.

Records contain endpoint identity, human-readable attempt number, exact
zero-based retry index, and invariant delay in milliseconds.

### 40B3A — generation-qualified attachment diagnostics

The northbound attachment projection publishes `AttachmentPublished` and
`AttachmentEnded` for committed live projection changes.

Both records contain authoritative endpoint identity and the matching
northbound attachment generation. Replacement retains the old generation for
ending and assigns a new generation to publication.

### 40B3B — production recovery activation

Native Protocol V1 and Compact Serial operational resource graphs wrap their
supplied reconnect policy immediately before supervisor construction.

The wrapper receives the runtime context diagnostic publisher and authoritative
runtime endpoint identity. It does not import the later northbound attachment
generation.

## Preserved behavior

- Native and Compact Serial supervisor loops are unchanged.
- The wrapped reconnect policy remains authoritative.
- Retry delays remain immediate, 1 s, 2 s, 5 s, and 10 s maximum.
- Attachment generation remains northbound projection state.
- Runtime endpoint authority and generation-qualified northbound targeting are
  unchanged.
- Default null diagnostics collect nothing.
- Diagnostic observer failures cannot affect runtime behavior.

## Privacy

Lifecycle records exclude:

- exception and free-form status text;
- private-network addresses and ports;
- COM names and USB discovery metadata;
- certificates, thumbprints, credentials, and secrets; and
- machine-specific configuration paths.

## Verification

Automated coverage verifies:

- record validation, immutability, UTC timestamps, and sequencing;
- bounded collection and filtering;
- null and throwing sink isolation;
- native and compact connection lifecycles;
- recovery scheduling and unchanged delay selection;
- attachment publication, ending, duplicate suppression, and replacement;
- authoritative endpoint identity; and
- attachment generation at the owning northbound boundary.

Physical validation and UI presentation are not required for 40B. They remain
planned for later ADR-0040 increments.

## Next

ADR-0040 Increment 40C will add structured Property, Command, and Event
interaction diagnostics.
