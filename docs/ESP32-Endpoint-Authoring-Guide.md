# HASE ESP32 Endpoint Authoring Guide

This guide explains how to create or adapt a Protocol Version 1 HASE endpoint
for an ESP32 using the repository library and the application boundary defined
by ADR-0054. The physical BME280/GPIO endpoint under `HaseESP32` is the
complete reference application.

The guide covers source authoring and controlled compilation. It does not
authorize firmware upload, deployment, serial-port access, or physical device
changes.

## Supported baseline

The controlled repository build uses:

| Component | Validated value |
| --- | --- |
| Arduino IDE | 2.3.7 |
| Embedded Arduino CLI | 1.3.1 |
| ESP32 core | `esp32:esp32` 3.3.10 |
| Board | DOIT ESP32 DEVKIT V1 |
| FQBN | `esp32:esp32:esp32doit-devkit-v1` |
| C++ compiler | Espressif GCC 14.2.0 (`esp-14.2.0_20260121`) |
| HASE library | `libraries/HaseEsp32Endpoint`, version 0.1.0 |

The BME280 example also uses these repository-controlled libraries:

- Adafruit BME280 Library 2.3.0;
- Adafruit BMP280 Library 3.0.0;
- Adafruit BusIO 1.17.4; and
- Adafruit Unified Sensor 1.1.15.

The controlled compilation script verifies these versions and does not install
or update tools, cores, or libraries.

## The application surface

An endpoint application has five tracked source files and one ignored local
secrets file:

```text
HaseESP32/
  HaseESP32.ino
  EndpointConfiguration.h
  EndpointDefinition.cpp
  EndpointApplication.h
  EndpointApplication.cpp
  HaseSecrets.h                  local and ignored
```

Arduino IDE therefore presents six application tabs when the local secrets
file exists. Only five are tracked. The Protocol, serialization, transport,
discovery, lifecycle, UTC, request-routing, response, and Event-framing sources
remain under `libraries/HaseEsp32Endpoint/src` and are not application tabs.

### File responsibilities

| File | Author responsibility |
| --- | --- |
| `HaseESP32.ino` | Compose the application, definition, configuration, and runtime; pass local Wi-Fi credentials to `begin`; call `update` from `loop`. |
| `EndpointConfiguration.h` | Set TCP, mDNS, payload, read-timeout, and UTC-synchronization values. |
| `EndpointDefinition.cpp` | Declare endpoint and instrument descriptors and register every Property, Command, and Event callback. |
| `EndpointApplication.h` | Declare the hardware application, pins, devices, state, callbacks, and Event binding. |
| `EndpointApplication.cpp` | Initialize hardware, implement callbacks, detect Events, and publish them through the runtime. |
| `HaseSecrets.h` | Hold only the local Wi-Fi SSID and password. Never track, package, print, or hash its contents. |

## Start from the reference application

For a new endpoint, copy the five tracked files from `HaseESP32` into a new
sketch directory whose `.ino` filename matches the directory name. Then adapt
the configuration, definition, and application files. Keep the HASE framework
library under `libraries/HaseEsp32Endpoint`; do not copy its source files into
the sketch root.

The controlled compilation tool currently targets the repository
`HaseESP32` example. A separately named application needs an equivalent
external staging and compilation check before it can become a maintained
repository example.

## Create local Wi-Fi secrets

Copy the tracked template from outside the sketch root:

```powershell
Copy-Item `
    -LiteralPath ".\templates\HaseESP32\HaseSecrets.example.h" `
    -Destination ".\HaseESP32\HaseSecrets.h"
```

Then edit only the two values in the local file:

```cpp
#pragma once

const char* WIFI_SSID =
    "your-local-ssid";

const char* WIFI_PASSWORD =
    "your-local-password";
```

Confirm that Git ignores it:

```powershell
git check-ignore --quiet -- HaseESP32/HaseSecrets.h

if ($LASTEXITCODE -ne 0)
{
    throw "HaseSecrets.h is not ignored."
}
```

