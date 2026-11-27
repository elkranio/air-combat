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
        [Serializable] private sealed class Rock { public Vector3[] vertices; }
        [Serializable] private sealed class Routes { public Vector3 c1, c2; public Vector3[] second, exit; public Rock[] rocks; }

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
                    data.second == null || data.second.Length < 8 || data.rocks == null || data.rocks.Length == 0)
                    throw new InvalidOperationException("CA07 requires accepted CA01/CG01, V06 and terrain.");
                BuildPaths(data, trial, terrain, out var c1, out var c2);
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

        [MenuItem("Air Combat/Missions/M02 - Apply CA07B Off-map Flight Paths")]
        private static void ApplyPaths()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != Mission || scene.isDirty)
            { Debug.LogError("CA07B: open and save existing M02 first."); return; }
            int undo = -1;
            try
            {
                var roots = scene.GetRootGameObjects();
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
                var ev = trial.GetComponent<Mission02CargoEvacuation>();
                var link = new SerializedObject(trial).FindProperty("cargoEvacuation");
                if (ev == null || link == null || link.objectReferenceValue != ev)
                    throw new InvalidOperationException("CA07B requires the installed CA07 component.");
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Layout);
                var data = asset != null ? JsonUtility.FromJson<Routes>(asset.text) : null;
                BuildPaths(data, trial, terrain, out var c1, out var c2);
                Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("CA07B off-map flight paths");
                Undo.RecordObject(ev, "Update cargo routes");
                var so = new SerializedObject(ev);
                SetPath(so.FindProperty("c1Path"), c1);
                SetPath(so.FindProperty("c2Path"), c2);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ev);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("CA07B scene save failed.");
                Undo.CollapseUndoOperations(undo);
                Selection.activeGameObject = ev.gameObject;
                Debug.Log("CA07B paths updated. C1 west ingress/egress; C2 south overflight. All existing timing and test settings preserved.", ev);
            }
            catch (Exception ex)
            { if (undo >= 0) Undo.RevertAllDownToGroup(undo); Debug.LogError("CA07B: " + ex); }
        }

        private static void SetPath(SerializedProperty property, Vector3[] path)
        {
            if (property == null) throw new InvalidOperationException("CA07B missing serialized path.");
            property.arraySize = path.Length;
            for (int i = 0; i < path.Length; i++) property.GetArrayElementAtIndex(i).vector3Value = path[i];
        }

        private static void BuildPaths(Routes data, Mission02RouteTrial trial, Terrain terrain,
            out Vector3[] c1, out Vector3[] c2)
        {
            if (data == null || data.second == null || data.second.Length < 8 || data.rocks == null ||
                data.rocks.Length == 0 || terrain == null || terrain.terrainData == null || !trial.HasSecondPoint)
                throw new InvalidOperationException("CA07B requires V06 rock geometry, guide and terrain.");
            Rect bounds = trial.OperationBounds;
            const float cruise = 78f, beyond = 420f, overflight = 170f;
            // Keep the accepted C1 approach through the pocket, then use the gap
            // between the western and southern massifs to reach the west edge.
            c1 = data.second.Take(8).Select(p => new Vector3(p.x, cruise, p.z))
                .Concat(new[] { new Vector3(bounds.xMin - beyond, cruise, -900f) }).ToArray();
            // The southern massif closes the low-altitude route out of C2.
            // Cross its rim high, with the altitude transition wholly in the basin.
            Vector3 pad = data.c2;
            c2 = new[] { new Vector3(pad.x, cruise, pad.z),
                new Vector3(pad.x, cruise, pad.z - 30f),
                new Vector3(pad.x, overflight, pad.z - 90f),
                new Vector3(pad.x, overflight, bounds.yMin - beyond) };
            CheckPath(c1, trial.PointC, bounds, terrain, data.rocks);
            CheckPath(c2, trial.PointC2, bounds, terrain, data.rocks);
        }

        private static void CheckPath(Vector3[] path, Vector3 expected, Rect bounds, Terrain terrain, Rock[] rocks)
        {
            if (Vector3.ProjectOnPlane(path[0] - expected, Vector3.up).sqrMagnitude > .01f)
                throw new InvalidOperationException("CA07 V06 cargo point differs from current scene.");
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 p = path[i];
                bool last = i == path.Length - 1;
                bool inside = bounds.Contains(new Vector2(p.x, p.z));
                if ((!last && (!inside || p.x < origin.x || p.x > origin.x + size.x ||
                    p.z < origin.z || p.z > origin.z + size.z)) || (last && inside) ||
                    (i > 0 && (p - path[i-1]).sqrMagnitude < 1f))
                    throw new InvalidOperationException("CA07B invalid route node: " + p);
            }
            // Conservative CA01 + attached cargo envelope against the authored
            // V06 west/south solids, including the off-map extension. Editor only.
            foreach (var rock in rocks)
            {
                if (rock.vertices == null || rock.vertices.Length < 6)
                    throw new InvalidOperationException("CA07B invalid V06 rock geometry.");
                float bottom = rock.vertices.Min(v => v.y), top = rock.vertices.Max(v => v.y);
                var polygon = rock.vertices.Where(v => Mathf.Abs(v.y - bottom) < .001f)
                    .Select(v => new Vector2(v.x, v.z)).ToArray();
                if (polygon.Length < 3) throw new InvalidOperationException("CA07B invalid V06 footprint.");
                for (int i = 1; i < path.Length; i++)
                {
                    int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(path[i-1], path[i]) / 2f));
                    for (int n = 0; n <= samples; n++)
                    {
                        Vector3 p = Vector3.Lerp(path[i-1], path[i], n / (float)samples);
                        if (p.y >= top + 24f) continue;
                        if (NearFootprint(new Vector2(p.x, p.z), polygon, 35f))
                            throw new InvalidOperationException("CA07B route too close to V06 rock: " + p);
                    }
                }
            }
        }

        private static bool NearFootprint(Vector2 p, Vector2[] poly, float clearance)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], edge = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(.000001f, edge.sqrMagnitude));
                if ((p - (a + t * edge)).sqrMagnitude < clearance * clearance) return true;
                if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (b.x - a.x) * (p.y - a.y) / (b.y - a.y))
                    inside = !inside;
            }
            return inside;
        }
    }
}
