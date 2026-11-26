using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Explicit editor commands only. No import hook, runtime generator or main-map edits.
    internal static class Terrain01Playground
    {
        private const string MainScene = "Assets/_Project/Scenes/CombatSandbox.unity";
        private const string TrialScene = "Assets/_Project/Scenes/Terrain01Playground.unity";
        private const string ArtParent = "Assets/_Project/Art/Environment";
        private const string ArtFolder = ArtParent + "/Terrain01";
        private const string DataPath = ArtFolder + "/Terrain01_Source.asset";
        private const string MeshPath = ArtFolder + "/Terrain01_Facets.asset";
        private const string MaterialPath = ArtFolder + "/Terrain01_Sand.mat";
        private const string PalettePath = ArtFolder + "/Terrain01_ReliefPalette.asset";
        private const int PaletteSize = 64;
        private const float MinAlbedo = .68f;
        private const float ContrastHeightRange = 180f;
        private const string SourceName = "TERRAIN-01 / Editable source";
        private const string MeshName = "TERRAIN-01 / Faceted surface";
        private const float MapSize = 2400f; // Visual playground extent.
        private const float HeightRange = 100f;
        private const int HeightResolution = 257;
        private const int Cells = 84; // Half the previous nominal facet edge length.

        [MenuItem("Air Combat/Terrain/Create or Open TERRAIN-01")]
        private static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TrialScene) != null)
            {
                EditorSceneManager.OpenScene(TrialScene, OpenSceneMode.Single);
                return; // Never overwrite an authored trial.
            }
            if (AssetDatabase.IsValidFolder(ArtFolder))
            {
                Debug.LogError("TERRAIN-01: art folder already exists; refusing to overwrite it. Check the previous trial assets.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(ArtParent) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) == null)
            {
                Debug.LogError("TERRAIN-01: expected main scene or environment folder is missing.");
                return;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("TERRAIN-01: URP Lit shader not found.");
                return;
            }

            // Copy first, then edit only the copy. Retain the exact scene overrides on
            // the production Kestrel, camera and lighting; don't reconstruct their tuning.
            if (!AssetDatabase.CopyAsset(MainScene, TrialScene))
            {
                Debug.LogError("TERRAIN-01: could not copy the main scene.");
                return;
            }
            Scene scene = EditorSceneManager.OpenScene(TrialScene, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            FlightSession session = roots.Select(r => r.GetComponent<FlightSession>())
                .FirstOrDefault(s => s != null);
            GameObject lighting = roots.FirstOrDefault(r => r.name == "E1 / CombatLightingRig");
            if (session == null || lighting == null || lighting.GetComponentInChildren<Light>(true) == null)
            {
                AbortCopy("flight session or lighting rig not found");
                return;
            }
            var serialized = new SerializedObject(session);
            var jet = serialized.FindProperty("player").objectReferenceValue as JetAssembly;
            var rig = serialized.FindProperty("cameraRig").objectReferenceValue as CombatCameraRig;
            if (jet == null || rig == null || jet.GetComponent<FlightController>() == null)
            {
                AbortCopy("player or camera reference missing");
                return;
            }
            GameObject playerRoot = jet.transform.root.gameObject;
            GameObject cameraRoot = rig.transform.root.gameObject;
            var trigger = jet.GetComponent<PlayerWeaponTrigger>();
            if (trigger == null)
            {
                AbortCopy("player weapon trigger missing");
                return;
            }

            // Disconnect the combat references before removing the test composition.
            serialized.FindProperty("combatHud").objectReferenceValue = null;
            serialized.FindProperty("airContacts").arraySize = 0;
            serialized.FindProperty("altitudeComparison").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var encounter = session.GetComponent<CombatSandboxEncounter>();
            if (encounter != null) UnityEngine.Object.DestroyImmediate(encounter);
            trigger.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            foreach (GameObject root in roots)
                if (root != session.gameObject && root != playerRoot && root != cameraRoot && root != lighting)
                    UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.CreateFolder(ArtParent, "Terrain01");
            var data = new TerrainData { heightmapResolution = HeightResolution,
                size = new Vector3(MapSize, HeightRange, MapSize) };
            data.SetHeights(0, 0, MakeHeights());
            AssetDatabase.CreateAsset(data, DataPath);
            GameObject sourceObject = Terrain.CreateTerrainGameObject(data);
            sourceObject.name = SourceName;
            sourceObject.transform.position = new Vector3(-MapSize * .5f, 0f, -MapSize * .5f);
            var source = sourceObject.GetComponent<Terrain>();
            source.allowAutoConnect = false;
            source.drawTreesAndFoliage = false;
            // The smooth authoring source must not render through the faceted surface.
            source.drawHeightmap = false;
            var collider = sourceObject.GetComponent<TerrainCollider>();
            if (collider != null) collider.enabled = false; // Retain for editor sculpting only.

            var material = new Material(shader) { name = "Terrain01 Sand" };
            material.SetColor("_BaseColor", new Color32(184, 169, 147, 255));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            AssetDatabase.CreateAsset(material, MaterialPath);

            var mesh = new Mesh { name = "Terrain01 Facets" };
            FillMesh(mesh, data, Cells);
            AssetDatabase.CreateAsset(mesh, MeshPath);
            var surface = new GameObject(MeshName, typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.position = source.transform.position;
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            // Intentionally no MeshCollider yet: this atom is a visual test only.

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("TERRAIN-01: scene save failed; save the trial manually.");
                return;
            }
            Selection.activeGameObject = sourceObject;
            Debug.Log("TERRAIN-01 ready. Play: A/D, W/S, R, H, 7/8/9 for altitude comparison.");
        }

        private static void AbortCopy(string reason)
        {
            // Called before modifying the copy or creating any terrain assets.
            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            AssetDatabase.DeleteAsset(TrialScene);
            Debug.LogError("TERRAIN-01: " + reason + "; main scene left unchanged.");
        }

        [MenuItem("Air Combat/Terrain/Rebake TERRAIN-01 Facets")]
        private static void Rebake() => Rebake(false);

        [MenuItem("Air Combat/Terrain/Trial Macro Relief")]
        private static void TrialMacroRelief() => Rebake(true);

        [MenuItem("Air Combat/Terrain/Trial Relief Contrast")]
        private static void TrialReliefContrast() => Rebake(true, true);

        [MenuItem("Air Combat/Terrain/Trial Canyon Run")]
        private static void TrialCanyonRun() => Rebake(true, true, true);

        private static void Rebake(bool regenerateMacroRelief, bool contrast = false, bool canyon = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TrialScene)
            {
                Debug.LogError("Open Terrain01Playground before rebaking.");
                return;
            }
            GameObject[] roots = scene.GetRootGameObjects();
            var sourceObject = roots.FirstOrDefault(r => r.name == SourceName);
            var surface = roots.FirstOrDefault(r => r.name == MeshName);
            Terrain source = sourceObject != null ? sourceObject.GetComponent<Terrain>() : null;
            MeshFilter filter = surface != null ? surface.GetComponent<MeshFilter>() : null;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (source == null || filter == null || mesh == null || filter.sharedMesh != mesh ||
                source.terrainData == null || AssetDatabase.GetAssetPath(source.terrainData) != DataPath)
            {
                Debug.LogError("TERRAIN-01: source or generated asset references are missing/changed; no assets overwritten.");
                return;
            }
            var renderer = surface.GetComponent<MeshRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            if (regenerateMacroRelief && (renderer == null || material == null ||
                renderer.sharedMaterial != material || !material.HasProperty("_BaseMap") ||
                (material.GetTexture("_BaseMap") != null && material.GetTexture("_BaseMap") != palette) ||
                (contrast && palette == null && AssetDatabase.LoadMainAssetAtPath(PalettePath) != null)))
            {
                Debug.LogError("TERRAIN-01: unexpected material or palette; no assets overwritten.");
                return;
            }
            JetAssembly canyonPlayer = null;
            if (canyon)
            {
                var session = roots.Select(r => r.GetComponent<FlightSession>())
                    .FirstOrDefault(s => s != null);
                if (session != null)
                    canyonPlayer = new SerializedObject(session).FindProperty("player")
                        .objectReferenceValue as JetAssembly;
                if (canyonPlayer == null)
                {
                    Debug.LogError("TERRAIN-06: trial player not found; no assets overwritten.");
                    return;
                }
            }
            if (regenerateMacroRelief && !EditorUtility.DisplayDialog(
                canyon ? "TERRAIN-06 / Canyon Run" :
                    (contrast ? "TERRAIN-05 / Relief Contrast" : "TERRAIN-04 / Macro Relief"),
                "Replace the trial heightmap and rebake its facets? This overwrites current trial sculpting. The main map is not affected.",
                "Generate", "Cancel")) return;
            if (contrast && palette == null)
                palette = CreateReliefPalette();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(mesh, "Rebake TERRAIN-01 mesh");
            Undo.RecordObject(source, "Hide smooth terrain source");
            Undo.RecordObject(surface, "Show faceted terrain");
            Undo.RecordObject(surface.transform, "Align faceted terrain");
            if (regenerateMacroRelief)
            {
                Undo.RegisterCompleteObjectUndo(source.terrainData, "Replace trial heightmap");
                Vector3 size = source.terrainData.size;
                size.y = contrast ? ContrastHeightRange : HeightRange;
                source.terrainData.size = size;
                source.terrainData.SetHeights(0, 0, canyon
                    ? MakeCanyonHeights(source.terrainData)
                    : MakeMacroHeights(source.terrainData, contrast));
                EditorUtility.SetDirty(source.terrainData);
                Undo.RecordObject(material, "Set relief contrast palette");
                material.SetTexture("_BaseMap", contrast ? palette : null);
                EditorUtility.SetDirty(material);
            }
            FillMesh(mesh, source.terrainData, Cells);
            surface.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            surface.transform.localScale = source.transform.localScale;
            source.drawHeightmap = false;
            var collider = source.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                Undo.RecordObject(collider, "Disable source terrain collider");
                collider.enabled = false;
            }
            surface.SetActive(true);
            if (canyon)
            {
                // Start inside the southern mouth, facing along the first bend.
                // Only this explicit trial command moves the player; normal Rebake never does.
                Vector3 size = source.terrainData.size;
                const float start = .055f;
                Vector3 localStart = new Vector3(CanyonCenter(start) * size.x, 0f, start * size.z);
                Vector3 position = source.transform.TransformPoint(localStart);
                position.y = 78f;
                float dx = (CanyonCenter(start + .001f) - CanyonCenter(start - .001f)) * size.x;
                Vector3 direction = source.transform.TransformDirection(new Vector3(dx, 0f, .002f * size.z));
                direction.y = 0f;
                Undo.RecordObject(canyonPlayer.transform, "Place player at canyon entrance");
                canyonPlayer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
                PrefabUtility.RecordPrefabInstancePropertyModifications(canyonPlayer.transform);
            }
            EditorUtility.SetDirty(mesh);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            Debug.Log("TERRAIN-01 rebaked from the edited heightmap. Save the scene if needed.");
        }

        // URP Lit does not multiply vertex colors. A small linear palette sampled
        // at each face's constant UV gives real albedo variation without a custom shader.
        private static Texture2D CreateReliefPalette()
        {
            var palette = new Texture2D(PaletteSize, 1, TextureFormat.RGBA32, false, true)
            {
                name = "Terrain01 Relief Palette",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var colors = new Color[PaletteSize];
            for (int i = 0; i < colors.Length; i++)
            {
                float value = Mathf.Lerp(MinAlbedo, 1f, i / (float)(PaletteSize - 1));
                colors[i] = new Color(value, value, value, 1f);
            }
            palette.SetPixels(colors);
            palette.Apply(false);
            AssetDatabase.CreateAsset(palette, PalettePath);
            return palette;
        }

        // One continuous S-shaped valley cut into a high desert plateau. The route
        // is expressed in normalized map coordinates; widths and relief are in world units.
        private static float CanyonCenter(float t)
        {
            return .5f + .14f * Mathf.Sin(t * 2f * Mathf.PI);
        }

        private static float[,] MakeCanyonHeights(TerrainData data)
        {
            int n = data.heightmapResolution;
            Vector3 size = data.size;
            var heights = new float[n, n];
            for (int z = 0; z < n; z++)
            {
                float t = z / (float)(n - 1);
                float center = CanyonCenter(t) * size.x;
                float slope = .14f * 2f * Mathf.PI * Mathf.Cos(t * 2f * Mathf.PI) * size.x / size.z;
                // Approximate perpendicular distance to the gently curving centerline.
                float distanceScale = 1f / Mathf.Sqrt(1f + slope * slope);
                float pocket = Mathf.Exp(-Mathf.Pow((t - .58f) / .085f, 2f));
                float mouth = Mathf.Max(1f - Mathf.SmoothStep(0f, 1f, t / .13f),
                    Mathf.SmoothStep(0f, 1f, (t - .85f) / .15f));
                float halfWidth = 72f + 18f * Mathf.Sin(t * 3f * Mathf.PI + .4f)
                    + 92f * pocket + 65f * mouth;
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)(n - 1);
                    float signedDistance = (u * size.x - center) * distanceScale;
                    float bank = signedDistance < 0f ? 13f : 41f;
                    // Each bank has its own low-frequency erosion and occasional spurs.
                    // The bounded perturbation cannot close the continuous valley floor.
                    float irregularity = 24f * (2f * Mathf.PerlinNoise(t * 8f + bank, 7.3f) - 1f)
                        + 9f * (2f * Mathf.PerlinNoise(t * 21f + bank, 21.7f) - 1f);
                    float edge = Mathf.Max(46f, halfWidth + irregularity);
                    float wallWidth = Mathf.Lerp(78f, 110f, Mathf.PerlinNoise(t * 5f + bank, 3.1f));
                    float wall = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(edge, edge + wallWidth, Mathf.Abs(signedDistance)));
                    float floor = 15f + 7f * Mathf.PerlinNoise(u * 5f + 11f, t * 7f + 19f);
                    float plateau = 156f + 15f * Mathf.PerlinNoise(u * 4f + 31f, t * 5f + 5f);
                    float rock = (2f * Mathf.PerlinNoise(u * 17f + 63f, t * 17f + 17f) - 1f)
                        * 9f * 4f * wall * (1f - wall);
                    // With the committed Y-30 transform, the plateau is above Y78,
                    // while the valley floor is comfortably below the player's plane.
                    float elevation = Mathf.Lerp(floor, plateau, wall) + rock;
                    heights[z, x] = Mathf.Clamp01(elevation / size.y);
                }
            }
            return heights;
        }

        // Macro geography first: unequal uplands separated by connected low country.
        // Noise only bends the outlines and adds restrained relief within the slopes.
        private static float[,] MakeMacroHeights(TerrainData data, bool contrast)
        {
            int n = data.heightmapResolution;
            var heights = new float[n, n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)(n - 1), v = z / (float)(n - 1);
                // Low-frequency coordinate warp makes the masses organic, not circles.
                float px = u + .075f * (2f * Mathf.PerlinNoise(u * 3.1f + 13f, v * 3.1f + 8f) - 1f);
                float pz = v + .075f * (2f * Mathf.PerlinNoise(u * 3.1f + 41f, v * 3.1f + 27f) - 1f);
                float a = Landform(px, pz, .17f, .76f, .35f, .23f, 32f, .23f) * 83f;
                float b = Landform(px, pz, .84f, .31f, .22f, .36f, -25f, .40f) * 75f;
                float c = Landform(px, pz, .70f, .91f, .25f, .17f, 15f, .12f) * 52f;
                float d = Landform(px, pz, .12f, .15f, .12f, .17f, -40f, .15f) * 30f;
                float mass = Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
                // The lowlands remain quiet; finer relief is proportional to the mass.
                float floor = 3f + (contrast ? 3f : 5f) * Mathf.PerlinNoise(u * 2f + 5f, v * 2f + 15f);
                float detail = (2f * Mathf.PerlinNoise(u * 9f + 63f, v * 9f + 17f) - 1f)
                    * (contrast ? 10f : 6f) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(mass / 35f));
                // Preserve the exact warped footprints; increase vertical relief only.
                float elevation = floor + mass * (contrast ? 1.8f : 1f) + detail;
                heights[z, x] = Mathf.Clamp01(elevation / data.size.y);
            }
            return heights;
        }

        private static float Landform(float x, float z, float cx, float cz,
            float rx, float rz, float angle, float shoulder)
        {
            float radians = angle * Mathf.Deg2Rad;
            float co = Mathf.Cos(radians), si = Mathf.Sin(radians);
            float dx = x - cx, dz = z - cz;
            float a = (co * dx + si * dz) / rx;
            float b = (-si * dx + co * dz) / rz;
            float radius = Mathf.Sqrt(a * a + b * b);
            float t = Mathf.InverseLerp(shoulder, 1f, radius);
            return 1f - Mathf.SmoothStep(0f, 1f, t);
        }

        private static float[,] MakeHeights()
        {
            var heights = new float[HeightResolution, HeightResolution];
            for (int z = 0; z < HeightResolution; z++)
            for (int x = 0; x < HeightResolution; x++)
            {
                float wx = x * MapSize / (HeightResolution - 1);
                float wz = z * MapSize / (HeightResolution - 1);
                // Two coherent scales, no per-vertex height noise. Large landforms
                // and broad saddles; all initial heights remain below combat Y78.
                float broad = Mathf.PerlinNoise(wx / 490f + 11.3f, wz / 410f + 22.7f);
                float ridge = Mathf.PerlinNoise(wx / 205f + 37.1f, wz / 270f + 9.2f);
                float t = Mathf.InverseLerp(.14f, .86f, .62f * broad + .38f * ridge);
                heights[z, x] = (3f + 65f * Mathf.SmoothStep(0f, 1f, t)) / HeightRange;
            }
            return heights;
        }

        internal static void FillMesh(Mesh mesh, TerrainData data, int cells)
        {
            Vector3 size = data.size;
            float dx = size.x / cells, dz = size.z / cells;
            var random = new System.Random(1701);
            var grid = new Vector3[cells + 1, cells + 1];
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                // Shared, mildly irregular grid: no cracks and no checkerboard diagonals.
                float px = x * dx, pz = z * dz;
                if (x > 0 && x < cells) px += ((float)random.NextDouble() - .5f) * dx * .40f;
                if (z > 0 && z < cells) pz += ((float)random.NextDouble() - .5f) * dz * .40f;
                float y = data.GetInterpolatedHeight(px / size.x, pz / size.z);
                grid[z, x] = new Vector3(px, y, pz);
            }
            int count = cells * cells * 6;
            var vertices = new Vector3[count];
            var indices = new int[count];
            var uv = new Vector2[count];
            int cursor = 0;
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                Vector3 a = grid[z, x], b = grid[z + 1, x];
                Vector3 c = grid[z + 1, x + 1], d = grid[z, x + 1];
                if (random.Next(2) == 0)
                {
                    Triangle(a, b, c); Triangle(a, c, d);
                }
                else
                {
                    Triangle(a, b, d); Triangle(b, c, d);
                }
            }
            mesh.Clear();
            mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.uv = uv;
            mesh.RecalculateNormals(); // Separate vertices for every face = flat shading.
            mesh.RecalculateBounds();

            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                // Height and slope, not random triangle colors. Shared material and
                // dynamic lighting remain intact; only the palette affects albedo.
                float elevation = (a.y + b.y + c.y) / 3f;
                float normalY = Vector3.Cross(b - a, c - a).normalized.y;
                float high = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 145f, elevation));
                float steep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.015f, .18f, 1f - normalY));
                float albedo = Mathf.Lerp(.84f, 1f, high) - .16f * steep;
                float t = Mathf.InverseLerp(MinAlbedo, 1f, albedo);
                var faceUV = new Vector2((.5f + t * (PaletteSize - 1)) / PaletteSize, .5f);
                vertices[cursor] = a; indices[cursor] = cursor; uv[cursor++] = faceUV;
                vertices[cursor] = b; indices[cursor] = cursor; uv[cursor++] = faceUV;
                vertices[cursor] = c; indices[cursor] = cursor; uv[cursor++] = faceUV;
            }
        }
    }
}
