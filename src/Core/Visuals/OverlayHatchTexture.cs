using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    // one row of alpha that repeats along the hatch uv. each texel gets the share of it the line covers,
    // so line edges stay soft instead of stepping
    internal static class OverlayHatchTexture
    {
        private const int TextureWidth = 256;

        private static readonly Color32[] Pixels = new Color32[TextureWidth];

        public static Texture2D Create() =>
            new Texture2D(TextureWidth, 1, TextureFormat.RGBA32, mipChain: true)
            {
                name = "PlantBiomeHeeler_HatchLines",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
                hideFlags = HideFlags.HideAndDontSave
            };

        public static void Fill(Texture2D texture, float lineFraction)
        {
            float lineEnd = lineFraction * TextureWidth;
            for (int texel = 0; texel < TextureWidth; texel++)
            {
                float coverage = Mathf.Clamp01(lineEnd - texel);
                Pixels[texel] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
            }

            texture.SetPixels32(Pixels);
            texture.Apply(updateMipmaps: true);
        }
    }
}
