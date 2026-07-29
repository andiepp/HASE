# HASE Descriptor-Driven Property Editing Tutorial

This tutorial validates the same Property descriptors through the Desktop
Runtime Host locally and the WPF Laptop Client remotely.

## Start the validation endpoint

Start `Hase.DesktopHost.App` with the existing external deployment arguments
and:

```text
--include-byte-buffer-simulation
```

Select endpoint `simulation-byte-buffer-validation` and instrument
`Simulated Property Editor Validation`.

## Editors and syntax

| Property | Editor | Example |
| --- | --- | --- |
| Enabled | Check box | `True` |
| Setpoint | Text | `23.5` |
| Label | Text | `HASE` |
| Buffer Value | Text | `00 53 FF` |

Numeric input is invariant and Setpoint accepts `-40` through `125` °C.
`23,5`, `NaN`, infinity, and out-of-range values are invalid. String input is
exact: empty and whitespace-only values are valid. ByteArray accepts
case-insensitive hexadecimal pairs with optional whitespace; incomplete bytes
are invalid.

## Local Desktop write

Enter a requested value and select **Write requested value**. A valid write
transitions through Executing to Succeeded, records a local operator activity,
and waits for authoritative inventory refresh. The requested editor remains
independent. **Reset to current** copies the authoritative value without
writing.

## Remote Laptop write

Connect the Laptop Client using the established external configuration. The
same descriptor chooses the same editor and shared validation semantics.
Successful writes use authenticated gRPC and show the endpoint-confirmed value.
Invalid input never produces an RPC. Writes are not automatically retried.

## Observation and reconnect

Keep both applications open. A write in either application should update the
authoritative value in both automatically. Disconnecting and reconnecting the
Laptop Client restores current values from the runtime host. Confirmed reads
remain visible across observation updates for the current attachment.

## ByteArray Command compatibility

Execute **Replace Buffer** with `00 7F FF`. The Command and direct Property
write share the same authoritative `Buffer.Value`; both applications should
display `007FFF` (spacing may differ).

## Safety

The runtime host owns endpoint lifecycles. Every operation targets endpoint,
attachment generation, instrument, and Property identity. A validation error
is local; a successful client conversion is not a substitute for runtime or
endpoint validation.

