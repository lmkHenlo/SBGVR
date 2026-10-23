// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HTraceAO.Rendering.URP.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SBGVR.Rendering
{
    /// <summary>
    /// VR Graphics.AmbientOcclusion: the game's ambient occlusion (HTrace AO) in VR.
    ///
    /// Before each eye is drawn, records that eye for <see cref="EyeView"/>, which
    /// <see cref="AmbientOcclusionEyePatches"/> makes HTrace AO use. If those patches couldn't be applied, or
    /// the setting is off, ambient occlusion is turned off while VR runs instead, the way the feature itself
    /// skips cameras it doesn't draw: its keyword is turned off, so lit shaders stop sampling the occlusion
    /// texture, and none of its passes are added. Flat-screen rendering is unchanged either way.
    /// </summary>
    [HarmonyPatch]
    internal static class AmbientOcclusionPatches
    {
        private const string OcclusionKeyword = "_SCREEN_SPACE_OCCLUSION";

        /// <summary>Set when <see cref="AmbientOcclusionEyePatches"/> applied.</summary>
        internal static bool EyePatchesApplied;

        [HarmonyPatch(typeof(HTraceAORendererFeature), nameof(HTraceAORendererFeature.AddRenderPasses))]
        [HarmonyPrefix]
        private static bool AddRenderPasses(ref RenderingData renderingData)
        {
            if (VrState.IsRunning && (!EyePatchesApplied || !Settings.AmbientOcclusion.Value))
            {
                Shader.DisableKeyword(OcclusionKeyword);
                return false;
            }

            if (EyePatchesApplied) EyeView.Capture(ref renderingData);
            return true;
        }
    }

    /// <summary>
    /// Makes HTrace AO draw correctly for each eye. HTrace AO asks the Camera for its size, view and field of
    /// view; in VR those are still the flat game window's (2560x1440 and a centred 60 degree view on the test
    /// PC), while each eye is 2528x2704 with an off-centre view of about 94 by 99 degrees. Only part of each
    /// eye got occlusion values and the rest read as fully occluded, so nearby surfaces drew black.
    ///
    /// In every HTrace AO method that reads them, these are redirected to <see cref="EyeView"/>:
    /// <list type="bullet">
    /// <item>Camera.scaledPixelWidth, scaledPixelHeight and pixelHeight: the size it renders at.</item>
    /// <item>Camera.fieldOfView: used to size the search radius on screen.</item>
    /// <item>Camera.projectionMatrix, nonJitteredProjectionMatrix and worldToCameraMatrix.</item>
    /// <item>The camera's hash where it keys the camera's history, so each eye keeps its own history for
    /// the temporal filter and reprojection.</item>
    /// <item>GTAO's depth-to-view factors (g_HDepthToViewParamsAO), which assume a centred view.</item>
    /// </list>
    /// </summary>
    [HarmonyPatch]
    internal static class AmbientOcclusionEyePatches
    {
        private static readonly Dictionary<MethodInfo, MethodInfo> CameraGetters = new Dictionary<MethodInfo, MethodInfo>
        {
            [Getter(nameof(Camera.scaledPixelWidth))] = Hook(nameof(EyeView.ScaledPixelWidth)),
            [Getter(nameof(Camera.scaledPixelHeight))] = Hook(nameof(EyeView.ScaledPixelHeight)),
            [Getter(nameof(Camera.pixelHeight))] = Hook(nameof(EyeView.PixelHeight)),
            [Getter(nameof(Camera.fieldOfView))] = Hook(nameof(EyeView.FieldOfView)),
            [Getter(nameof(Camera.projectionMatrix))] = Hook(nameof(EyeView.ProjectionMatrix)),
            [Getter(nameof(Camera.nonJitteredProjectionMatrix))] = Hook(nameof(EyeView.NonJitteredProjectionMatrix)),
            [Getter(nameof(Camera.worldToCameraMatrix))] = Hook(nameof(EyeView.WorldToCameraMatrix)),
        };

        private static readonly MethodInfo GetHashCodeMethod = AccessTools.Method(typeof(object), nameof(GetHashCode));
        private static readonly MethodInfo FrameCountGetter = AccessTools.PropertyGetter(typeof(Time), nameof(Time.frameCount));
        private static readonly MethodInfo SetGlobalVector =
            AccessTools.Method(typeof(CommandBuffer), nameof(CommandBuffer.SetGlobalVector), new[] { typeof(int), typeof(Vector4) });
        private const string DepthToViewParamsField = "g_HDepthToViewParamsAO";

        private static int _getterCount;
        private static int _historyKeyCount;
        private static int _depthToViewCount;

        private static MethodInfo Getter(string property)
        {
            return AccessTools.PropertyGetter(typeof(Camera), property);
        }

        private static MethodInfo Hook(string name)
        {
            return AccessTools.Method(typeof(EyeView), name);
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var assembly = typeof(HTraceAORendererFeature).Assembly;
            foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
            {
                if (type.ContainsGenericParameters) continue;

                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                         BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                    if (ReadsCamera(method)) yield return method;
                }
            }
        }

        private static bool ReadsCamera(MethodBase method)
        {
            try
            {
                var body = PatchProcessor.ReadMethodBody(method).ToList();
                for (var i = 0; i < body.Count; i++)
                {
                    if (!(body[i].Value is MethodInfo called)) continue;
                    if (CameraGetters.ContainsKey(called)) return true;
                    if (called == GetHashCodeMethod && i + 1 < body.Count &&
                        body[i + 1].Value as MethodInfo == FrameCountGetter) return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var code = instructions.ToList();
            var cameraArgument = CameraArgument(original);

            for (var i = 0; i < code.Count; i++)
            {
                var instruction = code[i];

                if (instruction.operand is MethodInfo called && CameraGetters.TryGetValue(called, out var hook) &&
                    (instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call))
                {
                    code[i] = new CodeInstruction(OpCodes.Call, hook).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                    _getterCount++;
                    continue;
                }

                // camera.GetHashCode(), Time.frameCount, then CameraHistorySystem.SyncCamera(hash, frame).
                if (instruction.operand is MethodInfo hashCall && hashCall == GetHashCodeMethod &&
                    i + 1 < code.Count && code[i + 1].Calls(FrameCountGetter) &&
                    (i == 0 || code[i - 1].opcode != OpCodes.Constrained))
                {
                    code[i] = new CodeInstruction(OpCodes.Call, Hook(nameof(EyeView.HistoryKey)))
                        .MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                    _historyKeyCount++;
                    continue;
                }

                // cmd.SetGlobalVector(g_HDepthToViewParamsAO, value): adjust value for the eye.
                if (cameraArgument >= 0 && instruction.opcode == OpCodes.Ldsfld &&
                    instruction.operand is FieldInfo field && field.Name == DepthToViewParamsField &&
                    i + 2 < code.Count && code[i + 1].IsLdloc() && code[i + 2].Calls(SetGlobalVector))
                {
                    code.Insert(i + 2, LoadArgument(cameraArgument));
                    code.Insert(i + 3, new CodeInstruction(OpCodes.Call, Hook(nameof(EyeView.DepthToViewParams))));
                    _depthToViewCount++;
                    i += 3;
                }
            }

            return code;
        }

        private static CodeInstruction LoadArgument(int index)
        {
            switch (index)
            {
                case 0: return new CodeInstruction(OpCodes.Ldarg_0);
                case 1: return new CodeInstruction(OpCodes.Ldarg_1);
                case 2: return new CodeInstruction(OpCodes.Ldarg_2);
                case 3: return new CodeInstruction(OpCodes.Ldarg_3);
                default: return new CodeInstruction(OpCodes.Ldarg_S, (byte)index);
            }
        }

        /// <summary>The IL argument index of the method's Camera parameter, or -1.</summary>
        private static int CameraArgument(MethodBase method)
        {
            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(Camera)) return method.IsStatic ? i : i + 1;
            }

            return -1;
        }

        /// <summary>
        /// Called after the patches are applied. Ambient occlusion is only kept on in VR if every part was
        /// found: an HTrace AO update that moved one would otherwise bring the black areas back. Until then the
        /// patched code behaves exactly as before, since <see cref="EyeView"/> only steps in for an eye it has
        /// captured.
        /// </summary>
        internal static void Verify()
        {
            var complete = _getterCount > 0 && _historyKeyCount > 0 && _depthToViewCount > 0;
            AmbientOcclusionPatches.EyePatchesApplied = complete;

            var counts = $"{_getterCount} camera reads, {_historyKeyCount} history keys, " +
                         $"{_depthToViewCount} view factor(s)";
            if (complete)
                Plugin.Log.LogInfo($"Ambient occlusion: draws for each eye in VR ({counts}).");
            else
                Plugin.Log.LogWarning(
                    $"Ambient occlusion: HTrace AO isn't the code SBGVR expects ({counts}), so it's off in VR.");
        }
    }
}
