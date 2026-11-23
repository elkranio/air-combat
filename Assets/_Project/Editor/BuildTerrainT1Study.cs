#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Terrain T1 is a fresh native Unity Terrain study, not a generated terrain mesh.
    // It has a small fixed set of hand-placed macro forms; everything is sculptable
    // afterwards with ordinary Unity Terrain brushes. No noise/FBX/Blender dependency.
    public static class BuildTerrainT1Study
    {
        private const string Source = "Assets/_Project/Scenes/FrontierStrata.unity";
        private const string Destination = "Assets/_Project/Scenes/FrontierTerrainT1.unity";
        private const string Output = "Assets/_Project/Generated/TerrainT1";
        private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
        private const int HeightResolution = 1025;
        private const int PaintResolution = 512;
        private const float Side = 900f;
        private const float Bottom = -15f;
        private const float VerticalRange = 165f;

        // World X/Z: deliberately asymmetric high plateau with a broken rim.
        // These are ART STATIONS, not a random field or an endlessly repeated slope.
        private static readonly Vector2[] WestMesa =
        {
            new Vector2(-318f, -32f), new Vector2(-285f, -100f),
            new Vector2(-218f, -129f), new Vector2(-132f, -105f),
            new Vector2(-68f, -35f), new Vector2(-77f, 59f),
            new Vector2(-121f, 138f), new Vector2(-204f, 184f),
            new Vector2(-274f, 137f), new Vector2(-321f, 48f)
        };
        private static readonly Vector2[] EastRidge =
        {
            new Vector2(228f, -278f), new Vector2(186f, -172f),
            new Vector2(155f, -60f), new Vector2(193f, 44f),
            new Vector2(239f, 132f), new Vector2(202f, 247f)
        };
        private static readonly Vector2[] DryWash =
        {
            new Vector2(-24f, -350f), new Vector2(14f, -220f),
            new Vector2(23f, -91f), new Vector2(0f, 26f),
            new Vector2(15f, 166f), new Vector2(42f, 326f)
        };

        [MenuItem("Air Combat/Art/Terrain T1 - Create Native Terrain Study")]
        private static void BuildFromMenu()
        {
            try { Debug.Log(Build()); }
            catch (Exception exception) { Debug.LogError("Terrain T1: " + exception); }
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building Terrain T1.");
            for (int i = 0; i < SceneManager.sceneCount; ++i)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard your open scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null || File.Exists(Destination))
                throw new InvalidOperationException("T1 already exists. Never overwrite an artist-edited terrain: " + Destination);
            if (AssetDatabase.IsValidFolder(Output))
                throw new InvalidOperationException("T1 generated output already exists. Inspect it before retrying: " + Output);

            Require<SceneAsset>(Source);
            var urp = Require<UniversalRenderPipelineAsset>(Pipeline);
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader == null)
                throw new InvalidOperationException("URP Terrain/Lit shader missing: check the active render pipeline.");
            EnsureFolder(Output);

            var t = new TerrainData
            {
                heightmapResolution = HeightResolution,
                alphamapResolution = PaintResolution,
                baseMapResolution = PaintResolution,
                size = new Vector3(Side, VerticalRange, Side)
            };
            AssetDatabase.CreateAsset(t, Output + "/T1_Terrain.asset");
            var heights = BuildHeights();
            t.SetHeights(0, 0, heights);

            var layers = new[]
            {
                MakeLayer("Dust", new Color(.58f, .49f, .395f), 0),
                MakeLayer("Sandstone", new Color(.51f, .405f, .32f), 1),
                MakeLayer("Rock", new Color(.37f, .365f, .345f), 2)
            };
            t.terrainLayers = layers;
            t.SetAlphamaps(0, 0, BuildPaint(heights));
            EditorUtility.SetDirty(t);
            var terrainMaterial = new Material(terrainShader) { name = "T1_Terrain_URP" };
            AssetDatabase.CreateAsset(terrainMaterial, Output + "/T1_Terrain_URP.mat");
            AssetDatabase.SaveAssets();

            // Copy ONLY the known-good player/camera/light rig from V17.
            // The source scene and combat labs stay untouched.
            var original = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(original, Destination, true))
                throw new InvalidOperationException("Cannot create separate scene. Check Windows folder permissions.");
            var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
            var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
            var cameraRoot = scene.GetRootGameObjects().Single(g => g.name == "Main Camera");
            var sunRoot = scene.GetRootGameObjects().Single(g => g.name == "Directional Light");
            var camera = cameraRoot.GetComponent<Camera>();
            var flight = player.GetComponent<FlightController>();
            var boost = player.GetComponent<Afterburner>();
            var sun = sunRoot.GetComponent<Light>();
            if (camera == null || flight == null || boost == null || sun == null)
                throw new InvalidOperationException("V17 flight rig contract has changed.");
            foreach (var root in scene.GetRootGameObjects())
                if (root != player && root != cameraRoot && root != sunRoot)
                    Object.DestroyImmediate(root);

            var ground = Terrain.CreateTerrainGameObject(t);
            ground.name = "T1 / Native Unity Terrain (select to sculpt)";
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(-Side * 0.5f, Bottom, -Side * 0.5f);
            var terrain = ground.GetComponent<Terrain>();
            var collider = ground.GetComponent<TerrainCollider>();
            if (terrain == null || collider == null || collider.terrainData != t)
                throw new InvalidOperationException("Native TerrainCollider creation failed.");
            terrain.materialTemplate = terrainMaterial;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 900f;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = ShadowCastingMode.On;

            // The old T1 idea was a 600x600 study. The extra flat margin on this
            // 900x900 tile lets the 45-70 unit/s jet actually fly for several seconds.
            var offset = camera.transform.position - player.transform.position;
            Vector3 spawn = new Vector3(0f, 48f, -205f); // inside +/- 268 art flight limit
            float spawnHeight = Bottom + t.GetInterpolatedHeight((spawn.x + Side * .5f) / Side,
                (spawn.z + Side * .5f) / Side);
            if (spawnHeight + 12f >= spawn.y)
                throw new InvalidOperationException("Unsafe T1 spawn height; redesign before saving.");
            player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            camera.transform.position = spawn + offset;
            camera.orthographic = true;
            camera.orthographicSize = 80f;
            camera.farClipPlane = Mathf.Max(700f, camera.farClipPlane);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.77f;
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.43f, .42f, .405f);

            var rig = new GameObject("T1 / Flight study + height collision");
            SceneManager.MoveGameObjectToScene(rig, scene);
            rig.AddComponent<ArtLabLandscapeFlight>().Configure(urp, player.transform,
                camera, flight, boost, Side * .5f,
                "T1 / UNITY TERRAIN - plateau, ridge, dry valley");
            rig.AddComponent<TerrainT1HeightCollision>().Configure(player.transform, terrain);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Cannot save the T1 terrain study scene.");
            Selection.activeGameObject = ground;
            return "Terrain T1 created: " + Destination + " (editable TerrainData, 3 layers, native TerrainCollider). " +
                   "Play at 1/2/3 pitch; steer toward the plateau or ridge. Select Terrain to sculpt.";
        }

        private static float[,] BuildHeights()
        {
            var heights = new float[HeightResolution, HeightResolution];
            float step = Side / (HeightResolution - 1);
            for (int z = 0; z < HeightResolution; z++)
            {
                float wz = -Side * .5f + z * step;
                for (int x = 0; x < HeightResolution; x++)
                {
                    float wx = -Side * .5f + x * step;
                    heights[z, x] = Mathf.Clamp01((WorldHeight(wx, wz) - Bottom) / VerticalRange);
                }
            }
            return heights;
        }

        private static float WorldHeight(float x, float z)
        {
            // One asymmetric plateau, one snaking ridge, and a SHALLOW wash.
            // No Perlin or other noise-driven mountain construction.
            float westDistance = SignedPolygonDistance(new Vector2(x, z), WestMesa);
            float west = 101f * (1f - Smooth(-23f, 54f, westDistance));

            float ridgeDistance = PolylineDistance(new Vector2(x, z), EastRidge);
            float ridge = (76f + 17f * Smooth(-200f, 205f, z)) *
                (1f - Smooth(17f, 104f, ridgeDistance));

            // Broad shelf makes a foothill on the southern side, below the jet.
            float lowHill = 35f * (1f - Smooth(32f, 114f,
                Vector2.Distance(new Vector2(x, z), new Vector2(85f, -100f))));
            float rise = Mathf.Max(Mathf.Max(west, ridge), lowHill);

            float washDistance = PolylineDistance(new Vector2(x, z), DryWash);
            float wash = 10f * (1f - Smooth(0f, 49f, washDistance));
            wash *= 1f - Smooth(8f, 28f, rise); // does not cut huge stripes through cliffs
            return 4f + rise - wash;
        }

        private static float SignedPolygonDistance(Vector2 p, Vector2[] points)
        {
            float smallestSq = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[j];
                Vector2 b = points[i];
                Vector2 segment = b - a;
                float projection = Mathf.Clamp01(Vector2.Dot(p - a, segment) / segment.sqrMagnitude);
                float d = (p - (a + projection * segment)).sqrMagnitude;
                if (d < smallestSq) smallestSq = d;
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            float distance = Mathf.Sqrt(smallestSq);
            return inside ? -distance : distance;
        }

        private static float PolylineDistance(Vector2 p, Vector2[] points)
        {
            float smallestSq = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 segment = points[i + 1] - a;
                float projection = Mathf.Clamp01(Vector2.Dot(p - a, segment) / segment.sqrMagnitude);
                float d = (p - (a + projection * segment)).sqrMagnitude;
                if (d < smallestSq) smallestSq = d;
            }
            return Mathf.Sqrt(smallestSq);
        }

        private static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static float[,,] BuildPaint(float[,] heights)
        {
            var alpha = new float[PaintResolution, PaintResolution, 3];
            float sample = Side / (HeightResolution - 1);
            for (int z = 0; z < PaintResolution; z++)
            {
                int hz = Mathf.RoundToInt(z * (HeightResolution - 1f) / (PaintResolution - 1));
                int previousZ = Mathf.Max(0, hz - 1);
                int nextZ = Mathf.Min(HeightResolution - 1, hz + 1);
                for (int x = 0; x < PaintResolution; x++)
                {
                    int hx = Mathf.RoundToInt(x * (HeightResolution - 1f) / (PaintResolution - 1));
                    int previousX = Mathf.Max(0, hx - 1);
                    int nextX = Mathf.Min(HeightResolution - 1, hx + 1);
                    float h = Bottom + heights[hz, hx] * VerticalRange;
                    float slopeX = (heights[hz, nextX] - heights[hz, previousX]) * VerticalRange /
                        ((nextX - previousX) * sample);
                    float slopeZ = (heights[nextZ, hx] - heights[previousZ, hx]) * VerticalRange /
                        ((nextZ - previousZ) * sample);
                    float steep = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ);
                    float rock = 0.04f + 0.89f * Smooth(.50f, 1.35f, steep);
                    float sandstone = (1f - rock) * .9f * Smooth(14f, 62f, h);
                    alpha[z, x, 0] = 1f - rock - sandstone;
                    alpha[z, x, 1] = sandstone;
                    alpha[z, x, 2] = rock;
                }
            }
            return alpha;
        }

        private static TerrainLayer MakeLayer(string label, Color tint, int seed)
        {
            var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
            for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                // Seamless small-scale surface variation ONLY, not height displacement.
                float a = Mathf.PI * 2f * x / 256f;
                float b = Mathf.PI * 2f * y / 256f;
                float detail = .018f * Mathf.Sin(a * (7 + seed)) * Mathf.Cos(b * (11 + seed)) +
                    .023f * Mathf.Sin(a * 3 + seed) * Mathf.Sin(b * 4 + seed);
                texture.SetPixel(x, y, new Color(
                    Mathf.Clamp01(tint.r + detail),
                    Mathf.Clamp01(tint.g + detail * .86f),
                    Mathf.Clamp01(tint.b + detail * .69f)));
            }
            texture.Apply();
            string texturePath = Output + "/T1_" + label + "_Tile.png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Could not import " + texturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            var layer = new TerrainLayer
            {
                name = "T1_" + label,
                diffuseTexture = Require<Texture2D>(texturePath),
                tileSize = new Vector2(22f, 22f),
                metallic = 0f,
                smoothness = .03f,
                smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly
            };
            AssetDatabase.CreateAsset(layer, Output + "/T1_" + label + ".terrainlayer");
            return layer;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash < 0) throw new InvalidOperationException("Invalid folder: " + path);
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static T Require<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
                throw new InvalidOperationException("Missing asset: " + path);
    }
}
#endif
