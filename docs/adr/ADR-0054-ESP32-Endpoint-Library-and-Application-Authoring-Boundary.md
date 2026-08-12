# ADR-0054 — ESP32 Endpoint Library and Application Authoring Boundary

- Status: Accepted; implementation pending
- Date: 2026-08-12

## Context

The physical Protocol Version 1 ESP32 endpoint is implemented as one Arduino
sketch rooted at `HaseEndpoint`. The sketch root contains 72 files: one `.ino`,
35 `.cpp`, and 36 `.h` files. Those files contain 7,129 lines, including 1,189
lines in `HaseEndpoint.ino`.

Arduino IDE exposes almost all of the HASE framework implementation as sketch
tabs. Protocol encoding, descriptor serialization, TCP framing, discovery,
mDNS, UTC synchronization, lifecycle recovery, physical BME280 access, GPIO
Properties and Commands, Event detection, and local configuration therefore
appear as one application source set.

Application-specific facts are also distributed through framework-looking
files. Endpoint and instrument identities are repeated between discovery and
descriptor code. BME280 Property routing, LED Property and Command routing,
GPIO pins, Event identity, debounce behavior, network configuration, and
startup behavior all require knowledge of several unrelated source files.

The implementation is physically validated and must not be replaced by a new
protocol or endpoint model merely to improve its source organization. The
authoring boundary must preserve the existing Runtime Host contract while
making custom ESP32 applications small and explicit.

The repository does not currently contain a controlled ESP32 compilation
workflow or an authoritative record of the installed ESP32 Arduino core,
compiler, board FQBN, and dependency versions. Those facts must be discovered
read-only before repeatable compilation is introduced; this decision does not
infer them.

## Decision

### Ownership layers

The ESP32 implementation is divided into three ownership layers.

1. The HASE ESP32 library owns stable Protocol Version 1 infrastructure:
   binary encoding and decoding, descriptor serialization, request validation,
   response creation, framed TCP transport, dispatch, discovery, mDNS, UTC,
   connection lifecycle, Event framing, bounded buffers, and framework
   diagnostics.
2. The endpoint definition owns endpoint identity and metadata, instruments,
   Properties, Commands, Events, data descriptors, access modes, and the
   registrations that bind those capabilities to application behavior.
3. The endpoint application owns hardware initialization, Property reads and
   writes, Command execution, local Event detection, sensors, actuators, and
   pins.

Application code operates on typed HASE concepts. It does not parse or create
protocol envelopes, transport frames, descriptor bytes, result payloads, or
EventNotification frames.

### Repository and Arduino layout

The stable framework becomes a conventional source-based Arduino library:

```text
libraries/
  HaseEsp32Endpoint/
    library.properties
    src/
      HaseEsp32Endpoint.h
      framework and internal implementation files
```

The existing physical example remains a sketch with approximately six visible
code tabs representing five clear authoring concerns:

```text
HaseEndpoint/
  HaseEndpoint.ino
  EndpointConfiguration.h
  EndpointDefinition.cpp
  EndpointApplication.h
  EndpointApplication.cpp
  HaseSecrets.h
```

The library declares the ESP32 Arduino architecture and exposes one intentional
public include. Moving a class into `src` does not by itself make that class a
supported application API. Protocol and transport internals remain library
implementation details.

`HaseSecrets.h` remains local and ignored. A tracked example or template is
kept outside the active sketch root so it does not add another ordinary
application tab. No actual Wi-Fi credential is included in repository content,
source packages, test output, or documentation.

### Configuration ownership

`EndpointConfiguration.h` contains public endpoint configuration that an
author may intentionally edit:

- TCP port;
- mDNS host and service-instance names;
- maximum payload length;
- read-progress timeout;
- UTC synchronization timeout; and
- optional framework diagnostic settings.

`HaseSecrets.h` contains only local Wi-Fi credentials.

Hardware pins, I2C configuration, sensor initialization, actuator defaults,
and local debounce logic belong to `EndpointApplication.*`. They describe the
application hardware rather than the HASE transport.

Endpoint and instrument identities belong to `EndpointDefinition.cpp` and are
consumed by both discovery and descriptor publication. The library must not
require a second independently edited discovery identity list.

### Registration and callback semantics

