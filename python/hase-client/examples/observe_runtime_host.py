"""Observe one explicitly selected HASE Runtime Host for a bounded count."""
from __future__ import annotations

import argparse
import asyncio
from collections.abc import Sequence
from pathlib import Path
import sys

from hase import (
    AttachmentEnded,
    AttachmentPublished,
    AutomationTargetRegistryError,
    ConnectionStatusChanged,
    EventOccurred,
    ObservationInitialSnapshot,
    PropertyValueChanged,
    RuntimeHostChannelError,
    RuntimeHostClient,
    RuntimeHostClientError,
    RuntimeHostObservation,
    load_automation_target_registry,
    open_runtime_host_channel,
)

_MIN_COUNT = 1
_MAX_COUNT = 1000


class ExampleObservationError(RuntimeError):
    """A bounded local live-observation example failure."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(code)


def _bounded_count(value: str) -> int:
    try:
        count = int(value)
    except ValueError:
        raise argparse.ArgumentTypeError("count must be an integer") from None
    if count < _MIN_COUNT or count > _MAX_COUNT:
        raise argparse.ArgumentTypeError(
            f"count must be between {_MIN_COUNT} and {_MAX_COUNT}"
        )
    return count


def _scalar(value: object) -> str:
    if value is None:
        return "<absent>"
    if isinstance(value, bytes):
        return value.hex()
    return str(value)


def _property_value(value: object) -> str:
    return (
        f"{_scalar(value.value)} "
        f"[quality={value.quality.value}, timestamp={value.timestamp_utc.isoformat()}]"
    )


def format_observation(observation: RuntimeHostObservation) -> str:
    lines = [
        f"Sequence: {observation.sequence}",
        f"Endpoint: {observation.endpoint_id}",
        f"Kind: {observation.kind.value}",
    ]
    payload = observation.payload

    if isinstance(payload, AttachmentPublished):
        lines.extend(
            (
                f"Published state: {payload.endpoint.connection_status.state.value}",
                f"Instruments: {len(payload.endpoint.descriptor.instruments)}",
            )
        )
    elif isinstance(payload, AttachmentEnded):
        lines.append(f"Ended UTC: {payload.ended_at_utc.isoformat()}")
    elif isinstance(payload, ConnectionStatusChanged):
        lines.extend(
            (
                f"Previous: {payload.previous_status.state.value}",
                f"Current: {payload.current_status.state.value}",
            )
        )
    elif isinstance(payload, PropertyValueChanged):
        lines.extend(
            (
                f"Instrument: {payload.instrument_id}",
                f"Property: {payload.property_id}",
                f"Previous value: {_property_value(payload.previous_value)}",
                f"Current value: {_property_value(payload.current_value)}",
            )
        )
    elif isinstance(payload, EventOccurred):
        lines.extend(
            (
                f"Instrument: {payload.instrument_id}",
                f"Event: {'/'.join(payload.event_path_segments)}",
                f"Occurred UTC: {payload.occurred_at_utc.isoformat()}",
                f"Value: {_scalar(payload.value)}",
            )
        )
    else:
        raise ExampleObservationError("observation-payload-unsupported")

    return "\n".join(lines)


async def observe_runtime_host(
    registry_path: Path,
    target_id: str,
    count: int,
) -> str:
    registry = load_automation_target_registry(registry_path)
    target = registry.resolve(target_id)
    channel = await open_runtime_host_channel(target.profile)

    lines = [
        f"Target: {target.display_name}",
        f"Live observation count: {count}",
    ]

    async with channel:
        client = RuntimeHostClient(channel)
        initial_seen = False
        live_count = 0

        async for message in client.observe():
            if isinstance(message, ObservationInitialSnapshot):
                if initial_seen:
                    raise ExampleObservationError(
                        "observation-initial-snapshot-repeated"
                    )
                initial_seen = True
                lines.extend(
                    (
                        f"Initial snapshot sequence: {message.snapshot_sequence}",
                        f"Endpoints: {len(message.snapshot.endpoints)}",
                    )
                )
                continue

            if not initial_seen:
                raise ExampleObservationError(
                    "observation-initial-snapshot-missing"
                )
            if not isinstance(message, RuntimeHostObservation):
                raise ExampleObservationError("observation-message-unsupported")

            lines.extend(("", format_observation(message)))
            live_count += 1
            if live_count == count:
                break

        if not initial_seen:
            raise ExampleObservationError("observation-initial-snapshot-missing")
        if live_count != count:
            raise ExampleObservationError("observation-stream-ended-early")

    return "\n".join(lines)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Observe one explicitly selected HASE Runtime Host."
    )
    parser.add_argument("--registry", required=True, type=Path)
    parser.add_argument(
        "--target",
        required=True,
        choices=("desktop-runtime-host", "minipc-runtime-host"),
    )
    parser.add_argument("--count", required=True, type=_bounded_count)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        output = asyncio.run(
            observe_runtime_host(
                args.registry,
                args.target,
                args.count,
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
    except RuntimeHostClientError as failure:
        print(
            f"ERROR: Runtime Host observation failed ({failure.code})",
            file=sys.stderr,
        )
        return 4
    except ExampleObservationError as failure:
        print(
            f"ERROR: live observation example failed ({failure.code})",
            file=sys.stderr,
        )
        return 5

    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
