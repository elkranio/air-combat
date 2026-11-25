using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Temporary A/B comparison. Both arrays are the SAME wall rotated relative
    // to the fixed game camera. No mission, prefab or runtime changes.
    internal static class CL06OrientationTrials
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Prefabs = "Assets/_Project/Prefabs/Environment/Cliffs/";
        private const string TrialName = "Mission01 / CL06 orientation trials";
        private const string UndoName = "CL06 orientation trials";
        private static readonly string[] Names = {
            "CL06_A_Bastion", "CL06_B_Pillar", "CL06_C_Fracture", "CL06_D_Buttress", "CL06_E_Crown"
        };
        private readonly struct Stone
        {
            public readonly int Kind;
            public readonly float X, Z, Yaw, Sx, Sy, Sz;
            public Stone(int kind, float x, float z, float yaw, float sx, float sy, float sz)
            { Kind = kind; X = x; Z = z; Yaw = yaw; Sx = sx; Sy = sy; Sz = sz; }
        }
        // Local X is along the wall. Compact core plus two dense staggered flanks;
        // variation is deliberately identical in the two trial orientations.
        private static readonly Stone[] Recipe = {
            new Stone(0, -240.000f, -19.912f, -9.000f, 1.100f, 1.000f, 1.100f),
            new Stone(2, -160.000f, -0.663f, 12.000f, 1.100f, 0.970f, 1.100f),
            new Stone(1, -80.000f, -10.420f, -18.000f, 1.100f, 1.040f, 1.100f),
            new Stone(4, 0.000f, 14.000f, 5.000f, 1.100f, 0.950f, 1.100f),
            new Stone(2, 80.000f, -3.580f, 17.000f, 1.100f, 1.020f, 1.100f),
            new Stone(3, 160.000f, 16.663f, -6.000f, 1.100f, 1.000f, 1.100f),
            new Stone(0, 240.000f, 10.912f, 3.000f, 1.100f, 0.970f, 1.100f),
            new Stone(3, -248.000f, -70.958f, -8.000f, 1.030f, 0.960f, 1.020f),
            new Stone(2, -186.000f, -91.460f, 12.000f, 0.970f, 0.890f, 1.020f),
            new Stone(0, -124.000f, -87.751f, -17.000f, 1.050f, 0.840f, 1.020f),
            new Stone(3, -62.000f, -83.001f, 5.000f, 1.000f, 1.020f, 1.020f),
            new Stone(1, 0.000f, -78.427f, -13.000f, 1.030f, 0.900f, 1.020f),
            new Stone(2, 62.000f, -75.313f, 16.000f, 0.970f, 0.960f, 1.020f),
            new Stone(3, 124.000f, -74.666f, -8.000f, 1.050f, 0.890f, 1.020f),
            new Stone(0, 186.000f, -76.907f, 12.000f, 1.000f, 0.840f, 1.020f),
            new Stone(3, 248.000f, -56.806f, -17.000f, 1.030f, 1.020f, 1.020f),
            new Stone(3, -248.000f, 47.645f, 8.000f, 1.030f, 0.960f, 1.020f),
            new Stone(1, -186.000f, 71.098f, -12.000f, 0.970f, 0.890f, 1.020f),
            new Stone(2, -124.000f, 76.718f, 17.000f, 1.050f, 0.840f, 1.020f),
            new Stone(3, -62.000f, 84.831f, -5.000f, 1.000f, 1.020f, 1.020f),
            new Stone(0, 0.000f, 93.573f, 13.000f, 1.030f, 0.900f, 1.020f),
            new Stone(3, 62.000f, 100.854f, -16.000f, 0.970f, 0.960f, 1.020f),
            new Stone(2, 124.000f, 104.866f, 8.000f, 1.050f, 0.890f, 1.020f),
            new Stone(0, 186.000f, 104.535f, -12.000f, 1.000f, 0.840f, 1.020f),
            new Stone(3, 248.000f, 75.711f, 17.000f, 1.030f, 1.020f, 1.020f),
        };

        [MenuItem("Air Combat/Missions/Place CL06 Orientation Trials")]
        private static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = GetScene();
                var roots = scene.GetRootGameObjects();
                var existing = roots.SingleOrDefault(g => g.name == TrialName);
                if (existing != null)
                { Selection.activeGameObject = existing; Debug.Log("CL06 orientation trials already placed."); return; }
                var barrier = roots.SingleOrDefault(g => g.name == "Mission01 / CL06 rock barrier");
                if (barrier == null || barrier.GetComponentsInChildren<RadarTerrainObstacle>(true).Length != 138)
                    throw new InvalidOperationException("Expected the saved 138-rock CL06B barrier.");
                var ground = roots.Single(g => g.name == "Mission01 / Faceted surface").GetComponent<MeshFilter>();
                if (ground == null || ground.sharedMesh == null || Mathf.Abs(ground.sharedMesh.bounds.min.y) > .01f ||
                    Mathf.Abs(ground.sharedMesh.bounds.max.y) > .01f)
                    throw new InvalidOperationException("Expected the flat mission ground.");
                var library = Names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + n + ".prefab")).ToArray();
                foreach (var asset in library)
                {
                    if (asset == null || asset.GetComponent<RadarTerrainObstacle>() == null)
                        throw new InvalidOperationException("Missing CL06 prefab or radar component.");
                    var filters = asset.GetComponentsInChildren<MeshFilter>(true);
                    if (filters.Length != 1 || filters[0].sharedMesh == null || !filters[0].sharedMesh.isReadable)
                        throw new InvalidOperationException("Expected a readable CL06 solid: " + asset.name);
                }
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    var root = NewGroup(scene, null, TrialName, Vector3.zero, 0);
                    // Camera yaw -45, pitch 50. Local +X rotates to a 45-degree
                    // diagonal in screen space (ground plane), and to screen-up.
                    AddWall(scene, root, library, "01 Diagonal - screen 45 deg", new Vector3(1050,0,-300), -97.546f);
                    AddWall(scene, root, library, "02 Vertical - screen 90 deg", new Vector3(550,0,-600), -135f);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = root.gameObject;
                    Debug.Log("CL06: two identical 25-rock orientation trials placed. Main barrier unchanged. Remove trials after review.");
                }
                catch { Undo.RevertAllDownToGroup(group); throw; }
            }
            catch (Exception ex) { Debug.LogError("CL06 orientation trials: " + ex); }
        }

        private static void AddWall(Scene scene, Transform root, GameObject[] library, string name, Vector3 center, float yaw)
        {
            var wall = NewGroup(scene, root, name, center, yaw);
            for (int i = 0; i < Recipe.Length; i++)
            {
                var p = Recipe[i];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(library[p.Kind], wall);
                if (instance == null) throw new InvalidOperationException("Cannot place " + Names[p.Kind]);
                Undo.RegisterCreatedObjectUndo(instance, UndoName);
                Undo.RecordObjects(new UnityEngine.Object[] { instance, instance.transform }, UndoName);
                instance.name = $"{i+1:00} / {Names[p.Kind]}";
                instance.transform.localPosition = new Vector3(p.X, 0, p.Z);
                instance.transform.localRotation = Quaternion.Euler(0, p.Yaw, 0);
                instance.transform.localScale = new Vector3(p.Sx, p.Sy, p.Sz);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            foreach (var renderer in wall.GetComponentsInChildren<MeshRenderer>())
            {
                var b = renderer.bounds;
                if (b.min.x < -1000 || b.min.z < -1000 || b.max.x > 2200 || b.max.z > 2200)
                    throw new InvalidOperationException("Trial extends past the mission ground: " + name);
            }
        }

        [MenuItem("Air Combat/Missions/Remove CL06 Orientation Trials")]
        private static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = GetScene();
                var root = scene.GetRootGameObjects().SingleOrDefault(g => g.name == TrialName);
                if (root == null) return;
                if (root.transform.childCount != 2 || root.GetComponentsInChildren<RadarTerrainObstacle>(true).Length != 50)
                    throw new InvalidOperationException("Trial group has been edited. Inspect it before manual removal.");
                Undo.DestroyObjectImmediate(root);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("CL06 orientation trials removed. Main barrier unchanged.");
            }
            catch (Exception ex) { Debug.LogError("CL06 orientation trials: " + ex); }
        }

        private static Scene GetScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Open Mission01_FirstSortie first.");
            return scene;
        }

        private static Transform NewGroup(Scene scene, Transform parent, string name, Vector3 position, float yaw)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            if (parent != null) Undo.SetTransformParent(go.transform, parent, UndoName);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one;
            return go.transform;
        }
    }
}
