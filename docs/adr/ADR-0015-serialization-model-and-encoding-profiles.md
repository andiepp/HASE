# ADR-0015: Serialization Model and Encoding Profiles

## Status

Accepted

## Context

ADR-0008 defines the Properties, Commands, and Events interaction model.

ADR-0009 defines protocol capability negotiation.

ADR-0010 defines the semantic protocol message model.

ADR-0011 defines the protocol connection lifecycle.

ADR-0012 defines Endpoint Sessions.

ADR-0013 defines the Protocol Context.

ADR-0014 separates protocol messages, serialization, framing, and transport
communication.

The protocol now requires a stable definition of how semantic protocol
messages are represented as serialized data.

HASE must support endpoints with significantly different resource and tooling
requirements.

Resource-constrained microcontrollers require:

* compact representations;
* bounded memory usage;
* predictable parsing;
* minimal processing overhead;
* small message sizes;
* limited schema complexity.

Feature-rich endpoints, development tools, diagnostics, and human-readable
tracing benefit from:

* descriptive field names;
* inspectable messages;
* extensible structures;
* straightforward tooling;
* human-readable representation.

These requirements should not produce separate HASE protocols.

The semantic protocol message model must remain common across all
representations.

The architecture therefore requires a distinction between:

* the semantic Protocol Message;
* the structural Serialization Model;
* the concrete Encoding Profile.

## Decision

HASE defines one canonical Serialization Model for protocol messages.

The Serialization Model defines the structural representation of the semantic
message model.

Concrete Encoding Profiles represent that Serialization Model in different
forms.

Initial Encoding Profiles are:

* Compact Binary;
* JSON.

Future Encoding Profiles may be added without changing the semantic protocol
message model.

Encoding Profiles are negotiated through the protocol capability model.

Only one mutually supported Encoding Profile is active for a protocol
connection unless a future protocol extension explicitly permits multiple
active profiles.

### Architectural layers

The serialization architecture is:

```text
Protocol Message
        │
        ▼
Serialization Model
        │
        ▼
Encoding Profile
        │
        ▼
Serialized Message
        │
        ▼
Framer
```

The Protocol Message defines meaning.

The Serialization Model defines structure.

The Encoding Profile defines representation.

The Framer defines transport boundaries.

These responsibilities remain separate.

### Protocol Message

A Protocol Message is a semantic protocol object as defined by ADR-0010.

Examples include:

* a Property-read Request;
* a Property-write Response;
* a Command invocation;
* a Property-change Notification;
* an Event Notification;
* a Stream data message.

A Protocol Message is independent of:

* field names;
* numeric field identifiers;
* byte order;
* text encoding;
* delimiters;
* binary layout;
* JSON syntax;
* framing.

### Serialization Model

The Serialization Model is the canonical structural representation of a
Protocol Message.

It defines:

* the message kind;
* required fields;
* optional fields;
* field meaning;
* field data types;
* field constraints;
* default-value rules;
* validation rules;
* version-evolution rules;
* relationships between fields.

The Serialization Model does not define:

* binary field numbers;
* JSON property names;
* byte order;
* numeric compression;
* text representation;
* frame boundaries.

Those details belong to Encoding Profiles.

### Canonical schema

Every protocol message type has one canonical serialization schema.

The canonical schema must map unambiguously to the semantic Protocol Message.

Two Encoding Profiles representing the same protocol message must preserve the
same protocol meaning.

An Encoding Profile must not redefine:

* the meaning of a field;
* whether an operation succeeds or fails;
* the semantics of Properties, Commands, or Events;
* correlation behavior;
* Endpoint Session ownership;
* protocol lifecycle behavior.

Encoding Profiles may differ only in representation and in explicitly
negotiated profile limits.

### Encoding Profiles

An Encoding Profile defines how the canonical Serialization Model is encoded.

An Encoding Profile may define:

* field identifiers;
* field names;
* field ordering;
* primitive value encoding;
* integer representation;
* floating-point representation;
* string encoding;
* binary-data encoding;
* collection representation;
* optional-field representation;
* default-value omission;
* message-type representation;
* version representation;
* maximum encoded sizes;
* deterministic encoding rules;
* unknown-field behavior permitted by the profile.

An Encoding Profile must preserve the canonical Serialization Model.

### Capability negotiation

Supported Encoding Profiles are protocol capabilities.

