param(
  [string]$SolutionPath = (Join-Path $PSScriptRoot "..\KSPCapcom.sln"),
  [string]$Configuration = "Debug"
)

$last = Get-Date

while ($true) {
  Start-Sleep -Milliseconds 500

  $changed = Get-ChildItem -Recurse -Filter *.cs (Split-Path $SolutionPath) |
    Where-Object { $_.LastWriteTime -gt $last } |
    Select-Object -First 1

  if ($changed) {
    $last = Get-Date
    Write-Host ("Change detected: " + $changed.FullName)

    & msbuild $SolutionPath /t:Build /p:Configuration=$Configuration

    Write-Host "Build complete. (Restart KSP to load new DLL.)"
  }
}
