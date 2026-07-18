# Project Status

## Project

**HASE – Hardware Access System Environment**

HASE is an open, modular framework for describing, discovering, communicating with, and controlling hardware instruments independently of transport technology.

---

# Overall Status

**Current Phase:** Phase 6 – Transport Infrastructure and Physical Endpoint Integration

The core architecture, runtime model, simulation framework, Protocol Version 1, runtime integration, Protocol Explorer, production TCP transport, duplex protocol infrastructure, endpoint synchronization, automatic connection recovery, active protocol health probing, runtime event routing, transport diagnostics, physical property access, physical command execution, and physical event notification are implemented.

Phase 6 has established:

- production framed TCP transport;
- physical DOIT ESP32 DEVKITC V4 endpoint;
- BME280 environment-sensor instrument;
- ESP32 GPIO controller instrument;
- explicit transport lifecycle and health tracking;
- coordinator-owned duplex protocol sessions;
- correlated responses and unsolicited notification routing through one receive path;
- automatic initial connection retry and transport-fault recovery;
- active protocol health probing for silent connection failures;
- complete descriptor and readable-property resynchronization;
- cached-value preservation during connection loss;
- logical exchange diagnostics across replacement sessions;
- physical Boolean property access;
- physical command execution;
- physical GPIO event notification;
- positive and negative validation with real ESP32 hardware.

The physical endpoint identity is:

```text
doit-esp32-devkitc-v4-01