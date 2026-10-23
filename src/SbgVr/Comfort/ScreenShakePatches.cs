// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SbgVr.Comfort
{
    /// <summary>
    /// Scales the game's camera shake in VR by VR Comfort.ScreenShake. The camera controller reads the game's
    /// Screen shake setting in two places (whether to shake at all, and how hard); both reads go through
    /// <see cref="Factor"/> instead. Patching the reads rather than the setting's tiny getter avoids Mono
    /// inlining the getter past the patch, and never changes the saved setting.
    /// </summary>
    [HarmonyPatch]
    internal static class ScreenShakePatches
    {
        private static readonly MethodInfo ScreenshakeFactorGetter =
            AccessTools.PropertyGetter(typeof(GameSettings.GeneralSettings), nameof(GameSettings.GeneralSettings.ScreenshakeFactor));

        private static readonly MethodInfo FactorMethod =
            AccessTools.Method(typeof(ScreenShakePatches), nameof(Factor));

        internal static float Factor(GameSettings.GeneralSettings settings)
        {
            var gameFactor = settings.ScreenshakeFactor;
            return VrState.IsRunning ? gameFactor * Settings.ScreenShake.Value : gameFactor;
        }

        [HarmonyPatch(typeof(CameraModuleController), "UpdateShake")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateShake(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceFactorReads(instructions, "CameraModuleController.UpdateShake");
        }

        [HarmonyPatch(typeof(CameraModuleController), "CanShakeAtAll")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CanShakeAtAll(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceFactorReads(instructions, "CameraModuleController.CanShakeAtAll");
        }

        private static IEnumerable<CodeInstruction> ReplaceFactorReads(IEnumerable<CodeInstruction> instructions, string method)
        {
            var result = new List<CodeInstruction>();
            var replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(ScreenshakeFactorGetter))
                {
                    // The settings object is already on the stack: call Factor(settings) instead of settings.ScreenshakeFactor.
                    var call = new CodeInstruction(OpCodes.Call, FactorMethod);
                    call.labels.AddRange(instruction.labels);
                    call.blocks.AddRange(instruction.blocks);
                    result.Add(call);
                    replaced++;
                    continue;
                }

                result.Add(instruction);
            }

            if (replaced == 0)
                throw new InvalidOperationException($"{method} no longer reads the Screen shake setting");

            return result;
        }
    }
}
