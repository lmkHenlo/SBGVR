# SBGVR

[![license](https://img.shields.io/badge/license-GPL--3.0--or--later-blue.svg?style=flat-square)](LICENSE)
[![standard-readme compliant](https://img.shields.io/badge/readme%20style-standard-brightgreen.svg?style=flat-square)](https://github.com/RichardLitt/standard-readme)

Play Super Battle Golf in a PC VR headset: Raicuparta's UUVR with Super Battle Golf fixes. Client-side.

SBGVR packages Raicuparta's [Universal Unity VR](https://github.com/Raicuparta/uuvr) (UUVR) as a mod for Super Battle Golf, in the Thunderstore package format that r2modman installs. It adds a plugin of its own for fixes specific to the game. UUVR starts a VR runtime inside the game, makes the game's camera follow the headset and moves the 2D interface onto a floating screen. The game is rendered in stereo with head tracking, and VR controllers can optionally act as a gamepad and aim weapons. Only the VR player's own view changes. Nothing is sent over the network, so a VR player can join any lobby.

The repository and the package are both named `SBGVR`. Versions up to 0.2.14 were named SBG VR (package `SBG_VR`).

SBGVR 1.0.0 is the first public release. Players have tested it on Steam Frame, Valve Index and Meta Quest 3. It's published on [GitHub Releases](https://github.com/lmkHenlo/SBGVR/releases). [CHANGELOG.md](CHANGELOG.md) lists what each version changed, including the unpublished test builds before 1.0.0.

## Table of Contents

- [Background](#background)
- [Install](#install)
  - [Dependencies](#dependencies)
  - [With r2modman](#with-r2modman)
  - [By hand](#by-hand)
  - [Updating](#updating)
  - [Uninstalling](#uninstalling)
- [Usage](#usage)
  - [Motion controls](#motion-controls)
  - [First person](#first-person)
  - [Controller layout in the menus](#controller-layout-in-the-menus)
  - [Settings](#settings)
  - [If the headset stays black](#if-the-headset-stays-black)
- [Known issues](#known-issues)
- [How it works](#how-it-works)
- [Building from source](#building-from-source)
- [Test plan](#test-plan)
- [Files](#files)
- [AI assistance](#ai-assistance)
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

### With r2modman

1. Download `lmkHenlo-SBGVR-<version>.zip` from [GitHub Releases](https://github.com/lmkHenlo/SBGVR/releases).
2. In r2modman, make sure the Super Battle Golf profile has `BepInExPack` (install it from r2modman's online list if not).
3. Use r2modman's **Import local mod** with the zip.

A zip you built yourself ([Building from source](#building-from-source) writes `artifacts\lmkHenlo-SBGVR-<version>.zip`) installs the same way.

### By hand

Install BepInEx 5 into the game folder and start the game once. Then run this in PowerShell. It installs the newest SBGVR zip in your Downloads folder, or in `artifacts` if you run it in the repository folder after a build. Change `$game` if the game is installed somewhere else.

```powershell
$game = "C:\Program Files (x86)\Steam\steamapps\common\Super Battle Golf"
$zip = Get-ChildItem "$HOME\Downloads\lmkHenlo-SBGVR-*.zip", ".\artifacts\lmkHenlo-SBGVR-*.zip" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
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
| F4 | Recentre the view: the direction you're facing becomes straight ahead. Set by `RecenterKey`. |
| F5 | Switch between first person and third person (see [First person](#first-person)). Set by `FirstPersonKey`. |

### Motion controls

Motion controls only work with UUVR's default VR runtime, OpenXR (`Preferred VR APi` = `OpenXr`); with `OpenVr`, the controllers don't work (see [Known issues](#known-issues)).

On by default (`VR Controls` → `MotionControls` in `com.sbgvr.cfg`; restart the game after changing it). While VR runs, your VR controllers work like a gamepad, so the game's gamepad controls, menus and your gamepad rebinds all apply. A real gamepad, keyboard and mouse still work alongside. The game shows gamepad button icons.

Controller inputs are named as in Meta's [Quest controller guidelines](https://developers.meta.com/vr/design/controllers/); other controllers use the input in the same place.

| Controller | Gamepad | In the game |
| --- | --- | --- |
| Left thumbstick | Left stick | Move; menu cursor |
| Right thumbstick | Right stick | Turn the camera |
| Left trigger button | LT | Aim |
| Right trigger button | RT | Swing (hold to charge) |
| A button / B button | A / B | Jump / dive |
| X button / Y button | X / Y | Interact / swing angle |
| Right grip button | RB | Next item |
| Tap the left grip button | LB | Previous item |
| Menu button (left controller) | Start | Pause |
| Press the left thumbstick | X | Interact (same as the X button) |
| Press the right thumbstick | R3 | Drop item |
| Hold the left grip button + right thumbstick | D-pad | Emotes, push to talk, votes, restart |
| Hold the left grip button + press the right thumbstick | – | Switch between first person and third person (see [First person](#first-person)) |
| Hold the left grip button + A button | Start | Pause (for controllers without a Menu button the game can use, such as the Valve Index) |
| Hold the left grip button + B button | Back | Scoreboard |

The Meta button on the right controller opens the headset's universal menu and isn't available to games. The golf cart's honk (L3 on a gamepad) has no controller input.

**Motion camera.** With `MotionCamera` on (the default once `MotionControls` is on), small movements of your `DominantHand` controller turn the camera while you aim and charge a golf club or rocket club swing, just like the right thumbstick:

- turn the controller, or move it sideways, to turn the camera left or right;
- with `MotionCameraMaxPitch` above 0 (it's 0 by default), tilt it to look up or down.

Your character faces the camera while you aim, so turning the camera this way fine-tunes where the swing goes, and the trajectory preview follows. Big hand movements make small changes: by default, turning the controller 8° or moving it 10 cm sideways turns the camera 2°, up to 30° left or right from where you started. It starts from however you hold the controller when you start aiming, stops the moment you swing (so pulling the trigger or following through doesn't move it), and the camera stays where it is when you stop aiming. The thumbstick still works alongside. Tune it with `MotionCameraStrength`, `MotionCameraMovement`, `MotionCameraMaxYaw` and `MotionCameraMaxPitch`. Like controller aiming, it only works while you're playing with the controllers.

**Motion camera for weapons.** With `MotionCamera` and `ControllerAiming` on, while you aim an item with the controllers, in third person or in first person, turning your `DominantHand` controller left or right, or tilting it up or down, turns the camera by the same angle: 1:1, with no scaling, dead zone or limit, and smoothed so hand tremor doesn't shake the view (an adaptive filter: steady when you hold still, close behind your hand when you turn quickly) (the `MotionCamera…` tuning settings are for swings only; the game's own up and down limit still applies). Weapons aim along the camera, as on a flat screen, so a 10° turn or tilt of your hand moves the aim 10°, and the crosshair, your character and the shot line up with the camera. It starts from however you hold the controller when you start aiming, and the thumbstick still works alongside. While a shot fires, the tilt holds still (charging the railgun is still aiming, so you can keep tilting while it charges); once it's done, and when you stop aiming, the camera's up and down tilt eases back to where it was before you aimed (about a third of a second), and if you're still aiming, tilting carries on from there. Left and right stays where you turned it. In first person, the camera's tilt is level again once it has eased back, as your head does the looking up and down there, and while you aim your character faces the camera rather than your head. Switching between first and third person while aiming or easing back sets the tilt back at once, so none is left behind. The railgun's scope, which the game switches to when you aim the railgun in third person, works the same way: the controller turns and tilts the scope, and when you close it the third-person camera eases back to the tilt it had before. It changes nothing else: your character isn't moved, and golf swings, golf carts and gamepad, keyboard or mouse aiming are as before.

**Mirrored buttons.** With `MirrorButtons` on (off by default), the layout is mirrored left to right, for example for left-handed play together with `DominantHand` = `Left`:

| Controller | Gamepad | In the game |
| --- | --- | --- |
| Right thumbstick | Left stick | Move; menu cursor |
| Left thumbstick | Right stick | Turn the camera |
| Right trigger button | LT | Aim |
| Left trigger button | RT | Swing (hold to charge) |
| X button / Y button | A / B | Jump / dive |
| A button / B button | X / Y | Interact / swing angle |
| Left grip button | RB | Next item |
| Tap the right grip button | LB | Previous item |
| Menu button (left controller) | Start | Pause (the Menu button is only on the left controller) |
| Press the right thumbstick | X | Interact (same as the A button here) |
| Press the left thumbstick | R3 | Drop item |
| Hold the right grip button + left thumbstick | D-pad | Emotes, push to talk, votes, restart |
| Hold the right grip button + press the left thumbstick | – | Switch between first person and third person |
| Hold the right grip button + X button | Start | Pause |
| Hold the right grip button + Y button | Back | Scoreboard |

Controllers without A/B and X/Y buttons (HTC Vive wands, Windows Mixed Reality controllers) use touchpad clicks instead: the edges of the right touchpad are Y (top), B (right), A (bottom) and X (left), and the left touchpad is the D-pad (swapped with `MirrorButtons`).

**Aiming with your controller.** With `ControllerAiming` on (the default once `MotionControls` is on), weapons aim where your dominant hand's controller points while you aim or use them: the dueling pistol, elephant gun, rocket launcher (and its orange lock-on), railgun, freeze bomb and thunderstorm. The reticle follows the controller, and your character turns to face that way when you fire. Set `DominantHand` to `Left` to aim with the left controller; the buttons stay where they are unless you also turn on `MirrorButtons` (see below). If the aim sits a little above or below where the controller points, adjust `ControllerAimPitch`. Golf swings, the flash camera and the airhorn still work as in the game: swings follow the camera, and the flash camera and airhorn act in front of your character. This only happens while you're playing with the controllers: once you use a gamepad, keyboard or mouse, weapons aim along the camera again until you next use a controller.

### First person

In VR you can see the game through your character's eyes instead of from the camera behind them. Off by default (`VR First Person` → `FirstPerson`). Switch with **F5** (`FirstPersonKey`), or on the controllers by holding the left grip button and pressing the right thumbstick (`FirstPersonControllerToggle`). The switch is remembered for next time.

- **Looking:** you look around and up and down with your head. The right thumbstick (or the mouse) only turns you left and right, so the horizon never tilts. By default the thumbstick turns you smoothly (`FirstPersonTurn` = `Smooth`); set `FirstPersonTurn` to `Snap` to turn in 30° steps (`SnapTurnAngle`). A mouse always turns smoothly.
- **Walking and aiming** go where your head is looking. **Golf swings** (golf club, rocket club, and the Jumbo Burger's giant swing) aim like in third person, starting where you're looking: when you start aiming, the shot points the way your head faces. From then until the swing is over, only the thumbstick or mouse turns the aim (smoothly, even with snap turning, so you can fine-tune it), never your head, so you can look down at the ball or around freely. The trajectory preview shows where the shot goes. Weapons aim where your dominant hand's controller points while you play with the controllers (`ControllerAiming`), and where you look with a gamepad, keyboard or mouse.
- **Your own character is hidden**, including what you're holding, so you don't see the inside of your head. The game does this itself whenever its first-person camera is on (it uses the same camera for the railgun's scope). Other players see you as usual: nothing is sent over the network.
- **Third person when needed:** while you're knocked out, respawning or spectating, the view goes back to third person, and returns to first person afterwards. The hole overview plays as usual, then returns to first person. The railgun's scope keeps the way you're facing.
- **Golf cart:** while you're in a golf cart, the view faces the front of the cart and turns with it as it steers, frame for frame; the thumbstick and mouse don't turn it. You can still look around with your head.
- **Eye height** is 1.5 m above your character's feet, and grows with the Jumbo Burger. Raise or lower it with `FirstPersonEyeOffset`.
- **VR only:** on a flat screen, the game always uses its third-person camera.

The motion camera works in first person too: while you aim and charge a golf club or rocket club swing with the controllers, turning your dominant controller or moving it sideways fine-tunes the aim, just as in third person (`MotionCameraStrength`, `MotionCameraMovement`, `MotionCameraMaxYaw`). It turns your view with the aim, like the thumbstick does. Tilting the controller does nothing for golf swings in first person, since your head looks up and down. For weapons, see the motion camera for weapons in [Motion controls](#motion-controls): turning and tilting the controller turns and tilts the first-person camera 1:1 while you aim, and the tilt eases back to level afterwards.

### Controller layout in the menus

With `MotionControls` on, the main menu shows the VR controller layout as soon as the game starts, and the pause menu shows it whenever you open it: every button on the left and right controllers with what it does, the combinations with the left grip button, the motion controls and the keyboard keys. It's on its own panel just below UUVR's floating screen, tilted towards you, so look down a little to read it. It never covers the menu, which works exactly as before. The menu's fades (between background scenes, and when the menu fades in or out) don't dim it: it stays fully visible for as long as the main menu is there, and goes away when you join a match. In a match it appears the moment you open the pause menu and goes away the moment you close it; the pause menu works exactly as before.

Set `VR HUD` → `MenuControlsSide` to `Left` or `Right` to put it beside the screen instead, or `MenuControls` to `false` to hide it. The panel keeps clear of the screen whatever UUVR's `VR UI Position` and `VR UI Scale` are and whatever the game window's shape.

### Settings

Settings are in `BepInEx\config`. In r2modman, open them with **Config editor**. Change them with the game closed, since BepInEx reads the files when the game starts. For example, these lines in `com.sbgvr.cfg` turn camera shake off completely and turn ambient occlusion off for a higher frame rate:

```ini
[VR Comfort]
ScreenShake = 0

[VR Graphics]
AmbientOcclusion = false
```

SBGVR's settings only change the game while VR is running. The game's own settings and save file are never changed.

| File | Setting | Default | What it does |
| --- | --- | --- | --- |
| `raicuparta.uuvr-modern.cfg` | `General` → `Preferred VR APi` | `OpenXr` | `OpenXr` or `OpenVr`. Motion controls only work with `OpenXr`. |
| `raicuparta.uuvr-modern.cfg` | `Camera` → `Camera Tracking Mode` | `RelativeTransform` | How the game camera follows the headset: `Absolute`, `RelativeMatrix`, `RelativeTransform` or `Child`. |
| `raicuparta.uuvr-modern.cfg` | `UI` → `UI Patch Mode` | `Mirror` | How the 2D interface reaches the floating screen: `None`, `Mirror` or `CanvasRedirect`. |
| `raicuparta.uuvr-modern.cfg` | `UI` → `Preferred UI Plane Render Mode` | `InWorld` | `InWorld` (scenery can cover it) or `OverlayCamera` (always on top). |
| `com.sbgvr.cfg` | `VR Comfort` → `ScreenShake` | `0.25` | Camera shake in VR as a share of the game's (0–1). |
| `com.sbgvr.cfg` | `VR Comfort` → `ImpactFrames` | `false` | Show impact frames (explosion flashes) in VR. |
| `com.sbgvr.cfg` | `VR Comfort` → `FlashFrames` | `true` | Show flash frames in VR: the full-view flash that blinds you when another player's flash camera catches you. `false` = you aren't blinded in VR. |
| `com.sbgvr.cfg` | `VR Comfort` → `CameraTransitions` | `Game` | Camera moves between views: `Game`, `Fast` (about a third of the time) or `Instant` (a cut). |
| `com.sbgvr.cfg` | `VR Comfort` → `MenuBackground` | `Still` | Main menu background scenes: `Moving` (the game's pans) or `Still`. |
| `com.sbgvr.cfg` | `VR Comfort` → `FullViewFades` | `true` | Fade the whole view, not only the floating screen, when the game fades. |
| `com.sbgvr.cfg` | `VR Comfort` → `RecenterKey` | `F4` | Key that recentres the view. `None` = no key. |
| `com.sbgvr.cfg` | `VR HUD` → `WorldSpaceMarkers` | `true` | Draw icons, name tags and the interaction reticle in the world at their target, at the right depth for each eye. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkersOnTop` | `true` | Draw world-space markers over scenery, like the game does. Menus on the floating screen still cover them. Weapon reticles are always on top. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkerScale` | `1` | Size of world-space markers (0.25–4); 1 = their size on the floating screen. |
| `com.sbgvr.cfg` | `VR HUD` → `MarkerViewAngle` | `70` | Degrees from straight ahead within which icons stay in the world; further round they become edge arrows (30–120). |
| `com.sbgvr.cfg` | `VR HUD` → `WorldSpaceReticles` | `true` | Draw weapon reticles in the world at the point you're aiming at, always on top of scenery and characters, instead of doubled on the floating screen. |
| `com.sbgvr.cfg` | `VR HUD` → `MenuControls` | `true` | With `MotionControls`, show the VR controller layout on a panel next to the floating screen in the main menu and the pause menu (see [Controller layout in the menus](#controller-layout-in-the-menus)). |
| `com.sbgvr.cfg` | `VR HUD` → `MenuControlsSide` | `Below` | Where that panel is: `Below` the floating screen (tilted towards you), or to its `Left` or `Right`. |
| `com.sbgvr.cfg` | `VR HUD` → `AimLine` | `false` | With `WorldSpaceReticles`, draw a thin white line from your character's hand to the weapon reticle while you aim (not for the railgun, thunderstorm or flash camera). |
| `com.sbgvr.cfg` | `VR HUD` → `PlayersCoverMarkers` | `false` | With `MarkersOnTop`, player characters in front of a marker cover it, while scenery still doesn't. A name tag or teammate icon isn't covered by its own player, and weapon reticles aren't covered at all. |
| `com.sbgvr.cfg` | `VR Graphics` → `AmbientOcclusion` | `true` | The game's ambient occlusion in VR. It costs more than on a flat screen, since each eye is drawn at the headset's resolution. |
| `com.sbgvr.cfg` | `VR Controls` → `MotionControls` | `true` | Use your VR controllers as a gamepad while VR runs (see [Motion controls](#motion-controls)). Restart the game after changing it. |
| `com.sbgvr.cfg` | `VR Controls` → `ControllerAiming` | `true` | With `MotionControls`, weapons aim where your `DominantHand` controller points. Off = they aim along the camera. |
| `com.sbgvr.cfg` | `VR Controls` → `DominantHand` | `Right` | The controller weapons aim with: `Right` or `Left`. |
| `com.sbgvr.cfg` | `VR Controls` → `MotionCamera` | `true` | With `MotionControls`, small controller movements turn the camera while you aim a golf club or rocket club swing, and in third and first person turning or tilting the controller turns the camera 1:1 while you aim an item, which weapons then aim along (see [Motion controls](#motion-controls)). |
| `com.sbgvr.cfg` | `VR Controls` → `MotionCameraStrength` | `0.25` | Share of the controller's turn and tilt that goes into the camera (0.05–1). |
| `com.sbgvr.cfg` | `VR Controls` → `MotionCameraMovement` | `2` | Degrees the camera turns per 10 cm of sideways hand movement (0–10). `0` = turning and tilting only. |
| `com.sbgvr.cfg` | `VR Controls` → `MotionCameraMaxYaw` | `30` | Furthest the controller turns the camera left or right while aiming, in degrees (1–45). |
| `com.sbgvr.cfg` | `VR Controls` → `MotionCameraMaxPitch` | `0` | Furthest the controller tilts the camera up or down while aiming, in degrees (0–30). `0` (the default) = left and right only. |
| `com.sbgvr.cfg` | `VR Controls` → `MirrorButtons` | `false` | Mirror the motion controls button layout left to right (see [Motion controls](#motion-controls)). |
| `com.sbgvr.cfg` | `VR Controls` → `ControllerAimPitch` | `0` | Degrees to tilt the controller's aim (−45 to 45): positive tilts it down, negative up. |
| `com.sbgvr.cfg` | `VR First Person` → `FirstPerson` | `false` | See through your character's eyes in VR (see [First person](#first-person)). The toggles change this, and it's remembered. |
| `com.sbgvr.cfg` | `VR First Person` → `FirstPersonKey` | `F5` | Key that switches between first person and third person. `None` = no key. |
| `com.sbgvr.cfg` | `VR First Person` → `FirstPersonControllerToggle` | `true` | With `MotionControls`, hold the left grip button and press the right thumbstick to switch views (mirrored with `MirrorButtons`). |
| `com.sbgvr.cfg` | `VR First Person` → `FirstPersonTurn` | `Smooth` | How the thumbstick turns you in first person: `Snap` (steps) or `Smooth`. A mouse always turns smoothly. |
| `com.sbgvr.cfg` | `VR First Person` → `SnapTurnAngle` | `30` | Degrees per snap turn (15–90). |
| `com.sbgvr.cfg` | `VR First Person` → `FirstPersonEyeOffset` | `0` | Raises (positive) or lowers (negative) your eyes in first person, in metres (−0.5 to 0.5). |
| `com.sbgvr.cfg` | `Diagnostics` → `Enabled` | `true` | Log VR, camera and renderer details to `LogOutput.log`. |
| `com.sbgvr.cfg` | `Diagnostics` → `PerformanceLogInterval` | `30` | Seconds between frame rate lines while VR runs (0–600). `0` = never. |

`raicuparta.uuvr-modern.cfg` also holds UUVR's other settings, such as the floating screen's position and size, each with a description.

### If the headset stays black

1. Check that SteamVR (or your runtime) is set as the OpenXR runtime.
2. Set `General` → `Preferred VR APi` to `OpenVr`. Motion controls don't work in that mode.
3. Set `Camera` → `Camera Tracking Mode` to `RelativeMatrix` or `Absolute`.

## Known issues

- **The game's field of view doesn't apply in VR.** The headset decides the field of view, so the game's FOV slider has no effect yet, and the railgun's zoomed aim shows its reticle without zooming.
- **The SteamVR menu opens on launch or when a match starts.** Steam shows Super Battle Golf as a flat game on SteamVR's theatre screen before the mod starts VR. SteamVR then docks that screen into its dashboard when the game becomes a VR app or stalls while loading, and there's no API for the mod to close the dashboard. Turn the theatre off in one of two ways:
  - For this game: in Steam, right-click Super Battle Golf → Properties → General, and untick **Use Desktop Game Theatre while SteamVR is active**.
  - For every game: in SteamVR Settings → Dashboard, turn off **Present Non-VR Applications on Theater Screen Upon Launch**.
- **MaxFOVSlider before 0.2.1** reshapes the headset's picture with its Panini projection. MaxFOVSlider 0.2.1 and later pause their projection and extra resolution while VR runs, and work alongside SBGVR; update it, or disable older versions while using VR.
- **Motion controls don't work with OpenVR.** With `General` → `Preferred VR APi` = `OpenVr` in `raicuparta.uuvr-modern.cfg`, the VR controllers don't control the game, so controller aiming and the motion camera don't work either. Keep the default, `OpenXr`, to use them; a gamepad, keyboard and mouse work with either.
- **First person is new.** It's tested on Meta Quest 3 with OpenXR, not yet on Valve Index or Steam Frame. Your hands and what you're holding aren't visible in first person, since the game hides your whole character while its first-person camera is on.
- **Motion controls are new.** They're tested on Meta Quest 3 with OpenXR, not yet on Valve Index or Steam Frame. They act as a gamepad: there's no pointing at the floating screen, and controllers don't vibrate (no haptics). Controller aiming covers weapons only; golf swings follow the camera. Vive wands and Windows Mixed Reality controllers use touchpad clicks for their missing buttons.

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
| Draw the VR UI on the last sorting layer, two orders below the highest sorting order (`UiSorting`), leaving those two for what must be drawn after it | Effects with a higher sorting order were drawn over the floating screen, whatever its render queue. |
| Add `VrCameraOffset.RecenterRotation` | Lets SBGVR recentre the view. Identity (the default) keeps upstream behaviour. |
| Remove the F3 key that turned VR off and on (`UuvrCore`) | Toggling VR with it didn't work in this game. VR now stays on from startup until the game closes. |
| Restore OpenXR controller input: `Input/OpenXRInput.cs`, `Features/Interactions/` and the call in `OpenXRLoader.StartInternal` (wrapped so a failure is logged and VR still starts) | UUVR's copy of the Unity OpenXR plugin left out the code that tells the OpenXR runtime which controller inputs to report, so OpenXR showed no controllers. It's adapted from the plugin's 1.4.2 source, which matches the bundled `UnityOpenXR.dll`: the action set setup and the action maps of the Oculus Touch, Valve Index, HTC Vive, Windows Mixed Reality and Khronos simple controller profiles, without their Input System layouts. `ControllerProfiles.Enable` turns the profiles on; until something calls it, OpenXR starts exactly as before. |

**SBGVR** (`src/SBGVR`, plugin ID `com.sbgvr`) depends on UUVR. Each feature checks `VrState.IsRunning`, so nothing changes on a flat screen. Each feature's Harmony patches are applied on their own: one that fails to patch (after a game update, say) is removed and logged, and the rest keep working.

| Feature | Files | How |
| --- | --- | --- |
| Screen shake | `Comfort/ScreenShakePatches.cs` | Transpilers on `CameraModuleController.UpdateShake` and `CanShakeAtAll` multiply the game's shake setting by `ScreenShake`. |
| Screen effects | `Comfort/ScreenEffectPatches.cs` | Prefixes skip `ImpactFrameController.PlayImpactFrame`, `FlashFrameFeature.AddRenderPasses`, `CRTFeature.AddRenderPasses` and `BlurFeature.AddRenderPasses`. The last is the pause menu's blur, which in VR blurred the floating screen too. |
| Camera transitions | `Comfort/CameraTransitionPatches.cs` | A prefix on `CameraModuleController.TransitionToInternal` shortens or zeroes the duration. |
| Ambient occlusion | `Rendering/` | HTrace AO asks the `Camera` for its size, view and field of view, which in VR are still the game window's, so part of each eye drew nearby surfaces black. Before each eye is drawn, `EyeView` records it. Transpilers then redirect HTrace AO's camera reads, history keys and GTAO's depth-to-view factors to that eye. If the patches don't apply, or `AmbientOcclusion` is off, the feature is skipped in VR. |
| Menu background | `Comfort/MenuBackground.cs` | Holds each `MainMenuScenario` camera halfway between its two pan points. |
| Full-view fades | `Fades/` | Hides `ColorOverlay`'s image and the main menu's overlay, and draws their colour on spheres around the camera, over and behind the floating screen. The one behind sorts with the screen at render queue 4999; the one over is at the screen's queue, 5000, one sorting order after it, since URP draws no transparent object above queue 5000. |
| Recentre | `Comfort/Recenter.cs` | Cancels the headset's yaw through `VrCameraOffset.RecenterRotation`. |
| World-space markers | `Hud/` | After the game lays out a marker (`WorldspaceIconUi`, `NameTagUi`, `TargetReticleUi`, `LockOnTargetUi` (the orange lock-on reticle), `AirhornTargetIndicator`, `JumboBurgerGiantFormTimerUi` or `TextPopupUi`), `WorldMarker` moves its UI objects under a world-space canvas at the target. It's scaled to its size on the floating screen. Markers return to the HUD when they go off screen or the game stops laying them out. With `MarkersOnTop`, a marker's materials ignore depth and it sorts on the last sorting layer one order below the floating screen, after every other transparent object (effects and glows included), like weapon reticles; without it, it's depth tested and sorts with the scene's transparent objects. |
| Players covering markers | `Hud/MarkerOcclusion.cs`, `PlayerOverlap.cs`, `PlayerOccluder.cs` | Each frame, `MarkerOcclusion` finds characters that are nearer than a marker they overlap in view and gives each a stencil bit. `PlayerOccluder` sets that bit wherever the character is visible. It draws the character's meshes again with a UI material that draws no colour, depth tested against the character itself (a twin skinned mesh, or `Graphics.RenderMesh`). Each marker's drawn materials then skip pixels with the bits of the characters in front of it. The characters are drawn once, as usual. |
| Motion controls | `Controls/MotionControls.cs`, `Plugin.EnableOpenXrControllers` | With `MotionControls` on, SBGVR enables UUVR's OpenXR controller profiles before UUVR starts VR. With OpenVR, controllers don't reach the game (a known issue; not investigated). Just before each Input System update, `MotionControls` reads both controllers through Unity's XR input (`UnityEngine.XR.InputDevices`) and queues their state to a virtual `Gamepad` it adds while VR runs. Nothing is sent until a controller is used, or while it's left alone, so other devices stay usable. The game's own input code is unchanged. |
| Controller aiming | `Controls/ControllerAim.cs`, `ControllerAimPatches.cs` | The game aims every weapon from `GameManager.Camera`'s transform. Transpilers on `PlayerInventory.GetFirearmAimPoint`, `GetFreezeBombAimDirection`, `TryGetThunderstormAimPoint`, `PlayerAnimatorIo.UpdateAimingAngle`, `PlayerMovement.AlignWithCameraImmediately` and, while the rocket launcher picks its target, `PlayerGolfer.TryGetBestLockOnTarget` send their `Component.transform` reads through `ControllerAim.TransformOf`. While the local player aims or uses an item, and the game's current input device is SBGVR's virtual gamepad (`MotionControls.InUse`: the controllers were the last thing used), that returns a stand-in transform on the dominant hand controller's pointing ray instead of the camera's. Otherwise, in first person, it returns one on the head's ray (UUVR's tracking frame with the headset's rotation), since the first-person camera stays level. Other transforms pass through, and with neither, the camera's own. These patches are always applied; they change nothing unless one of those two applies. The ray is the controller's pose (OpenXR's aim pose, or the grip pose) relative to the headset, placed under UUVR's `VrCameraOffset`, so it starts where your hand is in the view you see. `WorldReticles` casts along the same ray. |
| Motion camera | `Controls/MotionCamera.cs` | While aiming a swing, the game turns the character to the orbit camera's yaw (`PlayerMovement.UpdateTargetYaw`), and every swing goes the way the character faces (`PlayerGolfer.GetSwingDirection`). While the local player aims or charges the golf club or rocket club with the controllers (`MotionControls.InUse`) and the orbit camera is active, `MotionCamera` compares the dominant controller's pose in tracking space with the pose it had when aiming started: horizontal turn and sideways movement give a yaw, tilt gives a pitch, each through a dead zone, scaled and clamped. Each frame it adds only the change since the last frame to the camera with `OrbitCameraModule.SetYaw` and `SetPitch`, so thumbstick input adds up with it and nothing snaps back. It stops once the swing starts. The game's aiming code is unchanged. In SBGVR's first person, where the shot follows the first-person camera's yaw, the same yaw goes into `FirstPersonCameraModule.SetYaw` (pitch stays level there). |
| Motion camera for weapons | `Controls/WeaponMotionCamera.cs`, `Controls/ControllerAim.cs`, `View/FirstPersonView.cs` | While the controllers aim an item (`PlayerInventory.IsAimingItem`, then on while the item is in use), not during a camera transition, with the orbit camera (yaw not locked to the subject) or SBGVR's first-person camera (not in a golf cart): the dominant controller's heading and tilt in tracking space are remembered when aiming starts, along with the camera's pitch (level in first person), and each frame the change since then, smoothed by a One Euro filter (min cutoff 1 Hz, beta 0.04 Hz per °/s, speed cutoff 1 Hz; reset at the start of each aim), is added to the camera's yaw and pitch, 1:1 (only the change since the last frame, so the thumbstick still works; the modules' `maxPitch` still applies). Third person uses `OrbitCameraModule.SetYaw`/`SetPitch`; first person uses `FirstPersonCameraModule.SetYaw`, and its pitch is `WeaponMotionCamera.FirstPersonPitch`, which `FirstPersonView` applies before the module places itself in place of the level pitch, and `FirstPersonView.YawOf` returns the camera's yaw instead of the head's meanwhile. Hand poses are tracking-space, so the camera's turn doesn't feed back into them. `ControllerAim.TryGetAimRay` aims along the camera (`Euler(pitch, yaw, 0)`), so the turn isn't counted twice. While the item is being used (`CurrentItemUse` is `Regular` or `Alt`; not `Charging` or `FullyCharged`, the railgun's charge, which is still aiming) the pitch holds; when the use ends, and when aiming ends, the pitch eases back (0.12 s time constant) to the kept pitch and ends on it exactly, so repeated aiming and firing can't accumulate an error. Restores wait for camera transitions; if that camera isn't showing afterwards (a perspective switch) or a golf, rocket club or Jumbo Burger swing starts, the kept pitch is set at once. The railgun's scope from third person (the game's `FirstPersonCameraModule` while `PlayerInventory.IsInFirstPersonAim`, outside SBGVR's first person) is steered through the module's own `SetYaw`/`SetPitch`; the orbit camera isn't steered while the railgun is equipped, since the game copies its pitch and yaw into the scope on the way in and the scope's back into it on the way out, so the pitch kept before the scope is eased back on the orbit camera after it closes. Yaw isn't restored. |
| First person | `View/FirstPersonView.cs`, `FirstPersonPatches.cs` | Uses the game's own `FirstPersonCameraModule` (the railgun scope's camera), which sits at `viewer.TransformPoint(localPosition)` and looks along its yaw, pitch and roll. The game already moves the character relative to it (`PlayerMovement.ProcessMovementInput`), turns the character to it while aiming (`UpdateTargetYaw`), feeds it the right stick (`PlayerInput.UpdateCameraInput` calls `Rotate`) and hides the local player's renderers while it's the current module (`PlayerCosmeticsSwitcher`, through `forceRenderingOff`). `FirstPersonView` switches to it with `GameplayCameraManager.TransitionTo`, copying the orbit camera's yaw so the world doesn't move, and back again copying the camera's yaw (not the head's) into the orbit camera. Its viewer becomes an upright anchor 1.5 m × `PlayerMovement.CharacterScale` (+ `FirstPersonEyeOffset`) above the player's root, moved in a prefix on `UpdateModule`, which also keeps pitch and roll at 0 (UUVR's camera follows the game camera's full rotation, so a pitched camera would tilt the world). While `PlayerInfo.ActiveGolfCartSeat` is valid, the same prefix sets the yaw to the cart's heading and places the anchor on the cart's `GolfCartMovement` transform, and the `Rotate` prefix ignores look input. The cart is read directly because the camera is placed (`BUpdate`'s pre-late-update loop) before `PlayerMovement.LateUpdate` moves the player onto the cart, so the player's own transform is a frame behind while the cart moves. A prefix on `CameraModuleController.TransitionToInternal` turns requests for the orbit camera into first person while it's on, which covers the end of the hole overview and of the railgun scope; it steps aside (back to the orbit camera) while the player is spectating, knocked out or recovering, respawning or drowning. A prefix on `Rotate` drops the vertical input and, for `Snap`, turns in steps from the raw `Camera.Look` stick value. A prefix and postfix on `PlayerInventory.UpdateIsInFirstPersonAim` keep the yaw and eye height when the railgun scope copies in the orbit camera's and raises the view. Transpilers on `ProcessMovementInput` and `UpdateTargetYaw` read `FirstPersonView.YawOf` instead of the camera's yaw: the head's compass direction (from its up vector when looking nearly straight down). While the local `PlayerGolfer` aims, charges or plays a swing (`IsAimingSwing`, `IsChargingSwing`, `IsSwinging`), `YawOf` returns the camera's own yaw (turned to the head's direction when aiming starts, with `VrCameraOffset.RecenterRotation` turned back by the same angle so the view doesn't move), the head ray isn't offered to `ControllerAim.TransformOf` (so `ReleaseSwingChargeInternal`'s `AlignWithCameraImmediately` uses the camera), and `Rotate` turns smoothly instead of snapping: shots aim exactly as with the third-person camera. The grip + thumbstick toggle is read in `MotionControls`. |
| Aim line | `Hud/AimLine.cs` | While `WorldReticles` has a weapon reticle in the world (not the railgun's, thunderstorm's, flash camera's or "can't use here" one), a world-space `LineRenderer` facing the viewer runs from the local player's `RightHandEquipmentSwitcher` (the hand holding the item) to the middle of what the reticle draws (its graphics' combined bounds), since some reticles, such as the freeze bomb's, aren't drawn centred on the aim point. It updates every frame after the reticle is placed (execution order 30001), so it follows both. Its width is proportional to each end's distance from the camera, about 0.1°, so it stays visible far away without shimmering. It uses the UI shader with depth testing (`unity_GUIZTestMode` = `LessEqual`) in the transparent queue, so scenery covers it like a shot, and menus and fades cover it too. |
| World-space reticles | `Hud/WorldReticles.cs` | Each frame, casts a ray along the game camera's forward (the direction items aim), or the controller's aim with `ControllerAiming`, or the head's in first person, past the local player. It moves `ReticleManager`'s reticles into the world at the hit point: 50 m away if the ray hits nothing, and always between 2 m and 150 m. A gun's crosshair (dueling pistol, elephant gun, rocket launcher, railgun) goes instead at the game's own aim point for the shot (`GameAimPoint`): `PlayerInventory.GetFirearmAimPoint` with the arguments the gun's shot uses (its range, its layers, hits in front of the gun and not on your own character), up to the gun's range rather than 150 m. When you aim more than 45° to the side of your character, where the game turns your character before shooting and aims again, SBGVR repeats the game's ray without that limit. If the game's aiming methods change, gun crosshairs fall back to SBGVR's own ray and the log says so. They glide between distances, keep their floating-screen size, and sit in a holder the size of their HUD canvas. Reticles and the lock-on reticle always ignore depth and are never covered by characters. They sort on the last sorting layer, one order below the floating screen and fades. |
| Controller layout in the menus | `Hud/MenuControlsPanel.cs`, `Assets/controller-layout.png`, `tools/controller-layout/` | While VR runs with `MotionControls` and either `MainMenu` exists or `PauseMenu.IsPaused` is set (the game sets it when the pause menu opens and clears it when it closes), a quad with the layout image (embedded in `SBGVR.dll`, loaded with mipmaps and trilinear filtering) is placed each frame and in `Application.onBeforeRender` relative to the camera UUVR's floating screen follows, like the screen itself. Its angle comes from the floating screen's edge (from `VR UI Position`, `VR UI Scale` × 1.8 m and the window's aspect ratio) plus a 4° gap and half the panel's own angular size, so it never overlaps the screen; it's 0.95 × the screen's distance wide, at the same distance, and faces the camera. It uses UUVR's UI shader with depth testing off, on UUVR's VR UI layer, at the floating screen's render queue, 5000, two sorting orders after the screen: after the full-view fade behind the screen (4999) by render queue and after the one over it (one order after the screen) by sorting order, so no fade dims it, while the fades still cover everything else as before. It never overlaps the screen, so the order between the two doesn't matter. (Its queue can't go higher: URP only draws transparent objects in queues 2501–5000.) The image is rendered from `tools/controller-layout/controller-layout.html` by `render.py`; its controller artwork is Meta's (see `THIRD_PARTY_NOTICES.md`). |

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
4. Play a hole: teeing off, aiming, the swing power bar, the ball flight camera, the hole overview, spectating and the golf cart. Camera moves between views use the game's own sweep (`CameraTransitions` = `Game`).
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
   - With `AimLine` on (it's off by default): while aiming the dueling pistol, elephant gun, rocket launcher or freeze bomb, a thin white line runs from your character's hand to the reticle. The railgun, thunderstorm and flash camera show no line. It stays attached to both as you move, turn and change the aim, looks single and steady in the headset at any distance, and disappears when you stop aiming. With `AimLine` off (the default), there's no line.
7. Ambient occlusion:
   - No black areas cover nearby scenery or your character when you look around or move.
   - Ambient occlusion (soft shading where surfaces meet, under your character and in corners) looks the same in both eyes, and doesn't smear or flicker when you turn your head.
   - The log says `Ambient occlusion: draws for each eye in VR` and `drawn for each eye at 2528x2704` (your headset's eye size).
   - With `AmbientOcclusion` off, the frame rate goes up.
8. Explosions near you don't flash the view, and shake the camera only a little (a quarter of the game's shake, `ScreenShake` = 0.25). Another player's flash camera does blind you (`FlashFrames`). The pause menu and the settings opened from it are sharp, not blurred.
9. F4 recentres the view.
10. The frame rate lines in the log are close to the headset's refresh rate.
11. Motion controls, with `MotionControls` on (and once with it off, to check nothing changes):
    - The log shows `[ok] OpenXR controller profiles` and, when you pick up each controller, `Motion controls: left/right controller connected` with its inputs.
    - Every button in the [Motion controls](#motion-controls) table does what it says, in a match, the golf cart, spectating, the emote wheel and the menus (with the gamepad cursor).
    - A real gamepad, keyboard and mouse still work, before and after using the controllers.
    - With `Preferred VR APi` = `OpenVr`, controllers are known not to work; nothing to test there until that's fixed.
12. Controller aiming, with `MotionControls` on:
    - Aim each weapon (dueling pistol, elephant gun, rocket launcher, railgun, freeze bomb, thunderstorm): the reticle sits where the right controller points, and shots go there. The log says `Controller aiming: aiming with the right controller` and which pose it uses.
    - The rocket launcher's orange lock-on picks the target you point at. A full-power golf swing's homing target still follows the camera.
    - Golf swings, the flash camera and the airhorn behave as without motion controls.
    - Aim a weapon with a gamepad or the mouse while holding the controllers: it aims along the camera. Press a controller button and aim again: it aims with the controller.
    - With `DominantHand` = `Left`, the left controller aims and the buttons stay put. With `MirrorButtons` on, every button is mirrored as in the mirrored table, and the Menu button still pauses. With `ControllerAiming` off, weapons aim along the camera again.
    - If the aim sits above or below where the controller points, note which headset and the `ControllerAimPitch` that fixes it.
13. Motion camera, with `MotionControls` on:
    - Aim a golf club swing, then turn the right controller and move it sideways: the camera turns a little, smoothly, your character turns with it, and the trajectory preview follows. The ball goes where the preview showed.
    - Holding the controller still while aiming doesn't drift the camera. Pulling the trigger to swing doesn't move it. The thumbstick still turns the camera while aiming.
    - The rocket club works the same way. Outside swing aiming (walking, weapons, menus), the controller doesn't turn the camera.
    - Stopping aiming leaves the camera where it is; aiming again starts from how you hold the controller then.
    - Tilting the controller doesn't move the camera unless `MotionCameraMaxPitch` is above 0.
    - With `MotionCamera` off, the camera only turns with the thumbstick, as before.

14. First person (`FirstPerson`; once with it off, to check nothing changes from 1.1.1):
    - F5 and holding the left grip button + pressing the right thumbstick both switch views, and the log says `First person: on` / `off`. Restart the game: it starts in the view you left. Pressing the right thumbstick alone still drops your item.
    - The log has one `First person: eyes … m above the feet` line; it should say 1.50 m with `FirstPersonEyeOffset` at 0; note whether that feels right, and the `FirstPersonEyeOffset` that fixes it if not.
    - Your own body, head and held item aren't visible. Another player sees you as usual.
    - The thumbstick's up and down does nothing, and the horizon never tilts. The thumbstick turns smoothly; with `FirstPersonTurn` = `Snap` it turns 30° per push. Switching views doesn't move the world.
    - Walking goes where your head looks, also looking nearly straight down.
    - Aim, charge and hit golf club and rocket club swings: when you start aiming, the trajectory preview points where you're looking (the view doesn't jump; the log says `First person: shot aim starts where you're looking`); after that it doesn't move when you turn your head; the thumbstick turns the aim smoothly (also with `FirstPersonTurn` = `Snap`). The ball goes where the preview showed. Outside swings, walking and weapons still follow your head.
    - With the controllers, while aiming a golf club or rocket club swing, turning the right controller or moving it sideways fine-tunes the aim (the view and the trajectory preview turn a little, as in third person), and it stops when you swing. Tilting the controller doesn't move anything. With `MotionCamera` off, only the thumbstick turns the aim.
    - Each weapon with the controllers aims where the controller points; with a gamepad or the mouse, where you look (the reticle follows your head). The rocket launcher's lock-on follows the same aim.
    - The railgun's scope keeps the way you face and returns to first person when it ends.
    - The hole overview plays and returns to first person. Getting knocked out, drowning and respawning show third person and return to first person afterwards (`First person: third person while …` in the log). Spectating stays in third person.
    - In the golf cart (driving and as a passenger), the view faces the cart's front and turns with it as you steer, without lagging, drifting or jittering through turns; the thumbstick and mouse don't turn it. Getting out leaves you facing the same way.
    - The Jumbo Burger's giant form in first person: note how it feels.
    - Markers, name tags and reticles look right; with `AimLine` on, note how the line looks.

15. Controller layout in the menus (`MenuControls`):
    - As soon as the main menu appears, look down: the controller layout is just below the floating screen, tilted towards you, and readable. The log says `Menu controls: controller layout shown in the main menu, below the floating screen`.
    - It doesn't cover any part of the menu, also in the settings, lobby and match setup screens, and every menu item can still be pointed at and used.
    - It stays fully visible, without dimming or flicker, while the main menu's background scenes fade between each other and while the menu fades in or out; the rest of the view still fades as before. It disappears once the main menu is gone (joining a match).
    - In a match, opening the pause menu shows the layout in the same place at once (the log says `Menu controls: controller layout shown in the pause menu` the first time), and closing it (Resume, the Menu button, or the pause menu closing for any other reason) hides it at once. Every pause menu button, the settings and match setup it opens, and the rules pages stay fully visible and usable.
    - With `MenuControlsSide` = `Left` or `Right` it's beside the screen instead; with `MenuControls` = `false`, or `MotionControls` off, it isn't shown.
    - Try a different `VR UI Scale` or window size: it still stays clear of the screen.

Send `LogOutput.log` from the session with notes on each step.

## Files

```text
SBGVR/
├── CHANGELOG.md              changes in each version
├── Directory.Build.props     game and BepInEx paths, shared references, version
├── NuGet.config              no package sources: the build is offline
├── SBGVR.proj                builds everything and makes the Thunderstore zip
├── build.ps1                 Windows build wrapper
├── src/SBGVR/                the SBGVR plugin (com.sbgvr): Comfort/, Controls/, Diagnostics/, Fades/, Hud/, Rendering/
├── thunderstore/             manifest.json, icon.png and the package README
├── uuvr/                     UUVR v0.4.0 with the changes listed above
├── LICENSE                   GPL-3.0 (UUVR's licence text)
└── THIRD_PARTY_NOTICES.md    licences of the bundled VR runtime libraries
```

## AI assistance

SBGVR was developed with the help of Claude, an AI model made by Anthropic. Claude wrote most of the code and documentation in this repository, including the changes to UUVR, working from lmkHenlo's requests. lmkHenlo set the goals and requirements, chose between the approaches Claude proposed, tested the builds in a VR headset and reported what needed fixing, and decides what is released. The commit history doesn't mark which commits Claude helped write; this section covers the project as a whole.

## API

SBGVR has no API for other mods. Its BepInEx plugin ID is `com.sbgvr`, and it hard-depends on UUVR's `raicuparta.uuvr-modern`.

## Maintainers

[@lmkHenlo](https://github.com/lmkHenlo)

## Thanks

- [Raicuparta](https://github.com/Raicuparta), for [Universal Unity VR](https://github.com/Raicuparta/uuvr) (UUVR), which does the VR work this package is built on. SBGVR includes a modified copy of UUVR v0.4.0 (upstream commit `de8e921`), under its GPL-3.0-or-later licence; [How it works](#how-it-works) lists every change, and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) has the notices.
- The [BepInEx](https://github.com/BepInEx/BepInEx) team, for the mod loader.
- The players who tested SBGVR before 1.0.0 on Steam Frame, Valve Index and Meta Quest 3.

## Contributing

Ask questions and report bugs in [GitHub issues](https://github.com/lmkHenlo/SBGVR/issues), or by email to [ps.meiisbei@gmail.com](mailto:ps.meiisbei@gmail.com). For a bug, include `BepInEx\LogOutput.log` from a session where it happened. SBGVR writes the VR and camera details needed to track it down.

Pull requests are accepted on [GitHub](https://github.com/lmkHenlo/SBGVR). When you make changes:

- Write commit messages in the [Conventional Commits](https://www.conventionalcommits.org/) style.
- Put changes to UUVR's files (`uuvr/`) in their own commits, so the differences from upstream stay easy to review.
- Add your change to [CHANGELOG.md](CHANGELOG.md) under Unreleased, following [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
- Change the version in all three places listed in [Building from source](#building-from-source).

## License

[GPL-3.0-or-later](LICENSE) © 2026 lmkHenlo. UUVR is GPL-3.0-or-later © 2024 Raicuparta; SBGVR's copy is modified, as listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). The bundled VR runtime libraries keep their own licences; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
