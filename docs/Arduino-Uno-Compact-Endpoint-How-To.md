# How to Add Arduino Uno Compact Endpoints

This guide describes how to add one or more Arduino Uno-class endpoints to
HASE through Compact Serial Protocol Version 1. It covers the Arduino sketch,
the host-side descriptor and wire mappings, and the endpoint-composition
configuration consumed by the Desktop Runtime Host C# application.

The repository reference implementation is
[`HaseArduinoUno/HaseArduinoUno.ino`](../HaseArduinoUno/HaseArduinoUno.ino).
Its host definition is
[`ArduinoUnoCompactDefinitionFactory.cs`](../src/Hase.DesktopHost.App/Physical/ArduinoUnoCompactDefinitionFactory.cs).

This is a source-authoring guide. Building, uploading firmware, changing a
deployed Runtime Host profile, opening a serial device, and physically testing
hardware remain separately controlled operations.

## 1. Understand the boundary

An Arduino Uno does not transmit a complete HASE descriptor. Compact
bootstrap returns only:

- the authoritative endpoint ID;
- a descriptor ID; and
- a descriptor version.

The Runtime Host resolves that exact descriptor reference from a predefined
C# repository. The Arduino and Runtime Host must therefore agree on every
compact Property, Command, and Event ID and on every value encoding.

The current production boundary supports:

| Capability | Supported forms |
| --- | --- |
| Property | Boolean; unsigned 16-bit little-endian millivolts materialized as `double` volts |
| Property access | Read-only or read/write, as declared by the descriptor |
| Command | Parameterless request with success/unknown-command status |
| Event | Unsolicited, no payload, correlation ID zero |

The reference endpoint demonstrates several capability shapes:

| Compact ID | HASE capability | Type and access |
| --- | --- | --- |
| Property `0x01` | `Led/State` | Boolean, read/write |
| Property `0x02` | `Analog/Voltage` | Numeric voltage, read-only |
| Command `0x01` | `Led/Toggle` | Parameterless Command |
| Event `0x01` | `Controller/ButtonPressed` | Null-payload Event |

String Properties, arbitrary numeric encodings, command arguments/results,
and Event payload values are not configuration options today. Section 8
identifies the source changes required to add them.

## 2. Assign stable identities

Choose these values before editing code:

```text
EndpointId        arduino-uno-02
DescriptorId      laboratory-uno-controller
DescriptorVersion 1
InstrumentId      laboratory-uno-controller-01
```

Rules:

1. Every physical endpoint must report a unique `EndpointId`.
2. Devices with an identical capability contract may share one descriptor ID
   and version.
3. Any incompatible capability or wire-mapping change requires a new
   descriptor version (or a new descriptor ID).
4. Compact IDs are nonzero bytes and are unique within their capability
   family. Property `0x01`, Command `0x01`, and Event `0x01` may coexist.
5. HASE instrument IDs, Property IDs, and descriptor paths must be unique in
   the normal descriptor scopes.

Do not derive endpoint identity from a COM port. COM ports and USB metadata are
discovery hints; compact bootstrap supplies authoritative identity.

## 3. Create the Arduino firmware

Copy the reference sketch into a new sketch directory whose `.ino` file has
the same name as the directory. Change at least `EndpointId`, `DescriptorId`,
and `DescriptorVersion` near the top of the sketch.

Retain the validated transport constants unless a separately versioned
protocol change requires otherwise:

```cpp
const uint32_t SerialBaudRate = 115200UL;
const uint8_t ProtocolVersion = 0x01;
const uint8_t MaximumSupportedFrameLength = 64;
```

`Serial` is exclusively the binary HASE transport. Never write diagnostic
text to it. If local diagnostics are required, use a separate physical output
that cannot corrupt the protocol stream.

The frame layout is implemented by `SendFrame`, `ReceiveByte`, and
`ProcessCompleteFrame`. Requests use a nonzero correlation ID. Unsolicited
Events use correlation ID `0x00`. CRC is CRC-16/CCITT-FALSE over the version,
message type, correlation ID, payload length, and payload.

### Add a Boolean read/write Property

Allocate a compact Property ID and retain the state on the device:

```cpp
const uint8_t RelayEnabledPropertyId = 0x03;
bool relayEnabled = false;
```