Do not place an example secrets file in the active sketch root: Arduino IDE
would expose it as another ordinary tab. The tracked template belongs under
`templates/HaseESP32`.

## Configure the endpoint

`EndpointConfiguration.h` defines one `HaseEndpointConfiguration` value. Its
fields, in order, are:

1. TCP port;
2. mDNS host name;
3. mDNS instance name;
4. maximum Protocol payload length;
5. read-progress timeout in milliseconds; and
6. UTC synchronization timeout in milliseconds.

The reference application preserves port 5000, a 4,096-byte maximum payload,
a five-second read-progress timeout, and a 15-second UTC synchronization
timeout. The mDNS identity and the authoritative endpoint ID normally use the
same stable value, but they have different roles: mDNS advertises a candidate;
the Protocol `DiscoverResponse` supplies authoritative identity.

Do not add Wi-Fi credentials, pins, or sensor settings to this file. Wi-Fi
credentials belong in `HaseSecrets.h`; hardware details belong in
`EndpointApplication.*`.

## Define identity and capabilities

`EndpointDefinition.cpp` contains the endpoint descriptor, its instruments,
and their Property, Command, and Event descriptors. It also contains typed
registrations that connect each descriptor to application behavior.

Identity rules:

- keep endpoint, instrument, Property, Command, and Event identities unique;
- keep descriptor collection counts equal to their actual array lengths;
- keep registration pointers bound to descriptors in the published endpoint;
- register every exposed capability exactly once; and
- do not maintain a second discovery identity table.

The runtime validates the complete definition before hardware and network
startup. Duplicate identities, incompatible callback types, missing callbacks,
invalid access modes, or registrations pointing outside the descriptor fail
startup instead of publishing an unusable endpoint.

### Add an instrument

1. Create one `HaseInstrumentMetadata` value.
2. Create its Property, Command, and Event descriptor arrays, using `nullptr`
   and count zero for an empty capability family.
3. Add one `HaseInstrumentDescriptor` to the endpoint's instrument array.
4. Update the endpoint instrument count.
5. Add typed registrations for every capability on the new instrument.

Instrument order is observable in discovery and descriptor serialization. Keep
the order deliberate and stable once clients depend on it.

### Add a numeric read-only Property

The current public application boundary supports numeric reads through
`HaseReadNumericPropertyCallback`:

```cpp
HaseApplicationResult ReadMeasurement(
    void* context,
    double& value)
{
    return static_cast<EndpointApplication*>(context)->
        readMeasurement(value);
}
```

Declare a numeric, read-only `HasePropertyDescriptor`, then register it with
the numeric callback and null Boolean callbacks:

```cpp
properties[index] =
{
    &Instruments[instrumentIndex],
    &InstrumentProperties[propertyIndex],
    &application,
    ReadMeasurement,
    nullptr,
    nullptr
};
```

The callback sets `value` and returns `HaseApplicationResult::Success`, or
returns `HaseApplicationResult::Unavailable` when hardware cannot supply a
valid reading. The runtime owns UTC timestamps and response encoding.

### Add a Boolean Property

Boolean Properties use `HaseReadBooleanPropertyCallback` and, for read/write
access, `HaseWriteBooleanPropertyCallback`. A read-only Boolean registration
has a null write callback. A read/write registration supplies both:

```cpp
properties[index] =
{
    &Instruments[instrumentIndex],
    &InstrumentProperties[propertyIndex],
    &application,
    nullptr,
    ReadEnabled,
    WriteEnabled
};
```

Perform a write once and return its result. The framework never retries or
replays a mutation. When confirmation is meaningful, read back or retain the
device-authoritative state before returning `Success`.

### Add a Command

The current public Command form uses
`HaseExecuteNullBooleanCommandCallback`: it accepts a null argument and returns
Boolean:

```cpp
HaseApplicationResult ExecuteAction(
    void* context,
    bool& result)
{
    return static_cast<EndpointApplication*>(context)->
        executeAction(result);
}
```

Register it with the matching instrument and descriptor:

```cpp
commands[index] =
{
    &Instruments[instrumentIndex],
    &InstrumentCommands[commandIndex],
    &application,
    ExecuteAction
};
```

