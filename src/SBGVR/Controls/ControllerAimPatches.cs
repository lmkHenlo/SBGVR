// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SBGVR.Controls
{
    /// <summary>
    /// Points the game's weapon aiming at the controller: in each method below, every
    /// <c>Component.transform</c> read goes through <see cref="ControllerAim.TransformOf"/>, which swaps the game
    /// camera's transform for the controller's stand-in while controller aiming applies. Other components'
    /// transforms (the player, the gun's barrel) are passed through untouched.
    /// </summary>
    [HarmonyPatch]
    internal static class ControllerAimPatches
    {
        private static readonly MethodInfo GetTransform = AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform));
        private static readonly MethodInfo GetCamera = AccessTools.PropertyGetter(typeof(GameManager), nameof(GameManager.Camera));
        private static readonly MethodInfo TransformOf = AccessTools.Method(typeof(ControllerAim), nameof(ControllerAim.TransformOf));

        /// <summary>
        /// The aiming code: guns (dueling pistol, elephant gun, rocket launcher, railgun), the freeze bomb, the
        /// thunderstorm, the arm's aim pitch, and turning the player to face the aim when an item is used.
        /// </summary>
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return Method(typeof(PlayerInventory), "GetFirearmAimPoint");
            yield return Method(typeof(PlayerInventory), "GetFreezeBombAimDirection");
            yield return Method(typeof(PlayerInventory), "TryGetThunderstormAimPoint");
            yield return Method(typeof(PlayerAnimatorIo), "UpdateAimingAngle");
            yield return Method(typeof(PlayerMovement), "AlignWithCameraImmediately");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            Redirect(instructions, original, TransformOf);

        internal static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions, MethodBase original, MethodInfo replacement)
        {
            var list = instructions.ToList();
            if (!list.Any(i => i.Calls(GetCamera)))
                throw new InvalidOperationException($"{original.DeclaringType?.Name}.{original.Name} no longer reads GameManager.Camera.");

            var replaced = 0;
            foreach (var instruction in list)
            {
                if (!instruction.Calls(GetTransform)) continue;
                // Same stack shape: the component goes in, a Transform comes out. Labels stay on the instruction.
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced++;
            }

            if (replaced == 0)
                throw new InvalidOperationException($"{original.DeclaringType?.Name}.{original.Name} reads no transforms.");
            return list;
        }

        internal static MethodBase Method(Type type, string name) =>
            AccessTools.Method(type, name) ?? throw new MissingMethodException(type.Name, name);
    }

    /// <summary>
    /// The rocket launcher's lock-on (the orange reticle) picks its target around the camera's aim. Golf swings
    /// use the same method for their homing target, so it's only redirected while the rocket launcher asks.
    /// </summary>
    [HarmonyPatch]
    internal static class ControllerAimLockOnPatches
    {
        private static readonly MethodInfo TransformOfForRocketLauncher =
            AccessTools.Method(typeof(ControllerAim), nameof(ControllerAim.TransformOfForRocketLauncher));

        [HarmonyPatch(typeof(PlayerGolfer), "TryGetBestLockOnTarget")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> RedirectLockOn(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            ControllerAimPatches.Redirect(instructions, original, TransformOfForRocketLauncher);

        /// <summary>PlayerInventory's local function that updates the rocket launcher's lock-on each frame.</summary>
        private static MethodBase RocketLauncherLockOn() =>
            AccessTools.GetDeclaredMethods(typeof(PlayerInventory))
                .FirstOrDefault(m => m.Name.Contains("LocalPlayerUpdateRocketLauncherLockOnTarget"))
            ?? throw new MissingMethodException(nameof(PlayerInventory), "LocalPlayerUpdateRocketLauncherLockOnTarget");

        [HarmonyPatch]
        internal static class RocketLauncherScope
        {
            private static MethodBase TargetMethod() => RocketLauncherLockOn();

            private static void Prefix() => ControllerAim.PickingRocketLauncherTarget = true;

            private static Exception Finalizer(Exception __exception)
            {
                ControllerAim.PickingRocketLauncherTarget = false;
                return __exception;
            }
        }
    }
}