In the read handler, return `[propertyId, success, value]`, where Boolean
false is `0x00` and true is `0x01`. In the write handler, require exactly one
value byte, reject every value except `0x00` and `0x01`, perform the mutation
once, and return `[propertyId, status]`.

The reference `BuiltInLedStatePropertyId` branches in
`SendReadPropertyResponse` and `ProcessWritePropertyRequest` are the exact
model. Return `UnknownProperty` for an unmapped ID and `InvalidValue` for a
known Property with malformed bytes.

### Add a numeric voltage Property

Allocate another ID and encode volts as unsigned 16-bit little-endian
millivolts:

```cpp
const uint8_t SensorVoltagePropertyId = 0x04;

const uint16_t millivolts = 3300;
const uint8_t value[] =
{
  static_cast<uint8_t>(millivolts & 0xFF),
  static_cast<uint8_t>(millivolts >> 8)
};
```

The complete read response payload is the Property ID, status, and those two
bytes. On the host, `3300` becomes the `double` value `3.3` volts. The
reference `AnalogInputVoltagePropertyId` branch shows ADC conversion and
rounding for a 0-to-5 V Arduino Uno input.

The same encoding can back a read/write numeric Property, but the firmware
must validate the two-byte write value and the C# descriptor must declare
`PropertyAccessMode.ReadWrite`. Do not silently clamp an invalid value; return
`InvalidValue`.

### Add a parameterless Command

Allocate a nonzero Command ID, add one branch to `ExecuteCommand`, perform the
action at most once, and return `CommandStatusSuccess` only after the endpoint
accepts the action:

```cpp
const uint8_t ResetCounterCommandId = 0x02;

if (commandId == ResetCounterCommandId)
{
  counter = 0;
  return CommandStatusSuccess;
}
```

Compact Version 1 currently sends only the Command ID. It cannot carry a
command argument or typed result. Unknown IDs return
`CommandStatusUnknownCommand`.

### Add a null-payload Event

Allocate a nonzero Event ID and send it once for the physical transition:

```cpp
const uint8_t ThresholdCrossedEventId = 0x02;

const uint8_t payload[] = { ThresholdCrossedEventId };
SendFrame(EventNotificationMessageType, 0x00, payload, sizeof(payload));
```

The firmware owns polling, debounce, rearming, and duplicate suppression.
Events are not queued or replayed while disconnected. Keep `loop()`
non-blocking so request processing and Event detection both make progress.

## 4. Define the endpoint in the Runtime Host C# application

Create a definition factory beside
`ArduinoUnoCompactDefinitionFactory`, or add a deliberately named method to
that factory when the new version belongs to the same endpoint family.

Build one `EndpointDescriptorDefinition` containing the endpoint metadata,
instrument descriptors, and their interfaces. The following abbreviated
example shows the supported capability types:

```csharp
var relayEnabled = new PropertyDescriptor(
    new PropertyId("relay-enabled"),
    new DescriptorPath("Relay", "Enabled"),
    "Relay Enabled",
    new BooleanDataDescriptor())
{
    AccessMode = PropertyAccessMode.ReadWrite
};

var sensorVoltage = new PropertyDescriptor(
    new PropertyId("sensor-voltage"),
    new DescriptorPath("Sensor", "Voltage"),
    "Sensor Voltage",
    new NumericDataDescriptor(
        Quantities.Voltage,
        Units.Volt,
        new ValueRange(0.0, 5.0),
        new Resolution(5.0 / 1023.0)))
{
    AccessMode = PropertyAccessMode.Read
};

var resetCounter = new CommandDescriptor(
    new DescriptorPath("Counter", "Reset"),
    "Reset Counter");

var thresholdCrossed = new EventDescriptor(
    new DescriptorPath("Sensor", "ThresholdCrossed"),
    "Threshold Crossed");
```

Put those descriptors in an `InstrumentInterface`, then materialize a
`CompactEndpointDefinition` with exact wire mappings:

