// SBGVR: Super Battle Golf fixes for UUVR.
// Copyright (c) 2026 lmkHenlo. Licensed under the GNU GPL v3.0 or later; see LICENSE.

using System.IO;
using UnityEngine;

namespace SBGVR
{
    /// <summary>Loads the PNG images embedded in SBGVR.dll (see <c>EmbeddedResource</c> in SBGVR.csproj).</summary>
    internal static class EmbeddedImage
    {
        /// <summary>
        /// Loads an embedded PNG as an sRGB texture, with mipmaps so it stays smooth at any size or distance,
        /// clamped and trilinear. The CPU copy is released once it's on the GPU. Throws if it's missing or
        /// can't be read.
        /// </summary>
        internal static Texture2D Load(string resource, string textureName)
        {
            byte[] bytes;
            using (var stream = typeof(EmbeddedImage).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null) throw new FileNotFoundException($"{resource} is missing from SBGVR.dll");
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (texture.LoadImage(bytes, true)) return texture;

            Object.Destroy(texture);
            throw new InvalidDataException($"{resource} couldn't be read");
        }
    }
}
