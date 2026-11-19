using System;
using UnityEngine;

namespace AirCombat
{
    // One small color map per chunk. Edge texels sample identical world coordinates
    // in adjacent chunks. Soft contact shadows do not depend on camera shadow distance.
    internal sealed class LandscapeGroundPaint
    {
        private const int Resolution = 96;
        private const float Size = LandscapeChunkBuilder.Size;
        private const float Step = Size / (Resolution - 1);
        private readonly Color32[] pixels = new Color32[Resolution * Resolution];

        public LandscapeGroundPaint(float originX, float originZ, Func<float, float, Color> tint)
        {
            for (int z = 0; z < Resolution; z++)
            for (int x = 0; x < Resolution; x++)
            {
                // Explicit end samples avoid roundoff seams on tile boundaries.
                float lx = x == Resolution - 1 ? Size : x * Step;
                float lz = z == Resolution - 1 ? Size : z * Step;
                pixels[z * Resolution + x] = tint(originX + lx, originZ + lz);
            }
        }

        public static Vector2 Uv(Vector3 local)
        {
            // Match the first/last texel centers to the exact chunk edges.
            return new Vector2((local.x / Size * (Resolution - 1) + 0.5f) / Resolution,
                (local.z / Size * (Resolution - 1) + 0.5f) / Resolution);
        }

        public void AddShadow(float x, float z, float size, bool tree)
        {
            float radius = size * (tree ? 0.72f : 0.60f);
            // Existing sun points toward negative X, positive Z.
            float cx = x - size * 0.18f, cz = z + size * 0.22f;
            int minX = Mathf.Max(0, Mathf.FloorToInt((cx - radius) / Step));
            int maxX = Mathf.Min(Resolution - 1, Mathf.CeilToInt((cx + radius) / Step));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((cz - radius) / Step));
            int maxZ = Mathf.Min(Resolution - 1, Mathf.CeilToInt((cz + radius) / Step));
            for (int j = minZ; j <= maxZ; j++)
            for (int i = minX; i <= maxX; i++)
            {
                float px = i * Step, pz = j * Step;
                float dx = px - cx, dz = pz - cz;
                float distance = Mathf.Sqrt(dx * dx + dz * dz) / radius;
                if (distance >= 1f) continue;
                // Fade before the chunk border so a shadow never leaves a visible seam.
                float edge = Mathf.Min(Mathf.Min(i, Resolution - 1 - i), Mathf.Min(j, Resolution - 1 - j));
                float opacity = (1f - Mathf.SmoothStep(0f, 1f, distance)) * Mathf.Clamp01(edge * 0.5f);
                float factor = 1f - opacity * (tree ? 0.40f : 0.30f);
                int index = j * Resolution + i;
                var c = pixels[index];
                pixels[index] = new Color32((byte)(c.r * factor), (byte)(c.g * factor), (byte)(c.b * factor), 255);
            }
        }

        public Texture2D ToTexture(string name)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
