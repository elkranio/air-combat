using System;
using System.Collections.Generic;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Editor-only map builder. All resulting geometry and textures are saved as assets.
// The sandbox has no streaming, procedural updates or generation in Play Mode.
public static class ConfigureSandboxBattlefield
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
    private const string Output = "Assets/_Project/Generated/Battlefield";
    private const string Kenney = "Assets/ThirdParty/Kenney/";
    private const string RootName = "Battlefield (Generated)";
    private const float HalfSize = 1024f;
    private const float GroundHalfSize = 1344f; // scenery outside future combat boundary
    private const int TextureSize = 1024;

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes first.");
        var builder = new Builder(); // validate all imported assets before touching scene
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var landscape = RequireRoot(scene, "Landscape").GetComponent<ProceduralLandscape>();
        var camera = RequireRoot(scene, "Main Camera").GetComponent<Camera>();
        var ground = RequireRoot(scene, "Ground");
        var grid = RequireRoot(scene, "Grid");
        if (landscape == null || camera == null || !camera.orthographic)
            throw new InvalidOperationException("Missing sandbox landscape or orthographic camera.");
        EnsureFolder(Output);
        var previous = FindRoot(scene, RootName);
        var root = new GameObject(RootName + " (Building)");
        SceneManager.MoveGameObjectToScene(root, scene);
        try { builder.Build(root.transform); }
        catch { UnityEngine.Object.DestroyImmediate(root); throw; }
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        root.name = RootName;
        landscape.enabled = false;
        ground.SetActive(false);
        grid.SetActive(false);
        camera.orthographicSize = 80f;
        EditorUtility.SetDirty(landscape);
        EditorUtility.SetDirty(camera);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Sandbox.");
        Selection.activeGameObject = root;
        return "Battlefield saved: combat area 2048 x 2048; " + builder.PropCount +
            " static props. ProceduralLandscape disabled. Enter Play Mode.";
    }

    private sealed class Builder
    {
        private readonly Shader lit;
        private readonly Shader effect;
        private readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        private readonly Texture2D industrialTexture, suburbanTexture;
        private readonly System.Random random = new System.Random(42);
        private readonly List<Vector3> shadowVertices = new List<Vector3>();
        private readonly List<Color32> shadowColors = new List<Color32>();
        private readonly List<int> shadowTriangles = new List<int>();
        private readonly List<Vector3> surfaceVertices = new List<Vector3>();
        private readonly List<int>[] surfaceTriangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private Material industrial, suburban, leaf, pine, bark, stone;
        public int PropCount { get; private set; }

        public Builder()
        {
            lit = Shader.Find("Universal Render Pipeline/Lit");
            effect = Shader.Find("AirCombat/SandboxEffect");
            if (lit == null || effect == null) throw new InvalidOperationException("Missing sandbox shaders.");
            industrialTexture = RequireAsset<Texture2D>(Kenney + "CityKitIndustrial/Textures/colormap.png");
            suburbanTexture = RequireAsset<Texture2D>(Kenney + "CityKitSuburban/Textures/colormap.png");
            Load("CityKitIndustrial/", "building-a", "building-g", "building-h", "building-i", "building-j", "detail-tank-large", "shipping-container-a");
            Load("CityKitSuburban/", "building-type-a", "building-type-c", "building-type-d", "building-type-f");
            Load("NatureKit/Trees/", "tree_default", "tree_oak", "tree_pineRoundA", "tree_pineTallA");
            Load("NatureKit/Rocks/", "rock_largeA", "rock_largeB");
        }

        private void Load(string folder, params string[] names)
        {
            foreach (var name in names) models.Add(name, RequireAsset<GameObject>(Kenney + folder + name + ".obj"));
        }

        public void Build(Transform root)
        {
            industrial = MakeMaterial("Industrial", lit, new Color32(224, 224, 209, 255), industrialTexture);
            suburban = MakeMaterial("Suburban", lit, new Color32(225, 225, 212, 255), suburbanTexture);
            leaf = MakeMaterial("Leaf", lit, new Color32(77, 111, 69, 255));
            pine = MakeMaterial("Pine", lit, new Color32(60, 95, 75, 255));
            bark = MakeMaterial("Bark", lit, new Color32(109, 91, 72, 255));
            stone = MakeMaterial("Stone", lit, new Color32(131, 128, 115, 255));
            var groundMat = MakeMaterial("Ground", lit, Color.white, MakeGroundTexture());
            var groundMesh = new Mesh { name = "Battlefield ground" };
            float h = GroundHalfSize;
            groundMesh.vertices = new[] { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) };
            groundMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            groundMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            groundMesh.RecalculateNormals();
            groundMesh.RecalculateBounds();
            AddMesh(root, "Ground", SaveMesh("GroundMesh", groundMesh), new[] { groundMat });

            // A single continuous river, from north to south, well away from the spawn.
            var river = new List<Vector2>();
            for (int i = 0; i <= 128; i++)
            {
                float z = Mathf.Lerp(-GroundHalfSize, GroundHalfSize, i / 128f);
                river.Add(new Vector2(RiverX(z), z));
            }
            Strip(river, 38f, 0.015f, 0);
            // Main east-west road and secondary north-south connections.
            Strip(new[] { P(-HalfSize, 100), P(-180, 100), P(90, 100), P(450, 100), P(910, 100) }, 13, 0.035f, 1);
            Strip(new[] { P(-460, 100), P(-460, 440), P(-460, 830) }, 10, 0.036f, 1);
            Strip(new[] { P(500, -720), P(500, 100), P(500, 720) }, 10, 0.036f, 1);
            // Bridge deck on the east-west road, with a wider shoulder over the river.
            float bx = RiverX(100);
            Rect(bx, 100, 72, 17, 0.045f, 2);

            var airbase = Group(root, "Airbase");
            Rect(-95, -60, 118, 260, 0.04f, 2);
            Rect(-95, -60, 24, 240, 0.055f, 3);
            for (int z = -166; z <= 46; z += 22) Rect(-95, z, 1.4f, 10, 0.065f, 4);
            for (int x = -103; x <= -87; x += 4)
            {
                Rect(x, -174, 2, 7, 0.065f, 4);
                Rect(x, 54, 2, 7, 0.065f, 4);
            }
            Strip(new[] { P(-95, 70), P(-95, 100) }, 12, 0.05f, 1);
            for (int i = 0; i < 3; i++) Place(airbase, "building-h", -55, -110 + i * 32, 25, 0, industrial, 0.05f);
            Place(airbase, "building-i", -135, -62, 22, 90, industrial, 0.05f);
            Place(airbase, "building-j", -135, -18, 23, 90, industrial, 0.05f);
            Place(airbase, "detail-tank-large", -133, 35, 19, 0, industrial, 0.05f);

            var depot = Group(root, "Industrial Depot");
            Rect(500, 175, 148, 112, 0.04f, 2);
            for (int i = 0; i < 3; i++) Place(depot, i == 1 ? "building-g" : "building-a", 450 + i * 46, 145, 32, 0, industrial, 0.05f);
            for (int i = 0; i < 3; i++) Place(depot, "detail-tank-large", 450 + i * 23, 205, 19, 0, industrial, 0.05f);
            for (int i = 0; i < 4; i++) Place(depot, "shipping-container-a", 535 + (i % 2) * 12, 192 + (i / 2) * 18, 13, 0, industrial, 0.05f);

            var village = Group(root, "Village");
            string[] houses = { "building-type-a", "building-type-c", "building-type-d", "building-type-f" };
            Strip(new[] { P(-545, 440), P(-375, 440) }, 9, 0.037f, 1);
            for (int i = 0; i < 8; i++)
            {
                float x = new[] { -535f, -493f, -427f, -385f }[i % 4];
                float z = i < 4 ? 412 : 468;
                // Keep the north-south road clear.
                Place(village, houses[i % houses.Length], x, z, 22, i < 4 ? 180 : 0, suburban);
            }

            var nature = Group(root, "Woodlands");
            Grove(nature, -245, -170, 55, 16);
            Grove(nature, 100, 55, 42, 12);
            Grove(nature, -220, 360, 65, 18);
            Grove(nature, -650, -520, 80, 20);
            Grove(nature, 680, 350, 74, 18);
            Grove(nature, 710, -520, 70, 16);
            Grove(nature, -120, 790, 70, 16);
            Grove(nature, 130, -750, 67, 14);
            // A few rocks around the woodland edge, not hundreds of background instances.
            foreach (var p in new[] { P(-210, -220), P(143, 25), P(-190, 415), P(715, 400), P(660, -560), P(-70, 760) })
                Place(nature, random.Next(2) == 0 ? "rock_largeA" : "rock_largeB", p.x, p.y, 8, Next(0, 360), stone);

            var surfaces = new Mesh { name = "River, roads and airfield" };
            surfaces.SetVertices(surfaceVertices);
            surfaces.subMeshCount = surfaceTriangles.Length;
            for (int i = 0; i < surfaceTriangles.Length; i++) surfaces.SetTriangles(surfaceTriangles[i], i);
            surfaces.RecalculateNormals();
            surfaces.RecalculateBounds();
            AddMesh(root, "River, roads and airfield", SaveMesh("SurfacesMesh", surfaces), new[] {
                MakeMaterial("Water", lit, new Color32(82, 136, 151, 255)),
                MakeMaterial("Road", lit, new Color32(128, 130, 117, 255)),
                MakeMaterial("Apron", lit, new Color32(167, 164, 146, 255)),
                MakeMaterial("Runway", lit, new Color32(83, 89, 91, 255)),
                MakeMaterial("Markings", lit, new Color32(226, 225, 199, 255)) });
            var shadows = new Mesh { name = "Static contact shadows" };
            shadows.SetVertices(shadowVertices);
            shadows.SetColors(shadowColors);
            shadows.SetTriangles(shadowTriangles, 0);
            shadows.RecalculateBounds();
            AddMesh(root, "Static contact shadows", SaveMesh("ShadowsMesh", shadows), new[] { MakeMaterial("Shadows", effect, Color.white) });
        }

        private Texture2D MakeGroundTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Output + "/GroundColor.asset");
            bool create = texture == null;
            if (create) texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true, false);
            else if (texture.width != TextureSize || texture.height != TextureSize)
                texture.Reinitialize(TextureSize, TextureSize, TextureFormat.RGBA32, true);
            var pixels = new Color32[TextureSize * TextureSize];
            var grass = new Color(0.46f, 0.56f, 0.36f);
            var dark = new Color(0.37f, 0.47f, 0.32f);
            var meadow = new Color(0.57f, 0.60f, 0.39f);
            for (int z = 0; z < TextureSize; z++)
            for (int x = 0; x < TextureSize; x++)
            {
                float wx = Mathf.Lerp(-GroundHalfSize, GroundHalfSize, (x + 0.5f) / TextureSize);
                float wz = Mathf.Lerp(-GroundHalfSize, GroundHalfSize, (z + 0.5f) / TextureSize);
                float n = Mathf.PerlinNoise(wx * 0.005f + 37f, wz * 0.005f + 11f);
                var color = Color.Lerp(grass, dark, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.48f, 0.77f, n)));
                color = Color.Lerp(color, meadow, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.49f, 0.75f,
                    Mathf.PerlinNoise(wx * 0.003f + 130f, wz * 0.003f + 77f))));
                float bank = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(19, 34, Mathf.Abs(wx - RiverX(wz))));
                color = Color.Lerp(color, new Color(0.62f, 0.58f, 0.40f), bank);
                // Two broad sets of cultivated fields, with subtle parallel rows.
                if (InField(wx, wz, -690, 195, 148, 70) || InField(wx, wz, -690, 363, 148, 70) ||
                    InField(wx, wz, 740, 165, 105, 42) || InField(wx, wz, 740, 255, 105, 42))
                {
                    float row = 0.5f + 0.5f * Mathf.Cos(wx * 0.35f);
                    color = Color.Lerp(new Color(0.56f, 0.56f, 0.36f), new Color(0.61f, 0.60f, 0.39f), row);
                }
                pixels[z * TextureSize + x] = color;
            }
            texture.name = "Battlefield ground color";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(true, false); // readable in editor for deterministic rebuilds
            if (create) AssetDatabase.CreateAsset(texture, Output + "/GroundColor.asset");
            else EditorUtility.SetDirty(texture);
            return texture;
        }

        private static bool InField(float x, float z, float cx, float cz, float hx, float hz)
            => Mathf.Abs(x - cx) < hx && Mathf.Abs(z - cz) < hz;
        private static float RiverX(float z) => 290f + 53f * Mathf.Sin(z / 200f) + 22f * Mathf.Sin(z / 88f);
        private static Vector2 P(float x, float z) => new Vector2(x, z);
        private float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private void Grove(Transform parent, float x, float z, float radius, int count)
        {
            string[] trees = { "tree_default", "tree_oak", "tree_pineRoundA", "tree_pineTallA" };
            // Golden-angle layout with small deterministic jitter; no overlapping clumps.
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.399963f + Next(-0.1f, 0.1f);
                float r = radius * Mathf.Sqrt((i + 0.5f) / count);
                float px = x + Mathf.Cos(angle) * r, pz = z + Mathf.Sin(angle) * r;
                if (Mathf.Abs(px - RiverX(pz)) < 30 || Mathf.Abs(pz - 100) < 12) continue;
                Place(parent, trees[random.Next(trees.Length)], px, pz, Next(8, 14), Next(0, 360), null);
            }
        }

        private void Place(Transform parent, string model, float x, float z, float width, float yaw, Material overrideMat, float y = 0f)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(models[model], parent);
            if (instance == null) throw new InvalidOperationException("Failed to instantiate " + model);
            instance.name = model;
            var t = instance.transform;
            t.localPosition = new Vector3(x, y, z);
            t.localRotation = Quaternion.identity;
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Missing mesh on " + model);
            var bounds = BoundsOf(renderers);
            float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
            if (horizontal < 0.0001f || bounds.size.y < 0.0001f) throw new InvalidOperationException("Invalid size: " + model);
            float scale = width / horizontal;
            // Keep building and tree tops below the y=12 flight plane without making tiny footprints.
            t.localScale = Vector3.Scale(t.localScale, new Vector3(scale, Mathf.Min(scale, 9f / bounds.size.y), scale));
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            bounds = BoundsOf(renderers);
            t.position += new Vector3(x, y, z) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = BoundsOf(renderers);
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name.ToLowerInvariant() : "";
                    slots[i] = overrideMat != null ? overrideMat : name.Contains("bark") ? bark :
                        name.Contains("dark") || model.Contains("pine") ? pine : leaf;
                }
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            AddShadow(x - 3, z + 3, bounds.size.x * 0.68f + 3, bounds.size.z * 0.68f + 3);
            PropCount++;
        }

        private static Bounds BoundsOf(MeshRenderer[] renderers)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private void AddShadow(float x, float z, float rx, float rz)
        {
            const int sides = 16;
            int first = shadowVertices.Count;
            var center = new Vector3(x, 0.09f, z);
            shadowVertices.Add(center);
            shadowColors.Add(new Color32(25, 37, 29, 74));
            for (int ring = 0; ring < 2; ring++)
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, r = ring == 0 ? 0.48f : 1f;
                shadowVertices.Add(center + new Vector3(Mathf.Cos(a) * rx * r, 0, Mathf.Sin(a) * rz * r));
                shadowColors.Add(new Color32(25, 37, 29, (byte)(ring == 0 ? 55 : 0)));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = first + 1 + i, b = first + 1 + (i + 1) % sides;
                shadowTriangles.AddRange(new[] { first, b, a, a, b, b + sides, a, b + sides, a + sides });
            }
        }

        private void Rect(float x, float z, float w, float d, float y, int material)
        {
            Quad(new Vector3(x - w / 2, y, z - d / 2), new Vector3(x - w / 2, y, z + d / 2),
                new Vector3(x + w / 2, y, z + d / 2), new Vector3(x + w / 2, y, z - d / 2), material);
        }

        private void Strip(IList<Vector2> points, float width, float y, int material)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 before = i == 0 ? (b - a).normalized : (b - points[i - 1]).normalized;
                Vector2 after = i == points.Count - 2 ? (b - a).normalized : (points[i + 2] - a).normalized;
                Vector2 n0 = new Vector2(-before.y, before.x) * (width * 0.5f);
                Vector2 n1 = new Vector2(-after.y, after.x) * (width * 0.5f);
                Vector3 v0 = new Vector3(a.x + n0.x, y, a.y + n0.y);
                Vector3 v1 = new Vector3(b.x + n1.x, y, b.y + n1.y);
                Vector3 v2 = new Vector3(b.x - n1.x, y, b.y - n1.y);
                Vector3 v3 = new Vector3(a.x - n0.x, y, a.y - n0.y);
                // Keep the same upward-facing winding regardless of segment direction.
                Quad(v0, v3, v2, v1, material);
            }
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material)
        {
            int i = surfaceVertices.Count;
            surfaceVertices.AddRange(new[] { a, b, c, d });
            if (Vector3.Cross(b - a, c - a).y >= 0) surfaceTriangles[material].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            else surfaceTriangles[material].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
    }

    private static Transform Group(Transform parent, string name)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        return root;
    }

    private static void AddMesh(Transform root, string name, Mesh mesh, Material[] materials)
    {
        var owner = Group(root, name).gameObject;
        owner.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = owner.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = Output + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing);
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(mesh);
        return existing;
    }

    private static Material MakeMaterial(string name, Shader shader, Color color, Texture2D texture = null)
    {
        string path = Output + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value == null) throw new InvalidOperationException("Missing asset: " + path);
        return value;
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
            parent = next;
        }
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }

    private static GameObject RequireRoot(Scene scene, string name)
    {
        var root = FindRoot(scene, name);
        if (root == null) throw new InvalidOperationException("Missing Sandbox root: " + name);
        return root;
    }
}
