// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace SBGVR.Hud
{
    /// <summary>
    /// Places the world-space weapon reticles (<see cref="WorldReticles"/>) and then the aim line
    /// (<see cref="AimLine"/>) in the game's last late-update stage, BUpdate's OnPostPostLateBUpdate. By then the
    /// game has moved its camera for the frame (OnPostLateBUpdate), chosen which reticle shows
    /// (PlayerInventory) and laid out its HUD, so both are placed from this frame's view.
    /// </summary>
    /// <remarks>
    /// BUpdate runs a stage's callbacks from the last registered to the first. This one registers while SBGVR
    /// loads, before the game's own objects, so it runs after all of them. If it can't register, the two
    /// components place themselves in LateUpdate as before, which can be a frame behind the game's camera.
    /// </remarks>
    internal sealed class AfterGameLayout : IPostPostLateBUpdateCallback
    {
        private static AfterGameLayout _instance;
        private static bool _checkedOrder;

        internal static WorldReticles Reticles;
        internal static AimLine Line;

        /// <summary>Whether the reticles and aim line are placed here rather than in their own LateUpdate.</summary>
        internal static bool Registered => _instance != null;

        internal static void Register()
        {
            try
            {
                RegisterCallback();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("World-space reticles: couldn't run after the game's HUD layout " +
                                      $"({exception.GetBaseException().Message}); placed in LateUpdate instead, " +
                                      "which can trail the view by a frame.");
            }
        }

        // Kept apart so that, if a game update changes BUpdate, the error is caught in Register.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterCallback()
        {
            if (_instance != null) return;
            var instance = new AfterGameLayout();
            BUpdate.RegisterCallback(instance, BUpdateLoopType.PostPostLateUpdate);
            _instance = instance;
        }

        public void OnPostPostLateBUpdate()
        {
            if (!_checkedOrder) CheckOrder();
            if (Reticles != null && Reticles.isActiveAndEnabled) Reticles.Place();
            if (Line != null && Line.isActiveAndEnabled) Line.Draw();
        }

        /// <summary>With diagnostics on, logs once whether this runs after every other callback of the stage.</summary>
        private void CheckOrder()
        {
            _checkedOrder = true;
            if (!Settings.DiagnosticsEnabled.Value) return;
            try
            {
                var callbacks = AccessTools.Field(typeof(BUpdate), "postPostLateUpdateCallbacks")?.GetValue(null)
                    as List<IPostPostLateBUpdateCallback>;
                if (callbacks == null) return;

                var index = callbacks.IndexOf(this);
                Plugin.Log.LogInfo(index == 0
                    ? $"[diag] World-space reticles: placed after the game's HUD layout, last of {callbacks.Count} late callbacks."
                    : $"[diag] World-space reticles: {index} of {callbacks.Count} late callbacks run after the reticles are placed.");
            }
            catch (Exception exception)
            {
                Plugin.Log.LogInfo($"[diag] World-space reticles: couldn't check the late callback order: {exception.GetBaseException().Message}");
            }
        }
    }
}
