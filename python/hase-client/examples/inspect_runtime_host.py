"""Inspect one explicitly selected HASE Runtime Host using the public API only."""
from __future__ import annotations

import argparse
import asyncio
from collections.abc import Sequence
from pathlib import Path
import sys

from hase import (
    AutomationTargetRegistryError,
    NumericDataDescriptor,
    RuntimeHostChannelError,
    RuntimeHostClient,
    RuntimeHostClientError,
    RuntimeHostSnapshot,
    load_automation_target_registry,
    open_runtime_host_channel,
)


def _path(segments: tuple[str, ...]) -> str:
    return "/".join(segments)


def _numeric_suffix(data: object) -> str:
    if not isinstance(data, NumericDataDescriptor):
        return ""
    parts = [f"quantity={data.quantity.display_name}", f"unit={data.native_unit.symbol}"]
    if data.value_range is not None:
        parts.append(f"range={data.value_range.minimum:g}..{data.value_range.maximum:g}")
    if data.resolution is not None:
        parts.append(f"resolution={data.resolution:g}")
    return " [" + ", ".join(parts) + "]"


def format_snapshot(target_name: str, snapshot: RuntimeHostSnapshot) -> str:
    """Return deterministic descriptor inventory without deployment secrets."""
    lines = [f"Target: {target_name}", f"API: {snapshot.api_version.major}.{snapshot.api_version.minor}", ""]
    for endpoint in sorted(snapshot.endpoints, key=lambda item: item.endpoint_id):
        lines += [f"Endpoint: {endpoint.endpoint_id}", f"  State: {endpoint.connection_status.state.value}"]
        for instrument in sorted(endpoint.descriptor.instruments, key=lambda item: item.instrument_id):
            lines += [f"  Instrument: {instrument.name}", f"    Id: {instrument.instrument_id}", f"    Kind: {instrument.kind}"]
            if instrument.manufacturer is not None:
                lines.append(f"    Manufacturer: {instrument.manufacturer}")
            if instrument.model is not None:
                lines.append(f"    Model: {instrument.model}")
            if instrument.firmware_version is not None:
                lines.append(f"    Firmware: {instrument.firmware_version}")
            lines.append("    Properties:")
            for item in sorted(instrument.properties, key=lambda value: value.path_segments):
                lines.append(f"      {_path(item.path_segments)} — {item.display_name} ({item.access_mode.value}){_numeric_suffix(item.data)}")
            lines.append("    Commands:")
            for item in sorted(instrument.commands, key=lambda value: value.path_segments):
                argument = " [argument]" if item.argument is not None else ""
                lines.append(f"      {_path(item.path_segments)} — {item.display_name}{argument}")
            lines.append("    Events:")
            for item in sorted(instrument.events, key=lambda value: value.path_segments):
                payload = " [payload]" if item.payload is not None else ""
                lines.append(f"      {_path(item.path_segments)} — {item.display_name}{payload}")
        lines.append("")
    return "\n".join(lines).rstrip()


async def inspect_runtime_host(registry_path: Path, target_id: str) -> str:
    registry = load_automation_target_registry(registry_path)
    target = registry.resolve(target_id)
    channel = await open_runtime_host_channel(target.profile)
    async with channel:
        snapshot = await RuntimeHostClient(channel).get_snapshot()
    return format_snapshot(target.display_name, snapshot)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Inspect one explicitly selected HASE Runtime Host.")
    parser.add_argument("--registry", required=True, type=Path, help="Absolute external Laptop target-registry path.")
    parser.add_argument("--target", required=True, choices=("desktop-runtime-host", "minipc-runtime-host"), help="Exact Runtime Host target identifier.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        output = asyncio.run(inspect_runtime_host(args.registry, args.target))
    except AutomationTargetRegistryError as failure:
        print(f"ERROR: target registry is invalid ({failure.code})", file=sys.stderr)
        return 2
    except RuntimeHostChannelError as failure:
        print(f"ERROR: Runtime Host channel could not be opened ({failure.code})", file=sys.stderr)
        return 3
    except RuntimeHostClientError as failure:
        print(f"ERROR: Runtime Host snapshot could not be obtained ({failure.code})", file=sys.stderr)
        return 4
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
