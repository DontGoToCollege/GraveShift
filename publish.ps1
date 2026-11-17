# Publishes GraveShift as a self-contained, single-file x64 executable.
# Usage:  .\publish.ps1         -> GraveShift.exe (and dist\GraveShift.exe), archived as Versions\Archive\GraveShift-<version>.exe
#         .\publish.ps1 -Test   -> builds into test-build\ only (nothing archived)
# Refuses a version that's already archived: raise <Version> in src\GraveShift\GraveShift.csproj first.
param([switch]$Test)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root "src\GraveShift\GraveShift.csproj"
$out  = Join-Path $root ($(if ($Test) { "test-build" } else { "dist" }))
$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$archive = Join-Path $root "Versions\Archive\GraveShift-$version.exe"
if (-not $Test -and (Test-Path $archive)) { throw "Version $version is already archived. Raise <Version> in the csproj, then publish again." }

# A running copy from this folder locks the exe; it restores normal screen colours when it exits.
if (-not $Test) { Get-Process GraveShift -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$root*" } | Stop-Process -Force }

Write-Host "Publishing GraveShift $version..." -ForegroundColor Cyan
dotnet publish $proj -c Release -o $out
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
Get-ChildItem $out -File | Where-Object { $_.Name -ne "GraveShift.exe" } | Remove-Item -Force

$exe = Join-Path $out "GraveShift.exe"
if (-not $Test) {
    New-Item -ItemType Directory -Force (Split-Path $archive) | Out-Null
    Copy-Item $exe $archive
    Copy-Item $exe (Join-Path $root "GraveShift.exe")
}
Write-Host "Done: $exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)" -ForegroundColor Green
