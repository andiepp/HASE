# HASE Example 6 — A webcam (live video)

This example streams live video from a camera on the host PC into the
Client on the second PC — view-only, explicitly started and stopped by
the Client, delivered directly between the two machines over encrypted
WebRTC while the secured gRPC channel carries only control. It builds on
a completed [Example 3](Example-3-Client-on-a-Second-PC.md).

The contracts, fixed by design: **one session, one viewer**; the Client
must explicitly start and stop; **no recording**, no snapshots, **no
public relay**; camera device identifiers never leave the host PC —
the Client sees only the logical name you choose below. Media is bound to
the secured profile; the development loopback profile rejects it.

## Prerequisites

- A completed Example 3: the secured host and the remote Client
  connecting.
- A camera on the **host PC** (built-in or USB), optionally with a
  microphone.
- The Microsoft Edge WebView2 runtime on **both** PCs (preinstalled on
  current Windows; otherwise from Microsoft's WebView2 page).
- Windows camera privacy on the host PC must allow desktop apps:
  *Settings → Privacy & security → Camera*.

## Step 1 — Bind the camera on the host PC

With both applications closed, the Runtime Host's binding mode lists the
machine's cameras and writes your selection as the media configuration:

```powershell
$ErrorActionPreference = "Stop"
$secured = Join-Path $env:LocalAppData "HASE\Secured"

& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    --prepare-media-binding `
    (Join-Path $secured "media-configuration.json") `
    "runtime-host-camera-01" `
    ([Guid]::NewGuid().ToString("N")) `
    "Runtime Host Camera"
```

Select a camera (and optionally a microphone) in the window and confirm.
The file is written atomically and an existing one is refused — to
rebind, delete `media-configuration.json` first. With this static
configuration, the camera selection is fixed at host start; rebinding
means rerunning this step.

## Step 2 — Grant media to the client principal

On the **host PC** (like Steps 1 and 3 — all authored files live in the
host's `Secured` folder), rewrite the authorization policy with the six
media grants beside the six operational ones from Example 3:

```powershell
$ErrorActionPreference = "Stop"
$secured = Join-Path $env:LocalAppData "HASE\Secured"

@"
{
  "formatVersion": 1,
  "grants": [
    { "principalId": "laptop-validation-client", "permission": "runtime-host.snapshot.read" },
    { "principalId": "laptop-validation-client", "permission": "property.cached.read" },
    { "principalId": "laptop-validation-client", "permission": "property.authoritative.read" },
    { "principalId": "laptop-validation-client", "permission": "property.write" },
    { "principalId": "laptop-validation-client", "permission": "command.execute" },
    { "principalId": "laptop-validation-client", "permission": "observation.subscribe" },
    { "principalId": "laptop-validation-client", "permission": "media.capability.read" },
    { "principalId": "laptop-validation-client", "permission": "media.video.receive" },
    { "principalId": "laptop-validation-client", "permission": "media.audio.receive" },
    { "principalId": "laptop-validation-client", "permission": "media.session.start" },
    { "principalId": "laptop-validation-client", "permission": "media.session.negotiate" },
    { "principalId": "laptop-validation-client", "permission": "media.session.stop" }
  ]
}
"@ | Set-Content -Encoding utf8 (Join-Path $secured "authorization-policy.json")
```

## Step 3 — Reference the media configuration

On the **host PC**, rewrite the installation profile with the added
`mediaConfigurationFilePath` line:

```powershell
$ErrorActionPreference = "Stop"
$secured = Join-Path $env:LocalAppData "HASE\Secured"

@"
{
  "formatVersion": 1,
  "identityFilePath": "$($secured.Replace('\', '\\'))\\runtime-host-identity.json",
  "privateNetworkConfigurationFilePath": "$($secured.Replace('\', '\\'))\\desktop-private-network.json",
  "endpointCompositionFilePath": "$($secured.Replace('\', '\\'))\\desktop-runtime-endpoints.json",
  "authorizationPolicyFilePath": "$($secured.Replace('\', '\\'))\\authorization-policy.json",
  "mediaConfigurationFilePath": "$($secured.Replace('\', '\\'))\\media-configuration.json",
  "includeByteBufferSimulation": true
}
"@ | Set-Content -Encoding utf8 (Join-Path $secured "desktop-runtime-host.json")
```

## Step 4 — Start and stream

Start the secured Runtime Host on the host PC and the Client on the
client PC exactly as in Example 3, and press `Connect`. Then, in the
Client:

1. Open the **`Video / Audio`** window.
2. Select `Runtime Host Camera` and press **`Start Video`**. After the
   authorization and WebRTC negotiation, live video from the host PC's
   camera appears.
3. Optionally activate **audio** — microphone sound plays only after this
   explicit step, never automatically.
4. Press **`Stop`** — the host releases the camera and the presentation
   returns to black.
5. Close the media window during an active stream: the session stops and
   the window hides; reopening it and pressing `Start Video` again works
   repeatably. Note that many cameras have no activity indicator — the
  presentation state in the Client is the authoritative signal.

Try the failure path too: unplug a USB camera mid-stream. The session
ends with a source-loss error rather than freezing silently; replug the
camera, and a fresh `Start Video` streams again.

## Troubleshooting

- **The camera list in Step 1 is empty** — Windows camera privacy denies
  desktop apps, or another application holds the camera exclusively.
- **`Start Video` is refused** — the media grants of Step 2 are missing
  for the principal (capability reads fail first), or another viewer
  already holds the single session.
- **The media window shows no content at all** — the WebView2 runtime is
  missing on the Client PC.
- **The stream does not establish across the network** — both the gRPC
  control channel (Example 3's port) and direct WebRTC traffic between
  the two PCs must be possible; networks that isolate clients from each
  other block the media path exactly as they block everything else.
- The host publishes media capability only when the installation profile
  references a media configuration — a missing Step 3 means the Client
  sees no camera at all.

## The extended ladder

With Examples 0 through 6 you have seen every capability family HASE
publishes: simulated, USB, network, and SCPI instruments, the secured
remote and multi-host Client, and live media. From here, the
[SCPI Instrument Authoring Guide](../SCPI-Instrument-Authoring-Guide.md)
and the
[ESP32 Endpoint Authoring Guide](../ESP32-Endpoint-Authoring-Guide.md)
lead into authoring, and the
[Python client](../../python/hase-client/README.md) into automation.