Both peers advertise the Encoding Profiles they support.

The active Encoding Profile is selected from the mutually supported profiles.

A profile advertised by only one peer is not available for the connection.

The selected profile becomes part of the effective connection configuration.

Encoding Profile selection occurs before ordinary encoded protocol messages
are exchanged.

The bootstrap mechanism used before profile selection must be defined by the
applicable protocol or transport profile.

### Profile preference

Peers may advertise an ordered preference for supported Encoding Profiles.

Profile selection should prefer the most appropriate mutually supported
profile while respecting protocol and transport constraints.

Typical preferences may include:

* Compact Binary for constrained devices and bandwidth-limited transports;
* JSON for diagnostics, development, simulation, and human-readable tracing.

The exact negotiation message and preference algorithm are not defined by this
ADR.

### Connection scope

The selected Encoding Profile belongs to one protocol connection.

It must be renegotiated after reconnection when the lifecycle requires renewed
capability negotiation.

An Endpoint Session may continue across protocol reconnects while the selected
Encoding Profile changes, provided that endpoint identity and protocol
compatibility are successfully verified.

Serialized data from one Encoding Profile must not be interpreted using
another Encoding Profile.

### Compact Binary profile

The Compact Binary Encoding Profile is intended for constrained endpoints and
efficient communication.

Its design goals include:

* low encoded size;
* bounded parsing requirements;
* predictable memory usage;
* efficient integer representation;
* efficient identifier representation;
* minimal field-name overhead;
* suitability for small microcontrollers;
* deterministic validation.

The Compact Binary profile may use:

* numeric message identifiers;
* numeric field identifiers;
* compact integer encoding;
* fixed-width values where appropriate;
* length-prefixed values;
* omission of default-valued optional fields;
* compact collection representation.

The exact binary layout is not defined by this ADR.

The Compact Binary profile must not require dynamic schema discovery for basic
message parsing.

Implementations must be able to reject malformed or oversized encoded values
without unbounded memory allocation.

### JSON profile

The JSON Encoding Profile is intended for readability, tooling, diagnostics,
simulation, and feature-rich endpoints.

Its design goals include:

* human readability;
* straightforward debugging;
* compatibility with common tooling;
* explicit field names;
* extensibility;
* easy inspection in protocol traces.

The JSON profile uses a defined JSON structure representing the canonical
Serialization Model.

The exact JSON property names and layout are not defined by this ADR.

JSON field names are part of the JSON Encoding Profile and are not canonical
protocol identifiers.

Changing a JSON field name is an Encoding Profile compatibility change even
when the canonical Serialization Model remains unchanged.

### Equivalent meaning

Compact Binary and JSON must represent equivalent protocol meaning.

For a supported message and field set, decoding either profile must produce
the same semantic Protocol Message.

For example, a Property-read Request encoded in Compact Binary and the same
Request encoded in JSON must produce equivalent:

* message category;
* operation type;
* correlation identifier;
* endpoint or session scope where applicable;
* Property identifier;
* optional values;
* protocol behavior.

Byte-for-byte equivalence is neither required nor expected.

### Supported subsets

A constrained endpoint may support only a subset of protocol message types or
optional fields.

Such limitations are expressed through:

* protocol capabilities;
* protocol version;
* endpoint profile;
* message-specific capability requirements;
* negotiated size or feature limits.

Encoding Profiles must not silently create different protocol semantics.

A message unsupported by an endpoint remains unsupported regardless of whether
the Encoding Profile could technically represent it.

The Compact Binary profile is not inherently a reduced protocol.

The JSON profile is not inherently a full protocol.

Protocol capability negotiation determines the available semantic mechanisms.

### Required fields

Required fields must be present in every valid serialized representation of a
message unless the Encoding Profile defines an unambiguous implicit value.

An implicit required value is permitted only when:

* the value is fixed by the message type or profile;
* omission cannot create ambiguity;
* the canonical value can be reconstructed deterministically.

A missing required field causes deserialization failure.

Required-field behavior must be consistent across Encoding Profiles.

### Optional fields

Optional fields may be omitted when not present.

An Encoding Profile may omit optional fields whose values equal defined
defaults.

Omission must not change protocol meaning.

The canonical Serialization Model defines the semantic default, not the
Encoding Profile.

A decoder must distinguish where necessary between:

