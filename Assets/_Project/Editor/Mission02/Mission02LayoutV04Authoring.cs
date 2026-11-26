using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Incremental layout replacement, retaining D3 combat and all scene references.
    internal static class Mission02LayoutV04Authoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Art = "Assets/_Project/Art/Environment/Mission02_ConvoyInterdiction";
        private const string Geometry = "Assets/_Project/Editor/Mission02/Mission02LayoutV04.json";
        [Serializable] private sealed class Rock { public Vector3[] vertices; public int[] sides, tops; }
        [Serializable] private sealed class Layout
        {
            public Rock[] rocks;
            public Vector3 spawn, a, b, c1, c2;
            public float yaw;
            public Vector3[] direct, first, second, exit, convoy;
        }

        [MenuItem("Air Combat/Missions/M02 - Apply Two-Target Layout V04")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
            { Debug.LogError("M02E: open and save the current M02 scene first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                T Single<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                var trial = Single<Mission02RouteTrial>();
                var player = Single<JetAssembly>();
                var camera = Single<CombatCameraRig>();
                var tactical = Single<TacticalMapView>();
                var field = roots.Single(r => r.name == "Airfield B / reused M01");
                var group = roots.Single(r => r.name == "M02 / canyon massifs");
                var surface = roots.Single(r => r.name == "M02 / visible sand").GetComponent<Renderer>();
                var guide = trial.RouteGuide;
                if (trial.HasSecondPoint) throw new InvalidOperationException("V04 is already installed. No changes made.");
                if (guide == null || surface == null) throw new InvalidOperationException("M02 guide or sand missing.");
                var lines = guide.GetComponentsInChildren<LineRenderer>(true);
                var direct = lines.Single(l => l.name == "S to A");
                var first = lines.Single(l => l.name == "S to C");
                var exit = lines.Single(l => l.name == "C to B");
                var convoy = lines.Single(l => l.name.StartsWith("A to B", StringComparison.Ordinal));
                if (new SerializedObject(trial).FindProperty("convoyRoute").objectReferenceValue != convoy)
                    throw new InvalidOperationException("M02 convoy route binding differs from expected scene.");
                var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var layout = json != null ? JsonUtility.FromJson<Layout>(json.text) : null;
                var side = AssetDatabase.LoadAssetAtPath<Material>(Art + "/M02_Cliff.mat");
                var top = AssetDatabase.LoadAssetAtPath<Material>(Art + "/M02_RockTop.mat");
                if (layout == null || layout.rocks == null || layout.rocks.Length != 5 || side == null || top == null ||
                    new[] { layout.direct, layout.first, layout.second, layout.exit, layout.convoy }.Any(p => p == null || p.Length < 2))
                    throw new InvalidOperationException("M02 V04 geometry or materials missing.");
                if (Mathf.Abs(trial.OperationBounds.width - 4860f) > .1f || Mathf.Abs(trial.OperationBounds.height - 3690f) > .1f)
                    throw new InvalidOperationException("M02 bounds changed; V04 assumes original extent.");
                var oldRockChildren = group.transform.Cast<Transform>().Select(t => t.gameObject).ToArray();
                if (oldRockChildren.Length != 2 || oldRockChildren.Any(o => o.GetComponent<RadarTerrainObstacle>() == null))
                    throw new InvalidOperationException("Unexpected existing rock composition.");
                string[] paths = Enumerable.Range(1, layout.rocks.Length).Select(i => Art + $"/M02_V04_Massif_{i}.asset").ToArray();
                if (paths.Any(p => AssetDatabase.LoadMainAssetAtPath(p) != null))
                    throw new InvalidOperationException("V04 mesh assets already exist. Check previous install before retrying.");
                // Validate and create closed readable meshes before editing scene objects.
                var meshes = new Mesh[layout.rocks.Length];
                for (int i = 0; i < meshes.Length; i++)
                {
                    var r = layout.rocks[i];
                    if (r.vertices == null || r.sides == null || r.tops == null || r.vertices.Length < 6 ||
                        r.sides.Length % 3 != 0 || r.tops.Length % 3 != 0 ||
                        r.sides.Concat(r.tops).Any(k => k < 0 || k >= r.vertices.Length))
                        throw new InvalidOperationException("Bad V04 mesh indices.");
                    var mesh = new Mesh { name = $"M02_V04_Massif_{i+1}", vertices = r.vertices, subMeshCount = 2 };
                    mesh.SetTriangles(r.sides, 0); mesh.SetTriangles(r.tops, 1);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    meshes[i] = mesh;
                }
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("M02 V04 layout");
                int created = 0;
                try
                {
                    for (int i = 0; i < meshes.Length; i++)
                    { AssetDatabase.CreateAsset(meshes[i], paths[i]); created++; }
                    // Preserve exact airfield-to-player transform and runway ground offset.
                    Vector3 oldSpawn = player.transform.position;
                    Vector3 nextSpawn = new Vector3(layout.spawn.x, oldSpawn.y, layout.spawn.z);
                    Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                    Quaternion delta = Quaternion.FromToRotation(forward, Quaternion.Euler(0f, layout.yaw, 0f) * Vector3.forward);
                    Move(field.transform, nextSpawn + delta * (field.transform.position - oldSpawn), delta * field.transform.rotation);
                    Move(player.transform, nextSpawn, delta * player.transform.rotation);
                    Move(camera.transform, camera.transform.position + nextSpawn - oldSpawn, camera.transform.rotation);
                    foreach (var old in oldRockChildren) Undo.DestroyObjectImmediate(old);
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        var go = new GameObject(meshes[i].name, typeof(MeshFilter), typeof(MeshRenderer));
                        Undo.RegisterCreatedObjectUndo(go, "V04 rock");
                        go.transform.SetParent(group.transform, false);
                        go.GetComponent<MeshFilter>().sharedMesh = meshes[i];
                        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { side, top };
                        go.AddComponent<RadarTerrainObstacle>();
                    }
                    SetLine(direct, layout.direct); SetLine(first, layout.first);
                    SetLine(exit, layout.exit); SetLine(convoy, layout.convoy);
                    var second = NewLine(guide.transform, "C1 to C2", first.sharedMaterial, 3f, false);
                    SetLine(second, layout.second);
                    foreach (var l in lines.Where(l => l.loop)) Undo.DestroyObjectImmediate(l.gameObject);
                    Ring(guide.transform, layout.a, convoy.sharedMaterial);
                    Ring(guide.transform, layout.b, convoy.sharedMaterial);
                    Ring(guide.transform, layout.c1, first.sharedMaterial);
                    Ring(guide.transform, layout.c2, first.sharedMaterial);
                    Undo.RecordObject(trial, "V04 mission points");
                    trial.ConfigureTwoPartRoute(layout.a, layout.b, layout.c1, layout.c2);
                    EditorUtility.SetDirty(trial);
                    var map = MissionMapBaker.BakeForRouteTrial(scene, surface, player.transform);
                    if (map == null || map.Geography == null || map.ObstacleCount != meshes.Length || map.SectionTriangleCount == 0)
                        throw new InvalidOperationException("M02 V04 map bake incomplete.");
                    Undo.RecordObject(tactical, "V04 map binding");
                    tactical.ConfigureForAuthoring(map, player.transform, Array.Empty<TacticalMapView.PlannedSam>(),
                        Array.Empty<Transform>(), null, "CONVOY INTERDICTION / ROUTE TRIAL");
                    EditorUtility.SetDirty(tactical);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("M02 scene save failed.");
                    AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = trial.gameObject;
                    Debug.Log("M02E V04 installed. W starts sortie and convoy, Tab map, G guides, R restart. C1 and C2 are measurement points only.", trial);
                }
                catch
                {
                    // The existing map asset can have been rebaked; if this fails,
                    // rebake the old scene via the M02 map menu before playing.
                    Undo.RevertAllDownToGroup(undo);
                    for (int i = 0; i < created; i++) AssetDatabase.DeleteAsset(paths[i]);
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("M02E: " + ex); }
        }

        private static void Move(Transform t, Vector3 position, Quaternion rotation)
        {
            Undo.RecordObject(t, "V04 transform"); t.SetPositionAndRotation(position, rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        private static void SetLine(LineRenderer line, Vector3[] points)
        {
            Undo.RecordObject(line, "V04 guide"); line.useWorldSpace = true;
            line.positionCount = points.Length; line.SetPositions(points); EditorUtility.SetDirty(line);
        }
        private static LineRenderer NewLine(Transform parent, string name, Material material, float width, bool loop)
        {
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "V04 guide");
            go.transform.SetParent(parent, false); go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = true; line.loop = loop; line.startWidth = line.endWidth = width;
            line.alignment = LineAlignment.TransformZ; line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; return line;
        }
        private static void Ring(Transform parent, Vector3 point, Material material)
        {
            var positions = new Vector3[48];
            for (int i = 0; i < positions.Length; i++)
            {
                float t = i * Mathf.PI * 2f / positions.Length;
                positions[i] = point + new Vector3(Mathf.Cos(t)*34f,.55f,Mathf.Sin(t)*34f);
            }
            SetLine(NewLine(parent, "POI / arrival marker", material, 3f, true), positions);
        }
    }
}
