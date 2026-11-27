using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class CA07CargoEvacuationAuthoring
    {
        private const string Mission = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Layout = "Assets/_Project/Editor/Mission02/Mission02LayoutV06.json";
        [Serializable] private sealed class Routes { public Vector3 c1, c2; public Vector3[] second, exit; }

        [MenuItem("Air Combat/Missions/M02 - Install CA07 Cargo Evacuation")]
        private static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != Mission || scene.isDirty)
            { Debug.LogError("CA07: open and save existing M02 first."); return; }
            int undo = -1;
            try
            {
                var roots = scene.GetRootGameObjects();
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
                var hull = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Aircraft/CA01_Carryall.prefab");
                var cargo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/CG01_SpecialCargo.prefab");
                var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Layout);
                var data = layoutAsset != null ? JsonUtility.FromJson<Routes>(layoutAsset.text) : null;
                if (hull == null || cargo == null || terrain.terrainData == null || data == null || !trial.HasSecondPoint ||
                    data.second == null || data.second.Length < 5 || data.exit == null || data.exit.Length < 9)
                    throw new InvalidOperationException("CA07 requires accepted CA01/CG01, V06 and terrain.");
                Vector3[] c1 = data.second.Take(5).ToArray();
                Vector3[] c2 = data.exit.Take(9).ToArray();
                CheckPath(c1, trial.PointC, trial.OperationBounds, terrain);
                CheckPath(c2, trial.PointC2, trial.OperationBounds, terrain);
                var so = new SerializedObject(trial);
                var link = so.FindProperty("cargoEvacuation");
                if (link == null || link.objectReferenceValue != null || trial.GetComponent<Mission02CargoEvacuation>() != null)
                    throw new InvalidOperationException("CA07 already installed or missing route-trial link. No changes.");
                Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install CA07 cargo evacuation");
                var ev = Undo.AddComponent<Mission02CargoEvacuation>(trial.gameObject);
                ev.Configure(terrain, hull, cargo, c1, c2);
                EditorUtility.SetDirty(ev);
                link.objectReferenceValue = ev;
                so.FindProperty("baseDeploymentTrial").boolValue = false;
                so.FindProperty("carryallFiringRange").boolValue = false;
                so.ApplyModifiedProperties();
                // Earlier visual-only CG01 installs may still exist in local scenes.
                // Keep that root for provenance, but never show duplicate real targets.
                var old = roots.SingleOrDefault(r => r.name == "M02 / CG01 cargo visual trial");
                if (old != null) { Undo.RecordObject(old, "Hide superseded CG01 visual trial"); old.SetActive(false); }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("CA07 scene save failed.");
                Undo.CollapseUndoOperations(undo);
                Selection.activeGameObject = trial.gameObject;
                Debug.Log("CA07 installed. Test Site defaults to C1, Hold Test Aircraft ON. R applies C2/Full Sortie and timing changes.", ev);
            }
            catch (Exception ex)
            { if (undo >= 0) Undo.RevertAllDownToGroup(undo); Debug.LogError("CA07: " + ex); }
        }

        private static void CheckPath(Vector3[] path, Vector3 expected, Rect bounds, Terrain terrain)
        {
            if (Vector3.ProjectOnPlane(path[0] - expected, Vector3.up).sqrMagnitude > .01f)
                throw new InvalidOperationException("CA07 V06 cargo point differs from current scene.");
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 p = path[i];
                if (!bounds.Contains(new Vector2(p.x, p.z)) || p.x < origin.x || p.x > origin.x + size.x ||
                    p.z < origin.z || p.z > origin.z + size.z || (i > 0 && (p - path[i-1]).sqrMagnitude < 1f))
                    throw new InvalidOperationException("CA07 invalid route node: " + p);
            }
        }
    }
}
