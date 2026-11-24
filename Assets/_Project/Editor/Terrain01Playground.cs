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
        private const string MainScene = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string TrialScene = "Assets/_Project/Scenes/Terrain01Playground.unity";
        private const string ArtParent = "Assets/_Project/Art/Environment";
        private const string ArtFolder = ArtParent + "/Terrain01";
        private const string DataPath = ArtFolder + "/Terrain01_Source.asset";
        private const string MeshPath = ArtFolder + "/Terrain01_Facets.asset";
        private const string MaterialPath = ArtFolder + "/Terrain01_Sand.mat";
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
            E1MapFlightSession session = roots.Select(r => r.GetComponent<E1MapFlightSession>())
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
            var encounter = session.GetComponent<Air01Encounter>();
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
        private static void Rebake()
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
            Undo.RegisterCompleteObjectUndo(mesh, "Rebake TERRAIN-01 mesh");
            Undo.RecordObject(source, "Hide smooth terrain source");
            Undo.RecordObject(surface, "Show faceted terrain");
            Undo.RecordObject(surface.transform, "Align faceted terrain");
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
            EditorUtility.SetDirty(mesh);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TERRAIN-01 rebaked from the edited heightmap. Save the scene if needed.");
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

        private static void FillMesh(Mesh mesh, TerrainData data, int cells)
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
            mesh.RecalculateNormals(); // Separate vertices for every face = flat shading.
            mesh.RecalculateBounds();

            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                vertices[cursor] = a; indices[cursor] = cursor; cursor++;
                vertices[cursor] = b; indices[cursor] = cursor; cursor++;
                vertices[cursor] = c; indices[cursor] = cursor; cursor++;
            }
        }
    }
}