* absent value;
* explicit null value;
* default value;
* empty value.

The exact distinction depends on the canonical field definition.

### Null values

Null is permitted only for fields whose canonical definition allows null.

An Encoding Profile must not use null as a general substitute for:

* missing required fields;
* unknown values;
* invalid values;
* unsupported operations.

Unknown, unavailable, stale, and absent values must be represented according
to the relevant protocol message definition.

### Field ordering

The canonical Serialization Model does not depend on field ordering unless a
specific message definition explicitly requires ordered elements.

An Encoding Profile may define:

* fixed field order;
* arbitrary field order;
* canonical deterministic field order.

Decoders must follow the rules of the selected profile.

JSON object-property order must not affect protocol meaning.

Collection order remains significant when the canonical Serialization Model
defines an ordered collection.

### Unknown fields

Encoding Profiles must define how unknown fields are handled.

The default rule is:

* unknown optional fields may be ignored;
* unknown mandatory fields cause deserialization or compatibility failure.

An Encoding Profile must provide a way to distinguish optional extension
fields from fields that alter mandatory interpretation.

Ignoring an unknown field must not change the meaning of known fields.

A decoder must not silently accept unknown data that changes:

* message type;
* correlation behavior;
* endpoint identity;
* success or failure status;
* authorization requirements;
* protocol lifecycle transitions;
* Stream ordering or integrity.

### Unknown message types

An unknown message type cannot normally be interpreted as a valid Protocol
Message.

The receiver must report an unsupported or incompatible message according to
the applicable protocol state and error-handling rules.

Optional protocol extensions may define safely ignorable Notifications or
extension messages, but such behavior must be explicitly specified.

Unknown Requests must not be treated as successfully completed.

### Versioning

Serialization compatibility is governed by:

* protocol version;
* canonical Serialization Model version;
* Encoding Profile version.

These versions may evolve independently when necessary.

Protocol version governs semantic protocol compatibility.

Serialization Model version governs canonical structural compatibility.

Encoding Profile version governs representation compatibility.

An implementation must not infer semantic compatibility solely from a matching
Encoding Profile version.

The exact version fields and negotiation messages are not defined by this ADR.

### Schema evolution

Schema evolution should prefer backward-compatible additions.

Preferred compatible changes include:

* adding optional fields with defined defaults;
* adding optional message types guarded by capabilities;
* extending enumerations when unknown-value handling is defined;
* increasing negotiated limits;
* adding new Encoding Profiles.

Potentially incompatible changes include:

* changing the meaning of an existing field;
* changing a required field to a different type;
* reusing a field identifier for another meaning;
* changing correlation semantics;
* changing default values in a meaning-altering way;
* removing required fields;
* changing numeric units without an explicit model change.

Incompatible changes require protocol-version or Serialization Model version
handling.

### Field identifiers

The canonical Serialization Model defines stable semantic fields.

Encoding Profiles define their concrete field identifiers.

The Compact Binary profile may use stable numeric field identifiers.

The JSON profile may use stable textual property names.

Field identifiers must not be reused for different meanings within the same
Encoding Profile compatibility lineage.

Deprecated fields may remain reserved to prevent accidental reuse.

The exact identifiers are not defined by this ADR.

### Message identifiers

The canonical model defines stable semantic message types.

Encoding Profiles define how those message types are identified.

The Compact Binary profile may use numeric message identifiers.

The JSON profile may use textual message-type names.

A profile identifier must map unambiguously to one canonical message type.

Identifiers must not be reused for different semantic message types within the
same compatibility lineage.

### Identifiers and paths

HASE identifiers and paths must have canonical semantic forms.

Encoding Profiles may represent them differently.

Examples include:

* numeric compact identifiers;
* textual descriptor paths;
* structured path segments;
* dictionary-referenced values.

Decoding must reconstruct the same semantic identifier or path.

Encoding-specific shorthand must not change identity semantics.

### Numeric values

The Serialization Model defines the semantic numeric type and constraints.

Encoding Profiles define the representation.

Numeric definitions must specify where applicable:

* signed or unsigned;
* integer or floating point;
* valid range;
* precision requirements;
* handling of non-finite values;
* unit association;
* overflow behavior.

Decoders must reject values outside the canonical valid range.

Silent numeric truncation is not permitted.

### Floating-point values

Floating-point representation must preserve the precision required by the
canonical field definition.

