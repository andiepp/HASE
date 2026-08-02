# ADR-0043 — Increment 43G4C2C1 — Arduino A0 Voltage

## Decision

Arduino descriptor version 2 extends the existing Compact Serial validation
endpoint with a read-only A0 voltage Property while preserving the built-in LED
Property and Command and the active-low D7 pushbutton Event.

The firmware reads the Uno's 10-bit ADC, converts the result to 0–5000
millivolts, and returns the value as two unsigned little-endian bytes. The host
materializes that wire value as a `double` measured in volts.

## Contract

- Descriptor: `arduino-uno-validation`, version 2
- Existing endpoint identity: `arduino-uno-01`
- Existing LED Property: compact ID `0x01`
- A0 voltage Property: compact ID `0x02`
- Path: `Analog.Voltage`
- Unit: `V`
- Range: 0.0–5.0 V
- Resolution: 5/1023 V
- Access: read-only
- Wire format: unsigned 16-bit little-endian millivolts
- D7 pushbutton Event: compact ID `0x01`, unchanged

The compact millivolt decoder requires exactly two bytes. Its corresponding
encoder accepts only finite `System.Double` volt values representable by the
unsigned 16-bit millivolt wire format.

## Compatibility

The production Desktop Runtime Host registers both descriptor versions. The
existing desktop Arduino may therefore continue running version 1, while the
new MiniPC Arduino runs version 2. Firmware and descriptor mismatches still
fail closed.

## Physical validation

After all automated tests pass:

1. Open `HaseArduinoUno/HaseArduinoUno.ino` in Arduino IDE.
2. Select the MiniPC Arduino Uno and its COM port.
3. Compile and upload the firmware.
4. Verify descriptor version 2 bootstrap.
5. Verify LED read/write and toggle.
6. Press the D7-to-GND pushbutton and verify one event.
7. Turn the A0 potentiometer and verify changing values across approximately
   0–5 V.

No Runtime Host installation, Client registry mutation, or private-network
startup is authorized by this increment.
