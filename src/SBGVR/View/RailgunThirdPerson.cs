// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace SBGVR.View
{
    /// <summary>
    /// In VR third person, the railgun aims from the third-person camera like the other guns, instead of the game
    /// switching to its scope (its first-person camera, zoomed) while you hold aim. SBGVR's first person and a flat
    /// screen keep the scope.
    /// </summary>
    /// <remarks>
    /// The game opens the scope when <c>PlayerInventory.UpdateIsInFirstPersonAim</c> finds its check true: aiming
    /// the railgun, holding aim, and not firing. The check is a local function whose compiler-made name ends in
    /// numbers that change when the game's code does ("&lt;UpdateIsInFirstPersonAim&gt;g__ShouldBeInFirstPersonAim|279_0"),
    /// so it's found by the rest of its name. Without the scope the game aims and fires the railgun as it does
    /// before the scope opens: the non-zoom reticle, the third-person camera, the same shot.
    /// </remarks>
    [HarmonyPatch]
    internal static class RailgunThirdPersonPatches
    {
        private const string LocalFunctionName = ">g__ShouldBeInFirstPersonAim|";

        /// <summary>Whether the patch applied (set by <see cref="Plugin"/>).</summary>
        internal static bool Applied;

        /// <summary>Whether the railgun aims in third person right now, rather than opening its scope.</summary>
        internal static bool AimsInThirdPerson => Applied && VrState.IsRunning && !FirstPersonView.Owns;

        private static MethodBase TargetMethod() =>
            AccessTools.GetDeclaredMethods(typeof(PlayerInventory))
                .FirstOrDefault(m => m.Name.Contains(LocalFunctionName) && m.ReturnType == typeof(bool))
            ?? throw new MissingMethodException(nameof(PlayerInventory), "<...>g__ShouldBeInFirstPersonAim|...");

        private static void Postfix(PlayerInventory __instance, ref bool __result)
        {
            if (__result && __instance.isLocalPlayer && VrState.IsRunning && !FirstPersonView.Owns) __result = false;
        }
    }
}
