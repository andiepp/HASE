# ADR-0007: Simulation Models Physical Processes Independently of HASE Runtime Objects

## Status

Accepted

## Context

HASE supports physical instruments connected through transports such as LAN, WLAN, USB serial, and BLE.

For development, testing, demonstrations, and operation without physical hardware, HASE also needs simulated instruments.

A simulated instrument contains two fundamentally different concerns:

1. The physical process being simulated.
2. The HASE instrument that observes or influences that process.

Examples of physical processes include:

- environmental temperature, humidity, and air pressure;
- battery charge and voltage;
- motor speed and load;
- fluid level and flow;
- heating and cooling behavior.

Physical state exists independently of the instrument that measures it. Multiple instruments may observe the same physical process, and actuators may later influence that process.

Coupling physical simulation directly to HASE descriptors, runtime properties, transports, or protocols would make simulation models difficult to reuse and test.

## Decision

HASE simulation models physical processes independently of HASE runtime objects.

The simulation subsystem is divided into the following conceptual layers:

```text
Value generators
        |
        v
Physical process simulation
        |
        v
Physical state
        |
        v
Simulated instrument
        |
        v
HASE runtime