<#
  Builds SBG VR and its Thunderstore package: artifacts\lmkHenlo-SBG_VR-<version>.zip

  Requirements: the .NET SDK 8 or later, the game, and a BepInEx 5 folder (by default the
  r2modman Default profile's). Nothing is downloaded: the build compiles against the game's
  and BepInEx's own DLLs.

  Examples:
    ./build.ps1
    ./build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Super Battle Golf"
    ./build.ps1 -BepInExDir "C:\path\to\BepInEx"
    ./build.ps1 -Configuration Debug     # build without making the zip
#>
param(
    [string]$GameDir = "",
    [string]$BepInExDir = "",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK isn't installed or isn't on PATH. Install the .NET 8 SDK (or later) and try again."
}

# A trailing backslash would escape the closing quote when the path is passed to dotnet.
$arguments = @('build', (Join-Path $PSScriptRoot 'SBG_VR.proj'), '-c', $Configuration, '-nologo')
if ($GameDir) { $arguments += "-p:GameDir=$($GameDir.TrimEnd('\'))" }
if ($BepInExDir) { $arguments += "-p:BepInExDir=$($BepInExDir.TrimEnd('\'))" }

& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
