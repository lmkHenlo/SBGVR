// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace SBGVR.View
{
    /// <summary>
    /// VR Comfort.CameraDistance: in VR, how far the third-person camera is from your character, as a share of the
    /// game's default distance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game's third-person camera (<c>OrbitCameraModule</c>) works its distance out from the field of view, so
    /// your character fills the same share of a flat screen at any setting: <c>GetFov</c> is the setting plus a
    /// pitch curve and an offset the game animates, and the distance is half the subject's height over
    /// tan(fov / 2), plus distances for heading and pitch, eased halfway toward the same scaled by
    /// tan(30°) / tan(fov / 2) (<c>GetFovCorrectedDistance</c>). In VR the headset sets the field of view, so all the
    /// setting did there was move the camera (FOV 45 about 1.67 times the default distance, 90 about 0.46 times).
    /// </para>
    /// <para>
    /// In VR the FOV setting reads as <see cref="VrFov"/>, the game's default, to everything: the third-person camera,
    /// the first-person camera and the railgun scope's zoom from it (<see cref="RailgunScopeZoom"/>), and other mods
    /// (MaxFOVSlider then adds nothing). Writes to it are skipped while VR runs, so the flat-screen setting is never
    /// changed from VR (the settings menu's set-up would otherwise save 60 over it). The game saves the setting's
    /// field, not the property, so saving is unaffected. The third-person camera's distance is then multiplied by
    /// CameraDistance before the game pulls the camera in front of walls. Every third-person view follows it
    /// (aiming, ball flight, the golf cart, spectating), and the game eases the camera to each change.
    /// </para>
    /// </remarks>
    [HarmonyPatch]
    internal static class CameraDistancePatches
    {
        /// <summary>The field of view setting as everything reads it in VR: the game's default.</summary>
        internal const int VrFov = 60;

        [HarmonyPatch(typeof(GameSettings.GraphicsSettings), nameof(GameSettings.GraphicsSettings.FieldOfView), MethodType.Getter)]
        [HarmonyPostfix]
        private static void ReadFov(ref int __result)
        {
            if (VrState.IsRunning) __result = VrFov;
        }

        [HarmonyPatch(typeof(GameSettings.GraphicsSettings), nameof(GameSettings.GraphicsSettings.FieldOfView), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool WriteFov() => !VrState.IsRunning;

        [HarmonyPatch(typeof(OrbitCameraModule), "GetFovCorrectedDistance")]
        [HarmonyPostfix]
        private static void Distance(ref float __result)
        {
            if (VrState.IsRunning) __result *= Settings.CameraDistance.Value;
        }
    }

    /// <summary>
    /// While VR runs, the settings menu's Field of View slider shows and sets <see cref="Settings.CameraDistance"/>
    /// as "Camera Distance", from 50% to 175% in 5% steps; when VR isn't running it's the game's own slider.
    /// </summary>
    /// <remarks>
    /// The game sets the slider up once, in a coroutine after <c>SettingsMenu.Start</c>: its action (which writes the
    /// FOV setting), its value and its range of 45 to 90. Until then the slider is left alone, so the game's set-up
    /// can't write a value from SBGVR's range into the FOV setting. After that, each frame a settings menu is open,
    /// the slider is switched to the mode VR calls for:
    /// <list type="bullet">
    /// <item>Distance: the slider's action is swapped for SBGVR's, its range set on the Slider itself to 10 to 35
    /// whole units of 5% (so a gamepad press is a step; MaxFOVSlider rewrites <c>SliderOption.SetLimits</c> for this
    /// slider, so that isn't used), and its name label set to "Camera Distance", again whenever the game's
    /// localisation puts its own text back.</item>
    /// <item>Field of view: the game's action, range (<c>SetLimits(45, 90)</c>, which MaxFOVSlider widens as it
    /// always does), value, "60°" text and label come back.</item>
    /// </list>
    /// The game's FOV setting isn't written while the slider is in distance mode (and can't be while VR runs:
    /// <see cref="CameraDistancePatches"/>).
    /// </remarks>
    internal sealed class CameraDistanceSlider : MonoBehaviour
    {
        private const string Label = "Camera Distance";
        private const int PercentPerUnit = 5;
        private const int MinUnits = 10;
        private const int MaxUnits = 35;

        private static readonly AccessTools.FieldRef<SliderOption, Action> OnChanged =
            AccessTools.FieldRefAccess<SliderOption, Action>("onChanged");

        private static readonly AccessTools.FieldRef<SliderOption, TextMeshProUGUI> NameLabel =
            AccessTools.FieldRefAccess<SliderOption, TextMeshProUGUI>("nameLabel");

        private static readonly List<SettingsMenu> Menus = new List<SettingsMenu>();
        private static readonly Dictionary<SliderOption, Distance> InDistanceMode = new Dictionary<SliderOption, Distance>();

        /// <summary>A slider in distance mode: SBGVR's action, and what to give back.</summary>
        private sealed class Distance
        {
            internal Action GameAction;
            internal string GameLabel;
            internal Action Action;
            internal bool Applying;
        }

        internal static void Register(SettingsMenu menu)
        {
            if (menu != null && !Menus.Contains(menu)) Menus.Add(menu);
        }

        private void LateUpdate()
        {
            for (var i = Menus.Count - 1; i >= 0; i--)
            {
                var menu = Menus[i];
                if (menu == null)
                {
                    Menus.RemoveAt(i);
                    continue;
                }

                if (!menu.isActiveAndEnabled) continue;
                var option = menu.fieldOfView;
                if (option == null || option.Slider == null) continue;

                var inDistanceMode = InDistanceMode.TryGetValue(option, out var distance);
                if (VrState.IsRunning)
                {
                    if (!inDistanceMode)
                    {
                        // Not yet set up by the game.
                        if (OnChanged(option) == null) continue;
                        EnterDistanceMode(option);
                    }
                    else
                    {
                        Keep(option, distance);
                    }
                }
                else if (inDistanceMode)
                {
                    EnterFovMode(option, distance);
                }
            }

            // Forget sliders that went with their menus.
            if (InDistanceMode.Count > Menus.Count)
            {
                var gone = new List<SliderOption>();
                foreach (var option in InDistanceMode.Keys) if (option == null) gone.Add(option);
                foreach (var option in gone) InDistanceMode.Remove(option);
            }
        }

        private static void EnterDistanceMode(SliderOption option)
        {
            var label = NameLabel(option);
            var distance = new Distance
            {
                GameAction = OnChanged(option),
                GameLabel = label != null ? label.text : null,
            };
            distance.Action = () => DistanceChanged(option, distance);
            InDistanceMode[option] = distance;

            distance.Applying = true;
            try
            {
                OnChanged(option) = distance.Action;
                var slider = option.Slider;
                slider.wholeNumbers = true;
                slider.minValue = MinUnits;
                slider.maxValue = MaxUnits;
                slider.SetValueWithoutNotify(UnitsOf(Settings.CameraDistance.Value));
                option.SetValueText(PercentText(slider.value));
                if (label != null) label.text = Label;
            }
            finally
            {
                distance.Applying = false;
            }

            if (Settings.DiagnosticsEnabled.Value)
                Plugin.Log.LogInfo($"[diag] Camera distance: the settings menu's Field of View slider is Camera Distance ({PercentText(option.Slider.value)}) while VR runs.");
        }

        /// <summary>Each frame in distance mode: the label and value stay SBGVR's.</summary>
        private static void Keep(SliderOption option, Distance distance)
        {
            var label = NameLabel(option);
            if (label != null && label.text != Label)
            {
                // The game's localisation put its text back (a language change, say): that's the text to restore later.
                distance.GameLabel = label.text;
                label.text = Label;
            }

            // CameraDistance changed elsewhere (a config editor).
            var units = UnitsOf(Settings.CameraDistance.Value);
            if (!Mathf.Approximately(option.Slider.value, units))
            {
                distance.Applying = true;
                try
                {
                    option.Slider.SetValueWithoutNotify(units);
                    option.SetValueText(PercentText(units));
                }
                finally
                {
                    distance.Applying = false;
                }
            }
        }

        private static void EnterFovMode(SliderOption option, Distance distance)
        {
            InDistanceMode.Remove(option);
            // VR has stopped, so this is the real setting again.
            var fov = GameSettings.All?.Graphics?.FieldOfView ?? CameraDistancePatches.VrFov;

            distance.Applying = true;
            try
            {
                var slider = option.Slider;
                option.SetLimits(45f, 90f);
                slider.SetValueWithoutNotify(fov);
                OnChanged(option) = distance.GameAction;
                option.SetValueText($"{fov}°");
                RestoreLabel(option, distance.GameLabel);
            }
            finally
            {
                distance.Applying = false;
            }
        }

        private static void RestoreLabel(SliderOption option, string gameLabel)
        {
            var label = NameLabel(option);
            if (label == null) return;
            var localized = label.GetComponent<LocalizeStringEvent>();
            if (localized != null) localized.RefreshString();
            if (label.text == Label && gameLabel != null) label.text = gameLabel;
        }

        private static void DistanceChanged(SliderOption option, Distance distance)
        {
            if (distance.Applying) return;
            var units = Mathf.Clamp(Mathf.Round(option.Slider.value), MinUnits, MaxUnits);
            Settings.CameraDistance.Value = units * PercentPerUnit / 100f;
            option.SetValueText(PercentText(units));
        }

        private static float UnitsOf(float distance) =>
            Mathf.Clamp(Mathf.Round(distance * 100f / PercentPerUnit), MinUnits, MaxUnits);

        private static string PercentText(float units) => $"{Mathf.RoundToInt(units * PercentPerUnit)}%";
    }

    /// <summary>Hands each settings menu to <see cref="CameraDistanceSlider"/> as it opens.</summary>
    [HarmonyPatch]
    internal static class CameraDistanceSliderPatches
    {
        [HarmonyPatch(typeof(SettingsMenu), "OnEnable")]
        [HarmonyPostfix]
        private static void MenuOpened(SettingsMenu __instance) => CameraDistanceSlider.Register(__instance);
    }
}
