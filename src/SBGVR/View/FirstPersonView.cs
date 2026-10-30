// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using UnityEngine;
using UnityEngine.InputSystem;
using Uuvr.VrCamera;

namespace SBGVR.View
{
    internal enum TurnMode
    {
        Snap,
        Smooth,
    }

    /// <summary>
    /// VR First Person: shows the game through the local player's eyes in VR, with the game's own first-person
    /// camera module (the one its railgun scope uses).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game's <see cref="FirstPersonCameraModule"/> sits at <c>viewer.TransformPoint(localPosition)</c> and
    /// looks along its own yaw and pitch. Its viewer becomes an upright anchor at the character's eye height
    /// (<see cref="EyeHeight"/> times the character's scale), so dives and knockback don't swing the view
    /// around. In VR its pitch stays level and your head does the looking up and down
    /// (<see cref="FirstPersonPatches"/>). In a golf cart, it faces the front of the cart and the stick doesn't
    /// turn it.
    /// </para>
    /// <para>
    /// The game already moves the character relative to this camera, turns it to face the camera while
    /// aiming, reads the right stick into it, and hides the local player's own body, head and held item
    /// while it's showing. SBGVR adds the toggle, keeps the view in first person when the game asks for the
    /// third-person camera (<see cref="ShouldRedirectToFirstPerson"/>), steps back to third person while
    /// you're knocked out, respawning or spectating, and makes walking and aiming follow your head.
    /// </para>
    /// <para>Nothing is sent over the network: other players see your character as usual.</para>
    /// </remarks>
    internal sealed class FirstPersonView : MonoBehaviour
    {
        /// <summary>Time constant for following eye height changes (the Jumbo Burger's growth), in seconds.</summary>
        private const float EyeHeightSmoothing = 0.2f;

        /// <summary>
        /// Eye height above the feet at normal size, in metres: about 0.2 m above the head bone, where the eyes
        /// are. (The game's railgun scope uses 1 m, which felt too low.)
        /// </summary>
        private const float EyeHeight = 1.5f;

        /// <summary>Snap turning: the stick turns you once past this, and again only after going back under the reset.</summary>
        private const float SnapThreshold = 0.7f;
        private const float SnapReset = 0.3f;

        private static Transform _anchor;
        private static FirstPersonCameraModule _anchoredModule;
        private static bool _owns;
        private static float _eyeHeight;
        private static bool _snapArmed = true;
        private static string _lastStatus;
        private static bool _loggedEyeHeight;
        private static bool _wasAimingShot;

        /// <summary>
        /// The camera is in first person because of the toggle (not only for the railgun scope). The first-person
        /// patches only change anything while this is true.
        /// </summary>
        internal static bool Owns => _owns;

        /// <summary>Whether <paramref name="module"/> is the first-person camera SBGVR is showing right now.</summary>
        internal static bool Shows(FirstPersonCameraModule module) =>
            _owns && module != null && ReferenceEquals(module, _anchoredModule);

        /// <summary>Switches the setting, from the keyboard key or the controllers. VR only.</summary>
        internal static void Toggle()
        {
            if (!VrState.IsRunning) return;
            Settings.FirstPerson.Value = !Settings.FirstPerson.Value;
            Plugin.Log.LogInfo(Settings.FirstPerson.Value ? "First person: on." : "First person: off.");
        }

        private void OnEnable()
        {
            VrState.Changed += OnVrChanged;
        }

        private void OnDisable()
        {
            VrState.Changed -= OnVrChanged;
        }

        private static void OnVrChanged(bool running)
        {
            _snapArmed = true;
        }

        private void Update()
        {
            HandleKey();
            Refresh();
            StartShotAimWhereLooking();
        }

