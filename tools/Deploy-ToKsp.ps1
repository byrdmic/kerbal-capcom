param(
  [Parameter(Mandatory=$true)]
  [string]$TargetPath,

  [string]$KspDir = $env:KSP_DIR,

  [string]$ModName = "KSPCapcom",

  [switch]$Clean
)

if ([string]::IsNullOrWhiteSpace($KspDir)) {
  throw "KspDir not provided. Pass -KspDir or set environment variable KSP_DIR."
}

if (!(Test-Path -LiteralPath $TargetPath)) {
  throw "TargetPath not found: $TargetPath"
}

$destPlugins = Join-Path $KspDir ("GameData\" + $ModName + "\Plugins")
New-Item -ItemType Directory -Force -Path $destPlugins | Out-Null

if ($Clean) {
  # Remove old DLL/PDB with the same base name (keeps the folder tidy)
  $base = [System.IO.Path]::GetFileNameWithoutExtension($TargetPath)
  $oldDll = Join-Path $destPlugins ($base + ".dll")
  $oldPdb = Join-Path $destPlugins ($base + ".pdb")
  Remove-Item -Force -ErrorAction SilentlyContinue $oldDll, $oldPdb
}

Copy-Item -Force -LiteralPath $TargetPath -Destination $destPlugins

# Copy .pdb if it exists (debugging)
$pdb = [System.IO.Path]::ChangeExtension($TargetPath, ".pdb")
if (Test-Path -LiteralPath $pdb) {
  Copy-Item -Force -LiteralPath $pdb -Destination $destPlugins
}

Write-Host ("Deployed to: " + $destPlugins)
