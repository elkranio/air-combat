using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Incremental asymmetric canyon pass on the installed V05 scene.
    // No changes to the airfield, convoy route, combat or deployment settings.
    internal static class Mission02LayoutV06Authoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Art = "Assets/_Project/Art/Environment/Mission02_ConvoyInterdiction";
        private const string Geometry = "Assets/_Project/Editor/Mission02/Mission02LayoutV06.json";
        [Serializable] private sealed class Rock { public Vector3[] vertices; public int[] sides, tops; }
        [Serializable] private sealed class Layout
        {
            public Rock[] rocks;
            public Vector3 c1, c2;
            public Vector3[] first, second, exit;
        }

        [MenuItem("Air Combat/Missions/M02 - Apply Canyon Pockets V06")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
            { Debug.LogError("M02 V06: open and save the installed V05 scene first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                T Single<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                var trial = Single<Mission02RouteTrial>();
                var player = Single<JetAssembly>();
                var tactical = Single<TacticalMapView>();
                var terrain = Single<Terrain>();
                var collider = terrain.GetComponent<TerrainCollider>();
                var surface = roots.Single(r => r.name == "M02 / visible sand").GetComponent<Renderer>();
                var group = roots.Single(r => r.name == "M02 / canyon massifs");
                if (!trial.HasSecondPoint || trial.RouteGuide == null || surface == null ||
                    terrain.terrainData == null || collider == null || collider.terrainData != terrain.terrainData)
                    throw new InvalidOperationException("Install V05 first; terrain, guide or trial references are missing.");
                Rect oldBounds = trial.OperationBounds;
                if (Mathf.Abs(oldBounds.xMin + 2430f) > .1f || Mathf.Abs(oldBounds.yMin + 2645f) > .1f ||
                    Mathf.Abs(oldBounds.width - 4860f) > .1f || Mathf.Abs(oldBounds.height - 4490f) > .1f)
                    throw new InvalidOperationException("V06 expects the expanded V05 operation bounds; already installed or layout changed.");
                var so = new SerializedObject(trial);
                if (so.FindProperty("convoyTerrain").objectReferenceValue != terrain)
                    throw new InvalidOperationException("Unexpected convoy terrain binding.");
                var children = group.transform.Cast<Transform>().ToArray();
                if (children.Length != 5 || children.Any(t => t.GetComponent<RadarTerrainObstacle>() == null))
                    throw new InvalidOperationException("Expected five V05 rock masses.");
                var oldRocks = new[] { children.Single(t => t.name == "M02_V04_Massif_1"),
                    children.Single(t => t.name == "M02_V05_Massif_4"),
                    children.Single(t => t.name == "M02_V05_Massif_5") };
                var lines = trial.RouteGuide.GetComponentsInChildren<LineRenderer>(true);
                var first = lines.Single(l => l.name == "S to C");
                var second = lines.Single(l => l.name == "C1 to C2");
                var exit = lines.Single(l => l.name == "C to B");
                // Preserve A/B rings and their references. Only move the two optional rings.
                LineRenderer FindRing(Vector3 point) => lines.Where(l => l.loop && l.positionCount > 0).Single(l =>
                {
                    Vector3 p = l.GetPosition(0);
                    if (!l.useWorldSpace) p = l.transform.TransformPoint(p);
                    return Vector2.Distance(new Vector2(p.x,p.z),new Vector2(point.x,point.z)) < 40f;
                });
                var ring1 = FindRing(trial.PointC);
                var ring2 = FindRing(trial.PointC2);
                var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var layout = json != null ? JsonUtility.FromJson<Layout>(json.text) : null;
                var side = AssetDatabase.LoadAssetAtPath<Material>(Art + "/M02_Cliff.mat");
                var top = AssetDatabase.LoadAssetAtPath<Material>(Art + "/M02_RockTop.mat");
                if (layout == null || layout.rocks == null || layout.rocks.Length != 2 || side == null || top == null ||
                    new[] { layout.first, layout.second, layout.exit }.Any(p => p == null || p.Length < 2))
                    throw new InvalidOperationException("Missing V06 geometry or rock materials.");
                string[] paths = { Art + "/M02_V06_West.asset", Art + "/M02_V06_South.asset" };
                if (paths.Any(p => AssetDatabase.LoadMainAssetAtPath(p) != null))
                    throw new InvalidOperationException("V06 assets already exist; check the scene before reinstalling.");
                var meshes = new Mesh[2];
                for (int i = 0; i < meshes.Length; i++)
                {
                    var r = layout.rocks[i];
                    if (r.vertices == null || r.sides == null || r.tops == null || r.vertices.Length < 6 ||
                        r.sides.Length % 3 != 0 || r.tops.Length % 3 != 0 ||
                        r.sides.Concat(r.tops).Any(k => k < 0 || k >= r.vertices.Length))
                        throw new InvalidOperationException("Invalid V06 mesh indices.");
                    var mesh = new Mesh { name = i == 0 ? "M02_V06_West" : "M02_V06_South", vertices = r.vertices, subMeshCount = 2 };
                    mesh.SetTriangles(r.sides, 0); mesh.SetTriangles(r.tops, 1);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes[i] = mesh;
                }
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("M02 V06 canyon pockets");
                int created = 0;
                try
                {
                    for (int i = 0; i < meshes.Length; i++)
                    { AssetDatabase.CreateAsset(meshes[i], paths[i]); created++; }
                    foreach (var old in oldRocks) Undo.DestroyObjectImmediate(old.gameObject);
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        var go = new GameObject(meshes[i].name, typeof(MeshFilter), typeof(MeshRenderer));
                        Undo.RegisterCreatedObjectUndo(go, "V06 rock");
                        go.transform.SetParent(group.transform, false);
                        go.GetComponent<MeshFilter>().sharedMesh = meshes[i];
                        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { side, top };
                        go.AddComponent<RadarTerrainObstacle>();
                    }
                    SetLine(first, layout.first); SetLine(second, layout.second); SetLine(exit, layout.exit);
                    SetRing(ring1, layout.c1); SetRing(ring2, layout.c2);
                    Undo.RecordObject(trial, "V06 optional points");
                    trial.ConfigureTwoPartRoute(trial.PointA, trial.PointB, layout.c1, layout.c2);
                    EditorUtility.SetDirty(trial);
                    var map = MissionMapBaker.BakeForRouteTrial(scene, surface, player.transform);
                    if (map == null || map.Geography == null || map.ObstacleCount != 4 || map.SectionTriangleCount == 0 ||
                        Mathf.Abs(map.WorldBounds.yMin - oldBounds.yMin) > .1f || Mathf.Abs(map.WorldBounds.height - oldBounds.height) > .1f)
                        throw new InvalidOperationException("V06 map bake incomplete or wrong extent.");
                    Undo.RecordObject(tactical, "V06 map binding");
                    tactical.ConfigureForAuthoring(map, player.transform, Array.Empty<TacticalMapView.PlannedSam>(),
                        Array.Empty<Transform>(), null, "CONVOY INTERDICTION / ROUTE TRIAL");
                    EditorUtility.SetDirty(tactical);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("V06 scene save failed.");
                    AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = trial.gameObject;
                    Debug.Log("M02 V06 installed. C1/C2 in asymmetric west and southeast pockets; extent unchanged. W / Tab / G / R unchanged.", trial);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(undo);
                    for (int i = 0; i < created; i++) AssetDatabase.DeleteAsset(paths[i]);
                    // The map asset is updated in place. Restore the old map after rollback.
                    try { MissionMapBaker.BakeForRouteTrial(scene, surface, player.transform); }
                    catch (Exception bakeError) { Debug.LogError("V06 rollback map: " + bakeError); }
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("M02 V06: " + ex); }
        }

        private static void SetLine(LineRenderer line, Vector3[] points)
        {
            Undo.RecordObject(line, "V06 guide"); line.useWorldSpace = true;
            line.positionCount = points.Length; line.SetPositions(points); EditorUtility.SetDirty(line);
        }
        private static void SetRing(LineRenderer line, Vector3 point)
        {
            var positions = new Vector3[48];
            for (int i = 0; i < positions.Length; i++)
            {
                float t = i * Mathf.PI * 2f / positions.Length;
                positions[i] = point + new Vector3(Mathf.Cos(t) * 34f, .55f, Mathf.Sin(t) * 34f);
            }
            SetLine(line, positions);
        }
    }
}
