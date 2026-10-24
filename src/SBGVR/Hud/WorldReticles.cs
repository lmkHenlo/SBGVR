// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SBGVR.Hud
{
    /// <summary>
    /// VR HUD.WorldSpaceReticles: draws the weapon reticles in the world at the point being aimed at, instead
    /// of on UUVR's floating screen 1 m away, where each eye saw them in a different place against a target
    /// tens of metres off (a cross-eyed reticle).
    ///
    /// Items aim straight out from the game camera, and their reticles sit at the centre of the game's
    /// screen, so only the reticle's distance changes: each frame a ray goes from the camera along its
    /// forward, past the local player's own character, and the reticle is drawn where it hits, or at
    /// <see cref="SkyDistance"/> if it hits nothing. The distance glides to a new value rather than jumping,
    /// so the eyes can follow when the aim sweeps from something near to something far. The reticle keeps its
    /// size on the floating screen and is drawn like a world-space marker (<see cref="WorldMarker"/>).
    /// </summary>
    [DefaultExecutionOrder(30000)]
    internal sealed class WorldReticles : MonoBehaviour
    {
        private const float SkyDistance = 50f;
        private const float MinimumDistance = 2f;
        private const float MaximumDistance = 150f;
        private const float MaximumRayDistance = 500f;

        /// <summary>Roughly how long the reticle takes to reach a new distance, in seconds.</summary>
        private const float GlideSeconds = 0.12f;

        private static readonly AccessTools.FieldRef<ReticleManager, GameObject> DisallowedReticle =
            AccessTools.FieldRefAccess<ReticleManager, GameObject>("disallowedReticle");

        private static readonly string[] ReticleFields =
        {
            "duelingPistolReticle", "elephantGunReticle", "rocketLauncherReticle", "freezeBombReticle",
            "flashCameraReticle", "thunderstormReticle", "railgunNonZoomReticle", "railgunZoomReticle",
        };

        private static readonly AccessTools.FieldRef<ReticleManager, Reticle>[] Reticles = CreateReticleRefs();

        /// <summary>
        /// Whether a weapon reticle (not the "can't use here" one) is drawn in the world this frame, and where.
        /// Read by <see cref="AimLine"/>.
        /// </summary>
        internal static bool AimShowing { get; private set; }

        internal static Vector3 AimPoint { get; private set; }

        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly List<Component> _reticles = new List<Component>();
        private int _weaponReticleCount;
        private float _logDistance;
        private bool _wasShowing;

        private static AccessTools.FieldRef<ReticleManager, Reticle>[] CreateReticleRefs()
        {
            var refs = new AccessTools.FieldRef<ReticleManager, Reticle>[ReticleFields.Length];
            for (var i = 0; i < refs.Length; i++) refs[i] = AccessTools.FieldRefAccess<ReticleManager, Reticle>(ReticleFields[i]);
            return refs;
        }

        private void LateUpdate()
        {
            _reticles.Clear();
            if (SingletonBehaviour<ReticleManager>.HasInstance)
            {
                var manager = SingletonBehaviour<ReticleManager>.Instance;
                foreach (var reticle in Reticles)
                {
                    var component = reticle(manager);
                    if (component != null) _reticles.Add(component);
                }

                _weaponReticleCount = _reticles.Count;
                var disallowed = DisallowedReticle(manager);
                if (disallowed != null) _reticles.Add(disallowed.transform);
            }

            var enabled = WorldMarker.Enabled && Settings.WorldSpaceReticles.Value;
            var camera = enabled ? Fades.FullViewFade.ViewCamera() : null;
            var showing = false;
            var weaponShowing = false;
            for (var i = 0; i < _reticles.Count; i++)
            {
                if (camera == null || !_reticles[i].gameObject.activeSelf) continue;
                showing = true;
                if (i < _weaponReticleCount) weaponShowing = true;
            }

            var point = Vector3.zero;
            if (showing)
            {
                // Weapons aim along the camera, or along the controller with VR Controls.ControllerAiming.
                if (!Controls.ControllerAim.TryGetAimRay(out var origin, out var direction))
                {
                    origin = camera.transform.position;
                    direction = camera.transform.forward;
                }
                direction.Normalize();

                var target = Mathf.Log(AimDistance(origin, direction));
                _logDistance = _wasShowing
                    ? Mathf.Lerp(_logDistance, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / GlideSeconds))
                    : target;
                point = origin + direction * Mathf.Exp(_logDistance);
            }

            _wasShowing = showing;
            AimShowing = weaponShowing;
            AimPoint = point;
            foreach (var reticle in _reticles)
                WorldMarker.Place(reticle, point, showing && reticle.gameObject.activeSelf, reticle: true);
        }

        /// <summary>How far along the aim it meets something solid, past the local player.</summary>
        private float AimDistance(Vector3 origin, Vector3 direction)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, MaximumRayDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            var localPlayer = GameManager.LocalPlayerInfo;
            var nearest = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= nearest || hit.collider == null) continue;
                if (localPlayer != null && hit.collider.transform.IsChildOf(localPlayer.transform)) continue;
                nearest = hit.distance;
            }

            var distance = nearest < float.MaxValue ? nearest : SkyDistance;
            return Mathf.Clamp(distance, MinimumDistance, MaximumDistance);
        }
    }
}
