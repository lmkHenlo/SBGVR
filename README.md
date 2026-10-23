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

Version 0.1.0 was a baseline: UUVR with only the changes needed to build and package it for this game, plus diagnostic logging. Version 0.2.0 adds the first Super Battle Golf fixes and VR settings, based on testing 0.1.0 in a headset: the main menu, fades, HUD markers and comfort options.

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

Start the VR runtime, then the game. UUVR turns VR on at startup; **F3** turns it off and on. **F4** recentres the view (sets the direction you're facing as straight ahead).

Settings live in `BepInEx\config`. In r2modman, open them with **Config editor**.

| File | Setting | Default | What it does |
| --- | --- | --- | --- |
| `raicuparta.uuvr-modern.cfg` | `General` → `Preferred VR APi` | `OpenXr` | `OpenXr` or `OpenVr`. |
| `raicuparta.uuvr-modern.cfg` | `Camera` → `Camera Tracking Mode` | `RelativeTransform` | How the game camera follows the headset: `Absolute`, `RelativeMatrix`, `RelativeTransform` or `Child`. |
| `raicuparta.uuvr-modern.cfg` | `UI` → `UI Patch Mode` | `Mirror` | How the 2D interface reaches the floating screen: `None`, `Mirror` or `CanvasRedirect`. |
| `raicuparta.uuvr-modern.cfg` | `UI` → `Preferred UI Plane Render Mode` | `InWorld` | `InWorld` (scenery can cover it) or `OverlayCamera` (always on top). |
| `com.sbgvr.cfg` | `VR Comfort` → `ScreenShake` | `0` | Camera shake in VR as a share of the game's (0–1). |
| `com.sbgvr.cfg` | `VR Comfort` → `ImpactFrames` | `false` | Show impact frames (explosion flashes) in VR. |
| `com.sbgvr.cfg` | `VR Comfort` → `FlashFrames` | `false` | Show flash frames in VR. |
| `com.sbgvr.cfg` | `VR Comfort` → `CameraTransitions` | `Fast` | Camera moves between views: `Game`, `Fast` (about a third of the time) or `Instant` (a cut). |
| `com.sbgvr.cfg` | `VR Comfort` → `MenuBackground` | `Still` | Main menu background scenes: `Moving` (the game's pans) or `Still`. |
| `com.sbgvr.cfg` | `VR Comfort` → `FullViewFades` | `true` | Fade the whole view, not only the floating screen, when the game fades. |
| `com.sbgvr.cfg` | `VR Comfort` → `RecenterKey` | `F4` | Key that recentres the view. `None` = no key. |
| `com.sbgvr.cfg` | `VR HUD` → `WorldSpaceMarkers` | `true` | Draw icons, name tags and the reticle in the world at their target, at the right depth for each eye. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkersOnTop` | `true` | Draw world-space markers over scenery, like the game does. Menus on the floating screen still cover them. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkerScale` | `1` | Size of world-space markers (0.25–4); 1 = their size on the floating screen. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkerViewAngle` | `70` | Degrees from straight ahead within which icons stay in the world; further round they become edge arrows (30–120). |
| `com.sbgvr.cfg` | `VR HUD` → `WorldSpaceReticles` | `true` | Draw weapon reticles in the world at the point you're aiming at, instead of on the floating screen where they look doubled. |
| `com.sbgvr.cfg` | `VR HUD` → `PlayersCoverMarkers` | `true` | With `MarkersOnTop`, player characters in front of a marker cover it, while scenery still doesn't. A name tag or teammate icon isn't covered by its own player. |
| `com.sbgvr.cfg` | `VR Graphics` → `AmbientOcclusion` | `true` | The game's ambient occlusion in VR. It costs more than on a flat screen, since each eye is drawn at the headset's resolution. |
| `com.sbgvr.cfg` | `Diagnostics` → `Enabled` | `true` | Log VR, camera and renderer details to `LogOutput.log`. |
| `com.sbgvr.cfg` | `Diagnostics` → `PerformanceLogInterval` | `30` | Seconds between frame rate lines while VR runs. `0` = never. |

SBG VR's settings only change the game while VR is running; the game's own settings and save file are never changed. Change them with the game closed: BepInEx reads the file when the game starts.

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
| Pick the UI's camera again when the current one stops rendering | The main menu switches between seven cameras of equal depth; the floating screen kept following one that was switched off, and the menu disappeared. |
| Skip UUVR's self-rebuild while the game quits | It recreated itself twice during shutdown. |
| Draw the VR UI on the last sorting layer at the highest sorting order (`UiSorting`) | Effects with a higher sorting order were drawn over the floating screen, whatever its render queue. |
| Add `VrCameraOffset.RecenterRotation` | Lets SBG VR recentre the view. Identity (the default) keeps upstream behaviour. |

**SBG VR** (`src/SbgVr`, plugin ID `com.sbgvr`) depends on UUVR. Each feature checks `VrState.IsRunning`, so nothing changes on a flat screen, and each feature's Harmony patches are applied on their own: one that fails to patch (after a game update, say) is removed and logged, and the rest keep working.

| Feature | Files | How |
| --- | --- | --- |
| Screen shake | `Comfort/ScreenShakePatches.cs` | Transpilers on `CameraModuleController.UpdateShake` and `CanShakeAtAll` multiply the game's shake setting by `ScreenShake`. |
| Screen effects | `Comfort/ScreenEffectPatches.cs` | Prefixes skip `ImpactFrameController.PlayImpactFrame`, `FlashFrameFeature.AddRenderPasses`, `CRTFeature.AddRenderPasses` and `BlurFeature.AddRenderPasses` (the pause menu's blur, which in VR blurred the floating screen too). |
| Camera transitions | `Comfort/CameraTransitionPatches.cs` | A prefix on `CameraModuleController.TransitionToInternal` shortens or zeroes the duration. |
| Ambient occlusion | `Rendering/` | HTrace AO asks the `Camera` for its size, view and field of view, which in VR are still the game window's, so part of each eye drew nearby surfaces black. Before each eye is drawn, `EyeView` records it, and transpilers redirect HTrace AO's camera reads, history keys and GTAO's depth-to-view factors to that eye. If the patches don't apply, or `AmbientOcclusion` is off, the feature is skipped in VR. |
| Menu background | `Comfort/MenuBackground.cs` | Holds each `MainMenuScenario` camera halfway between its two pan points. |
| Full-view fades | `Fades/` | Hides `ColorOverlay`'s image and the main menu's overlay, and draws their colour on spheres around the camera, over and behind the floating screen. |
| Recentre | `Comfort/Recenter.cs` | Cancels the headset's yaw through `VrCameraOffset.RecenterRotation`. |
| World-space reticles | `Hud/WorldReticles.cs` | Each frame, casts a ray along the game camera's forward (the direction items aim), past the local player, and moves `ReticleManager`'s reticles into the world at the hit point (50 m if it hits nothing, kept between 2 m and 150 m), gliding between distances. They use `WorldMarker` with a holder the size of their HUD canvas and keep their floating-screen size. |
| World-space markers | `Hud/` | After `WorldspaceIconUi`, `NameTagUi`, `TargetReticleUi`, `LockOnTargetUi` (the orange lock-on reticle), `AirhornTargetIndicator`, `JumboBurgerGiantFormTimerUi` and `TextPopupUi` lay out a marker, `WorldMarker` moves its UI objects under a world-space canvas at the target, scaled to their size on the floating screen. They return to the HUD when off screen. Each frame, `MarkerOcclusion` finds characters that are nearer than a marker they overlap in view (`PlayerOverlap`) and gives each a stencil bit. `PlayerOccluder` sets that bit wherever the character is visible, by drawing its meshes again with a UI material that draws no colour, depth tested against the character itself (a twin skinned mesh, or `Graphics.RenderMesh`). Each marker then skips pixels with the bits of the characters in front of it. The characters are drawn once, as usual. Each marker part gets this through the material its canvas renderer actually draws with, set after masks and the game have set theirs. |

It also logs diagnostics, each kind once and again when it changes: the game, graphics API and URP renderer features; installed plugins; UUVR's settings; the VR runtime's state; each eye's field of view; every active camera with its URP settings; and the frame rate while VR runs. Lines start with `[diag]`.

## Known issues

- **0.2.x is being tested in a headset.** It fixes what testing 0.1.0 found; the test plan below covers the new features.
- **The game's field of view doesn't apply in VR.** The headset decides the field of view, so the game's FOV slider has no effect yet, and the railgun's zoomed aim shows its reticle without zooming.
- **Game folder files.** The patcher writes into the game's `Super Battle Golf_Data` folder, which mod managers can't clean up. Uninstalling leaves `Plugins\openvr_api.dll`, `Plugins\openxr_loader.dll`, `Plugins\UnityOpenXR.dll`, `Plugins\XRSDKOpenVR.dll`, the `UnitySubsystems` folder and `StreamingAssets\SteamVR`. They aren't used without the mod and can be deleted.
- **The SteamVR menu opens on launch or when a match starts.** Steam shows Super Battle Golf as a flat game on SteamVR's theatre screen before the mod starts VR, and SteamVR docks that screen into its dashboard when the game becomes a VR app or stalls while loading. There's no API for the mod to close the dashboard. Turn the theatre off: in Steam, right-click Super Battle Golf → Properties → General, untick **Use Desktop Game Theatre while SteamVR is active**; or, for every game, SteamVR Settings → Dashboard → **Present Non-VR Applications on Theater Screen Upon Launch** off.
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

1. Start the game from r2modman. In `LogOutput.log`, UUVR's patcher reports copying its files, `SBG VR 0.2.13 loaded` appears, and every SBG VR feature line starts with `[ok]`.
2. Main menu: the game shows in the headset and follows your head; the menu stays readable while the background scenes change; each scene holds still; the fade between scenes darkens the whole view behind the menu; the mouse cursor is visible and clicks land where it points.
3. Settings, lobby and match setup menus work, and item effects (lightning, for one) don't show through them. Joining or leaving a match fades the whole view.
4. A hole: teeing off, aiming, the swing power bar, the ball flight camera, the hole overview, spectating and the golf cart. Camera moves between views are quick.
5. Your ball's and the hole's icons sit on their targets at every distance, including close up, without doubling. Name tags and the interaction reticle too. Icons for targets behind you show as arrows at the edge of the floating screen. Opening a menu (Match Setup, say) covers the markers behind it, including interaction prompts such as Set Up Match. A player character, including yours, covers the parts of markers behind it, and markers in front of a character stay on top of it; hills and trees still don't hide markers. Name tags aren't covered by their own players. A shield or emblem on a character stays steady while markers pass behind it.
6. Weapon reticles (dueling pistol, elephant gun, rocket launcher, freeze bomb, flash camera, thunderstorm, railgun) sit on what you're aiming at, single and sharp, not doubled; sweeping the aim from near to far moves them smoothly in depth. The orange lock-on reticle (full-power swings, rocket launcher) sits on its target, single, as do airhorn target indicators, the giant-form timer and pop-up text. The railgun's zoomed reticle doesn't zoom the view in VR.
7. No black areas over nearby scenery or your character when you look around or move. Ambient occlusion (soft shading where surfaces meet, under your character and in corners) looks the same in both eyes and doesn't smear or flicker when you turn your head. The log says `Ambient occlusion: draws for each eye in VR` and `drawn for each eye at 2528x2704` (your headset's eye size). With `AmbientOcclusion` off, the frame rate goes up.
8. Explosions near you don't flash the view or shake the camera. The pause menu and the settings opened from it are sharp, not blurred.
9. F4 recentres the view; F3 turns VR off and on without errors, and the game looks normal on the monitor with VR off.
10. The frame rate lines in the log are close to the headset's refresh rate.

Send `LogOutput.log` from the session with notes on each step.

## Files

```
SBG_VR/
├── Directory.Build.props     game and BepInEx paths, shared references, version
├── NuGet.config              no package sources: the build is offline
├── SBG_VR.proj               builds everything and makes the Thunderstore zip
├── build.ps1                 Windows build wrapper
├── src/SbgVr/                the SBG VR plugin (com.sbgvr): Comfort/, Fades/, Hud/, Diagnostics/
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