        /// <summary>
        /// When you start aiming a swing in first person, the aim starts where you're looking: the camera's yaw
        /// (which the shot follows, <see cref="IsAimingShot"/>) turns to your head's direction, and UUVR's recentre
        /// rotation turns back by the same amount, so the world you see doesn't move. After that, only the stick
        /// or mouse turns the aim.
        /// </summary>
        private static void StartShotAimWhereLooking()
        {
            var aiming = _owns && IsAimingShot();
            var started = aiming && !_wasAimingShot;
            _wasAimingShot = aiming;
            if (!started) return;

            var module = _anchoredModule;
            if (!Shows(module) || !SBGVR.Controls.ControllerAim.TryGetHeadPose(out _, out var head)) return;

            var turn = Mathf.DeltaAngle(module.Yaw, HorizontalYaw(head));
            if (Mathf.Abs(turn) < 0.1f) return;

            module.SetYaw(module.Yaw + turn);
            VrCameraOffset.RecenterRotation = Quaternion.Euler(0f, -turn, 0f) * VrCameraOffset.RecenterRotation;
            if (Settings.DiagnosticsEnabled.Value)
                Plugin.Log.LogInfo($"First person: shot aim starts where you're looking ({turn:+0;-0;0}° from the view's forward).");
        }

        private static void HandleKey()
        {
            if (!VrState.IsRunning) return;

            var key = Settings.FirstPersonKey.Value;
            if (key == Key.None) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var control = keyboard[key];
            if (control != null && control.wasPressedThisFrame) Toggle();
        }

        /// <summary>Enters, leaves or keeps first person, depending on the toggle and what the player is doing.</summary>
        private static void Refresh()
        {
            var module = CameraModuleController.CurrentModuleType;

            // Something else took the camera (the hole overview, photo mode, a new scene).
            if (_owns && module != CameraModuleType.FirstPerson) _owns = false;

            var wanted = Settings.FirstPerson.Value;
            var allowed = IsAllowed(out var reason);
            LogStatus(wanted, allowed, reason);

            if (wanted && allowed)
            {
                if (!_owns && !CameraModuleController.IsTransitioning)
                {
                    if (module == CameraModuleType.Orbit) Enter();
                    else if (module == CameraModuleType.FirstPerson) Adopt(); // The railgun scope is already in first person.
                }
            }
            else if (_owns && !InRailgunScope())
            {
                // Leaving waits for the railgun scope to end, which the game handles itself.
                Exit();
            }

            if (!_owns && _anchoredModule != null && !CameraModuleController.IsTransitioning) ReleaseViewer();
            if (!_owns) _snapArmed = true;
        }

        /// <summary>
        /// Whether first person can show right now: VR is running, the local player is in the game, and isn't
        /// spectating, knocked out, respawning or drowning.
        /// </summary>
        private static bool IsAllowed(out string reason)
        {
            reason = null;
            if (!VrState.IsRunning) return false;

            var player = GameManager.LocalPlayerInfo;
            var movement = GameManager.LocalPlayerMovement;
            if (player == null || movement == null) return false;
            if (!CameraModuleController.TryGetFirstPersonModule(out var module) || module == null) return false;

            var spectator = GameManager.LocalPlayerAsSpectator;
            if (spectator != null && spectator.IsSpectating)
            {
                reason = "spectating";
                return false;
            }

            if (movement.IsKnockedOutOrRecovering)
            {
                reason = "knocked out";
                return false;
            }

            if (movement.IsRespawningOrDrowning)
            {
                reason = "respawning";
                return false;
            }

            return true;
        }

        /// <summary>Logs only when the reason changes, not every frame.</summary>
        private static void LogStatus(bool wanted, bool allowed, string reason)
        {
            var status = wanted && !allowed && reason != null ? reason : null;
            if (status == _lastStatus) return;
            var previous = _lastStatus;
            _lastStatus = status;
            if (status != null) Plugin.Log.LogInfo($"First person: third person while {status}.");
            else if (wanted && previous != null) Plugin.Log.LogInfo("First person: back in first person.");
        }

        private static bool InRailgunScope()
        {
            var inventory = GameManager.LocalPlayerInventory;
            return inventory != null && inventory.IsInFirstPersonAim;
        }

        private static void Enter()
        {
            if (!CameraModuleController.TryGetFirstPersonModule(out var module) || module == null) return;
            Prepare(module, true);
            _owns = true;
            GameplayCameraManager.TransitionTo(CameraModuleType.FirstPerson);
        }

        private static void Adopt()
        {
            if (!CameraModuleController.TryGetFirstPersonModule(out var module) || module == null) return;
            Prepare(module, false);
            _owns = true;
        }