```csharp
return new CompactEndpointDefinition(
    new DescriptorReference(
        new DescriptorId("laboratory-uno-controller"),
        version: 1),
    descriptorDefinition,
    [
        new CompactPropertyMapping(
            0x03,
            controllerInstrumentId,
            relayEnabledPropertyId,
            CompactPropertyValueEncoding.Boolean),
        new CompactPropertyMapping(
            0x04,
            controllerInstrumentId,
            sensorVoltagePropertyId,
            CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts)
    ],
    [
        new CompactEventMapping(
            0x02,
            controllerInstrumentId,
            new DescriptorPath("Sensor", "ThresholdCrossed"),
            CompactEventValueEncoding.None)
    ],
    [
        new CompactCommandMapping(
            0x02,
            controllerInstrumentId,
            new DescriptorPath("Counter", "Reset"))
    ]);
```

Construction validates that every mapping resolves to the declared
descriptor and that compact IDs are not duplicated. A mapping is transport
code, not an alternative descriptor: its encoding and IDs must exactly match
the firmware.

## 5. Register every descriptor version

`ProductionPrivateNetworkRuntimeHostBackend` currently creates the compact
definition repository explicitly:

```csharp
var definitionRepository =
    new InMemoryCompactEndpointDefinitionRepository(
        [legacyCompactDefinition, compactDefinition]);
```

Create each new definition and add it to this immutable list. The pair
`DescriptorId + DescriptorVersion` must be unique. If firmware reports a
reference absent from this list, authoritative verification fails and the
endpoint is not attached.

Devices with the same descriptor reference reuse one C# definition. Do not
duplicate a definition merely because the physical endpoint ID differs.

Add focused tests beside the existing compact definition, map, codec, and
Desktop Host tests. At minimum, test:

- exact descriptor reference and metadata;
- all Property, Command, and Event descriptors and mappings;
- duplicate and unresolved mappings rejected;
- valid and malformed values for every encoding;
- firmware bootstrap identity/reference agreement; and
- Runtime Host behavior when the device is present, absent, or reports the
  wrong identity.

## 6. Configure endpoints in the Runtime Host profile

The installed application profile points to an endpoint-composition JSON
file. Add one `CompactSerial` entry per intended physical endpoint:

```json
{
  "formatVersion": 1,
  "endpoints": [
    {
      "kind": "CompactSerial",
      "expectedEndpointId": "arduino-uno-01",
      "vendorId": 9025,
      "productId": 67,
      "baudRate": 115200,
      "verificationTimeoutMilliseconds": 3000
    },
    {
      "kind": "CompactSerial",
      "expectedEndpointId": "arduino-uno-02",
      "vendorId": 9025,
      "productId": 67,
      "baudRate": 115200,
      "verificationTimeoutMilliseconds": 3000
    }
  ]
}
```

The decimal values `9025` and `67` are USB VID/PID `0x2341`/`0x0043` for the
validated reference board. Use the actual enumerated metadata for another USB
serial implementation. `DesktopRuntimeHostEndpointCompositionProfileFile`
uses strict JSON parsing: casing, required members, supported endpoint kind,
positive baud rate, timeout, unique endpoint IDs, and the total limit of 1 to
64 endpoints are enforced.

Use the guided installation or profile-editing boundary to create or update a
deployed composition. Do not hand-edit protected installed files while HASE is
running. Preserve the installation profile, identity, authorization policy,
and recovery custody according to the deployment workflow.

If the northbound policy is default-deny, add only the required grants for the
new endpoint's intended Properties, Commands, and Events. Endpoint attachment
does not itself grant remote access.

## 7. Support multiple identical USB boards in C#

There is a current implementation limitation that matters when several Uno
boards share the same VID/PID. `AttachCompactEndpointAsync` filters candidates
by VID/PID and currently requires the complete verified inventory to contain
exactly one endpoint before it compares `ExpectedEndpointId`. Two connected
boards with the same USB metadata can therefore produce the error that exactly
one verified compact endpoint is required, even though their authoritative
bootstrap endpoint IDs differ.

Before configuring multiple identical boards, change that selection boundary
to:

1. discover and verify all candidates matching VID/PID;
2. filter `VerifiedEndpoints` by the configured `ExpectedEndpointId` using
   exact `EndpointId` equality;
3. require exactly one matching verified endpoint;
4. treat zero matches as unavailable for only that configured endpoint;
5. reject more than one matching physical candidate as ambiguous; and
6. attach from the selected verified port without falling back to another
   endpoint.

