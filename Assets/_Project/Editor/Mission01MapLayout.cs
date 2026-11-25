using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Authoring only. Mission terrain remains editable; no runtime map generation.
    internal static class Mission01MapLayout
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/";
        private const string SamPath = "Assets/_Project/Prefabs/Environment/SAM01_Picket.prefab";
        private const string PlanName = "H / Route plan (visual only)";
        private const string UndoName = "Mission 01 map layout";
        private const float MapSize = 3200f;
        private const float FacetSize = 2000f / 70f;
        private static readonly Vector3 Origin = new Vector3(-1000f, 0f, -1000f);

        [MenuItem("Air Combat/Missions/Apply Mission 01 Map Layout")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = GetScene();
                GetSurface(scene, out var terrain, out var mesh);
                var objectives = Root(scene, "Mission01 / Objectives");
                if (DirectChild(objectives.transform, PlanName) != null)
                {
                    Debug.Log("MISSION-01H: route plan already installed; use Rebake Mission 01 Terrain for subsequent terrain edits.");
                    return;
                }
                if (objectives.transform.childCount != 0 ||
                    (terrain.terrainData.size - new Vector3(2000f, 180f, 2000f)).sqrMagnitude > .01f)
                    throw new InvalidOperationException("Expected the original flat 2000 x 2000 mission and empty Objectives root.");
                var data = terrain.terrainData;
                int n = data.heightmapResolution;
                foreach (float h in data.GetHeights(0, 0, n, n))
                    if (Mathf.Abs(h) > .00001f)
                        throw new InvalidOperationException("Terrain is no longer flat; refusing to stretch existing relief.");
                var samPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SamPath);
                var samples = scene.GetRootGameObjects().Where(g =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == SamPath).ToArray();
                if (samPrefab == null || samples.Length != 1 || !samples[0].activeSelf)
                    throw new InvalidOperationException("Expected one active SAM01 visual trial at the airfield.");
                var samA = samples[0];
                if ((samA.transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                    throw new InvalidOperationException("Expected unit-scale SAM01 (size is baked into the meshes).");
                var session = scene.GetRootGameObjects().Select(g => g.GetComponent<E1MapFlightSession>()).Single(s => s != null);
                if (session.GetComponent<Mission01LaunchSequence>() == null || session.GetComponent<Air01Encounter>() != null ||
                    new SerializedObject(session).FindProperty("combatHud").objectReferenceValue == null)
                    throw new InvalidOperationException("Expected the saved mission launch and HUD, without a lab encounter.");

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    Undo.RegisterCompleteObjectUndo(data, UndoName);
                    Undo.RegisterCompleteObjectUndo(mesh, UndoName);
                    // Extend north and east; do not move the airfield, runway, jet or camera.
                    data.size = new Vector3(MapSize, data.size.y, MapSize);
                    Bake(data, mesh);

                    var plan = Anchor(objectives.transform, PlanName, Vector3.zero);
                    var sites = Anchor(plan, "01 / SAM sites - no combat yet", Vector3.zero);
                    Undo.SetTransformParent(samA.transform, sites, UndoName);
                    Undo.RecordObjects(new UnityEngine.Object[] { samA, samA.transform }, UndoName);
                    samA.name = "SAM A - visual";
                    samA.transform.SetPositionAndRotation(new Vector3(-650f, 0f, 350f), Quaternion.Euler(0f, 135f, 0f));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(samA);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(samA.transform);
                    var samB = (GameObject)PrefabUtility.InstantiatePrefab(samPrefab, sites);
                    if (samB == null) throw new InvalidOperationException("Second SAM visual could not be instantiated.");
                    Undo.RegisterCreatedObjectUndo(samB, UndoName);
                    samB.name = "SAM B - visual";
                    samB.transform.SetPositionAndRotation(new Vector3(1250f, 0f, 1050f), Quaternion.Euler(0f, 225f, 0f));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(samB);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(samB.transform);

                    var recon = Anchor(plan, "02 / Recon zone - empty markers", new Vector3(1550f, 78f, 1760f));
                    Anchor(recon, "Recon 01", new Vector3(1420f, 78f, 1690f));
                    Anchor(recon, "Recon 02", new Vector3(1550f, 78f, 1820f));
                    Anchor(recon, "Recon 03", new Vector3(1690f, 78f, 1740f));

                    var relief = Anchor(plan, "03 / Future relief - empty markers", Vector3.zero);
                    Anchor(relief, "Central ridge - planned footprint 1150 x 680", new Vector3(525f, 0f, 640f));
                    Anchor(relief, "South ridge - optional", new Vector3(450f, 0f, -650f));
                    Anchor(relief, "North ridge - optional", new Vector3(120f, 0f, 1940f));
                    var route = Anchor(plan, "04 / Suggested outbound route - not enforced", Vector3.zero);
                    Anchor(route, "01 - exit base", new Vector3(-330f, 78f, 100f));
                    Anchor(route, "02 - west corridor", new Vector3(-290f, 78f, 930f));
                    Anchor(route, "03 - north of ridge", new Vector3(40f, 78f, 1250f));
                    Anchor(route, "04 - approach", new Vector3(800f, 78f, 1400f));
                    Anchor(route, "05 - recon area", new Vector3(1550f, 78f, 1760f));

                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = plan.gameObject;
                    Debug.Log("MISSION-01H: flat 3200 x 3200 map and layout markers ready. Save scene and assets; SAMs are visual only.");
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("MISSION-01H: " + ex); }
        }

        [MenuItem("Air Combat/Missions/Apply Mission 01 Central Ridge")]
        private static void ApplyCentralRidge() => ApplyRidgeProfile(false);

        [MenuItem("Air Combat/Missions/Apply Mission 01 Cliff Profile")]
        private static void ApplyCliffProfile() => ApplyRidgeProfile(true);

        private static void ApplyRidgeProfile(bool cliff)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string undoName = cliff ? "Mission 01 cliff profile" : "Mission 01 central ridge";
            try
            {
                var scene = GetScene();
                GetSurface(scene, out var terrain, out var mesh);
                var data = terrain.terrainData;
                var objectives = Root(scene, "Mission01 / Objectives");
                var plan = DirectChild(objectives.transform, PlanName);
                var relief = DirectChild(plan, "03 / Future relief - empty markers");
                var ridge = DirectChild(relief, "Central ridge - planned footprint 1150 x 680");
                if (ridge == null)
                    throw new InvalidOperationException("Central ridge marker missing under the H route plan.");
                if ((data.size - new Vector3(3200f, 180f, 3200f)).sqrMagnitude > .01f)
                    throw new InvalidOperationException("Expected expanded H terrain, actual size: " + data.size);
                int n = data.heightmapResolution;
                Vector3 center = ridge.position;
                if (center.x - 575f < Origin.x || center.x + 575f > Origin.x + data.size.x ||
                    center.z - 340f < Origin.z || center.z + 340f > Origin.z + data.size.z)
                    throw new InvalidOperationException("Central ridge footprint lies outside the terrain.");
                var heights = data.GetHeights(0, 0, n, n);
                // Validate the entire source before editing. Unity stores normalized
                // heights with limited precision; allow a small quantization margin.
                // Preserve other relief outside the central footprint on the cliff pass.
                const float tolerance = .0003f;
                bool matchesOld = true, matchesCliff = true;
                for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Origin.x + x * data.size.x / (n - 1) - center.x;
                    float dz = Origin.z + z * data.size.z / (n - 1) - center.z;
                    if (!cliff)
                    {
                        if (Mathf.Abs(heights[z, x]) > .00001f)
                            throw new InvalidOperationException("Expected flat terrain. Use the cliff profile command for the existing ridge.");
                        heights[z, x] = CentralRidgeHeight(dx, dz) / data.size.y;
                        continue;
                    }
                    if (Mathf.Abs(dx) >= 575f || Mathf.Abs(dz) >= 340f) continue;
                    float oldHeight = CentralRidgeHeight(dx, dz) / data.size.y;
                    float newHeight = CliffRidgeHeight(dx, dz) / data.size.y;
                    matchesOld &= Mathf.Abs(heights[z, x] - oldHeight) <= tolerance;
                    matchesCliff &= Mathf.Abs(heights[z, x] - newHeight) <= tolerance;
                    heights[z, x] = newHeight;
                }
                if (cliff && !matchesOld && !matchesCliff)
                    throw new InvalidOperationException("Central ridge differs from the H1/H1b generated profile. Refusing to overwrite custom sculpting.");
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
                try
                {
                    Undo.RegisterCompleteObjectUndo(data, undoName);
                    Undo.RegisterCompleteObjectUndo(mesh, undoName);
                    data.SetHeights(0, 0, heights);
                    Bake(data, mesh);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = terrain.gameObject;
                    Debug.Log(cliff ? "MISSION-01H1b: cliff profile baked; save terrain and facet assets." :
                        "MISSION-01H1: central ridge baked. Save terrain and facet assets; visual trial only.");
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError((cliff ? "MISSION-01H1b: " : "MISSION-01H1: ") + ex); }
        }

        // A single elongated massif with a broad, crooked crest. Only low-frequency
        // shape changes: no noise over the rest of the mission, no independent rocks.
        private static float CentralRidgeHeight(float dx, float dz)
        {
            float u = dx / 575f;
            if (Mathf.Abs(u) >= 1f || Mathf.Abs(dz) >= 340f) return 0f;
            float bend = 38f * Mathf.Sin(u * Mathf.PI * 1.15f);
            float width = 265f + 30f * Mathf.Cos(u * Mathf.PI * 2f + .6f);
            float v = (dz - bend) / width;
            float radius = Mathf.Sqrt(u * u + v * v);
            float mass = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - radius) / .72f));
            float crest = 155f + 10f * Mathf.Cos(u * Mathf.PI * 2.4f) + 5f * Mathf.Sin(u * Mathf.PI * 1.3f);
            return mass * crest;
        }

        // Same bounded footprint, but a low apron, steep escarpment and broken
        // crest. Low-frequency variation only; no scattering or density changes.
        private static float CliffRidgeHeight(float dx, float dz)
        {
            float u = dx / 575f;
            if (Mathf.Abs(u) >= 1f || Mathf.Abs(dz) >= 340f) return 0f;
            float bend = 30f * Mathf.Sin(u * Mathf.PI * 1.15f) + 13f * Mathf.Sin(u * Mathf.PI * 3f);
            float width = 250f + 27f * Mathf.Cos(u * Mathf.PI * 2f + .6f)
                + 14f * Mathf.Sin(u * Mathf.PI * 4f);
            float v = (dz - bend) / width;
            float radius = Mathf.Sqrt(u * u + v * v);
            float apron = 18f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - radius) / .22f));
            float wall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((.82f - radius) / .34f));
            float crest = 140f + 29f * Mathf.PingPong((u + 1f) * 3.3f, 1f)
                + 4f * Mathf.Sin(u * Mathf.PI * 1.3f);
            // A slightly pitched top, rather than a flat mesa cap.
            crest -= 15f * Mathf.Clamp01(Mathf.Abs(v) / .48f);
            return apron + wall * (crest - 18f);
        }

        private const string RockPalettePath = Art + "Mission01_RockPalette.asset";
        private const int PaletteWidth = 64;
        private const int PaletteHeight = 32;

        [MenuItem("Air Combat/Missions/Apply Mission 01 Rock Palette")]
        private static void ApplyRockPalette()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = GetScene();
                GetSurface(scene, out var terrain, out var mesh);
                var material = GetSurfaceMaterial(scene);
                var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(RockPalettePath);
                if (material.GetTexture("_BaseMap") != null && material.GetTexture("_BaseMap") != palette)
                    throw new InvalidOperationException("Unexpected existing terrain texture; refusing to replace it.");
                // Compute UVs first: the current mesh geometry is not rebuilt or modified.
                var uv = MakeRockUV(mesh);
                if (palette == null)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(RockPalettePath) != null)
                        throw new InvalidOperationException("Rock palette path already occupied.");
                    palette = new Texture2D(PaletteWidth, PaletteHeight, TextureFormat.RGBA32, false, false)
                    {
                        name = "Mission01 Rock Palette", filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp, anisoLevel = 0
                    };
                    palette.SetPixels32(MakeRockPalette());
                    palette.Apply(false, false);
                    AssetDatabase.CreateAsset(palette, RockPalettePath);
                }
                else if (palette.width != PaletteWidth || palette.height != PaletteHeight)
                    throw new InvalidOperationException("Unexpected rock palette dimensions.");

                const string undoName = "Mission 01 rock palette";
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
                Undo.RegisterCompleteObjectUndo(mesh, undoName);
                Undo.RegisterCompleteObjectUndo(material, undoName);
                try
                {
                    mesh.uv = uv;
                    material.SetTexture("_BaseMap", palette);
                    material.SetTextureScale("_BaseMap", Vector2.one);
                    material.SetTextureOffset("_BaseMap", Vector2.zero);
                    material.SetColor("_BaseColor", Color.white);
                    EditorUtility.SetDirty(material);
                    EditorUtility.SetDirty(mesh);
                    AssetDatabase.SaveAssets();
                    Undo.CollapseUndoOperations(group);
                    Selection.activeGameObject = terrain.gameObject;
                    Debug.Log("MISSION-01H1a: rock palette applied; heights, vertices and triangles unchanged.");
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("MISSION-01H1a: " + ex); }
        }

        private static Material GetSurfaceMaterial(Scene scene)
        {
            var renderer = Root(scene, "Mission01 / Faceted surface").GetComponent<MeshRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Mission01_Sand.mat");
            if (renderer == null || material == null || renderer.sharedMaterial != material ||
                !material.HasProperty("_BaseMap") || !material.HasProperty("_BaseColor"))
                throw new InvalidOperationException("Expected the mission-only sand material on the faceted surface.");
            return material;
        }

        private static Color32[] MakeRockPalette()
        {
            // sRGB palette: preserve the original sand albedo at x=0.
            Color32 sand = new Color32(184, 169, 147, 255);
            Color32 crest = new Color32(137, 121, 108, 255);
            Color32 steep = new Color32(97, 82, 72, 255);
            var pixels = new Color32[PaletteWidth * PaletteHeight];
            for (int y = 0; y < PaletteHeight; y++)
            {
                Color32 rock = Color32.Lerp(crest, steep, y / (float)(PaletteHeight - 1));
                for (int x = 0; x < PaletteWidth; x++)
                    pixels[y * PaletteWidth + x] = Color32.Lerp(sand, rock, x / (float)(PaletteWidth - 1));
            }
            return pixels;
        }

        private static Vector2[] MakeRockUV(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            if (mesh.subMeshCount != 1 || indices.Length != vertices.Length || indices.Length % 3 != 0)
                throw new InvalidOperationException("Expected one flat-shaded mesh with unique vertices per triangle.");
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (indices[i] != i || indices[i + 1] != i + 1 || indices[i + 2] != i + 2)
                    throw new InvalidOperationException("Unexpected facet vertex ordering.");
                Vector3 a = vertices[i], b = vertices[i + 1], c = vertices[i + 2];
                float height = (a.y + b.y + c.y) / 3f;
                float normalY = Vector3.Cross(b - a, c - a).normalized.y;
                // Fully rock well before the Y78 cruise level. No tint on flat sand.
                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 42f, height));
                float slope = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.025f, .32f, 1f - normalY));
                int x = Mathf.RoundToInt(rock * (PaletteWidth - 1));
                int y = Mathf.RoundToInt(slope * (PaletteHeight - 1));
                var sample = new Vector2((x + .5f) / PaletteWidth, (y + .5f) / PaletteHeight);
                uv[i] = uv[i + 1] = uv[i + 2] = sample;
            }
            return uv;
        }

        // Reusable after sculpting or resizing the TerrainData in the Inspector.
        // No changes to the heightmap, actors, materials, or scene positions.
        [MenuItem("Air Combat/Missions/Rebake Mission 01 Terrain")]
        private static void Rebake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                GetSurface(GetScene(), out var terrain, out var mesh);
                Undo.RegisterCompleteObjectUndo(mesh, "Rebake Mission 01 Terrain");
                Bake(terrain.terrainData, mesh);
                Debug.Log("MISSION-01: faceted surface rebaked. Save assets after review.");
            }
            catch (Exception ex) { Debug.LogError("MISSION-01 terrain: " + ex); }
        }

        private static Scene GetScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Open Mission01_FirstSortie first.");
            return scene;
        }

        private static GameObject Root(Scene scene, string name) =>
            scene.GetRootGameObjects().Single(g => g.name == name);

        private static void GetSurface(Scene scene, out Terrain terrain, out Mesh mesh)
        {
            terrain = Root(scene, "Mission01 / Editable terrain").GetComponent<Terrain>();
            var filter = Root(scene, "Mission01 / Faceted surface").GetComponent<MeshFilter>();
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(Art + "Mission01_Terrain.asset");
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "Mission01_Facets.asset");
            if (terrain == null || filter == null || data == null || mesh == null ||
                terrain.terrainData != data || filter.sharedMesh != mesh ||
                (terrain.transform.position - Origin).sqrMagnitude > .0001f ||
                (filter.transform.position - Origin).sqrMagnitude > .0001f ||
                Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) > .01f ||
                Quaternion.Angle(filter.transform.rotation, Quaternion.identity) > .01f ||
                (terrain.transform.lossyScale - Vector3.one).sqrMagnitude > .0001f ||
                (filter.transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                throw new InvalidOperationException("Unexpected mission terrain or facet assets/transforms.");
        }

        private static void Bake(TerrainData data, Mesh mesh)
        {
            Vector3 size = data.size;
            int cells = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(size.x, size.z) / FacetSize), 1, 256);
            Terrain01Playground.FillMesh(mesh, data, cells);
            // Once H1a is installed, all later terrain bakes preserve its color rules.
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Mission01_Sand.mat");
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(RockPalettePath);
            if (material != null && palette != null && material.GetTexture("_BaseMap") == palette)
                mesh.uv = MakeRockUV(mesh);
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(mesh);
        }

        // Names in the authored hierarchy contain literal slashes. Transform.Find
        // interprets those as path separators; match direct child names instead.
        private static Transform DirectChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 worldPosition)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            Undo.SetTransformParent(go.transform, parent, UndoName);
            go.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            return go.transform;
        }
    }
}