Execute the physical mutation at most once. Return `Unavailable` when the
hardware cannot perform or confirm it. The framework creates the Protocol
response and does not retry the callback.

### Add an Event

The current public Event boundary publishes null-payload Events. Register the
Event descriptor:

```cpp
events[index] =
{
    &Instruments[instrumentIndex],
    &InstrumentEvents[eventIndex]
};
```

Bind that registration to application state before returning the completed
definition. Detect the physical transition in `EndpointApplication::update`
and publish it once:

```cpp
runtime.publishNullEvent(
    *_eventRegistration);
```

The application owns detection, debounce, rearming, and duplicate suppression.
The runtime owns UTC timestamping, Protocol framing, and live delivery. Events
are not queued or replayed when no client is connected.

## Implement hardware behavior

`EndpointApplication` derives from `HaseEndpointApplication` and implements:

```cpp
bool beginHardware() override;
void beginEventDetection() override;
void update(HaseEndpointRuntime& runtime) override;
```

The runtime startup order is fixed:

1. validate configuration and endpoint definition;
2. run `beginHardware`;
3. connect Wi-Fi;
4. synchronize UTC;
5. run `beginEventDetection`; and
6. start TCP and mDNS publication.

Return `false` from `beginHardware` when required hardware cannot initialize.
Do not start Event detection there when it requires synchronized UTC; use
`beginEventDetection` instead. Keep `update` non-blocking so transport,
recovery, and local Event detection continue to make progress.

### Add a hardware dependency

Keep endpoint-specific libraries and includes in the application layer. For a
repository-controlled dependency:

1. add the complete Arduino library under `HaseESP32/Libraries/<Library>`;
2. retain its `library.properties` identity and version;
3. include it only from `EndpointApplication.*`;
4. initialize it in `beginHardware`;
5. return `Unavailable` from callbacks when it cannot provide valid data; and
6. extend the controlled compilation checks to verify the dependency and its
   version.

Do not add sensor names, GPIO pins, or vendor headers to
`libraries/HaseEsp32Endpoint/src`. The HASE framework must remain independent
of the physical application.

## Compose the sketch

`HaseESP32.ino` should remain small. It creates the application, asks it to
create the definition, creates `HaseEndpointRuntime`, and delegates Arduino
startup and loop processing:

```cpp
EndpointApplication endpointApplication;

const HaseEndpointDefinition& endpointDefinition =
    CreateEndpointDefinition(endpointApplication);

HaseEndpointRuntime endpointRuntime(
    EndpointConfiguration,
    endpointDefinition,
    endpointApplication);
```

Call `endpointRuntime.begin(WIFI_SSID, WIFI_PASSWORD)` once from `setup` and
`endpointRuntime.update()` from `loop`. Protocol or hardware implementation
does not belong in the sketch file.

## Validate before any physical operation

Run validation from the repository root on the computer that holds the
controlled toolchain.

### 1. Check the application surface

Confirm that the sketch root contains the five tracked application files and
that local secrets remain ignored:

```powershell
$expectedSourceFiles = @(
    "EndpointApplication.cpp",
    "EndpointApplication.h",
    "EndpointConfiguration.h",
    "EndpointDefinition.cpp",
    "HaseESP32.ino"
) | Sort-Object

$actualSourceFiles = @(
    Get-ChildItem -LiteralPath ".\HaseESP32" -File |
    Where-Object {
        $_.Extension -in @(".ino", ".cpp", ".h") -and
        $_.Name -cne "HaseSecrets.h"
    } |
    ForEach-Object {
        $_.Name
    } |
    Sort-Object
)

$difference = @(
    Compare-Object `
        -ReferenceObject $expectedSourceFiles `
        -DifferenceObject $actualSourceFiles `
        -CaseSensitive
)

if ($difference.Count -ne 0)
{
    throw "The application source file set is invalid."
}

git check-ignore --quiet -- HaseESP32/HaseSecrets.h

