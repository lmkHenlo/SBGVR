// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SBGVR.View
{
    /// <summary>
    /// The game-side half of <see cref="FirstPersonView"/>. Every patch here leaves the game alone unless
    /// SBGVR is showing first person (<see cref="FirstPersonView.Owns"/>), or is about to.
    /// </summary>
    [HarmonyPatch]
    internal static class FirstPersonPatches
    {
        /// <summary>
        /// The game asks for the third-person camera after the hole overview and when the railgun scope ends;
        /// with first person on, it gets the first-person camera instead.
        /// </summary>
        [HarmonyPatch(typeof(CameraModuleController), "TransitionToInternal")]
        [HarmonyPrefix]
        private static void KeepFirstPerson(ref CameraModuleType cameraType)
        {
            if (cameraType == CameraModuleType.Orbit && FirstPersonView.ShouldRedirectToFirstPerson())
                cameraType = CameraModuleType.FirstPerson;
        }

        /// <summary>Places the eye anchor and levels the camera just before it positions itself.</summary>
        [HarmonyPatch(typeof(FirstPersonCameraModule), nameof(FirstPersonCameraModule.UpdateModule))]
        [HarmonyPrefix]
        private static void BeforeUpdateModule(FirstPersonCameraModule __instance) =>
            FirstPersonView.BeforeModuleUpdate(__instance);

        /// <summary>
        /// Turning with the right stick or mouse. In first person your head looks up and down, so only the
        /// sideways part is used; with FirstPersonTurn = Snap, a gamepad's (or the controllers') stick turns
        /// in steps instead. In a golf cart they don't turn it at all.
        /// </summary>
        [HarmonyPatch(typeof(FirstPersonCameraModule), nameof(FirstPersonCameraModule.Rotate))]
        [HarmonyPrefix]
        private static bool Turn(FirstPersonCameraModule __instance, ref Vector2 axis)
        {
            if (!FirstPersonView.Shows(__instance)) return true;

            // In a golf cart the view stays on the cart's front.
            if (FirstPersonView.InGolfCart())
            {
                __instance.InformNoRotation();
                return false;
            }

            axis.y = 0f;
            if (Settings.FirstPersonTurn.Value != TurnMode.Snap || !PlayerInput.UsingGamepad) return true;

            var stick = PlayerInput.Controls.Camera.Look.ReadValue<Vector2>();
            FirstPersonView.SnapTurn(__instance, stick.x);
            __instance.InformNoRotation();
            return false;
        }

        /// <summary>
        /// The railgun scope copies the third-person camera's (old) direction into the first-person camera when
        /// it starts, and raises and zooms it. In SBGVR's first person, keep your direction and eye height.
        /// </summary>
        [HarmonyPatch(typeof(PlayerInventory), "UpdateIsInFirstPersonAim")]
        [HarmonyPrefix]
        private static void BeforeRailgunScope(out float __state)
        {
            __state = float.NaN;
            if (FirstPersonView.Owns && CameraModuleController.TryGetFirstPersonModule(out var module) && module != null)
                __state = module.Yaw;
        }

        [HarmonyPatch(typeof(PlayerInventory), "UpdateIsInFirstPersonAim")]
        [HarmonyPostfix]
        private static void AfterRailgunScope(PlayerInventory __instance, float __state)
        {
            if (float.IsNaN(__state) || !FirstPersonView.Owns) return;
            if (!CameraModuleController.TryGetFirstPersonModule(out var module) || module == null) return;

            module.SetYaw(__state);
            module.SetPitch(0f);
            module.SetLocalPosition(Vector3.zero);
            if (!__instance.IsInFirstPersonAim) module.CancelFovOverrideInstantly();
        }
    }

    /// <summary>
    /// The game moves the character relative to the first-person camera's yaw, and turns it to face that yaw
    /// while aiming. In SBGVR's first person those read where your head faces instead
    /// (<see cref="FirstPersonView.YawOf"/>), so you walk and aim where you look.
    /// </summary>
    [HarmonyPatch]
    internal static class FirstPersonMovementPatches
    {
        private static readonly MethodInfo GetYaw =
            AccessTools.PropertyGetter(typeof(FirstPersonCameraModule), nameof(FirstPersonCameraModule.Yaw));

        private static readonly MethodInfo YawOf =
            AccessTools.Method(typeof(FirstPersonView), nameof(FirstPersonView.YawOf));

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return Method("ProcessMovementInput");
            yield return Method("UpdateTargetYaw");
        }

        private static MethodBase Method(string name) =>
            AccessTools.Method(typeof(PlayerMovement), name) ?? throw new MissingMethodException(nameof(PlayerMovement), name);

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = instructions.ToList();
            var replaced = 0;
            foreach (var instruction in list)
            {
                if (!instruction.Calls(GetYaw)) continue;
                // Same stack shape: the module goes in, a float comes out. Labels stay on the instruction.
                instruction.opcode = OpCodes.Call;
                instruction.operand = YawOf;
                replaced++;
            }

            if (replaced == 0)
                throw new InvalidOperationException($"PlayerMovement.{original.Name} no longer reads the first-person camera's yaw.");
            return list;
        }
    }
}
