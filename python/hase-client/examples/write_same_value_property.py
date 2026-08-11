"""Write one explicitly selected HASE Property's authoritative value back once."""

from __future__ import annotations

import argparse
import asyncio
import math
import sys
from collections.abc import Sequence
from pathlib import Path

from hase import (
    AutomationTargetRegistryError,
    EndpointConnectionState,
    MutationFailureClassification,
    NumericDataDescriptor,
    PropertyAccessMode,
    PropertyOperationStatus,
    PropertyTarget,
    RuntimeHostChannelError,
    RuntimeHostClient,
    RuntimeHostClientError,
    RuntimeHostMutationError,
    load_automation_target_registry,
    open_runtime_host_channel,
)


class ExampleWriteError(RuntimeError):
    """One sanitized example-local failure."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(code)


def resolve_property(
    snapshot,
    endpoint_id: str,
    instrument_id: str,
    property_id: str,
):
    endpoints = tuple(
        item for item in snapshot.endpoints
        if item.endpoint_id == endpoint_id
    )
    if len(endpoints) != 1:
        raise ExampleWriteError("endpoint-not-found")
    endpoint = endpoints[0]
    if endpoint.connection_status.state is not EndpointConnectionState.READY:
        raise ExampleWriteError("endpoint-not-ready")

    instruments = tuple(
        item for item in endpoint.descriptor.instruments
        if item.instrument_id == instrument_id
    )
    if len(instruments) != 1:
        raise ExampleWriteError("instrument-not-found")
    instrument = instruments[0]

    properties = tuple(
        item for item in instrument.properties
        if item.property_id == property_id
    )
    if len(properties) != 1:
        raise ExampleWriteError("property-not-found")
    descriptor = properties[0]
    if descriptor.access_mode is not PropertyAccessMode.READ_WRITE:
        raise ExampleWriteError("property-not-read-write")

    return (
        PropertyTarget(
            endpoint.endpoint_id,
            endpoint.attachment_generation,
            instrument.instrument_id,
            descriptor.property_id,
        ),
        descriptor,
    )


def _confirmed_value(result, code: str):
    if (
        result.status is not PropertyOperationStatus.SUCCESS
        or result.confirmed_value is None
        or result.diagnostic is not None
    ):
        raise ExampleWriteError(code)
    value = result.confirmed_value.value
    if value is None:
        raise ExampleWriteError(f"{code}-value-absent")
    if isinstance(value, float) and not math.isfinite(value):
        raise ExampleWriteError(f"{code}-value-invalid")
    if type(value) not in (bool, str, float, bytes):
        raise ExampleWriteError(f"{code}-value-invalid")
    return value


def _format_value(descriptor, value) -> str:
    if isinstance(descriptor.data, NumericDataDescriptor):
        if type(value) is not float or not math.isfinite(value):
            raise ExampleWriteError("numeric-value-invalid")
        return f"{value:g} {descriptor.data.native_unit.symbol}"
    if type(value) is bytes:
        return f"<{len(value)} bytes>"
    return str(value)


async def write_same_value_property(
    registry_path: Path,
    target_id: str,
    endpoint_id: str,
    instrument_id: str,
    property_id: str,
) -> str:
    registry = load_automation_target_registry(registry_path)
    selected = registry.resolve(target_id)
    channel = await open_runtime_host_channel(selected.profile)

    async with channel:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot()
        property_target, descriptor = resolve_property(
            snapshot,
            endpoint_id,
            instrument_id,
            property_id,
        )

        initial_result = await client.read_authoritative_property(property_target)
        initial = _confirmed_value(initial_result, "initial-read-invalid")

        write_result = await client.write_property(property_target, initial)
        confirmed = _confirmed_value(write_result, "write-confirmation-invalid")
        if confirmed != initial or type(confirmed) is not type(initial):
            raise ExampleWriteError("write-confirmation-mismatch")

        reconciled_result = await client.read_authoritative_property(property_target)
        reconciled = _confirmed_value(
            reconciled_result,
            "reconciliation-read-invalid",
        )
        if reconciled != initial or type(reconciled) is not type(initial):
            raise ExampleWriteError("write-reconciliation-mismatch")

    initial_text = _format_value(descriptor, initial)
    reconciled_text = _format_value(descriptor, reconciled)
    return "\n".join(
        (
            f"Target: {selected.display_name}",
            f"Property: {descriptor.display_name}",
            f"Initial authoritative value: {initial_text}",
            "Write: confirmed",
            f"Reconciled authoritative value: {reconciled_text}",
            "Reconciliation: matched",
        )
    )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Write one explicitly selected HASE Property's current "
            "authoritative value back exactly once."
        )
    )
    parser.add_argument("--registry", required=True, type=Path)
    parser.add_argument(
        "--target",
        required=True,
        choices=("desktop-runtime-host", "minipc-runtime-host"),
    )
    parser.add_argument("--endpoint", required=True)
    parser.add_argument("--instrument", required=True)
    parser.add_argument("--property", required=True)
    parser.add_argument(
        "--confirm-same-value-write",
        required=True,
        action="store_true",
        help=(
            "Confirm exactly one write of the current authoritative value; "
            "no new value can be supplied."
        ),
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    arguments = _parser().parse_args(argv)
    try:
        output = asyncio.run(
            write_same_value_property(
                arguments.registry,
                arguments.target,
                arguments.endpoint,
                arguments.instrument,
                arguments.property,
            )
        )
    except AutomationTargetRegistryError as failure:
        print(
            f"ERROR: target registry is invalid ({failure.code})",
            file=sys.stderr,
        )
        return 2
    except RuntimeHostChannelError as failure:
        print(
            f"ERROR: Runtime Host channel could not be opened ({failure.code})",
            file=sys.stderr,
        )
        return 3
    except RuntimeHostMutationError as failure:
        classification = failure.classification.value
        if failure.classification is MutationFailureClassification.OUTCOME_UNCERTAIN:
            classification = "outcome-uncertain"
        print(
            "ERROR: same-value Property write failed "
            f"({classification}; {failure.code})",
            file=sys.stderr,
        )
        return 4
    except RuntimeHostClientError as failure:
        print(
            f"ERROR: Runtime Host operation failed ({failure.code})",
            file=sys.stderr,
        )
        return 5
    except ExampleWriteError as failure:
        print(
            f"ERROR: same-value Property write example failed ({failure.code})",
            file=sys.stderr,
        )
        return 6

    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
