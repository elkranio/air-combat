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
                if (objectives.transform.Find(PlanName) != null)
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
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(mesh);
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
