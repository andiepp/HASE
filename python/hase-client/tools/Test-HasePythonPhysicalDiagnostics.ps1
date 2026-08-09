[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProfilePath)
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
& (Join-Path $root ".venv\Scripts\python.exe") -m hase._physical_diagnostics_validation $ProfilePath
exit $LASTEXITCODE
