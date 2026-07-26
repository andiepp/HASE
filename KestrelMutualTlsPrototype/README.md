# Kestrel Mutual TLS Prototype — P-001 through P-004

This standalone prototype isolates Kestrel mutual-TLS behavior from HASE. It
does not reference or modify any HASE project.

## Prerequisites

- Windows 11
- .NET 10 SDK
- PowerShell

No certificate is installed in a Windows certificate store. The prototype
creates a private test root and uses an explicit custom trust store on both
sides.

## Run

Open PowerShell in this directory and execute:

```powershell
.\Run-Prototype.ps1
```

The script:

1. builds the complete prototype solution;
2. creates a root, server, and client certificate under `artifacts\certificates`;
3. starts Kestrel on `https://localhost:7443`;
4. runs the client with HTTP/2 and its client certificate;
5. verifies that a client without a certificate is rejected during TLS;
6. verifies that a certificate from an unrelated root is explicitly rejected;
7. verifies that the application endpoint executed exactly once;
8. completes an authenticated unary gRPC call over HTTP/2;
9. verifies that the gRPC method executed exactly once;
10. stops the server.

## Expected result

The server output contains:

```text
Accepted client certificate: CN=HASE Kestrel Prototype Client
```

The client output contains:

```text
HTTP status       : 200 OK
HTTP version      : 2.0
Authenticated     : True
Client subject    : CN=HASE Kestrel Prototype Client
P-001 RESULT      : PASS
P-002 RESULT      : PASS
P-003 RESULT      : PASS
P-004 RESULT      : PASS
```

The combined result contains:

```text
P-001 authenticated client : PASS
P-002 missing certificate  : PASS
P-003 untrusted client     : PASS
P-004 authenticated gRPC   : PASS
HTTP probe executions      : 1
gRPC probe executions      : 1
Untrusted TLS rejections   : 1
```

If the script fails, retain the complete PowerShell output. The server output
is also written to:

```text
artifacts\server.stdout.log
artifacts\server.stderr.log
```

## Scope

P-001 validates the successful path. P-002 validates rejection when the client
does not supply a certificate. P-003 validates explicit chain rejection when
the client certificate was issued by an unrelated root.
P-004 validates a unary gRPC call through the same authenticated TLS and HTTP/2
connection boundary.
