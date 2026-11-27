using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // V06 pockets -> open-ended canyons. Preserve the installed scene, all
    // objective positions, route guides, and mission tuning.
    internal static class Mission02LayoutV07Authoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Art = "Assets/_Project/Art/Environment/Mission02_ConvoyInterdiction";
        private const string Geometry = "Assets/_Project/Editor/Mission02/Mission02LayoutV07.json";
        [Serializable] private sealed class Rock { public string name; public Vector3[] vertices; public int[] sides, tops; }
        [Serializable] private sealed class Layout { public Rock[] rocks; public Vector3[] c1Path, c2Path; }

        [MenuItem("Air Combat/Missions/M02 - Apply Open Canyons V07")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
            { Debug.LogError("V07: open and save existing M02 first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                T Single<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                var trial = Single<Mission02RouteTrial>();
                var player = Single<JetAssembly>();
                var terrain = Single<Terrain>();
                var surface = roots.Single(r => r.name == "M02 / visible sand").GetComponent<Renderer>();
                var group = roots.Single(r => r.name == "M02 / canyon massifs").transform;
                var ev = trial.GetComponent<Mission02CargoEvacuation>();
                var link = new SerializedObject(trial).FindProperty("cargoEvacuation");
                if (!trial.HasSecondPoint || ev == null || link == null || link.objectReferenceValue != ev ||
                    surface == null || terrain.terrainData == null || group.position.sqrMagnitude > .0001f ||
                    Quaternion.Angle(group.rotation, Quaternion.identity) > .01f || (group.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                    throw new InvalidOperationException("V07 requires the installed CA07/V06 scene and identity canyon group.");
                var bounds = trial.OperationBounds;
                if (Mathf.Abs(bounds.xMin + 2430f) > .1f || Mathf.Abs(bounds.yMin + 2645f) > .1f ||
                    Mathf.Abs(bounds.width - 4860f) > .1f || Mathf.Abs(bounds.height - 4490f) > .1f)
                    throw new InvalidOperationException("V07 expects the existing V06 operation bounds.");
                var children = group.Cast<Transform>().ToArray();
                if (children.Length != 4 || children.Any(t => t.GetComponent<RadarTerrainObstacle>() == null))
                    throw new InvalidOperationException("V07 expects four V06 massif objects; already installed or modified.");
                var old = new[] { children.Single(t => t.name == "M02_V06_West"), children.Single(t => t.name == "M02_V06_South") };
                var retained = children.Except(old).ToArray();
                if (!retained.Select(t => t.name).OrderBy(n => n).SequenceEqual(new[] { "M02_V04_Massif_2", "M02_V04_Massif_3" }))
                    throw new InvalidOperationException("Unexpected retained massifs.");
                foreach (var t in old)
                {
                    var filter = t.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || AssetDatabase.GetAssetPath(filter.sharedMesh) != Art + "/" + t.name + ".asset" ||
                        t.localPosition.sqrMagnitude > .0001f || Quaternion.Angle(t.localRotation, Quaternion.identity) > .01f ||
                        (t.localScale - Vector3.one).sqrMagnitude > .0001f)
                        throw new InvalidOperationException("V06 massif changed: " + t.name);
                }
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var data = asset != null ? JsonUtility.FromJson<Layout>(asset.text) : null;
                var side = AssetDatabase.LoadAssetAtPath<Material>(Art + "/M02_Cliff.mat");
                var top = AssetDatabase.LoadAssetAtPath<Material>(Art + "/M02_RockTop.mat");
                if (data == null || data.rocks == null || data.rocks.Length != 4 || side == null || top == null)
                    throw new InvalidOperationException("Missing V07 geometry or materials.");
                string[] expected = { "M02_V07_West_1", "M02_V07_West_2", "M02_V07_South_1", "M02_V07_South_2" };
                if (!data.rocks.Select(r => r.name).SequenceEqual(expected)) throw new InvalidOperationException("Unexpected V07 mesh names.");
                foreach (var rock in data.rocks)
                    if (rock.vertices == null || rock.vertices.Length < 6 || rock.vertices.Length % 2 != 0 ||
                        rock.sides == null || rock.tops == null || rock.sides.Length % 3 != 0 || rock.tops.Length % 3 != 0 ||
                        rock.sides.Concat(rock.tops).Any(i => i < 0 || i >= rock.vertices.Length))
                        throw new InvalidOperationException("Invalid mesh: " + rock.name);
                CheckPath(data.c1Path, trial.PointC, bounds, true, data.rocks, retained, terrain);
                CheckPath(data.c2Path, trial.PointC2, bounds, false, data.rocks, retained, terrain);
                var paths = expected.Select(n => Art + "/" + n + ".asset").ToArray();
                if (paths.Any(p => AssetDatabase.LoadMainAssetAtPath(p) != null))
                    throw new InvalidOperationException("V07 assets already exist. No overwrite.");
                var cargo = new SerializedObject(ev);
                if (cargo.FindProperty("c1Path") == null || cargo.FindProperty("c2Path") == null)
                    throw new InvalidOperationException("Missing CA07 path fields.");
                Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("M02 V07 open canyons");
                int created = 0;
                try
                {
                    for (int i = 0; i < data.rocks.Length; i++)
                    {
                        var r = data.rocks[i];
                        var mesh = new Mesh { name = r.name, vertices = r.vertices, subMeshCount = 2 };
                        mesh.SetTriangles(r.sides, 0); mesh.SetTriangles(r.tops, 1);
                        mesh.RecalculateNormals(); mesh.RecalculateBounds();
                        AssetDatabase.CreateAsset(mesh, paths[i]); created++;
                        var go = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
                        Undo.RegisterCreatedObjectUndo(go, "V07 massif");
                        go.transform.SetParent(group, false);
                        go.GetComponent<MeshFilter>().sharedMesh = mesh;
                        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { side, top };
                        go.AddComponent<RadarTerrainObstacle>();
                    }
                    foreach (var t in old) Undo.DestroyObjectImmediate(t.gameObject);
                    Undo.RecordObject(ev, "V07 cargo paths");
                    SetPath(cargo.FindProperty("c1Path"), data.c1Path);
                    SetPath(cargo.FindProperty("c2Path"), data.c2Path);
                    cargo.ApplyModifiedProperties(); EditorUtility.SetDirty(ev);
                    var map = MissionMapBaker.BakeForRouteTrial(scene, surface, player.transform);
                    if (map == null || map.Geography == null || map.ObstacleCount != 6 || map.SectionTriangleCount == 0 ||
                        Mathf.Abs(map.WorldBounds.xMin - bounds.xMin) > .1f || Mathf.Abs(map.WorldBounds.yMin - bounds.yMin) > .1f ||
                        Mathf.Abs(map.WorldBounds.width - bounds.width) > .1f || Mathf.Abs(map.WorldBounds.height - bounds.height) > .1f)
                        throw new InvalidOperationException("V07 tactical map bake incomplete.");
                    // Bake updates the existing map asset in place. Preserve TacticalMapView configuration.
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("V07 scene save failed.");
                    AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = ev.gameObject;
                    Debug.Log("M02 V07 installed: C1 NW / C2 SE at Y78; six rock solids. Cargo pads, player guides, dispatch and reserve settings unchanged.", ev);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(undo);
                    for (int i = 0; i < created; i++) AssetDatabase.DeleteAsset(paths[i]);
                    try { MissionMapBaker.BakeForRouteTrial(scene, surface, player.transform); }
                    catch (Exception ex) { Debug.LogError("V07 rollback map: " + ex); }
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("M02 V07: " + ex); }
        }

        private static void SetPath(SerializedProperty property, Vector3[] path)
        {
            property.arraySize = path.Length;
            for (int i = 0; i < path.Length; i++) property.GetArrayElementAtIndex(i).vector3Value = path[i];
        }

        private static void CheckPath(Vector3[] path, Vector3 pad, Rect bounds, bool west, Rock[] rocks, Transform[] retained, Terrain terrain)
        {
            if (path == null || path.Length != 2 || Vector3.ProjectOnPlane(path[0]-pad, Vector3.up).sqrMagnitude > .01f ||
                path.Any(p => Mathf.Abs(p.y-78f) > .01f) ||
                (west ? path[1].x > bounds.xMin-419f : path[1].z > bounds.yMin-419f))
                throw new InvalidOperationException("V07 invalid cargo path or exit.");
            var a = new Vector2(path[0].x, path[0].z); var b = new Vector2(path[1].x, path[1].z);
            var origin = terrain.transform.position; var size = terrain.terrainData.size;
            int count = Mathf.CeilToInt(Vector2.Distance(a,b));
            for (int i = 0; i <= count; i++)
            {
                var p = Vector2.Lerp(a,b,i/(float)count);
                foreach (var r in rocks)
                    if (NearFootprint(p,r.vertices,r.vertices.Length/2,35f))
                        throw new InvalidOperationException("V07 insufficient rock clearance: " + p);
                foreach (var t in retained)
                {
                    var r = t.GetComponent<Renderer>();
                    if (r == null) throw new InvalidOperationException("V07 retained massif has no renderer.");
                    var box = r.bounds;
                    if (p.x > box.min.x-35f && p.x < box.max.x+35f && p.y > box.min.z-35f && p.y < box.max.z+35f)
                        throw new InvalidOperationException("V07 path near retained massif: " + t.name);
                }
                if (p.x >= origin.x && p.x <= origin.x+size.x && p.y >= origin.z && p.y <= origin.z+size.z &&
                    terrain.SampleHeight(new Vector3(p.x,0f,p.y))+origin.y > 58f)
                    throw new InvalidOperationException("V07 terrain too high at " + p);
            }
        }

        private static bool NearFootprint(Vector2 p, Vector3[] v, int n, float clearance)
        {
            bool inside = false;
            for (int i = 0, j = n-1; i < n; j = i++)
            {
                var a = new Vector2(v[j].x,v[j].z); var b = new Vector2(v[i].x,v[i].z); var e = b-a;
                float t = Mathf.Clamp01(Vector2.Dot(p-a,e)/Mathf.Max(.000001f,e.sqrMagnitude));
                if ((p-a-e*t).sqrMagnitude < clearance*clearance) return true;
                if ((a.y>p.y)!=(b.y>p.y) && p.x<a.x+(b.x-a.x)*(p.y-a.y)/(b.y-a.y)) inside=!inside;
            }
            return inside;
        }
    }
}
