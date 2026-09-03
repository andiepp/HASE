# Security

HASE controls laboratory hardware through a Runtime Host that owns every
physical connection, behind a mutual-TLS northbound API with certificate
authorization. A vulnerability in the Runtime Host, the clients, the
provisioning tools or the firmware can reach physical equipment, so reports
are taken seriously and handled privately until a fix is available.

## Reporting a vulnerability

Report privately through GitHub's private vulnerability reporting for this
repository: open the repository's Security tab and choose "Report a
vulnerability". Do not open a public issue for a security problem, and do not
include credentials, certificates, private keys or real network addresses in
a report; describe the path and the effect.

You will receive an acknowledgement, an assessment, and, when a fix lands, a
note of the commit that carries it.

## Scope

In scope: the Runtime Host and its endpoint providers, the northbound gRPC
API and its authorization, the .NET and Python clients, the provisioning,
deployment and update tools under `tools/` and `python/hase-client/tools/`,
and the validation boards' firmware.

Out of scope: laboratory instrument families, which live in add-on
repositories that consume this one; report those to their own maintainers.

## Supported versions

The `main` branch and the latest tagged release. Fixes land on `main` and are
tagged when they are released.

## What this repository contains

No credentials, certificates, private keys, real network addresses or
computer names are committed. The firmware's Wi-Fi credentials are read from
an untracked, ignored file that is never part of the repository. Example
addresses use the ranges reserved for documentation.