        private static void Exit()
        {
            _owns = false;
            if (CameraModuleController.TryGetFirstPersonModule(out var module) && module != null &&
                CameraModuleController.TryGetOrbitModule(out var orbit) && orbit != null)
            {
                // The camera's own yaw, not your head's: the headset adds your head's turn on top of either
                // camera, so the world stays where it is.
                orbit.SetYaw(module.Yaw);
                orbit.ForceUpdateModule();
            }

            GameplayCameraManager.TransitionTo(CameraModuleType.Orbit);
        }

        /// <summary>
        /// Called when the game asks for the third-person camera (after the hole overview, when the railgun
        /// scope ends): whether to show first person instead. Gets the first-person camera ready if so.
        /// </summary>
        internal static bool ShouldRedirectToFirstPerson()
        {
            if (!Settings.FirstPerson.Value || !IsAllowed(out _)) return false;
            if (!CameraModuleController.TryGetFirstPersonModule(out var module) || module == null) return false;

            if (!_owns) Prepare(module, true);
            _owns = true;
            return true;
        }

        /// <summary>Points the first-person camera at the eye anchor, facing the way the third-person camera does.</summary>
        private static void Prepare(FirstPersonCameraModule module, bool fromOrbit)
        {
            if (fromOrbit)
            {
                if (CameraModuleController.TryGetOrbitModule(out var orbit) && orbit != null) module.SetYaw(orbit.Yaw);
                module.CancelFovOverrideInstantly();
            }

            module.SetPitch(0f);
            module.SetRoll(0f);
            UpdateAnchor(true);
            module.SetViewer(Anchor());
            module.SetLocalPosition(Vector3.zero);
            _anchoredModule = module;
            _snapArmed = true;
        }

        /// <summary>Gives the first-person camera back to the game (the player's own transform as its viewer).</summary>
        private static void ReleaseViewer()
        {
            var module = _anchoredModule;
            _anchoredModule = null;
            var player = GameManager.LocalPlayerInfo;
            if (module != null && player != null) module.SetViewer(player.transform);
        }

        /// <summary>
        /// Run just before the first-person camera places itself each frame: moves the anchor to the eyes and
        /// keeps the camera on it (the game resets the viewer when the player respawns, and the railgun scope
        /// raises its position). Pitch and roll stay level while SBGVR shows first person.
        /// </summary>
        internal static void BeforeModuleUpdate(FirstPersonCameraModule module)
        {
            if (module == null || !ReferenceEquals(module, _anchoredModule)) return;

            UpdateAnchor(false);
            module.SetViewer(_anchor);
            module.SetLocalPosition(Vector3.zero);
            if (!_owns) return;
            module.SetPitch(0f);
            module.SetRoll(0f);
            if (TryGetCart(out var cart)) module.SetYaw(HorizontalYaw(cart.rotation));
        }

        /// <summary>Whether the local player is in a golf cart, and the cart's transform (its position and facing).</summary>
        /// <remarks>
        /// The game places the camera before <c>PlayerMovement.LateUpdate</c> snaps the player onto the cart
        /// (<c>FollowGolfCart</c> sets the player to the cart's <see cref="GolfCartMovement"/> pose every frame),
        /// so the player's own transform is still where the cart was last frame. Reading the cart itself keeps
        /// the view's position and direction on the cart this frame, with no lag through turns.
        /// </remarks>
        private static bool TryGetCart(out Transform cart)
        {
            cart = null;
            var player = GameManager.LocalPlayerInfo;
            if (player == null) return false;

            var seat = player.ActiveGolfCartSeat;
            if (!seat.IsValid() || seat.golfCart == null) return false;

            var movement = seat.golfCart.Movement;
            cart = movement != null ? movement.transform : seat.golfCart.transform;
            return true;
        }

        /// <summary>In a golf cart the view faces the cart's front, so the stick and mouse don't turn it.</summary>
        internal static bool InGolfCart() => _owns && TryGetCart(out _);

