# SBGVR test plan

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
    - Every button in the [Motion controls](README.md#motion-controls) table does what it says, in a match, the golf cart, spectating, the emote wheel and the menus (with the gamepad cursor).
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
