// SBGVR: turns on the controller interaction profiles adapted from the Unity OpenXR plugin.
// Copyright (c) 2026 lmkHenlo. Part of SBGVR's modified copy of UUVR; licensed under the GNU GPL v3.0 or later.

using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.XR.OpenXR.Features.Interactions
{
    /// <summary>
    /// Adds the controller interaction profiles to <see cref="OpenXRSettings"/>, so the next OpenXR session
    /// reports controllers to Unity's XR input (<see cref="UnityEngine.XR.InputDevices"/>). UUVR's OpenXR loader
    /// starts with no interaction profiles; without this, OpenXR shows no controllers at all.
    /// </summary>
    public static class ControllerProfiles
    {
        /// <summary>
        /// Enables the Oculus Touch, Valve Index, HTC Vive, Windows Mixed Reality and Khronos simple controller
        /// profiles. Call it before OpenXR starts. Calling it again changes nothing.
        /// </summary>
        /// <returns>The names of the profiles that are enabled.</returns>
        public static string[] Enable()
        {
            if (OpenXRLoaderBase.Instance != null)
                throw new InvalidOperationException("Controller profiles must be enabled before OpenXR starts.");

            var settings = OpenXRSettings.Instance;
            var features = (settings.features ?? new OpenXRFeature[0]).Where(f => f != null).ToList();

            Add<OculusTouchControllerProfile>(features, "Oculus Touch Controller Profile", OculusTouchControllerProfile.featureId);
            Add<ValveIndexControllerProfile>(features, "Valve Index Controller Profile", ValveIndexControllerProfile.featureId);
            Add<HTCViveControllerProfile>(features, "HTC Vive Controller Profile", HTCViveControllerProfile.featureId);
            Add<MicrosoftMotionControllerProfile>(features, "Microsoft Motion Controller Profile", MicrosoftMotionControllerProfile.featureId);
            Add<KHRSimpleControllerProfile>(features, "Khronos Simple Controller Profile", KHRSimpleControllerProfile.featureId);

            settings.features = features.ToArray();
            return features.OfType<OpenXRInteractionFeature>().Where(f => f.enabled).Select(f => f.nameUi).ToArray();
        }

        private static void Add<T>(List<OpenXRFeature> features, string name, string featureId) where T : OpenXRInteractionFeature
        {
            if (features.Any(f => f is T))
                return;

            var feature = ScriptableObject.CreateInstance<T>();
            feature.hideFlags = HideFlags.HideAndDontSave;
            feature.name = name;
            feature.nameUi = name;
            feature.version = "0.0.1";
            feature.company = "Unity";
            feature.featureIdInternal = featureId;
            feature.openxrExtensionStrings = "";
            feature.enabled = true;
            features.Add(feature);
        }
    }
}