In outline:

```csharp
EndpointId expectedEndpointId =
    new(endpoint.ExpectedEndpointId);

VerifiedUsbSerialEndpoint[] matches =
    discoveryResult.CandidateResults
        .OfType<VerifiedUsbSerialEndpoint>()
        .Where(candidate => candidate.EndpointId == expectedEndpointId)
        .ToArray();

if (matches.Length == 0)
{
    throw new DesktopRuntimeHostEndpointUnavailableException(
        "NoVerifiedCandidate");
}

if (matches.Length > 1)
{
    throw new InvalidOperationException(
        "More than one verified compact endpoint reports the configured identity.");
}

VerifiedUsbSerialEndpoint selectedEndpoint = matches[0];
```

Retain the per-endpoint
`DesktopRuntimeHostEndpointStartupCoordinator.TryAttachAsync` containment:
one absent or unavailable Arduino must emit a scoped
`EndpointStartupUnavailable` warning and must not prevent other endpoints or
the Runtime Host from becoming Ready.

Use `CandidateResults`, rather than the deduplicated `VerifiedEndpoints` view,
when detecting duplicate physical candidates that report the same identity.
Add tests with two same-VID/PID candidates, different endpoint IDs, reversed
enumeration order, one unavailable port, duplicate reported identity, and no
matching identity. Selection by COM-port order is not acceptable.

## 8. Add new Property, Command, or Event value types

Richer types require a coordinated protocol extension. Do not label arbitrary
bytes as one of the existing encodings.

For a new Property encoding:

1. add a named value to `CompactPropertyValueEncoding`;
2. implement strict length/range conversion in
   `CompactPropertyValueDecoder` and, if writable,
   `CompactPropertyValueEncoder`;
3. add round-trip, wrong-type, invalid-length, overflow, and boundary tests;
4. declare a compatible HASE data descriptor in the endpoint factory;
5. map the Property to the new encoding; and
6. implement the identical byte layout and validation in firmware.

For an Event payload type, add a named `CompactEventValueEncoding`, extend the
Event resolver/decoder, add a compatible `EventDescriptor` value contract, and
implement the same payload layout after the Event ID in firmware.

For Command arguments or typed results, the present request and response
codecs are insufficient: `CompactExecuteCommandRequest` carries only a Command
ID and the response carries only ID plus status. Introduce a separately
reviewed wire-contract revision or backward-compatible codec extension, then
update firmware, codecs, executor, runtime Command adapter, descriptor
validation, and end-to-end tests together. Never reinterpret Version 1 frames
in a way that changes existing Commands.

## 9. Validate safely

Use this order:

1. validate descriptor and mapping tests;
2. run focused Compact Protocol and Desktop Host tests;
3. run the complete Release test suite;
4. build the Arduino sketch without uploading;
5. commit and synchronize the exact source;
6. prepare a controlled firmware/upload plan with rollback;
7. update the Runtime Host application and protected composition; and
8. physically validate one endpoint at a time, then validate simultaneous
   publication and unavailable-endpoint containment.

Physical acceptance should confirm authoritative endpoint identity, expected
descriptor version, Property reads and writes, at-most-once Commands, live
Events, clean disconnect/reconnect, Runtime Host Ready state, Client inventory,
and absence of unexpected Error/Critical diagnostics. Restore any mutable
physical output after validation.

## 10. Authoring checklist

- [ ] Every board has a unique authoritative endpoint ID.
- [ ] Descriptor ID/version exactly matches a registered C# definition.
- [ ] Compact IDs and encodings match firmware and host mappings.
- [ ] Properties declare correct HASE types and access modes.
- [ ] Commands are parameterless unless the protocol is deliberately extended.
- [ ] Events use correlation ID zero and are not replayed.
- [ ] `Serial` contains no diagnostic text.
- [ ] Each endpoint-composition entry has exact VID, PID, baud, timeout, and
      expected endpoint ID.
- [ ] Same-VID/PID multi-board selection is implemented and tested before use.
- [ ] Unavailable endpoints are contained without faulting Runtime Host startup.
- [ ] Authorization grants are explicit and least-privilege.
- [ ] Automated validation precedes upload, deployment, or physical mutation.