Encoding Profiles must define whether they support:

* 32-bit floating point;
* 64-bit floating point;
* decimal representation;
* non-finite values.

NaN and infinity must not be accepted unless explicitly permitted by the
canonical field definition.

JSON numeric parsing must not silently lose required precision.

### Enumerations

The canonical Serialization Model defines enumeration meaning.

Encoding Profiles may represent enumeration values as:

* numeric values;
* textual names;
* another deterministic profile-specific representation.

Unknown enumeration values must be handled according to the field definition.

An open enumeration may preserve an unknown value for forward compatibility.

A closed enumeration must reject unknown values.

### Strings

The canonical Serialization Model defines whether a field is textual and any
applicable limits.

Encoding Profiles must define:

* character encoding;
* maximum encoded length;
* normalization rules where required;
* invalid-sequence handling.

JSON strings use the character rules of the selected JSON profile.

Compact Binary strings should use a defined encoding, normally UTF-8 unless a
profile specifies otherwise.

Invalid text encoding causes deserialization failure.

### Binary data

Binary data must be represented without changing its byte content.

The Compact Binary profile may encode binary data directly.

The JSON profile must use a defined textual binary representation, such as an
explicitly selected base encoding.

The exact representation is not defined by this ADR.

Binary-field size limits must be enforced before unbounded allocation.

Large binary transfers should use protocol Streams rather than one oversized
message.

### Time values

Canonical time values must define:

* semantic time basis;
* precision;
* epoch or structured representation;
* timezone or UTC rules;
* unavailable or unknown handling.

Encoding Profiles may use different representations but must reconstruct the
same semantic time value.

HASE generally uses UTC for absolute timestamps unless a field definition
states otherwise.

The exact timestamp encoding is not defined by this ADR.

### Collections

The Serialization Model defines whether a collection is:

* ordered;
* unordered;
* keyed;
* bounded;
* optional;
* allowed to be empty.

Encoding Profiles must preserve those semantics.

Collection sizes must be validated before allocation or processing.

Unbounded collections are not permitted in protocol messages.

Large datasets should be transferred through Streams.

### Deterministic encoding

An Encoding Profile may define deterministic encoding.

Deterministic encoding means that equivalent canonical input produces one
defined encoded representation.

Determinism may be required for:

* protocol tracing;
* test vectors;
* hashing;
* signing;
* caching;
* reproducible diagnostics.

The Compact Binary profile should support deterministic encoding.

The JSON profile should define a deterministic canonical form where required,
while ordinary diagnostic JSON may permit flexible whitespace and property
order.

The exact canonicalization rules are not defined by this ADR.

### Size limits

Every Encoding Profile must define or negotiate limits for encoded content.

Limits may include:

* maximum serialized message size;
* maximum string length;
* maximum binary-field size;
* maximum collection length;
* maximum nesting depth;
* maximum number of fields;
* maximum identifier length.

Limits protect implementations from malformed input and resource exhaustion.

The effective serialized size must also fit within the framing limits defined
by ADR-0014.

A sender must not emit an encoded message that exceeds negotiated limits.

A receiver must validate declared or observed sizes before unbounded
allocation.

### Validation

Deserialization consists of both decoding and validation.

A decoded structure is not a valid Protocol Message until it satisfies:

* Encoding Profile rules;
* Serialization Model rules;
* message-specific constraints;
* protocol-version rules;
* negotiated capability rules;
* negotiated size limits.

Validation failures must not produce partially trusted Protocol Messages.

### Malformed input

Malformed serialized input must be rejected.

Examples include:

* invalid syntax;
* truncated values;
* duplicate forbidden fields;
* impossible lengths;
* invalid field types;
* invalid numeric ranges;
* unsupported mandatory fields;
* invalid text encoding;
* excessive nesting;
* excessive collection sizes;
* conflicting fields;
* missing required fields.

Malformed input is a serialization failure, not a protocol failure Response.

Repeated malformed input may cause the Protocol Context to leave Operational
state according to lifecycle and recovery policy.

### Duplicate fields

Encoding Profiles must define duplicate-field behavior.

The default rule is that duplicate fields are invalid unless the canonical
field is explicitly repeatable.

A decoder must not silently select an arbitrary duplicate value for a
non-repeatable field.

This avoids ambiguous or security-sensitive interpretation.

