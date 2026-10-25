// Adapted from the Unity OpenXR plugin (com.unity.xr.openxr) 1.4.2, Runtime/input/OpenXRInput.cs,
// copyright (c) 2020 Unity Technologies ApS, licensed under the Unity Companion License
// (https://unity3d.com/legal/licenses/unity_companion_license).
// Changed for SBGVR on 2026-10-24: kept only the code that sends the enabled interaction profiles' action maps
// to the OpenXR runtime (AttachActionSets and the native calls it uses). The Input System layouts, haptics and
// action lookups are left out. AttachActionSets does nothing when no interaction profile is enabled, so OpenXR
// starts exactly as before unless a profile was added with ControllerProfiles.Enable.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine.XR.OpenXR.Features;

namespace UnityEngine.XR.OpenXR.Input
{
    /// <summary>
    /// Sends the action maps of the enabled <see cref="OpenXRInteractionFeature"/>s to the OpenXR runtime, so the
    /// runtime reports controllers to Unity's XR input subsystem (<see cref="UnityEngine.XR.InputDevices"/>).
    /// </summary>
    public static class OpenXRInput
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct SerializedGuid
        {
            [FieldOffset(0)]
            public Guid guid;
            [FieldOffset(0)]
            public ulong ulong1;
            [FieldOffset(8)]
            public ulong ulong2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct SerializedBinding
        {
            /// <summary>Identifier of the action (created with CreateAction) to bind to</summary>
            public ulong actionId;

            /// <summary>OpenXR path to bind to (full path including user path)</summary>
            public string path;
        }

        /// <summary>
        /// Validates a given ActionMapConfig to ensure that it is generally set up correctly.
        /// </summary>
        private static bool ValidateActionMapConfig(OpenXRInteractionFeature interactionFeature, OpenXRInteractionFeature.ActionMapConfig actionMapConfig)
        {
            var valid = true;
            if (actionMapConfig.deviceInfos == null || actionMapConfig.deviceInfos.Count == 0)
            {
                Debug.LogError($"ActionMapConfig contains no `deviceInfos` in InteractionFeature '{interactionFeature.GetType()}'");
                valid = false;
            }

            if (actionMapConfig.actions == null || actionMapConfig.actions.Count == 0)
            {
                Debug.LogError($"ActionMapConfig contains no `actions` in InteractionFeature '{interactionFeature.GetType()}'");
                valid = false;
            }

            return valid;
        }

        /// <summary>
        /// Attach all enabled interaction profiles' actions to OpenXR.
        /// Note: this should not be called more than once per session.
        /// </summary>
        internal static void AttachActionSets()
        {
            var actionMaps = new List<OpenXRInteractionFeature.ActionMapConfig>();
            foreach (var interactionFeature in OpenXRSettings.Instance.features.OfType<OpenXRInteractionFeature>().Where(f => f.enabled))
            {
                var start = actionMaps.Count;
                interactionFeature.CreateActionMaps(actionMaps);

                for (var index = actionMaps.Count - 1; index >= start; index--)
                {
                    if (!ValidateActionMapConfig(interactionFeature, actionMaps[index]))
                        actionMaps.RemoveAt(index);
                }
            }

            // SBGVR: with no interaction profile enabled, leave the session exactly as UUVR always had it.
            if (actionMaps.Count == 0)
                return;

            foreach (var actionMap in actionMaps)
            {
                foreach (var deviceInfo in actionMap.deviceInfos)
                {
                    var localizedName = actionMap.desiredInteractionProfile == null ? UserPathToDeviceName(deviceInfo.userPath) : actionMap.localizedName;
                    if (0 == Internal_RegisterDeviceDefinition(deviceInfo.userPath, actionMap.desiredInteractionProfile, (uint) deviceInfo.characteristics, localizedName, actionMap.manufacturer, actionMap.serialNumber))
                    {
                        OpenXRRuntime.LogLastError();
                        return;
                    }
                }
            }

            var interactionProfiles = new Dictionary<string, List<SerializedBinding>>();
            foreach (var actionMap in actionMaps)
            {
                string actionMapLocalizedName = SanitizeStringForOpenXRPath(actionMap.localizedName);
                var actionSetId = Internal_CreateActionSet(SanitizeStringForOpenXRPath(actionMap.name), actionMapLocalizedName, new SerializedGuid());
                if (0 == actionSetId)
                {
                    OpenXRRuntime.LogLastError();
                    return;
                }

                // User paths specified in the deviceInfo
                var deviceUserPaths = actionMap.deviceInfos.Select(d => d.userPath).ToList();

                foreach (var action in actionMap.actions)
                {
                    // User paths specified in the bindings
                    var bindingUserPaths = action.bindings.Where(b => b.userPaths != null).SelectMany(b => b.userPaths).Distinct().ToList();

                    // Combination of all user paths
                    var allUserPaths = bindingUserPaths.Union(deviceUserPaths).ToArray();

                    var actionId = Internal_CreateAction(
                        actionSetId,
                        SanitizeStringForOpenXRPath(action.name),
                        action.localizedName,
                        (uint) action.type,
                        new SerializedGuid(),
                        allUserPaths, (uint) allUserPaths.Length,
                        action.usages?.ToArray(), (uint) (action.usages?.Count ?? 0));

                    if (actionId == 0)
                    {
                        OpenXRRuntime.LogLastError();
                        return;
                    }

                    foreach (var binding in action.bindings)
                    {
                        foreach (var userPath in binding.userPaths ?? deviceUserPaths)
                        {
                            var interactionProfile = binding.interactionProfileName ?? actionMap.desiredInteractionProfile;
                            if (!interactionProfiles.TryGetValue(interactionProfile, out var bindings))
                            {
                                bindings = new List<SerializedBinding>();
                                interactionProfiles[interactionProfile] = bindings;
                            }

                            bindings.Add(new SerializedBinding {actionId = actionId, path = userPath + binding.interactionPath});
                        }
                    }
                }
            }

            // Suggest bindings
            foreach (var kv in interactionProfiles)
            {
                if (!Internal_SuggestBindings(kv.Key, kv.Value.ToArray(), (uint) kv.Value.Count))
                    OpenXRRuntime.LogLastError();
            }

            // Attach actions sets to commit all bindings
            if (!Internal_AttachActionSets())
                OpenXRRuntime.LogLastError();
        }

        /// <summary>
        /// Sanitize the given character for use as an OpenXR Path
        /// </summary>
        private static char SanitizeCharForOpenXRPath(char c)
        {
            if (char.IsLower(c) || char.IsDigit(c))
                return c;

            if (char.IsUpper(c))
                return char.ToLower(c);

            if (c == '-' || c == '.' || c == '_' || c == '/')
                return c;

            return (char) 0;
        }

        /// <summary>
        /// OpenXR names can only contain certain characters. see https://www.khronos.org/registry/OpenXR/specs/1.0/html/xrspec.html#well-formed-path-strings
        /// </summary>
        private static string SanitizeStringForOpenXRPath(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            // Find the first character that is not sanitized
            var i = 0;
            for (; i < input.Length && SanitizeCharForOpenXRPath(input[i]) == input[i]; ++i) ;

            // Already clean
            if (i == input.Length)
                return input;

            // Build the rest of the string by sanitizing each character but start with the
            // portion of the string we already know is sanitized
            var sb = new StringBuilder(input, 0, i, input.Length);
            for (; i < input.Length; ++i)
            {
                var c = SanitizeCharForOpenXRPath(input[i]);
                if (c != 0)
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private static string UserPathToDeviceName(string userPath)
        {
            // Build the device name from the user path
            var parts = userPath.Split('/', '_');
            var nameBuilder = new StringBuilder("OXR");
            foreach (var part in parts)
            {
                if (part.Length == 0)
                    continue;

                var sanitizedPart = SanitizeStringForOpenXRPath(part);
                nameBuilder.Append(char.ToUpper(sanitizedPart[0]));
                nameBuilder.Append(sanitizedPart.Substring(1));
            }

            return nameBuilder.ToString();
        }

        private const string Library = "UnityOpenXR";

        [DllImport(Library, EntryPoint = "OpenXRInputProvider_RegisterDeviceDefinition", CharSet = CharSet.Ansi)]
        private static extern ulong Internal_RegisterDeviceDefinition(string userPath, string interactionProfile, uint characteristics, string name, string manufacturer, string serialNumber);

        [DllImport(Library, EntryPoint = "OpenXRInputProvider_CreateActionSet", CharSet = CharSet.Ansi)]
        private static extern ulong Internal_CreateActionSet(string name, string localizedName, SerializedGuid guid);

        [DllImport(Library, EntryPoint = "OpenXRInputProvider_CreateAction", CharSet = CharSet.Ansi)]
        private static extern ulong Internal_CreateAction(ulong actionSetId, string name, string localizedName, uint actionType, SerializedGuid guid, string[] userPaths, uint userPathCount, string[] usages, uint usageCount);

        [DllImport(Library, EntryPoint = "OpenXRInputProvider_SuggestBindings", CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool Internal_SuggestBindings(string interactionProfile, SerializedBinding[] serializedBindings, uint serializedBindingCount);

        [DllImport(Library, EntryPoint = "OpenXRInputProvider_AttachActionSets", CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool Internal_AttachActionSets();
    }
}