The endpoint definition explicitly registers each Property, Command, and
Event. The final C++ signatures are introduced and reviewed in Stage 54C, but
this decision fixes their semantics:

- endpoint, instrument, Property, Command, and Event identities are unique;
- descriptor identity and callback registration must agree;
- a readable Property has a compatible typed read callback;
- a writable Property has a compatible typed write callback;
- a read-only Property cannot register a write callback;
- callback data types agree with their descriptors;
- unknown instruments and paths produce deterministic not-found results;
- application failures map explicitly to HASE result codes;
- a Property write or Command invokes application code at most once;
- the framework does not retry or replay mutations;
- the application detects a local Event once;
- the library owns Event timestamping, encoding, and live delivery; and
- the library contains no BME280, GPIO16, GPIO17, or Adafruit dependency.

Registration is validated before endpoint publication. A duplicate identity,
missing required callback, incompatible type, or access-mode mismatch stops
startup rather than publishing a descriptor that the application cannot
implement.

## Current-behavior compatibility contract

The refactoring changes source ownership, not the externally observable HASE
endpoint contract.

### Protocol and TCP framing

The implementation preserves:

- HASE Protocol Version 1.0;
- TCP port 5000;
- a four-byte big-endian TCP payload-length prefix;
- a maximum protocol payload of 4,096 bytes;
- a five-second read-progress timeout;
- one connected client, with a newly accepted client replacing the previous
  client;
- TCP no-delay behavior;
- the 12-byte HASE envelope;
- the existing envelope field order;
- little-endian envelope correlation and payload lengths;
- request, response, and notification roles;
- every current request, response, notification, result, marker, quality, and
  Variant code;
- response correlation identifiers;
- notification correlation identifier zero;
- existing request validation and invalid or unsupported-message behavior;
- descriptor serialization order and wire representation; and
- no implicit retry or replay of a Property write or Command.

The supported exchanges remain Discover, ReadProperty, WriteProperty,
ExecuteCommand, ReadEndpointDescriptor, and EventNotification.

### Discovery and authoritative identity

The following values remain exact:

```text
Endpoint ID       : doit-esp32-devkitc-v4-01
mDNS host         : doit-esp32-devkitc-v4-01
mDNS instance     : doit-esp32-devkitc-v4-01
Service           : _hase._tcp.local
Instrument 1      : environment-sensor-01
Instrument 2      : controller-01
```

The discovery instrument order remains environment sensor followed by
controller. mDNS advertises reachability; the Protocol Version 1
DiscoverResponse remains authoritative for endpoint identity.

### Descriptor contract

The endpoint descriptor retains the current endpoint metadata and the exact
instrument, capability, path, metadata, access, quantity, unit, range, and
resolution values and ordering.

The environment instrument is `environment-sensor-01`, displayed as `BME280
Environment Sensor`, with kind `environment-sensor` and the established Bosch
Sensortec BME280 metadata. It exposes:

- `physical.environment-sensor.temperature` at
  `Environment.Temperature`, Double and read-only, from -100.0 through 100.0
  degrees Celsius with resolution 0.1;
- `physical.environment-sensor.relative-humidity` at
  `Environment.RelativeHumidity`, Double and read-only, from 0.0 through 100.0
  percent relative humidity with resolution 0.1; and
- `physical.environment-sensor.air-pressure` at
  `Environment.AirPressure`, Double and read-only, from 300.0 through 1100.0
  hectopascal with resolution 0.1.

The controller instrument is `controller-01`, displayed as `ESP32 GPIO
Controller`, with kind `controller` and the established Espressif ESP32
metadata. It exposes:

- Boolean read/write Property
  `physical.controller.status-led-enabled` at
  `Controller.StatusLedEnabled`;
- parameterless Command `Controller.ToggleStatusLed`, returning its new Boolean
  enabled state; and
- Event `Controller.ButtonPressed` with a null payload.

All current display names and descriptions remain unchanged.

### Physical application behavior

The migrated example preserves:

- BME280 I2C SDA GPIO21, SCL GPIO22, and address `0x76`;
- startup failure when the BME280 cannot initialize;
- temperature in Celsius, humidity in percent relative humidity, and pressure
  in hectopascal;
- unavailable or NaN BME280 readings mapped to the existing internal Property
  failure;
