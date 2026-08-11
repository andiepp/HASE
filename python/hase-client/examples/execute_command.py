"""Execute one explicitly selected parameterless HASE Command exactly once."""

from __future__ import annotations

import argparse
import asyncio
import sys
from collections.abc import Sequence
from pathlib import Path

from hase import (
    AutomationTargetRegistryError,
    CommandOperationStatus,
    CommandTarget,
    EndpointConnectionState,
    MutationFailureClassification,
    RuntimeHostChannelError,
    RuntimeHostClient,
    RuntimeHostClientError,
    RuntimeHostMutationError,
    load_automation_target_registry,
    open_runtime_host_channel,
)


class ExampleCommandError(RuntimeError):
    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(code)


def resolve_command(snapshot, endpoint_id: str, instrument_id: str,
                    command_path: tuple[str, ...]):
    endpoints = tuple(
        item for item in snapshot.endpoints
        if item.endpoint_id == endpoint_id
    )
    if len(endpoints) != 1:
        raise ExampleCommandError("endpoint-not-found")
    endpoint = endpoints[0]
    if endpoint.connection_status.state is not EndpointConnectionState.READY:
        raise ExampleCommandError("endpoint-not-ready")

    instruments = tuple(
        item for item in endpoint.descriptor.instruments
        if item.instrument_id == instrument_id
    )
    if len(instruments) != 1:
        raise ExampleCommandError("instrument-not-found")
    instrument = instruments[0]

    commands = tuple(
        item for item in instrument.commands
        if item.path_segments == command_path
    )
    if len(commands) != 1:
        raise ExampleCommandError("command-not-found")
    descriptor = commands[0]
    if descriptor.argument is not None:
        raise ExampleCommandError("command-argument-not-supported")

    return (
        CommandTarget(
            endpoint.endpoint_id,
            endpoint.attachment_generation,
            instrument.instrument_id,
            descriptor.path_segments,
        ),
        descriptor,
    )


async def execute_parameterless_command(
    registry_path: Path,
    target_id: str,
    endpoint_id: str,
    instrument_id: str,
    command_path: tuple[str, ...],
) -> str:
    registry = load_automation_target_registry(registry_path)
    selected = registry.resolve(target_id)
    channel = await open_runtime_host_channel(selected.profile)

    async with channel:
        client = RuntimeHostClient(channel)
        snapshot = await client.get_snapshot()
        command_target, descriptor = resolve_command(
            snapshot, endpoint_id, instrument_id, command_path
        )
        result = await client.execute_command(command_target)
        if result.status is not CommandOperationStatus.SUCCESS:
            raise ExampleCommandError(
                f"command-result-{result.status.value}"
            )
        if result.diagnostic is not None:
            raise ExampleCommandError("command-success-diagnostic-present")

    return "\n".join(
        (
            f"Target: {selected.display_name}",
            f"Command: {descriptor.display_name}",
            f"Command path: {'/'.join(descriptor.path_segments)}",
            "Execution: confirmed",
        )
    )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Execute one explicitly selected parameterless HASE Command "
            "exactly once."
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
    parser.add_argument(
        "--command",
        required=True,
        nargs="+",
        dest="command_path",
        help="Exact Command path segments, for example Led Toggle.",
    )
    parser.add_argument(
        "--confirm-command-execution",
        required=True,
        action="store_true",
        help="Confirm exactly one execution of the selected Command.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    arguments = _parser().parse_args(argv)
    try:
        output = asyncio.run(
            execute_parameterless_command(
                arguments.registry,
                arguments.target,
                arguments.endpoint,
                arguments.instrument,
                tuple(arguments.command_path),
            )
        )
    except AutomationTargetRegistryError as failure:
        print(f"ERROR: target registry is invalid ({failure.code})", file=sys.stderr)
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
            "ERROR: Command execution failed "
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
    except ExampleCommandError as failure:
        print(
            f"ERROR: Command example failed ({failure.code})",
            file=sys.stderr,
        )
        return 6

    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
