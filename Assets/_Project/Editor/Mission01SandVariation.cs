using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Visual-only trial on the authored flat Mission01 surface. No extra layers
    // of coplanar geometry, custom shaders, collision or radar changes.
    internal static class Mission01SandVariation
    {
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/";
        private const string OriginalMesh = Art + "Mission01_Facets.asset";
        private const string OriginalMaterial = Art + "Mission01_Sand.mat";
        private const string TrialMesh = Art + "Mission01_SandVariation_Mesh.asset";
        private const string TrialMaterial = Art + "Mission01_SandVariation.mat";
        private const string TrialTexture = Art + "Mission01_SandVariation_Texture.asset";
        private const int Resolution = 1024;
        private const float Strength = 1f;

        private readonly struct Patch
        {
            public readonly Vector2 Center, Radius;
            public readonly float Angle, Phase, Amount;
            public readonly bool Warm;
            public Patch(float x, float z, float rx, float rz, float angle, float phase, float amount, bool warm)
            {
                Center = new Vector2(x, z); Radius = new Vector2(rx, rz);
                Angle = angle * Mathf.Deg2Rad; Phase = phase; Amount = amount; Warm = warm;
            }
        }

        // Hand-placed broad regions in the existing map's world X/Z coordinates.
        // Partial regions can continue beyond the map edge instead of making
        // every silhouette look like a closed, isolated puddle.
        private static readonly Patch[] Patches =
        {
            new Patch(-870,   30, 360, 245,  30, .7f,  .86f, true),
            new Patch(-305,  405, 320, 165, -45, 2.4f, .67f, false),
            new Patch( 125,  785, 335, 220,  24, 4.1f, .95f, true),
            new Patch(-760, 1460, 330, 260, -18, 1.8f, .83f, false),
            new Patch(  50, 1900, 290, 180,  12, 3.5f, .66f, true),
            new Patch( 890, 1700, 375, 190, -22, 5.3f, .78f, false),
            new Patch(1160, 1050, 300, 235,  38, 2.8f, .94f, true),
            new Patch(2050, 1120, 355, 255, -32,  .3f, .78f, false),
            new Patch(1665,  315, 260, 190,  62, 4.7f, .95f, true),
            new Patch(1840, -620, 390, 225, -25, 1.2f, .87f, false),
        };

        [MenuItem("Air Combat/Art/Apply Mission01 Sand Variation v0")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var filter = FindSurface(out var renderer);
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(OriginalMesh);
                var original = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterial);
                if (source == null || original == null)
                    throw new InvalidOperationException("Original sand mesh/material missing.");
                if (filter.sharedMesh != source && filter.sharedMesh != AssetDatabase.LoadAssetAtPath<Mesh>(TrialMesh))
                    throw new InvalidOperationException("Ground mesh changed; refusing to overwrite unrelated art.");
                if (renderer.sharedMaterial != original && renderer.sharedMaterial != AssetDatabase.LoadAssetAtPath<Material>(TrialMaterial))
                    throw new InvalidOperationException("Ground material changed; refusing to overwrite unrelated art.");
                var vertices = source.vertices;
                var originalUV = source.uv;
                if (vertices.Length == 0 || originalUV.Length != vertices.Length || source.bounds.size.y > .01f ||
                    originalUV.Any(uv => (uv - originalUV[0]).sqrMagnitude > 1e-10f))
                    throw new InvalidOperationException("This trial requires a flat, single-palette-color ground mesh.");
                var palette = original.GetTexture("_BaseMap") as Texture2D;
                if (palette == null || !palette.isReadable)
                    throw new InvalidOperationException("Readable sand palette missing.");
                var paletteUV = Vector2.Scale(originalUV[0], original.GetTextureScale("_BaseMap")) + original.GetTextureOffset("_BaseMap");
                int px = Mathf.Clamp(Mathf.FloorToInt(paletteUV.x * palette.width), 0, palette.width - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt(paletteUV.y * palette.height), 0, palette.height - 1);
                Color32 sand = palette.GetPixels32()[py * palette.width + px];
                Bounds bounds = renderer.bounds;
                if (bounds.size.x <= 0f || bounds.size.z <= 0f || bounds.size.y > .01f)
                    throw new InvalidOperationException("Unexpected world-space ground bounds.");

                var uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; ++i)
                {
                    Vector3 world = filter.transform.TransformPoint(vertices[i]);
                    uv[i] = new Vector2((world.x - bounds.min.x) / bounds.size.x,
                        (world.z - bounds.min.z) / bounds.size.z);
                }
                var mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = "Mission01 Sand Variation Mesh";
                mesh.uv = uv;
                var texture = BuildTexture(bounds, sand, !GraphicsFormatUtility.IsSRGBFormat(palette.graphicsFormat));
                mesh = SaveAsset(mesh, TrialMesh);
                texture = SaveAsset(texture, TrialTexture);
                var material = new Material(original) { name = "Mission01 Sand Variation" };
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", texture);
                    material.SetTextureScale("_MainTex", Vector2.one);
                    material.SetTextureOffset("_MainTex", Vector2.zero);
                }
                material = SaveAsset(material, TrialMaterial);
                Assign(filter, renderer, mesh, material);
                AssetDatabase.SaveAssets();
                Debug.Log("SAND-V0: 10 broad organic color regions applied to original geometry. Save scene after review.");
            }
            catch (Exception ex) { Debug.LogError("SAND-V0: " + ex); }
        }

        [MenuItem("Air Combat/Art/Restore Mission01 Original Sand")]
        private static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var filter = FindSurface(out var renderer);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(OriginalMesh);
                var material = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterial);
                if (mesh == null || material == null) throw new InvalidOperationException("Original sand assets missing.");
                if (filter.sharedMesh != mesh && filter.sharedMesh != AssetDatabase.LoadAssetAtPath<Mesh>(TrialMesh))
                    throw new InvalidOperationException("Ground mesh is not the sand trial.");
                if (renderer.sharedMaterial != material && renderer.sharedMaterial != AssetDatabase.LoadAssetAtPath<Material>(TrialMaterial))
                    throw new InvalidOperationException("Ground material is not the sand trial.");
                Assign(filter, renderer, mesh, material);
                Debug.Log("SAND-V0: original sand restored. Generated trial assets retained for reuse.");
            }
            catch (Exception ex) { Debug.LogError("SAND-V0: " + ex); }
        }

        private static MeshFilter FindSurface(out MeshRenderer renderer)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.name != "Mission01_FirstSortie")
                throw new InvalidOperationException("Open Mission01_FirstSortie in Edit Mode.");
            var ground = scene.GetRootGameObjects().Single(g => g.name == "Mission01 / Faceted surface");
            renderer = ground.GetComponent<MeshRenderer>();
            var filter = ground.GetComponent<MeshFilter>();
            if (renderer == null || filter == null || filter.sharedMesh == null || renderer.sharedMaterials.Length != 1)
                throw new InvalidOperationException("Expected the original single-material ground renderer.");
            return filter;
        }

        private static void Assign(MeshFilter filter, MeshRenderer renderer, Mesh mesh, Material material)
        {
            Undo.RecordObjects(new UnityEngine.Object[] { filter, renderer }, "Mission01 sand variation");
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(filter.gameObject.scene);
        }

        private static T SaveAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("Unexpected asset at " + path);
                AssetDatabase.CreateAsset(value, path);
                return value;
            }
            // Retain generated asset GUIDs and scene references on re-apply.
            EditorUtility.CopySerialized(value, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(value);
            return existing;
        }

        private static Texture2D BuildTexture(Bounds bounds, Color32 sand, bool linear)
        {
            var pixels = new Color32[Resolution * Resolution];
            for (int i = 0; i < pixels.Length; ++i) pixels[i] = sand;
            float dx = bounds.size.x / Resolution, dz = bounds.size.z / Resolution;
            foreach (var patch in Patches)
            {
                float c = Mathf.Cos(patch.Angle), s = Mathf.Sin(patch.Angle);
                float extent = Mathf.Max(patch.Radius.x, patch.Radius.y) * 1.55f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((patch.Center.x - extent - bounds.min.x) / dx), 0, Resolution - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((patch.Center.x + extent - bounds.min.x) / dx), 0, Resolution - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((patch.Center.y - extent - bounds.min.z) / dz), 0, Resolution - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((patch.Center.y + extent - bounds.min.z) / dz), 0, Resolution - 1);
                Color32 tint = patch.Warm ? Tint(sand, .97f, .91f, .83f) : Tint(sand, .88f, .90f, .93f);
                for (int z = z0; z <= z1; ++z)
                for (int x = x0; x <= x1; ++x)
                {
                    float wx = bounds.min.x + (x + .5f) * dx - patch.Center.x;
                    float wz = bounds.min.z + (z + .5f) * dz - patch.Center.y;
                    float u = (c * wx + s * wz) / patch.Radius.x;
                    float v = (-s * wx + c * wz) / patch.Radius.y;
                    float angle = Mathf.Atan2(v, u);
                    // Low-frequency asymmetry and long lobes, not random per-pixel noise.
                    float edge = 1f + .19f * Mathf.Sin(2f * angle + patch.Phase)
                        + .17f * Mathf.Sin(3f * angle - patch.Phase * .7f)
                        + .09f * Mathf.Cos(5f * angle + patch.Phase * 1.3f);
                    float r = Mathf.Sqrt(u * u + v * v) / edge;
                    float mask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.84f, 1f, r));
                    if (mask <= 0f) continue;
                    float interior = .77f + .23f * (1f - Mathf.Clamp01(r));
                    int index = z * Resolution + x;
                    pixels[index] = Color32.Lerp(pixels[index], tint, mask * interior * patch.Amount * Strength);
                }
            }
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, true, linear)
            {
                name = "Mission01 Sand Variation Texture", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static Color32 Tint(Color32 c, float r, float g, float b) => new Color32(
            (byte)Mathf.RoundToInt(c.r * r), (byte)Mathf.RoundToInt(c.g * g),
            (byte)Mathf.RoundToInt(c.b * b), c.a);
    }
}