        private static void UpdateAnchor(bool snap)
        {
            var player = GameManager.LocalPlayerInfo;
            if (player == null) return;

            var root = player.transform;
            var movement = player.Movement;
            var scale = movement != null ? movement.CharacterScale : 1f;
            var target = EyeHeight * scale * root.lossyScale.y + Settings.FirstPersonEyeOffset.Value;
            _eyeHeight = snap
                ? target
                : Mathf.Lerp(_eyeHeight, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / EyeHeightSmoothing));
            // In a golf cart, the cart's pose this frame (the player is moved onto it only after the camera).
            var feet = TryGetCart(out var cart) ? cart.position : root.position;
            Anchor().SetPositionAndRotation(feet + Vector3.up * _eyeHeight, Quaternion.identity);

            if (snap) LogEyeHeight(player, scale);
        }

        private static void LogEyeHeight(PlayerInfo player, float scale)
        {
            if (_loggedEyeHeight || !Settings.DiagnosticsEnabled.Value) return;
            _loggedEyeHeight = true;

            var head = player.HeadBone;
            var headHeight = head != null ? $"{head.position.y - player.transform.position.y:0.00} m" : "unknown";
            Plugin.Log.LogInfo($"First person: eyes {_eyeHeight:0.00} m above the feet ({EyeHeight:0.00} m × scale {scale:0.##}, offset {Settings.FirstPersonEyeOffset.Value:0.00} m); " +
                               $"head bone {headHeight} above the feet.");
        }

        private static Transform Anchor()
        {
            if (_anchor != null) return _anchor;

            var anchor = new GameObject("SBGVR First Person Eyes");
            anchor.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(anchor);
            _anchor = anchor.transform;
            return _anchor;
        }

        /// <summary>
        /// While the local player aims, charges or plays a golf swing (golf club, rocket club, or the Jumbo
        /// Burger's giant swing). Shots then aim like in third person: along the first-person camera's own yaw,
        /// which only the stick or mouse turns, not your head.
        /// </summary>
        internal static bool IsAimingShot()
        {
            var golfer = GameManager.LocalPlayerAsGolfer;
            return golfer != null && (golfer.IsAimingSwing || golfer.IsChargingSwing || golfer.IsSwinging);
        }

        /// <summary>
        /// The yaw the game should treat as the first-person camera's: where your head is facing while SBGVR shows
        /// first person (so walking and weapon aiming follow your head), otherwise, and while aiming a shot
        /// (<see cref="IsAimingShot"/>), the camera's own.
        /// </summary>
        internal static float YawOf(FirstPersonCameraModule module)
        {
            if (Shows(module) && !IsAimingShot() && SBGVR.Controls.ControllerAim.TryGetHeadPose(out _, out var rotation))
                return HorizontalYaw(rotation);
            return module.Yaw;
        }

        /// <summary>
        /// Your head's ray while SBGVR shows first person: weapons aim along it without controllers. Not while
        /// aiming a shot, so releasing a swing (which turns the character to the camera) ignores your head.
        /// </summary>
        internal static bool TryGetHeadAimRay(out Vector3 origin, out Vector3 direction)
        {
            direction = default;
            if (!Shows(_anchoredModule) || IsAimingShot() ||
                !SBGVR.Controls.ControllerAim.TryGetHeadPose(out origin, out var rotation))
            {
                origin = default;
                return false;
            }

            direction = rotation * Vector3.forward;
            return true;
        }

        /// <summary>
        /// The compass direction of a rotation. Looking almost straight down or up, the forward direction has
        /// no useful heading, so the top of the head (down) or the chin (up) gives it.
        /// </summary>
        private static float HorizontalYaw(Quaternion rotation)
        {
            var forward = rotation * Vector3.forward;
            var flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.04f)
            {
                var up = rotation * Vector3.up;
                flat = forward.y < 0f ? new Vector3(up.x, 0f, up.z) : new Vector3(-up.x, 0f, -up.z);
            }

            return Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Snap turning from the thumbstick's sideways value: a turn of SnapTurnAngle each time it's pushed past
        /// the threshold, then nothing until it comes back towards the middle.
        /// </summary>
        internal static void SnapTurn(FirstPersonCameraModule module, float stickX)
        {
            var amount = Mathf.Abs(stickX);
            if (amount < SnapReset)
            {
                _snapArmed = true;
                return;
            }

            if (!_snapArmed || amount < SnapThreshold) return;
            _snapArmed = false;
            module.SetYaw(module.Yaw + Mathf.Sign(stickX) * Settings.SnapTurnAngle.Value);
        }
    }
}
