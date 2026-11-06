// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using UnityEngine;

namespace SBGVR.Rendering
{
    /// <summary>
    /// The depth test of Unity's UI shaders (UI/Default, TextMeshPro and the like), as a CompareFunction:
    /// Always draws over the scenery, LessEqual behind it. Those shaders read it without always declaring it in
    /// their Properties block, so set it without checking HasProperty.
    /// </summary>
    internal static class GuiZTest
    {
        internal const string Name = "unity_GUIZTestMode";
        internal static readonly int Id = Shader.PropertyToID(Name);
    }
}
