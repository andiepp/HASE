HASE Increment 46J2 source overlay

Apply from the H:\Development repository root with the HASE Client and both
Runtime Hosts stopped. Extract this archive into H:\Development and allow the
two files under src and tests to be replaced.

Build the complete solution in Visual Studio 2026 Release configuration and run
the complete automated test suite before updating the installed HASE Client.

After the automated suite passes, update the Client from H:\Development with:

Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
& .\tools\Deployment\Update-HaseClient.ps1

The Runtime Hosts do not require an update for this Client-only correction.
