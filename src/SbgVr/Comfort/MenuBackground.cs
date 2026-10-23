// SBG VR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SbgVr.Comfort
{
    /// <summary>
    /// VR Comfort.MenuBackground = Still: holds each main menu background scene still while VR runs. The game
    /// pans each scene's camera from one point to another over a few seconds; in a headset that moves you
    /// without you moving. The scene is held at the middle of its pan, and the game still fades between scenes.
    /// </summary>
    internal sealed class MenuBackground : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<MainMenuScenario, Transform> PointA =
            AccessTools.FieldRefAccess<MainMenuScenario, Transform>("cameraPointA");

        private static readonly AccessTools.FieldRef<MainMenuScenario, Transform> PointB =
            AccessTools.FieldRefAccess<MainMenuScenario, Transform>("cameraPointB");

        private readonly List<MainMenuScenario> _scenarios = new List<MainMenuScenario>();
        private bool _searched;

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            _scenarios.Clear();
            _searched = false;
        }

        private void LateUpdate()
        {
            if (!VrState.IsRunning || Settings.MenuBackground.Value != MenuBackgroundMode.Still) return;

            if (!_searched)
            {
                _searched = true;
                _scenarios.AddRange(FindObjectsByType<MainMenuScenario>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            }

            foreach (var scenario in _scenarios)
            {
                if (scenario == null) continue;

                var cameraTransform = scenario.CameraTransform;
                var a = PointA(scenario);
                var b = PointB(scenario);
                if (cameraTransform == null || a == null || b == null || !cameraTransform.gameObject.activeInHierarchy)
                    continue;

                cameraTransform.SetPositionAndRotation(
                    Vector3.Lerp(a.position, b.position, 0.5f),
                    Quaternion.Slerp(a.rotation, b.rotation, 0.5f));
            }
        }
    }
}