if ($LASTEXITCODE -ne 0)
{
    throw "HaseSecrets.h is not ignored."
}
```

The five expected names are `HaseESP32.ino`, `EndpointConfiguration.h`,
`EndpointDefinition.cpp`, `EndpointApplication.h`, and
`EndpointApplication.cpp`.

`tools/Arduino/Test-HaseEsp32EndpointAuthoringBoundary.ps1` records the exact
54D1 transition and verifies its 30-path pre-commit state. Because that audit
is deliberately bound to the 54C3 parent and 54D1 diff, it is not a general
validator for a differently named or differently shaped custom endpoint.

### 2. Run controlled compilation

Choose a new evidence directory outside the repository for each run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force

& ".\tools\Arduino\Test-HaseEsp32EndpointCompilation.ps1" `
    -RepositoryRoot (Get-Location).Path `
    -EvidenceRoot "H:\HASE-Packages\HaseESP32-Compile-Evidence"
```

Set the process execution policy if required, then run the tool. The tool:

- compiles the focused runtime fixture;
- stages the application externally with placeholder Wi-Fi values;
- does not read or hash local Wi-Fi secrets;
- resolves the HASE and Adafruit libraries explicitly;
- performs two clean application compilations;
- rejects compiler warnings;
- verifies equal artifact names and lengths; and
- retains logs, build trees, and output artifacts outside the repository.

The two builds can have different `.bin`, `.elf`, `.map`, or merged-image
hashes because the selected toolchain embeds non-reproducible build metadata.
Equal artifact names and lengths, successful clean builds, zero warnings, and
the retained logs are the required evidence; byte-identical hashes are not
claimed.

Neither validation step installs or updates dependencies, detects a connected
board, opens a serial port, uploads firmware, or changes physical state.

## Common failures

| Failure | Meaning and correction |
| --- | --- |
| Visible application source set is invalid | Keep exactly the five tracked application files; move templates and framework sources out of the sketch root. |
| Local `HaseSecrets.h` is not ignored | Restore the repository ignore rule before entering credentials. Never stage the file. |
| Definition validation fails | Check unique identities, descriptor counts, access modes, callback types, and registration ownership. |
| Property or Command is reported not found | Confirm that the request identity matches a registered descriptor on the intended instrument. |
| Property or hardware is unavailable | Confirm required hardware initialization and return `Success` only after obtaining or applying a valid value. |
| Event never appears | Confirm registration binding, detection initialization, debounce/rearming logic, an active client, and exactly one `publishNullEvent` call per occurrence. |
| Framework contains hardware references | Move vendor includes, pins, devices, and hardware behavior back to `EndpointApplication.*`. |
| Evidence directory already exists | Select a new outside-repository evidence directory. Do not delete retained evidence implicitly. |
| Artifact hashes differ | Expected with this toolchain when names and lengths match and both clean compilations succeed without warnings. |

## Controlled deployment remains separate

Successful compilation does not by itself authorize firmware upload. The
accepted deployment sequence uses the read-only physical preflight, prepares
exact Current and Rollback bundles with `New-HaseEsp32DeploymentBundle.ps1`,
creates a bound readiness plan, obtains explicit operator confirmation, and
then permits one invocation of `Invoke-HaseEsp32ControlledUpload.ps1`. The
uploader performs no automatic retry or rollback and retains sanitized begin
and result evidence.

Compiled firmware contains local configuration and remains sensitive in
current-user-local custody. Do not copy source, secrets, or sensitive binaries
into retained sanitized evidence. Arduino CLI 1.3.1 can create `_flashed.bin`
files beside its upload inputs, so uploads must use an isolated workspace
rather than the retained bundle. Classify and retain unexpected artifacts for
an explicit recovery decision.

After upload, validate the authoritative endpoint identity, BME280 Properties,
GPIO behavior and Events, and disconnect/reconnect recovery through the Runtime
Host and Laptop Client. Use Capability C-005 to read and strictly validate the
complete native Protocol Version 1 descriptor. That native descriptor does not
contain the numeric descriptor-version field used by Compact Serial Arduino
descriptors.
