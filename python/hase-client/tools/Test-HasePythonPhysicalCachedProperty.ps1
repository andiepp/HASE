[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProfilePath)
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
& (Join-Path $root ".venv\Scripts\python.exe") -m hase._physical_cached_property_validation $ProfilePath
exit $LASTEXITCODE
