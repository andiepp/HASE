# ADR-0017 – Duplex Protocol Health Probing

- Status: Accepted
- Date: 2026-07-18

---

# Context

HASE runtime endpoint connections use a duplex protocol session.

A duplex session owns the transport receive path so that responses and unsolicited notifications can be routed through a single protocol binding.

The runtime endpoint connection coordinator owns:

- duplex session creation and disposal;
- endpoint synchronization;
- transport replacement after faults;
- runtime event notification routing;
- logical exchange diagnostics across replacement sessions.

A transport failure is normally detected when a protocol read or write operation fails.

However, some physical failures do not immediately produce a socket error.

For example, resetting an ESP32 can leave the host TCP connection appearing established even though the remote endpoint and its protocol session no longer exist. The duplex receive pump can remain blocked waiting for data because no TCP FIN or reset was received.

Passive transport observation is therefore insufficient to guarantee timely detection of silent connection loss.

Performing an independent protocol exchange directly on the underlying transport is not valid because it would introduce a second reader alongside the duplex receive pump. All protocol exchanges, including health probes, must use the active duplex binding.

A coordinator-owned mechanism is required to:

- perform protocol-level health probes through the active duplex session;
- apply an explicit response timeout;
- mark the transport as faulted when the probe demonstrates that the session is unusable;
- allow the existing coordinator recovery path to establish and synchronize a replacement session;
- preserve runtime event observers across that replacement.

---

# Decision

The runtime endpoint connection coordinator provides duplex protocol health probing.

Health probing is exposed through:

```text
IRuntimeEndpointProtocolHealthProbe