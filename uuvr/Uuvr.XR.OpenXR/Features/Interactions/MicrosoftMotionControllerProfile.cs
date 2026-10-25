// Adapted from the Unity OpenXR plugin (com.unity.xr.openxr) 1.4.2, copyright (c) 2020 Unity Technologies ApS,
// licensed under the Unity Companion License (https://unity3d.com/legal/licenses/unity_companion_license).
// Changed for SBGVR on 2026-10-24: removed the Input System device layout and the Editor-only attributes,
// keeping only the action map that tells the OpenXR runtime which controller inputs to report.

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace UnityEngine.XR.OpenXR.Features.Interactions
{
    /// <summary>
    /// This <see cref="OpenXRInteractionFeature"/> enables the use of Microosft Motion Controllers interaction profiles in OpenXR.
    /// </summary>
    public class MicrosoftMotionControllerProfile : OpenXRInteractionFeature
    {
        /// <summary>
        /// The feature id string. This is used to give the feature a well known id for reference.
        /// </summary>
        public const string featureId = "com.unity.openxr.feature.input.microsoftmotioncontroller";

        /// <summary>
        /// The interaction profile string used to reference the <a href="https://www.khronos.org/registry/OpenXR/specs/1.0/html/xrspec.html#_microsoft_mixed_reality_motion_controller_profile">Microsoft Mixed Reality Motion Controller</a>.
        /// </summary>
        public const string profile = "/interaction_profiles/microsoft/motion_controller";

        /// <summary>
        /// Constant for a boolean interaction binding '.../input/menu/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string menu = "/input/menu/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/squeeze/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string squeeze = "/input/squeeze/click";
        /// <summary>
        /// Constant for a float interaction binding '.../input/trigger/value' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string trigger = "/input/trigger/value";
        /// <summary>
        /// Constant for a Vector2 interaction binding '.../input/thumbstick' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string thumbstick = "/input/thumbstick";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/thumbstick/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string thumbstickClick = "/input/thumbstick/click";
        /// <summary>
        /// Constant for a Vector2 interaction binding '.../input/trackpad' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string trackpad = "/input/trackpad";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/trackpad/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string trackpadClick = "/input/trackpad/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/trackpad/touch' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string trackpadTouch = "/input/trackpad/touch";
        /// <summary>
        /// Constant for a pose interaction binding '.../input/grip/pose' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string grip = "/input/grip/pose";
        /// <summary>
        /// Constant for a pose interaction binding '.../input/aim/pose' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string aim = "/input/aim/pose";
        /// <summary>
        /// Constant for a haptic interaction binding '.../output/haptic' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string haptic = "/output/haptic";

        private const string kDeviceLocalizedName = "Windows MR Controller OpenXR";

        /// <inheritdoc/>
        protected override void RegisterActionMapsWithRuntime()
        {
            ActionMapConfig actionMap = new ActionMapConfig()
            {
                name = "microsoftmotioncontroller",
                localizedName = kDeviceLocalizedName,
                desiredInteractionProfile = profile,
                manufacturer = "Microsoft",
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
                // Joystick
                new ActionConfig()
                {
                    name = "joystick",
                    localizedName = "Joystick",
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
                // Touchpad
                new ActionConfig()
                {
                    name = "touchpad",
                    localizedName = "Touchpad",
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
                // Grip
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
                // Grip Pressed
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
                // Menu
                new ActionConfig()
                {
                    name = "menu",
                    localizedName = "Menu",
                    type = ActionType.Binary,
                    usages = new List<string>()
                    {
                        "MenuButton"
                    },
                    bindings = new List<ActionBinding>()
                    {
                        new ActionBinding()
                        {
                            interactionPath = menu,
                            interactionProfileName = profile,
                        }
                    }
                },
                // Trigger
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
                // Trigger Pressed
                new ActionConfig()
                {
                    name = "triggerPressed",
                    localizedName = "Trigger Pressed",
                    type = ActionType.Binary,
                    usages = new List<string>()
                    {
                        "TriggerButton"
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
                //Joystick Clicked
                new ActionConfig()
                {
                    name = "joystickClicked",
                    localizedName = "JoystickClicked",
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
                //Touchpad Clicked
                new ActionConfig()
                {
                    name = "touchpadClicked",
                    localizedName = "Touchpad Clicked",
                    type = ActionType.Binary,
                    usages = new List<string>()
                    {
                        "Secondary2DAxisClick"
                    },
                    bindings = new List<ActionBinding>()
                    {
                        new ActionBinding()
                        {
                            interactionPath = trackpadClick,
                            interactionProfileName = profile,
                        }
                    }
                },
                //Touchpad Touched
                new ActionConfig()
                {
                    name = "touchpadTouched",
                    localizedName = "Touchpad Touched",
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
