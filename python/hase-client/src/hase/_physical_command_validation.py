"""Fixed-output validation for one safe KEL-103 CC-selection command."""
from __future__ import annotations
import asyncio, sys
from collections.abc import Sequence
from hase import (CommandOperationStatus, CommandTarget, EndpointConnectionState,
    PropertyOperationStatus, PropertyQuality, PropertyTarget, RuntimeHostClient,
    load_runtime_host_profile, open_runtime_host_channel)

INSTRUMENT = "electronic-load-01"
COMMAND = ("Mode", "SelectConstantCurrent")

class ValidationError(RuntimeError):
    def __init__(self, code): self.code = code; super().__init__(code)

async def validate(path: str) -> None:
    channel = await open_runtime_host_channel(load_runtime_host_profile(path),
        readiness_timeout=10.0)
    try:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot(timeout=10.0)
        matches = []
        for endpoint in snapshot.endpoints:
            if endpoint.connection_status.state is not EndpointConnectionState.READY:
                continue
            for instrument in endpoint.descriptor.instruments:
                if instrument.instrument_id != INSTRUMENT: continue
                command = [x for x in instrument.commands if x.path_segments == COMMAND]
                if len(command) == 1 and command[0].argument is None:
                    matches.append((endpoint, instrument))
        if len(matches) != 1: raise ValidationError("command-target-not-unique")
        endpoint, _ = matches[0]
        async def read(property_id, expected):
            result = await client.read_authoritative_property(PropertyTarget(
                endpoint.endpoint_id, endpoint.attachment_generation,
                INSTRUMENT, property_id), timeout=10.0)
            if (result.status is not PropertyOperationStatus.SUCCESS
                or result.confirmed_value is None
                or result.confirmed_value.quality is not PropertyQuality.GOOD
                or type(result.confirmed_value.value) is not expected):
                raise ValidationError("property-result-invalid")
            return result.confirmed_value.value
        if await read("operating-mode", str) != "CC" or await read(
                "input-enabled", bool) is not False:
            raise ValidationError("kel103-state-not-safe")
        result = await client.execute_command(CommandTarget(endpoint.endpoint_id,
            endpoint.attachment_generation, INSTRUMENT, COMMAND), timeout=10.0)
        if result.status is not CommandOperationStatus.SUCCESS:
            raise ValidationError("command-result-invalid")
        if await read("operating-mode", str) != "CC" or await read(
                "input-enabled", bool) is not False:
            raise ValidationError("command-reconciliation-failed")
    finally: await channel.close()

def main(arguments: Sequence[str] | None = None) -> int:
    supplied = tuple(sys.argv[1:] if arguments is None else arguments)
    try:
        if len(supplied) != 1: raise ValidationError("arguments-invalid")
        asyncio.run(validate(supplied[0]))
    except (KeyboardInterrupt, SystemExit): raise
    except Exception as failure:
        code = getattr(failure, "code", "unexpected-failure")
        print(f"Python physical command validation failed: {code}.", file=sys.stderr)
        return 1
    print("Profile loaded              : True")
    print("Safe KEL-103 state verified : True")
    print("CC command executed once    : True")
    print("CC/OFF reconciliation exact : True")
    print("Channel closed              : True")
    print("Validation succeeded        : True")
    return 0

if __name__ == "__main__": raise SystemExit(main())
