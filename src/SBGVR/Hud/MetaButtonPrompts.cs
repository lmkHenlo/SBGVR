// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace SBGVR.Hud
{
    /// <summary>
    /// While you play with Meta Quest Touch controllers (VR Controls.MotionControls), the game's button prompts
    /// show the controllers' own buttons instead of an Xbox controller's: A, B, X, Y and Menu, the thumbsticks
    /// (L, R), triggers (LT, RT) and grip buttons (LG, RG), outlined in the left or right controller's colour. Each prompt shows the button that sends its gamepad input (MotionControls), including
    /// MirrorButtons; the D-pad and View button, which take the grip button held, show both.
    /// </summary>
    /// <remarks>
    /// The game draws every prompt from its current icon set (InputManager.currentDeviceIconSet): sprites for
    /// images, and TextMeshPro sprite tags for prompts in text. The controllers reach the game as a gamepad, so
    /// the game picks its Xbox set. While the controllers are the device in use, this puts in a copy of that set
    /// whose icons are SBGVR's (Assets/meta-button-prompts.png, drawn by tools/button-prompts),
    /// keeping its binding paths and merges, and tells the game's prompts to refresh, as the game does when the
    /// device changes. The game's set comes back as soon as anything else is used, or VR stops. Nothing about
    /// the input or the prompts themselves changes. The left thumbstick press has no controller input
    /// (MotionControls sends nothing as L3), so that one prompt keeps the game's icon.
    /// </remarks>
    internal sealed class MetaButtonPrompts : MonoBehaviour
    {
        private const string ImageResource = "SBGVR.meta-button-prompts.png";
        internal const string SpriteAssetName = "SBGVR Meta Quest Touch";
        private const int Cell = 128, Columns = 8;

        /// <summary>The atlas's cells in order; tools/button-prompts/button-prompts.html draws them in this order.</summary>
        internal static readonly string[] GlyphNames =
        {
            "StickLeft", "StickLeft_Up", "StickLeft_Down", "StickLeft_LeftDir", "StickLeft_RightDir",
            "StickLeft_Horizontal", "StickLeft_Vertical", "StickLeft_Press", "TriggerLeft", "GripLeft",
            "StickRight", "StickRight_Up", "StickRight_Down", "StickRight_LeftDir", "StickRight_RightDir",
            "StickRight_Horizontal", "StickRight_Vertical", "StickRight_Press", "TriggerRight", "GripRight",
            "ButtonA", "ButtonB", "ButtonX", "ButtonY", "ButtonMenu",
            "GripLeft+StickRight", "GripLeft+StickRight_Up", "GripLeft+StickRight_Down",
            "GripLeft+StickRight_LeftDir", "GripLeft+StickRight_RightDir", "GripLeft+StickRight_Horizontal",
            "GripLeft+StickRight_Vertical", "GripLeft+ButtonB",
            "GripRight+StickLeft", "GripRight+StickLeft_Up", "GripRight+StickLeft_Down",
            "GripRight+StickLeft_LeftDir", "GripRight+StickLeft_RightDir", "GripRight+StickLeft_Horizontal",
            "GripRight+StickLeft_Vertical", "GripRight+ButtonY",
        };

        /// <summary>
        /// Gamepad binding → icon, as MotionControls sends them: the left controller moves (left stick, X/Y, LB,
        /// LT), the right one turns the camera and swings (right stick, A/B, RB, RT); the left grip button held
        /// turns the right thumbstick into the D-pad and B into View; Menu is Start. The X button and pressing the
        /// left thumbstick both send X (west); its prompts (Interact, and leaving the golf cart) show the
        /// thumbstick press.
        /// </summary>
        /// <remarks>
        /// Follows MotionControls.ReadState, which decides what each button sends. Change it together with
        /// that, tools/controller-layout/controller-layout.html (the menu panel's images) and the docs (README.md's
        /// Motion controls tables, thunderstore/README.md's VR Controls summary, and the MotionControls,
        /// MirrorButtons and FirstPersonControllerToggle descriptions in Settings.cs). TEST_PLAN.md step 17 checks
        /// that they agree.
        /// </remarks>
        internal static readonly Dictionary<string, string> Layout = new Dictionary<string, string>
        {
            ["<Gamepad>/buttonSouth"] = "ButtonA",
            ["<Gamepad>/buttonEast"] = "ButtonB",
            ["<Gamepad>/buttonWest"] = "StickLeft_Press",
            ["<Gamepad>/buttonNorth"] = "ButtonY",
            ["<Gamepad>/leftShoulder"] = "GripLeft",
            ["<Gamepad>/rightShoulder"] = "GripRight",
            ["<Gamepad>/leftTrigger"] = "TriggerLeft",
            ["<Gamepad>/rightTrigger"] = "TriggerRight",
            ["<Gamepad>/leftStick"] = "StickLeft",
            ["<Gamepad>/leftStick/x"] = "StickLeft_Horizontal",
            ["<Gamepad>/leftStick/y"] = "StickLeft_Vertical",
            ["<Gamepad>/leftStick/up"] = "StickLeft_Up",
            ["<Gamepad>/leftStick/down"] = "StickLeft_Down",
            ["<Gamepad>/leftStick/left"] = "StickLeft_LeftDir",
            ["<Gamepad>/leftStick/right"] = "StickLeft_RightDir",
            ["<Gamepad>/rightStick"] = "StickRight",
            ["<Gamepad>/rightStick/x"] = "StickRight_Horizontal",
            ["<Gamepad>/rightStick/y"] = "StickRight_Vertical",
            ["<Gamepad>/rightStick/up"] = "StickRight_Up",
            ["<Gamepad>/rightStick/down"] = "StickRight_Down",
            ["<Gamepad>/rightStick/left"] = "StickRight_LeftDir",
            ["<Gamepad>/rightStick/right"] = "StickRight_RightDir",
            ["<Gamepad>/rightStickPress"] = "StickRight_Press",
            ["<Gamepad>/start"] = "ButtonMenu",
            ["<Gamepad>/select"] = "GripLeft+ButtonB",
            ["<Gamepad>/dpad"] = "GripLeft+StickRight",
            ["<Gamepad>/dpad/x"] = "GripLeft+StickRight_Horizontal",
            ["<Gamepad>/dpad/y"] = "GripLeft+StickRight_Vertical",
            ["<Gamepad>/dpad/up"] = "GripLeft+StickRight_Up",
            ["<Gamepad>/dpad/down"] = "GripLeft+StickRight_Down",
            ["<Gamepad>/dpad/left"] = "GripLeft+StickRight_LeftDir",
            ["<Gamepad>/dpad/right"] = "GripLeft+StickRight_RightDir",
        };

        /// <summary>
        /// The same with VR Controls.MirrorButtons: the right controller moves and the left one turns the camera
        /// and swings, X and Y act as A and B, the right grip button opens the D-pad layer. Menu stays Start.
        /// </summary>
        internal static readonly Dictionary<string, string> MirroredLayout = Mirror(Layout);

        private static bool _enabled;
        private static bool _failed;
        private static Texture2D _atlas;
        private static Dictionary<string, Sprite> _sprites;
        private static TMP_SpriteAsset _spriteAsset;
        private static InputIconsSet _set;
        private static InputIconsSet _mirroredSet;

        private void OnEnable()
        {
            _enabled = true;
            TMP_Text.OnSpriteAssetRequest += FindSpriteAsset;
        }

        private void OnDisable()
        {
            _enabled = false;
            Refresh();
            TMP_Text.OnSpriteAssetRequest -= FindSpriteAsset;
        }

        // Catches what the game's device switch doesn't: VR starting or stopping, a controller connecting, and
        // MirrorButtons changing.
        private void LateUpdate() => Refresh();

        /// <summary>
        /// Puts the Meta set in while the Touch controllers are the device in use, and the game's own set back
        /// otherwise. Runs after the game picks the device's set (MetaButtonPromptPatches) and every frame.
        /// </summary>
        internal static void Refresh()
        {
            // After a failure nothing of ours is in use unless a set was already built; then put the game's back.
            if (_failed && _set == null && _mirroredSet == null) return;
            try
            {
                RefreshSet();
            }
            catch (Exception exception)
            {
                if (_failed) return;
                _failed = true;
                Plugin.Log.LogError($"[FAILED] Meta Quest button prompts: {exception.GetBaseException().Message}. " +
                                    "The game's own button prompts stay.");
            }
        }

        private static void RefreshSet()
        {
            if (Game.CurrentSetField == null || Game.IconSetsField == null) return;
            var current = Game.CurrentSetField.GetValue(null) as InputIconsSet;
            if (current == null) return; // the game's input isn't set up yet

            var wanted = _enabled && !_failed && VrState.IsRunning && Controls.MotionControls.InUse &&
                         Controls.MotionControls.TouchControllers
                ? GetSet(Settings.MirrorButtons.Value)
                : null;

            if (wanted != null)
            {
                if (!ReferenceEquals(current, wanted)) Switch(wanted, "Meta Quest Touch");
            }
            else if (ReferenceEquals(current, _set) || ReferenceEquals(current, _mirroredSet))
            {
                var sets = Game.IconSetsField.GetValue(null) as InputIconsSets;
                Switch(sets != null ? sets.GetIconSet(InputManager.CurrentDeviceType) : null, "the game's");
            }
        }

        private static void Switch(InputIconsSet set, string which)
        {
            if (set == null) return;
            Game.CurrentSetField.SetValue(null, set);
            Plugin.Log.LogInfo($"Button prompts: showing {which} icons.");
            try
            {
                // What the game itself raises when the device changes: every prompt redraws with the new set.
                (Game.SwitchedField?.GetValue(null) as Action)?.Invoke();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"Button prompts: a prompt couldn't refresh: {exception.GetBaseException().Message}");
            }
        }

        private static TMP_SpriteAsset FindSpriteAsset(int hashCode, string name) =>
            name == SpriteAssetName ? _spriteAsset : null;

        private static InputIconsSet GetSet(bool mirrored)
        {
            var set = mirrored ? _mirroredSet : _set;
            if (set != null) return set;
            try
            {
                set = BuildSet(mirrored ? MirroredLayout : Layout, mirrored);
            }
            catch (Exception exception)
            {
                _failed = true;
                Plugin.Log.LogError($"[FAILED] Meta Quest button prompts: {exception.GetBaseException().Message}. " +
                                    "The game's own button prompts stay.");
                return null;
            }
            if (mirrored) _mirroredSet = set;
            else _set = set;
            return set;
        }

        /// <summary>
        /// A copy of the game's Xbox icon set with Meta icons. The copy keeps the game's binding paths and merges
        /// (a stick's up and down become its vertical icon, and so on), so prompts find their icons exactly as
        /// before.
        /// </summary>
        private static InputIconsSet BuildSet(Dictionary<string, string> layout, bool mirrored)
        {
            var sets = Game.IconSetsField.GetValue(null) as InputIconsSets;
            var xbox = sets != null ? sets.GetIconSet(InputManager.DeviceType.Xbox) : null;
            if (xbox == null || Game.SetIcons(xbox) == null || Game.SetSpriteAsset(xbox) == null)
                throw new InvalidOperationException("the game's gamepad icons weren't found");
            LoadIcons(Game.SetSpriteAsset(xbox));

            var collection = Instantiate(Game.SetIcons(xbox));
            collection.name = SpriteAssetName + (mirrored ? " icons (mirrored)" : " icons");
            collection.hideFlags = HideFlags.HideAndDontSave;
            var replaced = 0;
            foreach (var icon in Game.CollectionIcons(collection))
            {
                if (icon == null || Game.IconBindingPath(icon) == null ||
                    !layout.TryGetValue(Game.IconBindingPath(icon), out var glyph)) continue;
                Game.IconSprite(icon) = _sprites[glyph];
                Game.IconName(icon) = glyph;
                replaced++;
            }

            var set = new InputIconsSet();
            Game.SetIcons(set) = collection;
            Game.SetSpriteAsset(set) = _spriteAsset;
            Plugin.Log.LogInfo($"[ok] Meta Quest button prompts{(mirrored ? " (MirrorButtons)" : "")}: " +
                               $"{replaced} of {Game.CollectionIcons(collection).Length} gamepad icons replaced.");
            return set;
        }

        /// <summary>
        /// The icon atlas as sprites (for prompts that are images) and as a TextMeshPro sprite asset (for prompts
        /// in text), sized and placed exactly like the game's own gamepad icons. Icons the atlas doesn't have
        /// come from the game's sprite asset, set as the fallback.
        /// </summary>
        private static void LoadIcons(TMP_SpriteAsset gameAsset)
        {
            if (_spriteAsset != null) return;

            _atlas = LoadAtlas();
            _sprites = new Dictionary<string, Sprite>();
            var gameGlyph = gameAsset.spriteGlyphTable.Count > 0 ? gameAsset.spriteGlyphTable[0] : null;
            var metrics = gameGlyph != null ? gameGlyph.metrics : new GlyphMetrics(100f, 100f, -2f, 67.5f, 100f);
            var scale = gameGlyph != null ? gameGlyph.scale : 1.4f;

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = SpriteAssetName;
            asset.hideFlags = HideFlags.HideAndDontSave;
            Game.AssetVersion(asset) = "1.1.0";
            asset.spriteSheet = _atlas;
            asset.material = new Material(gameAsset.material)
            {
                name = SpriteAssetName + " material",
                mainTexture = _atlas,
                hideFlags = HideFlags.HideAndDontSave,
            };
            asset.fallbackSpriteAssets = new List<TMP_SpriteAsset> { gameAsset };

            var glyphs = new List<TMP_SpriteGlyph>();
            var characters = new List<TMP_SpriteCharacter>();
            for (var i = 0; i < GlyphNames.Length; i++)
            {
                // Texture coordinates start at the bottom; the atlas's cells are numbered from the top.
                var x = i % Columns * Cell;
                var y = _atlas.height - (i / Columns + 1) * Cell;
                var sprite = Sprite.Create(_atlas, new Rect(x, y, Cell, Cell), new Vector2(0.5f, 0.5f), Cell, 0,
                    SpriteMeshType.FullRect);
                sprite.name = GlyphNames[i];
                sprite.hideFlags = HideFlags.HideAndDontSave;
                _sprites[GlyphNames[i]] = sprite;

                var glyph = new TMP_SpriteGlyph((uint)i, metrics, new GlyphRect(x, y, Cell, Cell), scale, 0, sprite);
                glyphs.Add(glyph);
                characters.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = GlyphNames[i] });
            }
            Game.AssetGlyphs(asset) = glyphs;
            Game.AssetCharacters(asset) = characters;
            asset.UpdateLookupTables();
            _spriteAsset = asset;
        }

        private static Texture2D LoadAtlas()
        {
            // Mipmapped, so the icons stay smooth on the floating screen at any size.
            var texture = EmbeddedImage.Load(ImageResource, SpriteAssetName + " icons");
            if (texture.width != Columns * Cell || texture.height < (GlyphNames.Length + Columns - 1) / Columns * Cell)
                throw new InvalidDataException($"the button prompt icons are {texture.width} x {texture.height}, not {Columns * Cell} wide");
            return texture;
        }

        /// <summary>
        /// Swaps each controller part for the other controller's (A↔X, B↔Y, left↔right thumbstick, trigger and
        /// grip button). Directions stay as they are, and Menu stays: it only exists on the left controller.
        /// </summary>
        private static Dictionary<string, string> Mirror(Dictionary<string, string> layout)
        {
            var swap = new Dictionary<string, string>
            {
                ["StickLeft"] = "StickRight", ["StickRight"] = "StickLeft",
                ["GripLeft"] = "GripRight", ["GripRight"] = "GripLeft",
                ["TriggerLeft"] = "TriggerRight", ["TriggerRight"] = "TriggerLeft",
                ["ButtonA"] = "ButtonX", ["ButtonX"] = "ButtonA",
                ["ButtonB"] = "ButtonY", ["ButtonY"] = "ButtonB",
            };
            var mirrored = new Dictionary<string, string>();
            foreach (var pair in layout)
                mirrored[pair.Key] = Regex.Replace(pair.Value, @"(Stick|Grip|Trigger)(Left|Right)|Button[ABXY]\b",
                    match => swap[match.Value]);
            return mirrored;
        }

        /// <summary>
        /// The game's and TextMeshPro's fields this needs. Kept apart, so if an update renamed one, the error is
        /// caught in Refresh (and the feature turns off) instead of breaking the game's device switch.
        /// </summary>
        private static class Game
        {
            internal static readonly FieldInfo IconSetsField = AccessTools.Field(typeof(InputManager), "iconSets");
            internal static readonly FieldInfo CurrentSetField = AccessTools.Field(typeof(InputManager), "currentDeviceIconSet");
            internal static readonly FieldInfo SwitchedField = AccessTools.Field(typeof(InputManager), "SwitchedInputDeviceType");

            internal static readonly AccessTools.FieldRef<InputIconsSet, InputIconCollection> SetIcons =
                AccessTools.FieldRefAccess<InputIconsSet, InputIconCollection>("icons");
            internal static readonly AccessTools.FieldRef<InputIconsSet, TMP_SpriteAsset> SetSpriteAsset =
                AccessTools.FieldRefAccess<InputIconsSet, TMP_SpriteAsset>("spriteAsset");
            internal static readonly AccessTools.FieldRef<InputIconCollection, InputIcon[]> CollectionIcons =
                AccessTools.FieldRefAccess<InputIconCollection, InputIcon[]>("icons");
            internal static readonly AccessTools.FieldRef<InputIcon, string> IconBindingPath =
                AccessTools.FieldRefAccess<InputIcon, string>("bindingPath");
            internal static readonly AccessTools.FieldRef<InputIcon, string> IconName =
                AccessTools.FieldRefAccess<InputIcon, string>("iconName");
            internal static readonly AccessTools.FieldRef<InputIcon, Sprite> IconSprite =
                AccessTools.FieldRefAccess<InputIcon, Sprite>("icon");

            // TextMeshPro only sets these itself (in the editor), so a sprite asset made at runtime sets them directly.
            internal static readonly AccessTools.FieldRef<TMP_Asset, string> AssetVersion =
                AccessTools.FieldRefAccess<TMP_Asset, string>("m_Version");
            internal static readonly AccessTools.FieldRef<TMP_SpriteAsset, List<TMP_SpriteGlyph>> AssetGlyphs =
                AccessTools.FieldRefAccess<TMP_SpriteAsset, List<TMP_SpriteGlyph>>("m_GlyphTable");
            internal static readonly AccessTools.FieldRef<TMP_SpriteAsset, List<TMP_SpriteCharacter>> AssetCharacters =
                AccessTools.FieldRefAccess<TMP_SpriteAsset, List<TMP_SpriteCharacter>>("m_SpriteCharacterTable");
        }
    }

    /// <summary>Puts the Meta set back in right after the game picks the icon set for the device in use.</summary>
    [HarmonyPatch(typeof(InputManager), "RefreshCurrentDeviceType")]
    internal static class MetaButtonPromptPatches
    {
        [HarmonyPostfix]
        private static void AfterRefreshCurrentDeviceType() => MetaButtonPrompts.Refresh();
    }
}
