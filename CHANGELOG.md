# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and version numbers use Thunderstore's `Major.Minor.Patch` format.

1.0.0 is the first public release. Versions 0.1.0 to 0.2.17 are pre-release test builds that were never published; each was built from the commit that set its version number. Versions 0.1.0 to 0.2.14 were named SBG VR, with the Thunderstore package `SBG_VR`. Dates are in US Central time (UTC−05:00).

## [Unreleased]

## [1.3.1] - 2026-10-29

### Added

- `VR HUD` → `BallAndHoleIconsOnTop` (off by default): draws the golf ball and hole icons over everything, as the game does on a flat screen, so scenery, player characters and effects never hide them. Menus still cover them, and other markers are unchanged.

### Fixed

- With `FullViewFades` on, the game's fade from black into the main menu now shows in VR (the game uses it when it starts by joining a Steam lobby, for example from an invite). The game's own fade was hidden and its replacement was never drawn. The controller layout panel still isn't dimmed by it.
- A controller that reports an unset (all-zero) pointing pose can no longer make weapons aim along a fixed world direction for that frame.

### Changed

- If a game update moves the check behind `MarkerViewAngle`, only that wider view angle is lost; world-space markers, reticles, the aim line and lock-on markers keep working.
- World-space markers, and `PlayersCoverMarkers`, no longer allocate memory every frame, which means fewer garbage collection pauses.
- `SBGVR.dll` no longer records the folder it was built in, so builds of the same commit are identical.

## [1.3.0] - 2026-10-29

### Added

- The main menu and the pause menu show the VR controller layout (`VR HUD` → `MenuControls`, on with `MotionControls`): every button on both controllers and what it does, on a panel just below the floating screen, from the moment the game starts and whenever the pause menu is open. It never covers the menu, and the menu's fades don't dim it. `MenuControlsSide` moves it to the left or right of the screen.

## [1.2.0] - 2026-10-29

### Added

- First person in VR (`VR First Person` → `FirstPerson`, off by default): see the game through your character's eyes. Switch with F5 (`FirstPersonKey`) or, with the controllers, by holding the left grip button and pressing the right thumbstick (`FirstPersonControllerToggle`); the choice is remembered.
  - You look up and down with your head; the thumbstick only turns you, smoothly by default (`FirstPersonTurn` = `Smooth`, or `Snap` with `SnapTurnAngle`).
  - Walking goes where your head looks. Without the controllers, weapons aim where you look.
  - Golf swings aim like in third person: the aim starts where you're looking when you start aiming, then your head doesn't move it, and the thumbstick turns it smoothly, even with snap turning.
  - The motion camera (`MotionCamera`) fine-tunes golf club and rocket club swings in first person too, by turning or moving your controller, as in third person. Tilting the controller does nothing in first person.
  - Your own character is hidden while in first person, as the game does for the railgun's scope. Nothing is sent to other players.
  - The view goes back to third person while you're knocked out, respawning or spectating, and the hole overview plays as usual, each returning to first person afterwards.
  - In a golf cart, the view faces the front of the cart; the thumbstick doesn't turn it.
  - Your eyes are 1.5 m above your character's feet; `FirstPersonEyeOffset` raises or lowers them.

### Changed

- New defaults: `VR Comfort` → `ScreenShake` is `0.25` (a quarter of the game's shake; was `0`), `VR Comfort` → `CameraTransitions` is `Game` (the game's own camera moves; was `Fast`) and `VR HUD` → `PlayersCoverMarkers` is `false` (markers stay on top of player characters; was `true`). A settings file you already have keeps its values; change them there, or delete those lines to get the new defaults.
- Pressing the right thumbstick while holding the left grip button now switches first person instead of dropping your item (pressing it alone still drops it). Turn `FirstPersonControllerToggle` off to keep the old behaviour.
- The weapon aiming patches are now always applied, so first person can aim with your head; while neither controller aiming nor first person applies, weapons aim along the camera exactly as before.

## [1.1.1] - 2026-10-25

### Changed