- GPIO16 status LED output with active-low behavior;
- LED initialization to disabled on first hardware initialization;
- authoritative LED readback after a Property write;
- exactly one LED toggle per accepted Command;
- GPIO17 pushbutton input with active-low `INPUT_PULLUP` behavior;
- 50 millisecond debounce;
- one Event after a stable press;
- rearming only after a stable release;
- no queued or replayed Event when no client is connected;
- null Event payload; and
- endpoint UTC timestamp on Event and successful Property results.

### Lifecycle and recovery

Wi-Fi connection precedes endpoint publication. UTC synchronization precedes
Event-capable operation and retains the existing 15-second synchronization
timeout and NTP boundary. TCP and mDNS start only after successful required
hardware, network, and time initialization.

Wi-Fi loss stops advertisement and disconnects the active client. Successful
Wi-Fi reconnection and UTC resynchronization restart TCP and mDNS operation.
Runtime Host supervision must continue to observe the same disconnect,
reconnect, descriptor, Property, Command, Event, reset-recovery, and normal
Wi-Fi-recovery behavior.

Exact serial-log wording, source filenames, internal class names, framework
object construction, and sketch-tab ordering are not compatibility contracts.
Protected values and address data are not required diagnostic output.

## Staged implementation

### 54A — decision and compatibility contract

Stage 54A records this decision and reconciles Project Status and Roadmap. It
changes no firmware or executable source, performs no Arduino compilation, and
has no deployment or physical effect.

### 54B — library packaging and repeatable compilation

Stage 54B first discovers the actual AEPRAKETE Arduino IDE, ESP32 core,
compiler, board FQBN, and dependency versions without changing them. It then
creates the conventional library package, moves only stable framework code,
and adds a clean repeatable compilation workflow.

Compilation must resolve the framework from the library rather than duplicate
sketch-root sources and must succeed twice from clean staging. Binary
byte-for-byte equality is not claimed unless the selected toolchain proves
deterministic output. Stage 54B does not upload firmware.

### 54C — application callback and registration boundary

Stage 54C introduces the typed callback and registration API, moves generic
request routing and result mapping into the library, removes duplicated
application identity, validates descriptor-to-registration consistency, and
proves at-most-once mutation invocation. It compiles but does not upload.

### 54D — BME280/GPIO example migration and authoring guide

Stage 54D migrates the existing physical example to the new boundary, reduces
the active sketch to the intended authoring files, places the tracked secrets
template outside the active sketch root, and adds a step-by-step authoring
guide. The guide covers adding an instrument, Property, Command, Event, and
hardware dependency. It compiles but does not upload.

### 54E — physical compatibility validation and closure

Stage 54E requires separate explicit authorization after the implementation is
compiled, tested, reviewed, committed, pushed, and synchronized. It controls
firmware upload and independently validates discovery, identity, descriptor
equivalence, BME280 Properties, LED read/write/restore, one Command toggle and
restore, GPIO17 Event delivery, disconnect/reconnect, ESP32 reset recovery,
normal Wi-Fi recovery, Runtime Host compatibility, and Laptop Client
compatibility.

Closure reconciles this ADR, Project Status, and Roadmap only after physical
compatibility is accepted.

## Consequences

- Custom ESP32 authors see a small application rather than the Protocol V1
  implementation.
- Stable framing, serialization, discovery, lifecycle, and validation behavior
  is shared and versioned once.
- Endpoint descriptors and physical handlers become explicit peers rather than
  unrelated string tables.
- Registration validation prevents publishing capabilities that lack matching
  application behavior.
- The physical BME280/GPIO endpoint remains the compatibility example and does
  not become hard-coded library behavior.
- The new library introduces a supported application API that later changes
  must version deliberately.
- ESP32 build-tool and dependency versions become explicit repository evidence
  rather than workstation assumptions.
- Diagnostic Export and Offline Analysis remains the agreed objective after
  ADR-0054.

## Stage 54A effects and recovery

Stage 54A modifies only this ADR, `docs/ProjectStatus.md`, and
`docs/Roadmap.md`. It does not modify `README.md`, `HaseEndpoint`, Runtime Host
or Client source, deployment configuration, credentials, firmware, or physical
state.

Before commit, rollback restores or removes only the three documentation
paths. After commit, recovery is a documentation-only revert. No device or
deployment recovery procedure is required.
