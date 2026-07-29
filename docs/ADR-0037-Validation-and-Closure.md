# ADR-0037 — Validation and Closure

## Status

Completed and accepted.

## Baseline

ADR-0037 began with 3,573 automated tests passing and closes with:

```text
3,643 automated tests passing
.NET solution builds
```

## Delivered increments

- 9A introduced `Hase.Operator.Input`, invariant typed parsing, stable
  validation failures, descriptor range enforcement, exact String handling,
  and shared ADR-0036 ByteArray parsing.
- 9B added Boolean, Numeric, String, and ByteArray editors to the remote WPF
  Laptop Client.
- 9C added the same editor set to the Desktop Runtime Host using local
  normalized operator services.
- 9D expanded the opt-in in-process simulation to four writable types.
- 9E validated local and remote writes, rejection, confirmation, observation,
  Command compatibility, and reconnect restoration.

## End-to-end evidence

Local Desktop writes succeeded for `True`, `23.5`, `Desktop local`, and
`00 53 FF`. Remote Laptop writes succeeded for `False`, `-12.5`, an exact
whitespace-bearing String, and `DE AD BE EF`. Invalid comma-decimal, out-of-
range, non-finite, and incomplete hexadecimal input remained local.

Both inclusive numeric boundaries (`-40` and `125`) succeeded. Empty and
whitespace-only Strings succeeded. Desktop-to-Laptop and Laptop-to-Desktop
Property observations arrived automatically. `Buffer.Replace 00 7F FF`
updated `Buffer.Value` in both applications without changing other Properties.
Laptop reconnect restored the final authoritative values automatically.

Desktop operator activity recorded Desktop-originated writes and did not claim
remote Laptop operations.

## Defects found and corrected

1. Laptop confirmed reads were cleared on every observation reprojection.
   Confirmed values are now retained for the current attachment and pruned
   when its generation disappears.
2. The Laptop gRPC Property mapper omitted ByteArray for read and write.
   Mapping is now symmetric and exact in both directions.

## Closure

ADR-0037 has no remaining implementation work. Enumeration editors, richer
numeric controls, localization, and structured binary schemas are independent
future capabilities.

