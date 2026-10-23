# SBG VR

[![license](https://img.shields.io/badge/license-GPL--3.0--or--later-blue.svg?style=flat-square)](LICENSE)
[![standard-readme compliant](https://img.shields.io/badge/readme%20style-standard-brightgreen.svg?style=flat-square)](https://github.com/RichardLitt/standard-readme)

Play Super Battle Golf in a PC VR headset. Client-side: only you need it.

SBG VR packages Raicuparta's [Universal Unity VR](https://github.com/Raicuparta/uuvr) (UUVR) as a Thunderstore mod for Super Battle Golf, with a plugin of its own for fixes specific to the game. UUVR starts a VR runtime inside the game, makes the game's camera follow the headset and moves the 2D interface onto a floating screen. It renders the game in stereo with head tracking; motion controllers aren't used. Only the VR player's own view changes: nothing is sent over the network, so a VR player can join any lobby.

## Table of Contents

- [Background](#background)
- [Install](#install)
- [Usage](#usage)
- [How it works](#how-it-works)
- [Known issues](#known-issues)
- [Building from source](#building-from-source)
- [Test plan](#test-plan)
- [Files](#files)
- [API](#api)
- [Maintainers](#maintainers)
- [Contributing](#contributing)
- [License](#license)

## Background

Super Battle Golf is a Unity 6 game (6000.3) on the Mono scripting backend with the Universal Render Pipeline (URP). It has no VR support. UUVR adds VR to Unity games without their source: for Unity 2020 and later it ships Unity's XR plugin code and native VR libraries, builds an XR loader at runtime and starts it.

UUVR is installed with [Rai Pal](https://pal.raicuparta.com), which works per game but isn't a Thunderstore mod manager. SBG VR puts UUVR into a Thunderstore package that r2modman can install next to other Super Battle Golf mods, builds it against the game's own assemblies, and adds a plugin (`com.sbgvr`) for Super Battle Golf fixes.

Version 0.1.0 is a baseline: UUVR with only the changes needed to build and package it for this game, plus diagnostic logging. Testing it in a headset decides which Super Battle Golf fixes come next.

### See Also

- [UUVR](https://github.com/Raicuparta/uuvr), the VR mod this package is built on
- [BepInEx](https://github.com/BepInEx/BepInEx), the mod loader
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), the licences of the VR runtime libraries in the package

## Install

### Dependencies

- [BepInEx 5](https://github.com/BepInEx/BepInEx) for Windows x64. On Thunderstore this is `BepInEx-BepInExPack-5.4.2305`.
- A PC VR runtime: SteamVR, or another OpenXR runtime. With SteamVR (including Steam Link), set SteamVR as the OpenXR runtime in SteamVR's settings (on the OpenXR or Developer page, depending on the SteamVR version).
- **Client-side only.** Only the VR player needs the mod. The host and other players need nothing.

### With a mod manager

Install `SBG_VR` from Thunderstore with r2modman or another Thunderstore mod manager. To install a local build, use r2modman's **Import local mod** with `artifacts\lmkHenlo-SBG_VR-<version>.zip`.

### By hand

Install BepInEx 5 into the game folder and start the game once. Then copy the package's `plugins` and `patchers` folders into BepInEx:

```
Super Battle Golf/
└── BepInEx/
    ├── patchers/
    │   └── SBG_VR/
    │       ├── Uuvr.Patcher.dll
    │       └── CopyToGame/...
    └── plugins/
        └── SBG_VR/
            ├── SbgVr.dll
            ├── Uuvr.dll
            ├── Uuvr.XR.Management.dll
            ├── Uuvr.XR.OpenVR.dll
            ├── Uuvr.XR.OpenXR.dll
            └── Assets/cursor.bmp
```

## Usage

Start the VR runtime, then the game. UUVR turns VR on at startup; **F3** turns it off and on.

Settings live in `BepInEx\config`. In r2modman, open them with **Config editor**.

| File | Setting | Default | What it does |
| --- | --- | --- | --- |
| `raicuparta.uuvr-modern.cfg` | `General` → `Preferred VR APi` | `OpenXr` | `OpenXr` or `OpenVr`. |
| `raicuparta.uuvr-modern.cfg` | `Camera` → `Camera Tracking Mode` | `RelativeTransform` | How the game camera follows the headset: `Absolute`, `RelativeMatrix`, `RelativeTransform` or `Child`. |
| `raicuparta.uuvr-modern.cfg` | `UI` → `UI Patch Mode` | `Mirror` | How the 2D interface reaches the floating screen: `None`, `Mirror` or `CanvasRedirect`. |
| `raicuparta.uuvr-modern.cfg` | `UI` → `Preferred UI Plane Render Mode` | `InWorld` | `InWorld` (scenery can cover it) or `OverlayCamera` (always on top). |
| `com.sbgvr.cfg` | `Diagnostics` → `Enabled` | `true` | Log VR, camera and renderer details to `LogOutput.log`. |
| `com.sbgvr.cfg` | `Diagnostics` → `PerformanceLogInterval` | `30` | Seconds between frame rate lines while VR runs. `0` = never. |

UUVR's README lists every UUVR setting. If the headset stays black, switch `Preferred VR APi` first, then `Camera Tracking Mode`.

## How it works

**UUVR** (`uuvr/`, vendored at upstream tag v0.4.0) does the VR work:

1. Its patcher (`Uuvr.Patcher`) runs before the game starts and copies the native VR libraries (`openxr_loader`, `UnityOpenXR`, `openvr_api`, `XRSDKOpenVR`), their Unity subsystem manifests and SteamVR input files into `Super Battle Golf_Data`.
2. Its plugin (`Uuvr`) creates Unity XR Management settings at runtime, adds an OpenXR or OpenVR loader and starts it.
3. It adds a `VrCamera` to every camera that renders to the screen and drives it from the headset's rotation.
4. It captures the 2D interface into a texture and shows it on a quad in front of the camera.

Changes made to UUVR in this repository, each in its own commit:

| Change | Why |
| --- | --- |
| Build against the game's `Managed` folder and a BepInEx install, with no NuGet | Matches the game exactly and builds offline. |
| Read `GraphicRegistry.m_Graphics` by reflection | The game's `UnityEngine.UI` keeps the field private. Upstream built against a modified copy. |
| AssetsTools.NET and `classdata.tpk` only in legacy builds | Only the Unity 2019 path edits `globalgamemanagers`. |
| Fix the patcher's clean-up list (`ucrtbased`) and add `OVRPlugin` | The old entry never matched, and both files are no longer shipped. |
| Remove the x86 libraries, `ucrtbased.dll` and `OVRPlugin.dll` | The game is 64-bit, and nothing imports or loads the other two. `ucrtbased.dll` is Microsoft's debug runtime, which isn't redistributable. |
| Remove `uuvr/lib` | The build uses the game's assemblies instead. |

**SBG VR** (`src/SbgVr`, plugin ID `com.sbgvr`) depends on UUVR and, in 0.1.0, only logs diagnostics, each kind once and again when it changes: the game, graphics API and URP renderer features; installed plugins; UUVR's settings; the VR runtime's state; each eye's field of view; every active camera with its URP settings; and the frame rate while VR runs. Lines start with `[diag]`.

## Known issues

- **Untested in a headset.** 0.1.0 is the first build; expect the game's screen effects, markers and HUD to need fixes.
- **Game folder files.** The patcher writes into the game's `Super Battle Golf_Data` folder, which mod managers can't clean up. Uninstalling leaves `Plugins\openvr_api.dll`, `Plugins\openxr_loader.dll`, `Plugins\UnityOpenXR.dll`, `Plugins\XRSDKOpenVR.dll`, the `UnitySubsystems` folder and `StreamingAssets\SteamVR`. They aren't used without the mod and can be deleted.
- **MaxFOVSlider** isn't handled yet. Disable it while using VR.
- **No motion controllers.** UUVR doesn't support them.

## Building from source

Requirements: the .NET SDK 8 or later, Super Battle Golf, and a BepInEx 5 folder. The build compiles against the game's own DLLs and BepInEx's, so it downloads nothing.

```powershell
./build.ps1
# or, with other paths:
./build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Super Battle Golf" -BepInExDir "C:\path\to\BepInEx"
```

`build.ps1` runs `dotnet build SBG_VR.proj -c Release`, which builds UUVR (its `modern-mono` configuration) and SBG VR, then writes the Thunderstore package to `artifacts\lmkHenlo-SBG_VR-<version>.zip`. Defaults for the paths are in `Directory.Build.props`: the Steam install folder, and r2modman's Default profile for BepInEx.

The version is in three places, and the build fails if they differ: `SbgVrVersion` in `Directory.Build.props`, `Plugin.Version` in `src/SbgVr/Plugin.cs`, and `version_number` in `thunderstore/manifest.json`.

## Test plan

For each build, with SteamVR running and SteamVR set as the OpenXR runtime:

1. Start the game from r2modman. In `LogOutput.log`, UUVR's patcher reports copying its files, and `SBG VR 0.1.0 loaded` appears.
2. Main menu: the game shows in the headset and follows your head; the menu is readable on the floating screen; the mouse cursor is visible and clicks land where it points.
3. Settings, lobby and match setup menus work.
4. A hole: teeing off, aiming, the swing power bar, the ball flight camera, the hole overview, spectating and the golf cart.
5. Name tags, icons and the lock-on reticle sit over the right players.
6. F3 turns VR off and on without errors.
7. The frame rate lines in the log are close to the headset's refresh rate.

Send `LogOutput.log` from the session with notes on each step.

## Files

```
SBG_VR/
├── Directory.Build.props     game and BepInEx paths, shared references, version
├── NuGet.config              no package sources: the build is offline
├── SBG_VR.proj               builds everything and makes the Thunderstore zip
├── build.ps1                 Windows build wrapper
├── src/SbgVr/                the SBG VR plugin (com.sbgvr)
├── thunderstore/             manifest.json, icon.png and the package README
├── uuvr/                     UUVR v0.4.0 with the changes listed above
├── LICENSE                   GPL-3.0 (UUVR's licence text)
└── THIRD_PARTY_NOTICES.md    licences of the bundled VR runtime libraries
```

## API

SBG VR has no API for other mods. Its BepInEx plugin ID is `com.sbgvr`, and it hard-depends on UUVR's `raicuparta.uuvr-modern`.

## Maintainers

[@lmkHenlo](https://github.com/lmkHenlo)

## Contributing

Commits follow [Conventional Commits](https://www.conventionalcommits.org/). Changes to UUVR's files go in their own commits, so the differences from upstream stay easy to review. There's no changelog until the first release; it will follow [Keep a Changelog](https://keepachangelog.com/).

## License

[GPL-3.0-or-later](LICENSE) © 2026 lmkHenlo. UUVR is GPL-3.0-or-later © 2024 Raicuparta. The bundled VR runtime libraries keep their own licences; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