- `VR Controls` → `MotionControls` is on by default, so VR controllers work as a gamepad, aim weapons and turn the camera while aiming swings without changing any settings (OpenXR only; with OpenVR the controllers still don't work).
- `VR HUD` → `AimLine` is off by default. Turn it on to draw the white line from your hand to the weapon reticle.
- A settings file you already have keeps its values; change them there, or delete those lines to get the new defaults.
- Controller aiming and the motion camera only work while you're playing with the controllers. After you use a gamepad, keyboard or mouse, weapons aim along the camera and the controllers don't turn it, until you use a controller again. Before, a held or awake controller could aim weapons while you played with something else.

### Fixed

- Changing `DominantHand` while the game runs (with a config editor) now switches the aiming controller straight away; it used to keep the old hand until that controller disconnected.
- If setting up OpenXR controller input ever fails, VR still starts, without controllers, and the error is logged. Before, such a failure would have stopped VR from starting.

## [1.1.0] - 2026-10-24

Motion controls are tested on Meta Quest 3 with OpenXR, not yet on Valve Index or Steam Frame. They don't work with OpenVR.

### Added

- Motion controls, off by default: `VR Controls` → `MotionControls`. They work with UUVR's default runtime, OpenXR; with `Preferred VR APi` = `OpenVr` the controllers don't work (a known issue). While VR runs, your VR controllers work like a gamepad, so the game's gamepad controls, menus and your gamepad rebinds all apply; a real gamepad, keyboard and mouse still work. Holding the left grip button turns the right thumbstick into a D-pad and A and B into pause and scoreboard. Pressing the left thumbstick interacts, like X; the golf cart's honk has no controller input. Controllers without face buttons use touchpad clicks. The README lists every button.
- OpenXR reports controllers again. UUVR's copy of the Unity OpenXR plugin left out the code that asks the OpenXR runtime for controller input; SBGVR restores it from the plugin's 1.4.2 source, with the Oculus Touch, Valve Index, HTC Vive, Windows Mixed Reality and Khronos simple controller profiles. It's only used with `MotionControls` on.
- Aiming with your controller: `VR Controls` → `ControllerAiming` (on), `DominantHand` (`Right`) and `ControllerAimPitch` (0). With `MotionControls` on, weapons aim where your dominant hand's controller points while you aim or use them: the dueling pistol, elephant gun, rocket launcher and its lock-on, railgun, freeze bomb and thunderstorm. The world-space reticle follows the same aim, and your character turns to face it when you fire. Golf swings still follow the camera.
- Motion camera: `VR Controls` → `MotionCamera` (on), `MotionCameraStrength` (0.25), `MotionCameraMovement` (2° per 10 cm), `MotionCameraMaxYaw` (30°) and `MotionCameraMaxPitch` (0°). With `MotionControls` on, while you aim and charge a golf club or rocket club swing, turning or moving your dominant controller sideways turns the camera left or right, like the right thumbstick; with `MotionCameraMaxPitch` above 0, tilting it also looks up or down. Your character faces the camera while aiming, so this fine-tunes the swing through the game's own aiming. It stops when you swing.
- `VR Controls` → `MirrorButtons` (off): mirrors the motion controls button layout left to right, for example for left-handed play with `DominantHand` = `Left`. The Menu button stays on the left controller.
- `VR HUD` → `AimLine` (on): while you aim the dueling pistol, elephant gun, rocket launcher or freeze bomb, a thin white line runs from your character's hand to the reticle. It follows both as they move, is drawn at the right depth for each eye, keeps the same thickness near and far, and is hidden by scenery in between.

### Changed

- `VR Comfort` → `FlashFrames` is on by default, so a flash camera blinds you in VR as it does on a flat screen. Turn it off to keep the view clear. A settings file you already have keeps its value; change it there, or delete the line to get the new default.

### Removed

- The F3 key that turned VR off and on. It didn't work in this game, so VR now stays on from when the game starts until it closes. To play on a flat screen, disable SBGVR in your mod manager.

## [1.0.2] - 2026-10-24

### Changed

- Install instructions point to [GitHub Releases](https://github.com/lmkHenlo/SBGVR/releases) instead of Thunderstore.

## [1.0.1] - 2026-10-24

### Changed

- New package icon: a player character from the game.

## [1.0.0] - 2026-10-24

### Changed

- First public release. The mod itself is unchanged from 0.2.17, the last pre-release test build, which players tested on Steam Frame, Valve Index and Meta Quest 3.
- The READMEs no longer call SBGVR experimental, list the tested headsets, and say that MaxFOVSlider 0.2.1 and later work alongside SBGVR (they pause their own projection in VR).
- Install instructions start with installing from Thunderstore; installing a local build is still described.

### Added

- Credits: UUVR's modified copy (version, upstream commit and licence), the testers, and how Claude, an AI model made by Anthropic, was used to develop SBGVR.
- `THIRD_PARTY_NOTICES.md` says what was changed in SBGVR's copy of UUVR, by whom and when, as UUVR's licence (GPL-3.0) asks of modified copies.
- The source is public at <https://github.com/lmkHenlo/SBGVR>, linked from the Thunderstore page, as UUVR's licence asks of anyone distributing the compiled mod. Questions and bug reports can go to its issues.

## [0.2.17] - 2026-10-23

### Removed

- The game's field of view in VR (`VR Graphics` → `FieldOfView` and `RailgunZoom`, added in 0.2.16). Values above 60 stretched the view enough to cause nausea in the headset. The headset sets the field of view again, as in 0.2.15, and the railgun's aim doesn't zoom in VR. Those two settings stay in `com.sbgvr.cfg` until you delete them, but nothing reads them.
- The flat-screen fix from 0.2.16 is undone with it: after turning VR off with F3, the Field of view slider again doesn't change the view.

## [0.2.16] - 2026-10-23

### Added

- The game's field of view in VR. `VR Graphics` → `FieldOfView` (`Setting` by default) makes the game's Field of view slider work in VR, MaxFOVSlider's range included. At the game's default of 60 you get the headset's own view; higher values show more of the world, squeezed into the headset, and lower ones magnify it. `Headset` keeps the headset's view, and `Game` also follows the game's own changes, such as the overview camera. The floating screen, markers and reticles keep their size, and icons turn into edge arrows at the new edge of the view.
- `VR Graphics` → `RailgunZoom` (on by default): the railgun's aim zooms the view in VR, as it does on a flat screen. While the view is magnified, the eyes are drawn closer together by the same amount, so it stays easy to focus.

### Fixed

- After turning VR off with F3, the Field of view slider didn't change the view on the flat screen. UUVR blocked every field of view change, not only while VR runs.

## [0.2.15] - 2026-10-23

### Added

- This changelog.

### Changed

- The project is renamed from SBG VR to SBGVR, without a space:
  - The Thunderstore package is `SBGVR` (it was `SBG_VR`), and the zip is `lmkHenlo-SBGVR-<version>.zip`.
  - The plugin is `SBGVR.dll` (it was `SbgVr.dll`), and the build file is `SBGVR.proj` (it was `SBG_VR.proj`).
  - The plugin ID `com.sbgvr` and the settings file `com.sbgvr.cfg` are unchanged, so your settings carry over.
  - Remove the old `SBG_VR` mod before installing this one, so the game doesn't end up with two copies.
- The package description is shorter and matches the README's.
- The README follows the [Standard Readme](https://github.com/RichardLitt/standard-readme) specification. It now has install and usage examples, how to update and uninstall, and how to contribute.

## [0.2.14] - 2026-10-23

### Fixed

- Weapon reticles and the orange lock-on reticle could be partly hidden. Player characters in front of them covered them, including your own character in third person. With `MarkersOnTop` off, they also sank into the surface they sit on. They're now always drawn on top of scenery, characters and effects. Menus and fades still cover them.

## [0.2.13] - 2026-10-23

### Added

- The orange lock-on reticle (homing shots from a full-power swing, and the rocket launcher) is drawn in the world on its target, instead of doubled on the floating screen. So are the airhorn's target indicators, the Jumbo Burger giant-form timer and pop-up text. When the game stops updating one of them, it goes back to the HUD and is hidden there, rather than staying where it was in the world.

## [0.2.12] - 2026-10-23

### Added

- Weapon reticles are drawn in the world at the point you're aiming at, instead of doubled on the floating screen. This covers the dueling pistol, elephant gun, rocket launcher, freeze bomb, flash camera, thunderstorm and railgun reticles, and the "can't use here" one. The reticle sits where the aim meets something: 50 m away when it meets nothing, and always between 2 m and 150 m. It glides between distances and keeps its floating-screen size.
- `VR HUD` → `WorldSpaceReticles` (on by default) turns this off.

## [0.2.11] - 2026-10-23

### Fixed

- Player characters and their faces flickered, and parts of them disappeared, while markers moved behind them. Characters are drawn once again, as usual. They now cover the markers behind them through a stencil mask instead of being drawn a second time.

## [0.2.10] - 2026-10-23

### Added

- Diagnostics log the game's RenderObjects renderer feature: when it draws, what it draws and its depth settings.

### Fixed

- Item effects showed through menus on the floating screen and through full-view fades. The floating screen (a change to UUVR) and the fades are now drawn after every other see-through object.

## [0.2.9] - 2026-10-23

### Added

- Known issue in both READMEs: how to stop the SteamVR menu opening when the game starts or a match begins, by turning off Steam's Desktop Game Theatre for the game.

### Fixed

- See-through parts of player characters, such as the shield on the chest, flickered while markers moved behind the character.

## [0.2.8] - 2026-10-23

### Fixed

- Distance labels on markers still drew over the player characters in front of them.

## [0.2.7] - 2026-10-23

### Fixed

- Player characters didn't cover the ball and tee markers, or marker text, behind them.
- A ball's icon and name tag weren't covered by the ball's own player. Only a player's own name tag and teammate icons are exempt now.

## [0.2.6] - 2026-10-23

### Added

- `VR HUD` → `PlayersCoverMarkers` (on by default): a player character in front of a marker covers the part of the marker behind it, while scenery still doesn't hide markers. A name tag or teammate icon isn't covered by the player it belongs to.

### Removed

- `VR HUD` → `HideMarkersOverPlayers`, replaced by `PlayersCoverMarkers`. Markers stay visible instead of fading out.

## [0.2.5] - 2026-10-23

### Added

- `VR HUD` → `HideMarkersOverPlayers` (on by default): a marker fades out while it's drawn over a player character, and back in afterwards. Markers for targets behind a player no longer cover that player. A name tag or teammate icon isn't hidden by the player it belongs to.

## [0.2.4] - 2026-10-23

### Added

- `VR Graphics` → `AmbientOcclusion` (on by default). Turn it off for a higher frame rate in VR.

### Changed

- Ambient occlusion is drawn in VR again, correctly for each eye: at the eye's size, with its off-centre view. If the fix can't be applied (after a game update, say), ambient occlusion stays off in VR, as in 0.2.3.

## [0.2.3] - 2026-10-22

### Fixed

- Black areas covered nearby scenery and your character, moving with you and the headset. The game's ambient occlusion was sized for the game window rather than each eye, so it's now off in VR.

## [0.2.2] - 2026-10-22

### Fixed

- Markers, including interaction prompts such as Set Up Match, showed through menus on the floating screen, such as Match Setup.

## [0.2.1] - 2026-10-22

### Fixed

- The pause menu, and the settings opened from it, were blurred and unreadable in VR. The game's menu blur is now off while VR runs.

## [0.2.0] - 2026-10-22

### Added

- `VR Comfort` settings, which only apply while VR runs:
  - `ScreenShake`: camera shake as a share of the game's own setting. 0 by default: no shake.
  - `ImpactFrames` and `FlashFrames`: the game's full-screen flashes, off by default. The CRT effect is always off in VR.
  - `CameraTransitions`: camera moves between views play as in the game (`Game`), in about a third of the time (`Fast`, the default) or as a cut (`Instant`).
  - `MenuBackground`: `Still` (the default) holds the main menu's background scenes still; `Moving` keeps the game's pans.
  - `FullViewFades` (on by default): the game's fades darken your whole view, not just the floating screen.
  - `RecenterKey` (F4 by default): turns the view so the direction you're facing becomes straight ahead.
- World-space markers: icons (your ball, the hole), name tags and the interaction reticle are drawn in the world at their target, at the right depth for each eye, instead of doubled on the floating screen. Their settings are in `VR HUD`: `WorldSpaceMarkers`, `MarkersOnTop`, `MarkerScale` and `MarkerViewAngle`.
- Each SBG VR feature is applied on its own. If one fails to load (after a game update, say), it's turned off and logged, and the rest keep working.

### Fixed

- The main menu disappeared from the headset when its background scene changed. UUVR kept following a camera that had stopped rendering.
- UUVR rebuilt itself while the game was quitting.

## [0.1.0] - 2026-10-22

### Added

- First test build. It packages Raicuparta's Universal Unity VR (UUVR) v0.4.0 as a BepInEx 5 Thunderstore mod for Super Battle Golf, built against the game's own assemblies. It starts VR with the game (F3 turns VR off and on), follows the headset, and shows the game's 2D interface on a floating screen.
- Changes to UUVR for this game:
  - Its Canvas redirect UI mode works with the game's UI library.
  - Its patcher removes leftover copies of `ucrtbased.dll` and `OVRPlugin.dll` from the game folder.
  - The package includes only the 64-bit VR libraries the game loads.
- The `com.sbgvr` plugin, which logs VR, camera, renderer and frame rate details for bug reports. The `Diagnostics` → `Enabled` and `PerformanceLogInterval` settings control it.
- `build.ps1` and `SBG_VR.proj`, which build everything and make the Thunderstore zip.
- `THIRD_PARTY_NOTICES.md`, with the licences of the bundled VR libraries.
