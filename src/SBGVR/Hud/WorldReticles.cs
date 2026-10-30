// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

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
        /// Whether a weapon reticle that gets an aim line (see <see cref="DrawsAimLine"/>; never the "can't use here" one) is
        /// drawn in the world this frame, and where.
        /// Read by <see cref="AimLine"/>.
        /// </summary>
        internal static bool AimShowing { get; private set; }

        internal static Vector3 AimPoint { get; private set; }

        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly List<Component> _reticles = new List<Component>();
        /// <summary>For each entry in <see cref="_reticles"/>: whether <see cref="AimLine"/> draws a line to it.</summary>
        private readonly List<bool> _drawsAimLine = new List<bool>();
        private float _logDistance;
        private bool _wasShowing;

        /// <summary>
        /// Whether the aim line draws to this reticle: the dueling pistol, elephant gun, rocket launcher and
        /// freeze bomb, but not the railgun, thunderstorm or flash camera.
        /// </summary>
        private static bool DrawsAimLine(string reticleField) =>
            !reticleField.StartsWith("railgun") && reticleField != "thunderstormReticle" &&
            reticleField != "flashCameraReticle";

        private static AccessTools.FieldRef<ReticleManager, Reticle>[] CreateReticleRefs()
        {
            var refs = new AccessTools.FieldRef<ReticleManager, Reticle>[ReticleFields.Length];
            for (var i = 0; i < refs.Length; i++) refs[i] = AccessTools.FieldRefAccess<ReticleManager, Reticle>(ReticleFields[i]);
            return refs;
        }

        private void LateUpdate()
        {
            _reticles.Clear();
            _drawsAimLine.Clear();
            if (SingletonBehaviour<ReticleManager>.HasInstance)
            {
                var manager = SingletonBehaviour<ReticleManager>.Instance;
                for (var i = 0; i < Reticles.Length; i++)
                {
                    var component = Reticles[i](manager);
                    if (component == null) continue;
                    _reticles.Add(component);
                    _drawsAimLine.Add(DrawsAimLine(ReticleFields[i]));
                }

                var disallowed = DisallowedReticle(manager);
                if (disallowed != null)
                {
                    _reticles.Add(disallowed.transform);
                    _drawsAimLine.Add(false);
                }
            }

            var enabled = WorldMarker.Enabled && Settings.WorldSpaceReticles.Value;
            var camera = enabled ? Fades.FullViewFade.ViewCamera() : null;
            var showing = false;
            var weaponShowing = false;
            for (var i = 0; i < _reticles.Count; i++)
            {
                if (camera == null || !_reticles[i].gameObject.activeSelf) continue;
                showing = true;
                if (_drawsAimLine[i]) weaponShowing = true;
            }

            var point = Vector3.zero;
            if (showing)
            {
                // Weapons aim along the camera, along the controller with VR Controls.ControllerAiming, or along
                // your head in first person.
                if (!Controls.ControllerAim.TryGetWeaponRay(out var origin, out var direction))
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
            foreach (var reticle in _reticles)
                WorldMarker.Place(reticle, point, showing && reticle.gameObject.activeSelf, reticle: true);

            AimShowing = weaponShowing;
            AimPoint = point;
            if (!weaponShowing) return;

            // The line goes to the crosshair's middle as drawn. Some reticles (the freeze bomb's) aren't drawn
            // centred on their own origin, which is what sits on the aim point.
            for (var i = 0; i < _reticles.Count; i++)
            {
                if (!_drawsAimLine[i] || !_reticles[i].gameObject.activeSelf) continue;
                if (TryGetDrawnCentre(_reticles[i].transform, out var centre)) AimPoint = centre;
                LogCentreOffset(_reticles[i], point);
                break;
            }
        }

        private static readonly List<Graphic> Graphics = new List<Graphic>();
        private static readonly Vector3[] Corners = new Vector3[4];
        private readonly HashSet<string> _loggedCentres = new HashSet<string>();

        /// <summary>The middle of everything the reticle draws, in world space.</summary>
        private static bool TryGetDrawnCentre(Transform reticle, out Vector3 centre)
        {
            centre = default;
            var bounds = new Bounds();
            var any = false;
            reticle.GetComponentsInChildren(false, Graphics);
            foreach (var graphic in Graphics)
            {
                if (!graphic.enabled || graphic.color.a <= 0f) continue;
                graphic.rectTransform.GetWorldCorners(Corners);
                foreach (var corner in Corners)
                {
                    if (!any) bounds = new Bounds(corner, Vector3.zero);
                    else bounds.Encapsulate(corner);
                    any = true;
                }
            }

            Graphics.Clear();
            if (any) centre = bounds.center;
            return any;
        }

        /// <summary>Logs once per reticle how far its drawn middle sits from its origin, for bug reports.</summary>
        private void LogCentreOffset(Component reticle, Vector3 origin)
        {
            if (!Settings.DiagnosticsEnabled.Value || !_loggedCentres.Add(reticle.name)) return;
            var scale = reticle.transform.lossyScale.y;
            var offset = scale > 0f ? reticle.transform.InverseTransformVector(AimPoint - origin) : Vector3.zero;
            Plugin.Log.LogInfo($"[diag] Aim line: '{reticle.name}' is drawn centred {offset.x:0.#}, {offset.y:0.#} " +
                               "HUD units from its origin; the line ends at the drawn centre.");
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
