// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using UnityEngine;

namespace SBGVR.Controls
{
    /// <summary>Angles of a direction, as <see cref="MotionCamera"/> and <see cref="WeaponMotionCamera"/> use them.</summary>
    internal static class AimAngles
    {
        /// <summary>The direction flattened onto the ground and normalised; forward if it points straight up or down.</summary>
        internal static Vector3 Horizontal(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }

        /// <summary>Compass heading in degrees: 0 along +z, 90 along +x.</summary>
        internal static float HeadingOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        /// <summary>Degrees above the horizon.</summary>
        internal static float TiltOf(Vector3 forward) => Mathf.Asin(Mathf.Clamp(forward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
    }
}
