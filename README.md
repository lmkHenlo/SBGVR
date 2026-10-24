# SBGVR

[![license](https://img.shields.io/badge/license-GPL--3.0--or--later-blue.svg?style=flat-square)](LICENSE)
[![standard-readme compliant](https://img.shields.io/badge/readme%20style-standard-brightgreen.svg?style=flat-square)](https://github.com/RichardLitt/standard-readme)

Play Super Battle Golf in a PC VR headset: Raicuparta's UUVR with Super Battle Golf fixes. Client-side.

SBGVR packages Raicuparta's [Universal Unity VR](https://github.com/Raicuparta/uuvr) (UUVR) as a Thunderstore mod for Super Battle Golf. It adds a plugin of its own for fixes specific to the game. UUVR starts a VR runtime inside the game, makes the game's camera follow the headset and moves the 2D interface onto a floating screen. The game is rendered in stereo with head tracking; motion controllers aren't used. Only the VR player's own view changes. Nothing is sent over the network, so a VR player can join any lobby.

The repository, its source archive and the Thunderstore package are all named `SBGVR`. Locally, the repository is kept in a folder named `source`, inside `sbgvr_master\master` next to its release files, the same layout as the maintainer's other mods. Versions up to 0.2.14 were named SBG VR (package `SBG_VR`).

SBGVR is experimental. No version has been published to Thunderstore yet: every version so far is a test build, installed from a local zip. [CHANGELOG.md](CHANGELOG.md) lists what each version changed.

## Table of Contents

- [Background](#background)
- [Install](#install)
  - [Dependencies](#dependencies)
  - [With r2modman](#with-r2modman)
  - [By hand](#by-hand)
  - [Updating](#updating)
  - [Uninstalling](#uninstalling)
- [Usage](#usage)
  - [Settings](#settings)
  - [If the headset stays black](#if-the-headset-stays-black)
- [Known issues](#known-issues)
- [How it works](#how-it-works)
- [Building from source](#building-from-source)
- [Test plan](#test-plan)
- [Files](#files)
- [API](#api)
- [Maintainers](#maintainers)
- [Thanks](#thanks)
- [Contributing](#contributing)
- [License](#license)

## Background

Super Battle Golf is a Unity 6 game (6000.3) on the Mono scripting backend with the Universal Render Pipeline (URP). It has no VR support. UUVR adds VR to Unity games without their source. For Unity 2020 and later, it ships Unity's XR plugin code and native VR libraries, then builds an XR loader at runtime and starts it.

UUVR is normally installed with [Rai Pal](https://pal.raicuparta.com), which works per game but isn't a Thunderstore mod manager. SBGVR does three things:

- It puts UUVR into a Thunderstore package that r2modman can install next to other Super Battle Golf mods.
- It builds UUVR against the game's own assemblies.
- It adds a plugin (`com.sbgvr`) for what the game needs in VR: comfort options, HUD markers and weapon reticles drawn in the world at the right depth, full-view fades, and ambient occlusion drawn for each eye.

### See Also

- [UUVR](https://github.com/Raicuparta/uuvr), the VR mod this package is built on
- [BepInEx](https://github.com/BepInEx/BepInEx), the mod loader
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), the licences of the VR runtime libraries in the package

## Install

### Dependencies

- [BepInEx 5](https://github.com/BepInEx/BepInEx) for Windows x64. On Thunderstore this is `BepInEx-BepInExPack-5.4.2305`, which the package depends on.
- A PC VR runtime: SteamVR, or another OpenXR runtime. With SteamVR (including Steam Link), set SteamVR as the OpenXR runtime in SteamVR's settings. It's on the OpenXR page or the Developer page, depending on the SteamVR version.
- **Client-side only.** Only the VR player needs the mod. The host and other players need nothing.

Both ways below install a zip made by [Building from source](#building-from-source): `artifacts\lmkHenlo-SBGVR-<version>.zip`.

### With r2modman

Make sure the profile has BepInExPack, then use r2modman's **Import local mod** with the zip.

### By hand

Install BepInEx 5 into the game folder and start the game once. Then run this in PowerShell, in the repository folder. Change `$game` if the game is installed somewhere else.

```powershell
$game = "C:\Program Files (x86)\Steam\steamapps\common\Super Battle Golf"
$zip = Get-ChildItem .\artifacts\lmkHenlo-SBGVR-*.zip | Sort-Object LastWriteTime | Select-Object -Last 1
$package = Join-Path $env:TEMP "SBGVR"
Expand-Archive $zip.FullName -DestinationPath $package -Force
New-Item -ItemType Directory -Force "$game\BepInEx\plugins\SBGVR", "$game\BepInEx\patchers\SBGVR" | Out-Null
Copy-Item "$package\plugins\*" "$game\BepInEx\plugins\SBGVR" -Recurse -Force
Copy-Item "$package\patchers\*" "$game\BepInEx\patchers\SBGVR" -Recurse -Force
```

This gives:

```text
Super Battle Golf/
└── BepInEx/
    ├── patchers/
    │   └── SBGVR/
    │       ├── Uuvr.Patcher.dll
    │       └── CopyToGame/...
    └── plugins/
        └── SBGVR/
            ├── SBGVR.dll
            ├── Uuvr.dll
            ├── Uuvr.XR.Management.dll
            ├── Uuvr.XR.OpenVR.dll
            ├── Uuvr.XR.OpenXR.dll
            └── Assets/cursor.bmp
```

### Updating

Install the new zip the same way: import it in r2modman, or run the commands above again. Settings are kept, since they're in `BepInEx\config`, outside the mod's folders. [CHANGELOG.md](CHANGELOG.md) lists what changed, including settings that were added or removed.

Coming from 0.2.14 or earlier, when the package was named `SBG_VR`: remove that mod first (in r2modman, or delete `BepInEx\plugins\SBG_VR` and `BepInEx\patchers\SBG_VR`), so the game doesn't end up with two copies. Your settings carry over, since the settings file is still `com.sbgvr.cfg`.

### Uninstalling

Remove the mod in r2modman, or delete `BepInEx\plugins\SBGVR` and `BepInEx\patchers\SBGVR`.

Each time the game starts, UUVR's patcher copies VR runtime files into the game's `Super Battle Golf_Data` folder, which mod managers can't clean up. Uninstalling leaves them behind. They aren't used without the mod and can be deleted:

- `Plugins\openvr_api.dll`
- `Plugins\openxr_loader.dll`
- `Plugins\UnityOpenXR.dll`
- `Plugins\XRSDKOpenVR.dll`
- the `UnitySubsystems` folder
- `StreamingAssets\SteamVR`

## Usage

Start the VR runtime, then the game. UUVR turns VR on at startup.

| Key | Action |
| --- | --- |
| F3 | Turn VR off and on. |
| F4 | Recentre the view: the direction you're facing becomes straight ahead. Set by `RecenterKey`. |

### Settings

Settings are in `BepInEx\config`. In r2modman, open them with **Config editor**. Change them with the game closed, since BepInEx reads the files when the game starts. For example, these lines in `com.sbgvr.cfg` keep a little camera shake and turn ambient occlusion off for a higher frame rate:

```ini
[VR Comfort]
ScreenShake = 0.25

[VR Graphics]
AmbientOcclusion = false
```

SBGVR's settings only change the game while VR is running. The game's own settings and save file are never changed.

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
| `com.sbgvr.cfg` | `VR HUD` → `WorldSpaceMarkers` | `true` | Draw icons, name tags and the interaction reticle in the world at their target, at the right depth for each eye. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkersOnTop` | `true` | Draw world-space markers over scenery, like the game does. Menus on the floating screen still cover them. Weapon reticles are always on top. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkerScale` | `1` | Size of world-space markers (0.25–4); 1 = their size on the floating screen. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkerViewAngle` | `70` | Degrees from straight ahead within which icons stay in the world; further round they become edge arrows (30–120). |
| `com.sbgvr.cfg` | `VR HUD` → `WorldSpaceReticles` | `true` | Draw weapon reticles in the world at the point you're aiming at, always on top of scenery and characters, instead of doubled on the floating screen. |
| `com.sbgvr.cfg` | `VR HUD` → `PlayersCoverMarkers` | `true` | With `MarkersOnTop`, player characters in front of a marker cover it, while scenery still doesn't. A name tag or teammate icon isn't covered by its own player, and weapon reticles aren't covered at all. |
| `com.sbgvr.cfg` | `VR Graphics` → `AmbientOcclusion` | `true` | The game's ambient occlusion in VR. It costs more than on a flat screen, since each eye is drawn at the headset's resolution. |
| `com.sbgvr.cfg` | `Diagnostics` → `Enabled` | `true` | Log VR, camera and renderer details to `LogOutput.log`. |
| `com.sbgvr.cfg` | `Diagnostics` → `PerformanceLogInterval` | `30` | Seconds between frame rate lines while VR runs (0–600). `0` = never. |

`raicuparta.uuvr-modern.cfg` also holds UUVR's other settings, such as the floating screen's position and size, each with a description.

### If the headset stays black

1. Check that SteamVR (or your runtime) is set as the OpenXR runtime.
2. Set `General` → `Preferred VR APi` to `OpenVr`.
3. Set `Camera` → `Camera Tracking Mode` to `RelativeMatrix` or `Absolute`.

## Known issues

- **The game's field of view doesn't apply in VR.** The headset decides the field of view, so the game's FOV slider has no effect yet, and the railgun's zoomed aim shows its reticle without zooming.
- **The SteamVR menu opens on launch or when a match starts.** Steam shows Super Battle Golf as a flat game on SteamVR's theatre screen before the mod starts VR. SteamVR then docks that screen into its dashboard when the game becomes a VR app or stalls while loading, and there's no API for the mod to close the dashboard. Turn the theatre off in one of two ways:
  - For this game: in Steam, right-click Super Battle Golf → Properties → General, and untick **Use Desktop Game Theatre while SteamVR is active**.
  - For every game: in SteamVR Settings → Dashboard, turn off **Present Non-VR Applications on Theater Screen Upon Launch**.
- **MaxFOVSlider** isn't handled yet. Disable it while using VR.
- **No motion controllers.** UUVR doesn't support them.

## How it works

**UUVR** (`uuvr/`, vendored at upstream tag v0.4.0) does the VR work:

1. Its patcher (`Uuvr.Patcher`) runs before the game starts. It copies the native VR libraries (`openxr_loader`, `UnityOpenXR`, `openvr_api`, `XRSDKOpenVR`), their Unity subsystem manifests and SteamVR input files into `Super Battle Golf_Data`.
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
| Pick the UI's camera again when the current one stops rendering | The main menu switches between seven cameras of equal depth. The floating screen kept following one that was switched off, and the menu disappeared. |
| Skip UUVR's self-rebuild while the game quits | It recreated itself twice during shutdown. |
| Draw the VR UI on the last sorting layer at the highest sorting order (`UiSorting`) | Effects with a higher sorting order were drawn over the floating screen, whatever its render queue. |
| Add `VrCameraOffset.RecenterRotation` | Lets SBGVR recentre the view. Identity (the default) keeps upstream behaviour. |

**SBGVR** (`src/SBGVR`, plugin ID `com.sbgvr`) depends on UUVR. Each feature checks `VrState.IsRunning`, so nothing changes on a flat screen. Each feature's Harmony patches are applied on their own: one that fails to patch (after a game update, say) is removed and logged, and the rest keep working.

| Feature | Files | How |
| --- | --- | --- |
| Screen shake | `Comfort/ScreenShakePatches.cs` | Transpilers on `CameraModuleController.UpdateShake` and `CanShakeAtAll` multiply the game's shake setting by `ScreenShake`. |
| Screen effects | `Comfort/ScreenEffectPatches.cs` | Prefixes skip `ImpactFrameController.PlayImpactFrame`, `FlashFrameFeature.AddRenderPasses`, `CRTFeature.AddRenderPasses` and `BlurFeature.AddRenderPasses`. The last is the pause menu's blur, which in VR blurred the floating screen too. |
| Camera transitions | `Comfort/CameraTransitionPatches.cs` | A prefix on `CameraModuleController.TransitionToInternal` shortens or zeroes the duration. |
| Ambient occlusion | `Rendering/` | HTrace AO asks the `Camera` for its size, view and field of view, which in VR are still the game window's, so part of each eye drew nearby surfaces black. Before each eye is drawn, `EyeView` records it. Transpilers then redirect HTrace AO's camera reads, history keys and GTAO's depth-to-view factors to that eye. If the patches don't apply, or `AmbientOcclusion` is off, the feature is skipped in VR. |
| Menu background | `Comfort/MenuBackground.cs` | Holds each `MainMenuScenario` camera halfway between its two pan points. |
| Full-view fades | `Fades/` | Hides `ColorOverlay`'s image and the main menu's overlay, and draws their colour on spheres around the camera, over and behind the floating screen. |
| Recentre | `Comfort/Recenter.cs` | Cancels the headset's yaw through `VrCameraOffset.RecenterRotation`. |
| World-space markers | `Hud/` | After the game lays out a marker (`WorldspaceIconUi`, `NameTagUi`, `TargetReticleUi`, `LockOnTargetUi` (the orange lock-on reticle), `AirhornTargetIndicator`, `JumboBurgerGiantFormTimerUi` or `TextPopupUi`), `WorldMarker` moves its UI objects under a world-space canvas at the target. It's scaled to its size on the floating screen. Markers return to the HUD when they go off screen or the game stops laying them out. |
| Players covering markers | `Hud/MarkerOcclusion.cs`, `PlayerOverlap.cs`, `PlayerOccluder.cs` | Each frame, `MarkerOcclusion` finds characters that are nearer than a marker they overlap in view and gives each a stencil bit. `PlayerOccluder` sets that bit wherever the character is visible. It draws the character's meshes again with a UI material that draws no colour, depth tested against the character itself (a twin skinned mesh, or `Graphics.RenderMesh`). Each marker's drawn materials then skip pixels with the bits of the characters in front of it. The characters are drawn once, as usual. |
| World-space reticles | `Hud/WorldReticles.cs` | Each frame, casts a ray along the game camera's forward (the direction items aim), past the local player. It moves `ReticleManager`'s reticles into the world at the hit point: 50 m away if the ray hits nothing, and always between 2 m and 150 m. They glide between distances, keep their floating-screen size, and sit in a holder the size of their HUD canvas. Reticles and the lock-on reticle always ignore depth and are never covered by characters. They sort on the last sorting layer, one order below the floating screen and fades. |

SBGVR also logs diagnostics, each kind once and again when it changes: the game, graphics API and URP renderer features; installed plugins; UUVR's settings; the VR runtime's state; each eye's field of view; every active camera with its URP settings; and the frame rate while VR runs. Lines start with `[diag]`.

## Building from source

Requirements: the .NET SDK 8 or later, Super Battle Golf, and a BepInEx 5 folder. The build compiles against the game's own DLLs and BepInEx's, so it downloads nothing.

```powershell
./build.ps1
# or, with other paths:
./build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Super Battle Golf" -BepInExDir "C:\path\to\BepInEx"
# build without making the zip:
./build.ps1 -Configuration Debug
```

`build.ps1` runs `dotnet build SBGVR.proj`. That builds UUVR (its `modern-mono` configuration) and SBGVR, then, in Release, writes the Thunderstore package to `artifacts\lmkHenlo-SBGVR-<version>.zip`. The path defaults are in `Directory.Build.props`: the Steam install folder, and r2modman's Default profile for BepInEx. `dotnet build SBGVR.proj -t:Clean` deletes `artifacts\`.

The version is in three places, and the build fails if they differ:

- `SBGVRVersion` in `Directory.Build.props`
- `Plugin.Version` in `src/SBGVR/Plugin.cs`
- `version_number` in `thunderstore/manifest.json`

## Test plan

For each build, with SteamVR running and SteamVR set as the OpenXR runtime:

1. Start the game from r2modman. In `LogOutput.log`, UUVR's patcher reports copying its files, `SBGVR <version> loaded` appears, and every SBGVR feature line starts with `[ok]`.
2. Main menu:
   - The game shows in the headset and follows your head.
   - The menu stays readable while the background scenes change, and each scene holds still.
   - The fade between scenes darkens the whole view behind the menu.
   - The mouse cursor is visible, and clicks land where it points.
3. Settings, lobby and match setup menus work, and item effects don't show through them. Joining or leaving a match fades the whole view.
4. Play a hole: teeing off, aiming, the swing power bar, the ball flight camera, the hole overview, spectating and the golf cart. Camera moves between views are quick.
5. Markers:
   - Your ball's and the hole's icons sit on their targets at every distance, including close up, without doubling. So do name tags and the interaction reticle.
   - Icons for targets behind you show as arrows at the edge of the floating screen.
   - Opening a menu (Match Setup, say) covers the markers behind it, including interaction prompts such as Set Up Match.
   - A player character, including yours, covers the parts of markers behind it, and markers in front of a character stay on top of it. Hills and trees still don't hide markers.
   - Name tags aren't covered by their own players.
   - A shield or emblem on a character stays steady while markers pass behind it.
6. Weapon reticles:
   - Every weapon reticle sits on what you're aiming at, single and sharp, not doubled: dueling pistol, elephant gun, rocket launcher, freeze bomb, flash camera, thunderstorm and railgun.
   - Sweeping the aim from near to far moves them smoothly in depth.
   - No scenery, character (including yours) or effect ever covers any part of them; menus and fades still do.
   - The orange lock-on reticle (full-power swings, rocket launcher) sits on its target, single. So do airhorn target indicators, the giant-form timer and pop-up text.
   - The railgun's zoomed reticle doesn't zoom the view in VR.
7. Ambient occlusion:
   - No black areas cover nearby scenery or your character when you look around or move.
   - Ambient occlusion (soft shading where surfaces meet, under your character and in corners) looks the same in both eyes, and doesn't smear or flicker when you turn your head.
   - The log says `Ambient occlusion: draws for each eye in VR` and `drawn for each eye at 2528x2704` (your headset's eye size).
   - With `AmbientOcclusion` off, the frame rate goes up.
8. Explosions near you don't flash the view or shake the camera. The pause menu and the settings opened from it are sharp, not blurred.
9. F4 recentres the view. F3 turns VR off and on without errors, and the game looks normal on the monitor with VR off.
10. The frame rate lines in the log are close to the headset's refresh rate.

Send `LogOutput.log` from the session with notes on each step.

## Files

```text
SBGVR/
├── CHANGELOG.md              changes in each version
├── Directory.Build.props     game and BepInEx paths, shared references, version
├── NuGet.config              no package sources: the build is offline
├── SBGVR.proj                builds everything and makes the Thunderstore zip
├── build.ps1                 Windows build wrapper
├── src/SBGVR/                the SBGVR plugin (com.sbgvr): Comfort/, Diagnostics/, Fades/, Hud/, Rendering/
├── thunderstore/             manifest.json, icon.png and the package README
├── uuvr/                     UUVR v0.4.0 with the changes listed above
├── LICENSE                   GPL-3.0 (UUVR's licence text)
└── THIRD_PARTY_NOTICES.md    licences of the bundled VR runtime libraries
```

## API

SBGVR has no API for other mods. Its BepInEx plugin ID is `com.sbgvr`, and it hard-depends on UUVR's `raicuparta.uuvr-modern`.

## Maintainers

[@lmkHenlo](https://github.com/lmkHenlo)

## Thanks

- [Raicuparta](https://github.com/Raicuparta), for [Universal Unity VR](https://github.com/Raicuparta/uuvr), which does the VR work this package is built on.
- The [BepInEx](https://github.com/BepInEx/BepInEx) team, for the mod loader.

## Contributing

Send questions and bug reports to [ps.meiisbei@gmail.com](mailto:ps.meiisbei@gmail.com). For a bug, include `BepInEx\LogOutput.log` from a session where it happened. SBGVR writes the VR and camera details needed to track it down.

Pull requests are accepted. The repository isn't public yet, so email to ask about contributing until it is. When you make changes:

- Write commit messages in the [Conventional Commits](https://www.conventionalcommits.org/) style.
- Put changes to UUVR's files (`uuvr/`) in their own commits, so the differences from upstream stay easy to review.
- Add your change to [CHANGELOG.md](CHANGELOG.md) under Unreleased, following [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
- Change the version in all three places listed in [Building from source](#building-from-source).

## License

[GPL-3.0-or-later](LICENSE) © 2026 lmkHenlo. UUVR is GPL-3.0-or-later © 2024 Raicuparta. The bundled VR runtime libraries keep their own licences; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