### Security considerations

Serialization validation is required even on transports that provide reliable
delivery.

Decoders must be designed to resist:

* oversized allocations;
* excessive nesting;
* integer overflow;
* length overflow;
* malformed text;
* duplicate-field ambiguity;
* invalid numeric conversion;
* resource-exhaustion input;
* intentionally inconsistent fields.

Serialization integrity does not provide endpoint authentication,
authorization, confidentiality, or protection against intentional message
modification.

Security mechanisms require separate architectural decisions.

### Diagnostics and tracing

Tooling may display protocol messages using the JSON Encoding Profile or
another diagnostic representation.

A diagnostic representation must not be confused with the Encoding Profile
actually used on the connection.

Protocol tracing may record:

* semantic Protocol Messages;
* canonical Serialization Model values;
* encoded profile data;
* framed transport data.

The trace must identify which layer is represented.

### Simulation

Simulation should support the same canonical Serialization Model.

Simulation tests may operate directly on semantic Protocol Messages when
testing higher layers.

Serialization integration tests must verify both Compact Binary and JSON
Encoding Profiles.

Simulation-specific representations must not redefine protocol meaning.

### Extensibility

New Encoding Profiles may be added through capability negotiation.

A new Encoding Profile must define:

* profile identifier;
* profile version;
* representation rules;
* required and optional field handling;
* unknown-field behavior;
* size limits;
* malformed-input behavior;
* deterministic encoding rules where applicable;
* compatibility with the canonical Serialization Model.

A new profile must not require changes to application-facing runtime semantics.

## Consequences

### Positive

* HASE has one semantic protocol independent of representation.
* Compact Binary and JSON remain equivalent encodings.
* Constrained devices can use efficient representations.
* Tooling can use human-readable representations.
* Future encodings can be introduced without redefining protocol messages.
* Encoding selection integrates with capability negotiation.
* Schema evolution has an explicit compatibility model.
* Validation and resource limits are architectural requirements.
* Protocol traces can distinguish semantic, serialized, framed, and transport
  layers.

### Negative

* The architecture introduces a distinction between semantic messages,
  canonical structure, and encoded representation.
* Every Encoding Profile requires dedicated implementation and test vectors.
* Capability negotiation must establish a profile before normal message
  exchange.
* Bootstrap encoding requires a transport or protocol-profile decision.
* Equivalent behavior must be tested across all supported Encoding Profiles.
* Field and message identifiers require long-term compatibility governance.

## Alternatives considered

### Define separate binary and JSON protocols

Rejected because this would duplicate protocol semantics and risk behavioral
divergence.

### Encode Protocol Messages directly without a canonical Serialization Model

Rejected because field meaning, compatibility, and schema evolution would
become dependent on individual encodings.

### Require Compact Binary for every endpoint

Rejected because diagnostics, tooling, simulation, and development benefit
from a human-readable representation.

### Require JSON for every endpoint

Rejected because JSON can impose unacceptable code size, memory, parsing, and
bandwidth requirements on constrained microcontrollers.

### Bind Encoding Profiles permanently to transport types

Rejected because encoding requirements depend on endpoint capabilities,
resource limits, diagnostics, and deployment policy rather than transport
alone.

A transport profile may still recommend or restrict available encodings.

### Allow encodings to define different message semantics

Rejected because this would create multiple incompatible HASE protocols.

### Negotiate individual fields rather than Encoding Profiles

Rejected because it would significantly complicate negotiation, validation,
testing, and compatibility management.

Capabilities may still govern optional semantic features.

### Ignore unknown fields unconditionally

Rejected because unknown mandatory or meaning-altering fields cannot safely be
ignored.

### Accept unlimited encoded values

Rejected because bounded resource use is essential for constrained and
security-sensitive implementations.

## Relationship to previous ADRs

ADR-0009 provides the capability-negotiation model used to select an Encoding
Profile.

ADR-0010 defines the semantic Protocol Message model.

This ADR defines the canonical Serialization Model and the Encoding Profiles
that represent it.

ADR-0014 defines that one encoded serialized message is carried in exactly one
HASE frame.

ADR-0013 defines the Protocol Context, which operates above the serialization
boundary.

The concrete field identifiers, message identifiers, binary layout, JSON
property names, bootstrap representation, and numeric limits will be defined
during protocol implementation and supporting protocol specifications.
