// Adapted from the Unity OpenXR plugin (com.unity.xr.openxr) 1.4.2, copyright (c) 2020 Unity Technologies ApS,
// licensed under the Unity Companion License (https://unity3d.com/legal/licenses/unity_companion_license).
// Changed for SBGVR on 2026-10-24: removed the Input System device layout and the Editor-only attributes,
// keeping only the action map that tells the OpenXR runtime which controller inputs to report.

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace UnityEngine.XR.OpenXR.Features.Interactions
{
    /// <summary>
    /// This <see cref="OpenXRInteractionFeature"/> enables the use of Valve Index Controllers interaction profiles in OpenXR.
    /// </summary>
    public class ValveIndexControllerProfile : OpenXRInteractionFeature
    {
        /// <summary>
        /// The feature id string. This is used to give the feature a well known id for reference.
        /// </summary>
        public const string featureId = "com.unity.openxr.feature.input.valveindex";

        /// <summary>
        /// The interaction profile string used to reference the <a href="https://www.khronos.org/registry/OpenXR/specs/1.0/html/xrspec.html#_valve_index_controller_profile">Valve Index Controller</a>.
        /// </summary>
        public const string profile = "/interaction_profiles/valve/index_controller";

        /// <summary>
        /// Constant for a boolean interaction binding '.../input/system/click' OpenXR Input Binding.
        /// </summary>
        public const string system = "/input/system/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/system/touch' OpenXR Input Binding.
        /// </summary>
        public const string systemTouch = "/input/system/touch";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/a/click' OpenXR Input Binding.
        /// </summary>
        public const string buttonA = "/input/a/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/a/touch' OpenXR Input Binding.
        /// </summary>
        public const string buttonATouch = "/input/a/touch";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/b/click' OpenXR Input Binding.
        /// </summary>
        public const string buttonB = "/input/b/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/b/touch' OpenXR Input Binding.
        /// </summary>
        public const string buttonBTouch = "/input/b/touch";
        /// <summary>
        /// Constant for a float interaction binding '.../input/squeeze/value' OpenXR Input Binding.
        /// </summary>
        public const string squeeze = "/input/squeeze/value";
        /// <summary>
        /// Constant for a float interaction binding '.../input/squeeze/force' OpenXR Input Binding.
        /// </summary>
        public const string squeezeForce = "/input/squeeze/force";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/trigger/click' OpenXR Input Binding.
        /// </summary>
        public const string triggerClick = "/input/trigger/click";
        /// <summary>
        /// Constant for a float interaction binding '.../input/trigger/value' OpenXR Input Binding.
        /// </summary>
        public const string trigger = "/input/trigger/value";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/trigger/touch' OpenXR Input Binding.
        /// </summary>
        public const string triggerTouch = "/input/trigger/touch";
        /// <summary>
        /// Constant for a Vector2 interaction binding '.../input/thumbstick' OpenXR Input Binding.
        /// </summary>
        public const string thumbstick = "/input/thumbstick";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/thumbstick/click' OpenXR Input Binding.
        /// </summary>
        public const string thumbstickClick = "/input/thumbstick/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/thumbstick/touch' OpenXR Input Binding.
        /// </summary>
        public const string thumbstickTouch = "/input/thumbstick/touch";
        /// <summary>
        /// Constant for a Vector2 interaction binding '.../input/trackpad' OpenXR Input Binding.
        /// </summary>
        public const string trackpad = "/input/trackpad";
        /// <summary>
        /// Constant for a float interaction binding '.../input/trackpad/force' OpenXR Input Binding.
        /// </summary>
        public const string trackpadForce = "/input/trackpad/force";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/trackpad/touch' OpenXR Input Binding.
        /// </summary>
        public const string trackpadTouch = "/input/trackpad/touch";
        /// <summary>
        /// Constant for a pose interaction binding '.../input/grip/pose' OpenXR Input Binding.
        /// </summary>
        public const string grip = "/input/grip/pose";
        /// <summary>
        /// Constant for a pose interaction binding '.../input/aim/pose' OpenXR Input Binding.
        /// </summary>
        public const string aim = "/input/aim/pose";
        /// <summary>
        /// Constant for a haptic interaction binding '.../output/haptic' OpenXR Input Binding.
        /// </summary>
        public const string haptic = "/output/haptic";

        private const string kDeviceLocalizedName = "Index Controller OpenXR";

        /// <inheritdoc/>
        protected override void RegisterActionMapsWithRuntime()
        {
            ActionMapConfig actionMap = new ActionMapConfig()
            {
                name = "valveindexcontroller",
                localizedName = kDeviceLocalizedName,
                desiredInteractionProfile = profile,
                manufacturer = "Valve",
                serialNumber = "",
                deviceInfos = new List<DeviceConfig>()
                {
                    new DeviceConfig()
                    {
                        characteristics = (InputDeviceCharacteristics)(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.TrackedDevice | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left),
                        userPath = UserPaths.leftHand
                    },
                    new DeviceConfig()
                    {
                        characteristics = (InputDeviceCharacteristics)(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.TrackedDevice | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right),
                        userPath = UserPaths.rightHand
                    }
                },
                actions = new List<ActionConfig>()
                {
                    new ActionConfig()
                    {
                        name = "system",
                        localizedName = "System",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "MenuButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = system,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "systemTouched",
                        localizedName = "System Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "MenuTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = systemTouch,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "primaryButton",
                        localizedName = "Primary Button",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "PrimaryButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = buttonA,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "primaryTouched",
                        localizedName = "Primary Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "PrimaryTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = buttonATouch,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "secondaryButton",
                        localizedName = "Secondary Button",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "SecondaryButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = buttonB,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "secondaryTouched",
                        localizedName = "Secondary Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "SecondaryTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = buttonBTouch,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "grip",
                        localizedName = "Grip",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "Grip"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = squeeze,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "gripPressed",
                        localizedName = "Grip Pressed",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "GripButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = squeeze,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "gripForce",
                        localizedName = "Grip Force",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "GripForce"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = squeezeForce,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "trigger",
                        localizedName = "Trigger",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "Trigger"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = trigger,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "triggerPressed",
                        localizedName = "Triggger Pressed",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "TriggerButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = triggerClick,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "triggerTouched",
                        localizedName = "Trigger Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "TriggerTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = triggerTouch,
                                interactionProfileName = profile,
                            }
                        }
                    },

                    new ActionConfig()
                    {
                        name = "thumbstick",
                        localizedName = "Thumbstick",
                        type = ActionType.Axis2D,
                        usages = new List<string>()
                        {
                            "Primary2DAxis"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = thumbstick,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "thumbstickClicked",
                        localizedName = "Thumbstick Clicked",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "Primary2DAxisClick"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = thumbstickClick,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "thumbstickTouched",
                        localizedName = "Thumbstick Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "Primary2DAxisTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = thumbstickTouch,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "trackpad",
                        localizedName = "Trackpad",
                        type = ActionType.Axis2D,
                        usages = new List<string>()
                        {
                            "Secondary2DAxis"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = trackpad,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "trackpadForce",
                        localizedName = "Trackpad Force",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "Secondary2DAxisForce"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = trackpadForce,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    new ActionConfig()
                    {
                        name = "trackpadTouched",
                        localizedName = "Trackpad Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "Secondary2DAxisTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = trackpadTouch,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Device Pose
                    new ActionConfig()
                    {
                        name = "devicePose",
                        localizedName = "Device Pose",
                        type = ActionType.Pose,
                        usages = new List<string>()
                        {
                            "Device"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = grip,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Pointer Pose
                    new ActionConfig()
                    {
                        name = "pointer",
                        localizedName = "Pointer Pose",
                        type = ActionType.Pose,
                        usages = new List<string>()
                        {
                            "Pointer"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = aim,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Haptics
                    new ActionConfig()
                    {
                        name = "haptic",
                        localizedName = "Haptic Output",
                        type = ActionType.Vibrate,
                        usages = new List<string>() { "Haptic" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = haptic,
                                interactionProfileName = profile,
                            }
                        }
                    }
                }
            };

            AddActionMap(actionMap);
        }
    }
}
