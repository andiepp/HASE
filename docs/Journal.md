Current Task

Design and implement the first simulated engineering system.

The first example will be an environment sensor exposing:

Temperature
Relative humidity
Atmospheric pressure
Planned Simulation Concepts
SimulationHost
EnvironmentSimulation
ValueGenerator
ConstantGenerator
RandomGenerator
RandomWalkGenerator
RampGenerator
SineGenerator
NoiseGenerator

These names are proposals and have not yet been finalized.

Accepted Simulation Principle

A simulated endpoint shall be indistinguishable from a physical endpoint to a HASE application.

The engineering system exists independently of its execution environment.

Immediate Next Step

Design the responsibilities of:

SimulationHost
EnvironmentSimulation
ValueGenerator

Do not implement transports yet.

Backlog
Descriptor repository
Descriptor resolution services
Serial transport
TCP/IP transport
BLE transport
Gateway support
Diagnostics and message tracer
HASE Studio
HASE SDK
Parameter dependency system
Recording and replay