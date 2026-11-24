#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AirCombat
{
    // B1d2: one-time correction to an already-installed B1d TerrainLayer.
    // Does not regenerate B1d roads, replace the TerrainData, or touch scene objects.
    public static class RefinePreparedGroundB1D2
    {
        private const string TerrainPath = "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_Terrain.asset";
        private const string LayerPath = "Assets/_Project/Art/Environment/BaseRoadB1d/PreparedGround.terrainlayer";
        private const string TexturePath = "Assets/_Project/Art/Environment/BaseRoadB1d/PreparedGround.png";
        private const float GroundOpacity = .97f;
        private const float EdgeFeather = 1.8f; // ~1-2 alpha-map texels, not the old 10-13 m halo.

        [MenuItem("Air Combat/Art/B1d2 - Refine Prepared Ground")]
        private static void FromMenu()
        {
            try { Debug.Log(Apply()); }
            catch (Exception ex) { Debug.LogError("B1d2 ground refinement failed: " + ex); }
        }

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before refining the painted terrain.");

            var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (terrain == null || layer == null || texture == null || !File.Exists(TexturePath))
                throw new InvalidOperationException("Missing committed B1d TerrainData/ground layer/texture. Install B1d first.");
            var layers = terrain.terrainLayers;
            if (layers == null || layers.Length != 2 || layers[1] != layer || layer.diffuseTexture != texture)
                throw new InvalidOperationException("Terrain layers differ from installed B1d. Refusing to overwrite hand-painted layers.");

            // Refuse repeats / significant hand edits. B1d originally painted the
            // inner barracks region at ~0.82 coverage, well below new 0.97.
            int sampleX = ToX(-485f, -1000f, terrain.size.x, terrain.alphamapWidth);
            int sampleZ = ToX(-269f, -1000f, terrain.size.z, terrain.alphamapHeight);
            float installedCoverage = terrain.GetAlphamaps(sampleX, sampleZ, 1, 1)[0, 0, 1];
            if (installedCoverage < .70f || installedCoverage > .90f)
                throw new InvalidOperationException($"Unexpected B1d soil coverage {installedCoverage:F2} at barracks. Already refined or manually edited; no overwrite.");

            // Only repaint the existing B1d local patch, never the whole map.
            // The terrain footprint and terrain origin are the authored E1 constants.
            int minX = ToX(-575f, -1000f, terrain.size.x, terrain.alphamapWidth);
            int maxX = ToX(-363f, -1000f, terrain.size.x, terrain.alphamapWidth);
            int minZ = ToX(-464f, -1000f, terrain.size.z, terrain.alphamapHeight);
            int maxZ = ToX(-204f, -1000f, terrain.size.z, terrain.alphamapHeight);
            int countX = maxX - minX + 1;
            int countZ = maxZ - minZ + 1;
            if (countX <= 0 || countZ <= 0)
                throw new InvalidOperationException("Prepared soil region lies outside the authored E1 terrain.");

            var updated = new float[countZ, countX, 2];
            for (int z = 0; z < countZ; z++)
            for (int x = 0; x < countX; x++)
            {
                float wx = -1000f + (minX + x + .5f) / terrain.alphamapWidth * terrain.size.x;
                float wz = -1000f + (minZ + z + .5f) / terrain.alphamapHeight * terrain.size.z;
                float mask = Mathf.Max(
                    RoundedRect(wx, wz, -485f, -269f, 73f, 47f, 11f),
                    RoundedRect(wx, wz, -422f, -331f, 43f, 54f, 13f),
                    RoundedRect(wx, wz, -453f, -373f, 19f, 71f, 8f));
                float prepared = GroundOpacity * mask;
                updated[z, x, 0] = 1f - prepared;
                updated[z, x, 1] = prepared;
            }

            // Reimport at the SAME path: TerrainLayer/texture GUIDs remain stable.
            // Restore original bytes if the terrain update itself throws.
            byte[] originalPng = File.ReadAllBytes(TexturePath);
            try
            {
                WriteWarmTexture();
                AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
                Undo.RegisterCompleteObjectUndo(terrain, "B1d2 warm prepared soil with crisp edges");
                terrain.SetAlphamaps(minX, minZ, updated);
                EditorUtility.SetDirty(terrain);
                AssetDatabase.SaveAssets();
            }
            catch
            {
                File.WriteAllBytes(TexturePath, originalPng);
                AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
            foreach (var sceneTerrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                if (sceneTerrain.terrainData == terrain) sceneTerrain.Flush();

            return "B1d2 applied: warm #A38B72, almost solid terrain paint, ~2 m irregular edge. " +
                   "No buildings/roads/plateau changed. Commit E1_Terrain.asset and PreparedGround.png.";
        }

        private static int ToX(float world, float start, float terrainSize, int resolution) =>
            Mathf.Clamp(Mathf.FloorToInt((world - start) / terrainSize * resolution), 0, resolution - 1);

        private static float RoundedRect(float x, float z, float cx, float cz,
            float halfX, float halfZ, float radius)
        {
            float dx = Mathf.Abs(x - cx) - halfX + radius;
            float dz = Mathf.Abs(z - cz) - halfZ + radius;
            float outside = Mathf.Sqrt(Mathf.Max(0f, dx) * Mathf.Max(0f, dx) +
                                       Mathf.Max(0f, dz) * Mathf.Max(0f, dz));
            float sdf = outside + Mathf.Min(Mathf.Max(dx, dz), 0f) - radius;
            // Deliberate slight irregularity without blurry 10+ meter feathering.
            sdf += 1.4f * (Mathf.PerlinNoise(x * .043f + 9f, z * .043f + 3f) - .5f);
            float edge = Mathf.Clamp01((EdgeFeather - sdf) / (2f * EdgeFeather));
            return Mathf.SmoothStep(0f, 1f, edge);
        }

        private static void WriteWarmTexture()
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, true, false);
            const float tau = Mathf.PI * 2f;
            if (!ColorUtility.TryParseHtmlString("#A38B72", out var color))
                throw new InvalidOperationException("Invalid B1d2 soil color.");
            try
            {
                for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    float u = x / (float)tex.width;
                    float v = y / (float)tex.height;
                    float n = .004f * Mathf.Sin(tau * u) * Mathf.Cos(tau * v) +
                              .002f * Mathf.Cos(2f * tau * u + .5f) * Mathf.Sin(tau * v);
                    tex.SetPixel(x, y, new Color(Mathf.Clamp01(color.r + n),
                        Mathf.Clamp01(color.g + n * .9f), Mathf.Clamp01(color.b + n * .75f), 1f));
                }
                tex.Apply(true);
                File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }
    }
}
#endif
