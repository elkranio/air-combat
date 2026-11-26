using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Incremental southern geography pass on the installed V04 scene.
    // No changes to the airfield, convoy route, combat or deployment settings.
    internal static class Mission02LayoutV05Authoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Art = "Assets/_Project/Art/Environment/Mission02_ConvoyInterdiction";
        private const string Geometry = "Assets/_Project/Editor/Mission02/Mission02LayoutV05.json";
        private const float SouthExtension = 800f;
        [Serializable] private sealed class Rock { public Vector3[] vertices; public int[] sides, tops; }
        [Serializable] private sealed class Layout
        {
            public Rock[] rocks;
            public Vector3 c1, c2;
            public Vector3[] first, second, exit;
        }

        [MenuItem("Air Combat/Missions/M02 - Apply Canyon Pockets V05")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
            { Debug.LogError("M02 V05: open and save the installed V04 scene first."); return; }
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
                    throw new InvalidOperationException("Install V04 first; terrain, guide or trial references are missing.");
                Rect oldBounds = trial.OperationBounds;
                if (Mathf.Abs(oldBounds.xMin + 2430f) > .1f || Mathf.Abs(oldBounds.yMin + 1845f) > .1f ||
                    Mathf.Abs(oldBounds.width - 4860f) > .1f || Mathf.Abs(oldBounds.height - 3690f) > .1f)
                    throw new InvalidOperationException("V05 expects the original V04 operation bounds; already installed or layout changed.");
                var so = new SerializedObject(trial);
                if (so.FindProperty("convoyTerrain").objectReferenceValue != terrain)
                    throw new InvalidOperationException("Unexpected convoy terrain binding.");
                var children = group.transform.Cast<Transform>().ToArray();
                if (children.Length != 5 || children.Any(t => t.GetComponent<RadarTerrainObstacle>() == null))
                    throw new InvalidOperationException("Expected five V04 rock masses.");
                var oldRocks = new[] { children.Single(t => t.name == "M02_V04_Massif_4"),
                    children.Single(t => t.name == "M02_V04_Massif_5") };
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
                    throw new InvalidOperationException("Missing V05 geometry or rock materials.");
                string[] paths = { Art + "/M02_V05_Massif_4.asset", Art + "/M02_V05_Massif_5.asset", Art + "/M02_V05_Terrain.asset" };
                if (paths.Any(p => AssetDatabase.LoadMainAssetAtPath(p) != null))
                    throw new InvalidOperationException("V05 assets already exist; check the scene before reinstalling.");
                var oldData = terrain.terrainData;
                Vector3 size = oldData.size;
                Vector3 origin = terrain.transform.position;
                // The old height field is deliberately flat. Do not stretch a future authored terrain.
                var heights = oldData.GetHeights(0, 0, oldData.heightmapResolution, oldData.heightmapResolution);
                foreach (float height in heights)
                    if (Mathf.Abs(height) > .00001f) throw new InvalidOperationException("V05 assumes a flat height field.");
                if (Mathf.Abs(size.x - 6060f) > .1f || Mathf.Abs(size.z - 4890f) > .1f ||
                    Mathf.Abs(origin.x + 3030f) > .1f || Mathf.Abs(origin.z + 2445f) > .1f)
                    throw new InvalidOperationException("Unexpected V04 terrain extent.");
                var newSize = size + Vector3.forward * SouthExtension;
                var newOrigin = origin - Vector3.forward * SouthExtension;
                var newBounds = Rect.MinMaxRect(oldBounds.xMin, oldBounds.yMin - SouthExtension, oldBounds.xMax, oldBounds.yMax);
                // New flat physics asset; the existing sand plane remains the visible ground.
                var data = new TerrainData { name = "M02 V05 flat desert", heightmapResolution = oldData.heightmapResolution,
                    size = newSize };
                data.SetHeights(0, 0, heights);
                var meshes = new Mesh[2];
                for (int i = 0; i < meshes.Length; i++)
                {
                    var r = layout.rocks[i];
                    if (r.vertices == null || r.sides == null || r.tops == null || r.vertices.Length < 6 ||
                        r.sides.Length % 3 != 0 || r.tops.Length % 3 != 0 ||
                        r.sides.Concat(r.tops).Any(k => k < 0 || k >= r.vertices.Length))
                        throw new InvalidOperationException("Invalid V05 mesh indices.");
                    var mesh = new Mesh { name = $"M02_V05_Massif_{i + 4}", vertices = r.vertices, subMeshCount = 2 };
                    mesh.SetTriangles(r.sides, 0); mesh.SetTriangles(r.tops, 1);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes[i] = mesh;
                }
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("M02 V05 canyon pockets");
                int created = 0;
                try
                {
                    for (int i = 0; i < meshes.Length; i++)
                    { AssetDatabase.CreateAsset(meshes[i], paths[i]); created++; }
                    AssetDatabase.CreateAsset(data, paths[2]); created++;
                    foreach (var old in oldRocks) Undo.DestroyObjectImmediate(old.gameObject);
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        var go = new GameObject(meshes[i].name, typeof(MeshFilter), typeof(MeshRenderer));
                        Undo.RegisterCreatedObjectUndo(go, "V05 rock");
                        go.transform.SetParent(group.transform, false);
                        go.GetComponent<MeshFilter>().sharedMesh = meshes[i];
                        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { side, top };
                        go.AddComponent<RadarTerrainObstacle>();
                    }
                    Undo.RecordObject(terrain, "V05 terrain data");
                    Undo.RecordObject(collider, "V05 terrain collider");
                    Undo.RecordObject(terrain.transform, "V05 terrain origin");
                    terrain.terrainData = data; collider.terrainData = data;
                    terrain.transform.position = newOrigin; terrain.drawHeightmap = false;
                    Undo.RecordObject(surface.transform, "V05 visible sand");
                    surface.transform.SetPositionAndRotation(newOrigin + new Vector3(newSize.x * .5f, -.02f, newSize.z * .5f), Quaternion.identity);
                    surface.transform.localScale = new Vector3(newSize.x / 10f, 1f, newSize.z / 10f);
                    SetLine(first, layout.first); SetLine(second, layout.second); SetLine(exit, layout.exit);
                    SetRing(ring1, layout.c1); SetRing(ring2, layout.c2);
                    Undo.RecordObject(trial, "V05 points and boundary");
                    trial.ConfigureTwoPartRoute(trial.PointA, trial.PointB, layout.c1, layout.c2);
                    so.Update(); so.FindProperty("operationBounds").rectValue = newBounds;
                    so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(trial);
                    var map = MissionMapBaker.BakeForRouteTrial(scene, surface, player.transform);
                    if (map == null || map.Geography == null || map.ObstacleCount != 5 || map.SectionTriangleCount == 0 ||
                        Mathf.Abs(map.WorldBounds.yMin - newBounds.yMin) > .1f || Mathf.Abs(map.WorldBounds.height - newBounds.height) > .1f)
                        throw new InvalidOperationException("V05 map bake incomplete or wrong extent.");
                    Undo.RecordObject(tactical, "V05 map binding");
                    tactical.ConfigureForAuthoring(map, player.transform, Array.Empty<TacticalMapView.PlannedSam>(),
                        Array.Empty<Transform>(), null, "CONVOY INTERDICTION / ROUTE TRIAL");
                    EditorUtility.SetDirty(tactical);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("V05 scene save failed.");
                    AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = trial.gameObject;
                    Debug.Log("M02 V05 installed. C1/C2 in two dead-end pockets; south extent +800. W / Tab / G / R unchanged.", trial);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(undo);
                    for (int i = 0; i < created; i++) AssetDatabase.DeleteAsset(paths[i]);
                    // The map asset is updated in place. Restore the old map after rollback.
                    try { MissionMapBaker.BakeForRouteTrial(scene, surface, player.transform); }
                    catch (Exception bakeError) { Debug.LogError("V05 rollback map: " + bakeError); }
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("M02 V05: " + ex); }
        }

        private static void SetLine(LineRenderer line, Vector3[] points)
        {
            Undo.RecordObject(line, "V05 guide"); line.useWorldSpace = true;
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
