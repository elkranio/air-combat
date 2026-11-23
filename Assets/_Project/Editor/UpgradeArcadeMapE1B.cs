#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // One-time, non-destructive E1 scene upgrade. New authored OBJ meshes are
    // immutable source assets. The old small stones remain disabled for A/B.
    // Never rerun the original E1 scene builder; never mutate shared prefabs.
    public static class UpgradeArcadeMapE1B
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string ArtRoot = "Assets/_Project/Art/Environment/ArcadeDesertE1";
        private const string ModelRoot = ArtRoot + "/Models";
        private const string Output = ArtRoot + "/E1B";
        private const float NewFlightHeight = 78f;

        private static readonly string[] ModelNames =
            { "Chunk_Broad", "Chunk_Ledge", "Chunk_Upright" };
        private static readonly string[] OriginalPaint =
            { "Kestrel_Hull", "Kestrel_Upper", "Kestrel_Panels" };
        private static readonly string[] NewPaintColors =
            { "#CCD3D1", "#496775", "#D67A50" };
        private static readonly string[] RockColors =
            { "#9F8D7A", "#B09B85", "#83786E" };

        // Individual placements, not random scattering. Preserve broad flight
        // corridors and make several outcrops visible from the first spawn.
        // Each authored OBJ is supplied at world-unit size, never stretched thin.
        private readonly struct Placement
        {
            public readonly float X, Z, Yaw, Scale;
            public readonly int Model, Palette;
            public Placement(float x, float z, int model, int palette, float yaw, float scale = 1f)
            {
                X = x; Z = z; Model = model; Palette = palette; Yaw = yaw; Scale = scale;
            }
        }
        private static readonly Placement[] Outcrops =
        {
            new Placement(-90, -169, 0, 0,  14, 1.15f),
            new Placement(108, -111, 1, 1, -17, 1.05f),
            new Placement(-125, -12, 2, 2,  23, 1.05f),
            new Placement(122, 87,   0, 0,  65, 1.20f),
            new Placement(-60, 184,  1, 1,  26, 1.00f),
            new Placement(88, 272,   2, 2, -14, 0.95f),
            new Placement(-245, 212, 1, 0,  69, 1.30f),
            new Placement(258, 210,  0, 1,  40, 1.20f),
            new Placement(-242, -326, 1, 2,  12, 1.15f),
            new Placement(250, -367, 0, 0,  78, 1.25f),
            new Placement(-370, 55,  2, 1,  47, 1.22f),
            new Placement(361, -83,  1, 2, -36, 1.04f),
            new Placement(-470, 402, 0, 1,  83, 1.35f),
            new Placement(440, 466,  1, 0,  25, 1.17f),
            new Placement(-530, -470,2, 2,  61, 1.13f),
            new Placement(553, -458, 1, 1, -21, 1.05f),
            new Placement(-652, 10,  0, 0,  10, 1.28f),
            new Placement(632, 56,   2, 2,  52, 1.15f)
        };

        [MenuItem("Air Combat/Art/E1b - Install Muted Rocks + Aircraft Paint")]
        private static void Menu()
        {
            try { Debug.Log(Build()); }
            catch (Exception error) { Debug.LogError("E1b upgrade failed: " + error); }
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before E1b.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard dirty scenes first.");
            if (!AssetDatabase.IsValidFolder(ArtRoot))
                throw new InvalidOperationException("Missing committed E1 map art folder. Build E1 first, without rebuilding an existing map.");
            if (AssetDatabase.IsValidFolder(Output) || Directory.Exists(Output))
                throw new InvalidOperationException("E1b output already exists; refusing to overwrite accepted art.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Missing existing E1 scene: " + ScenePath);

            // Preflight every imported source BEFORE touching the scene/assets.
            var meshes = ModelNames.Select(name => LoadModelMesh(ModelRoot + "/" + name + ".obj")).ToArray();
            var sourcePaint = OriginalPaint.Select(name => Require<Material>(
                "Assets/_Project/Art/Aircraft/FrontierMaterials/" + name + ".mat")).ToArray();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            var terrainRoot = roots.Single(x => x.name == "E1 / Terrain - sculpt here later");
            var oldRocks = roots.Single(x => x.name == "E1 / Sparse rock groups - move freely");
            var jet = roots.Single(x => x.name == "E1 / PlayerJet (current model)");
            var camera = roots.Single(x => x.name == "E1 / Main Camera");
            if (roots.Any(x => x.name == "E1B / Chunky formations"))
                throw new InvalidOperationException("E1b is already installed in the open scene.");
            var terrain = terrainRoot.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null ||
                terrain.GetComponent<TerrainCollider>() == null)
                throw new InvalidOperationException("E1 Terrain/TerrainData/collider contract changed.");
            var jetMaterialsUsed = sourcePaint.Select(m => jet.GetComponentsInChildren<Renderer>(true)
                .Sum(r => r.sharedMaterials.Count(slot => slot == m))).ToArray();
            if (jetMaterialsUsed.Any(n => n == 0))
                throw new InvalidOperationException("E1 player Kestrel material slots changed. Paint pass needs inspection.");

            AssetDatabase.CreateFolder(ArtRoot, "E1B");
            var groundLayer = MakeGroundLayer();
            Material[] rockMats = new Material[RockColors.Length];
            for (int i = 0; i < rockMats.Length; i++)
                rockMats[i] = CreateMat("E1B_Rock_" + (i + 1), RockColors[i]);
            Material[] newPaint = new Material[OriginalPaint.Length];
            for (int i = 0; i < newPaint.Length; i++)
            {
                var material = new Material(sourcePaint[i]) { name = "E1B / " + OriginalPaint[i] };
                material.SetColor("_BaseColor", Parse(NewPaintColors[i]));
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", .14f);
                AssetDatabase.CreateAsset(material, Output + "/E1B_" + OriginalPaint[i] + ".mat");
                newPaint[i] = material;
            }

            // Terrain geometry is NOT flattened and can still be sculpted in Unity.
            // The old bright ochre layer is preserved as an asset for rollback.
            terrain.terrainData.terrainLayers = new[] { groundLayer };
            EditorUtility.SetDirty(terrain.terrainData);
            terrain.Flush();

            // Preserve earlier stones, but disable their entire hierarchy so they
            // cannot compete with the larger new shapes or double-cast shadows.
            oldRocks.SetActive(false);
            var formations = new GameObject("E1B / Chunky formations");
            SceneManager.MoveGameObjectToScene(formations, scene);
            for (int i = 0; i < Outcrops.Length; i++)
            {
                Placement at = Outcrops[i];
                var obj = new GameObject($"Outcrop {i + 1:00} / {ModelNames[at.Model]}");
                obj.transform.SetParent(formations.transform, false);
                obj.transform.SetPositionAndRotation(new Vector3(at.X,
                    terrain.SampleHeight(new Vector3(at.X, 0f, at.Z)) + terrain.transform.position.y,
                    at.Z), Quaternion.Euler(0f, at.Yaw, 0f));
                obj.transform.localScale = Vector3.one * at.Scale;
                obj.AddComponent<MeshFilter>().sharedMesh = meshes[at.Model];
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = rockMats[at.Palette];
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                obj.AddComponent<MeshCollider>().sharedMesh = meshes[at.Model];
            }

            // Height is gameplay staging here, not global FlightController tuning.
            // Translate camera too, preserving the C3 relative-height framing.
            float deltaHeight = NewFlightHeight - jet.transform.position.y;
            jet.transform.position += Vector3.up * deltaHeight;
            camera.transform.position += Vector3.up * deltaHeight;
            PrefabUtility.RecordPrefabInstancePropertyModifications(jet.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);

            int replaced = 0;
            foreach (var renderer in jet.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                for (int j = 0; j < sourcePaint.Length; j++)
                    if (slots[i] == sourcePaint[j])
                    {
                        slots[i] = newPaint[j];
                        changed = true;
                        replaced++;
                        break;
                    }
                if (!changed) continue;
                renderer.sharedMaterials = slots;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save updated E1 map.");
            Selection.activeGameObject = formations;
            return $"E1b installed in {ScenePath}: muted flat terrain, {Outcrops.Length} chunky outcrops " +
                   $"(3 OBJ models), Y={NewFlightHeight}, {replaced} paint slots, original small rocks disabled. " +
                   "Play to inspect real CombatLightingRig shadows. Do not rerun the E1/E1b builders.";
        }

        private static TerrainLayer MakeGroundLayer()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGB24, false);
            Color color = Parse("#B8A993"); // light-medium warm grey sand, low saturation
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, color);
            tex.Apply(false);
            string path = Output + "/E1B_GroundMuted.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Could not import E1B ground PNG.");
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            var layer = new TerrainLayer
            {
                name = "E1B / muted warm dust",
                diffuseTexture = Require<Texture2D>(path),
                tileSize = new Vector2(160f, 160f),
                smoothness = 0f,
                metallic = 0f,
                smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly
            };
            AssetDatabase.CreateAsset(layer, Output + "/E1B_GroundMuted.terrainlayer");
            return layer;
        }

        private static Material CreateMat(string name, string html)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is missing.");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", Parse(html));
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", .02f);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, Output + "/" + name + ".mat");
            return mat;
        }

        private static Mesh LoadModelMesh(string path)
        {
            var model = Require<GameObject>(path);
            var filter = model.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException("OBJ mesh import missing: " + path);
            var bounds = filter.sharedMesh.bounds.size;
            if (bounds.x < 10f || bounds.x > 80f || bounds.y < 8f || bounds.y > 40f)
                throw new InvalidOperationException("Unexpected OBJ scale for " + path + ": " + bounds);
            return filter.sharedMesh;
        }

        private static T Require<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException("Missing asset: " + path);

        private static Color Parse(string html)
        {
            if (!ColorUtility.TryParseHtmlString(html, out var value))
                throw new ArgumentException("Invalid color: " + html);
            return value;
        }
    }
}
#endif
