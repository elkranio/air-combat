using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class Mission01WatcherAuthoring
    {
        private const string Art = "Assets/_Project/Art/Aircraft/Dominion/RE01_Watcher/";
        private const string VisualName = "RE01 Watcher visual";
        private static readonly string[] Parts = { "Hull", "Upper", "Equipment", "Dark", "Gold" };
        private static readonly Color32[] Palette = {
            new Color32(76,94,100,255), new Color32(102,122,125,255), new Color32(92,103,70,255),
            new Color32(43,51,54,255), new Color32(210,174,62,255)
        };
        private static Transform Direct(Transform root, string name) => root.Cast<Transform>().Single(t => t.name == name);

        [MenuItem("Air Combat/Missions/Install Mission01 Watchers v0")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int group = -1;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.name != "Mission01_FirstSortie") throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var roots = scene.GetRootGameObjects();
                var launch = roots.SelectMany(g => g.GetComponentsInChildren<Mission01LaunchSequence>(true)).Single();
                var serialized = new SerializedObject(launch);
                var player = serialized.FindProperty("flight").objectReferenceValue as FlightController;
                if (player == null) throw new InvalidOperationException("Missing mission player.");
                float altitude = serialized.FindProperty("cruiseAltitude").floatValue;
                var ground = roots.Single(g => g.name == "Mission01 / Faceted surface").GetComponent<Renderer>();
                if (ground == null || ground.bounds.size.y > .05f) throw new InvalidOperationException("Expected flat Mission01 ground.");
                float floor = ground.bounds.center.y;
                var objectives = roots.Single(g => g.name == "Mission01 / Objectives").transform;
                var plan = Direct(objectives, "M01 / Rock layout v3 (visual only)");
                var recon = Direct(plan, "02 / Recon zone - empty markers");
                var markers = recon.Cast<Transform>().OrderBy(t => t.position.x).ToArray();
                if (markers.Length != 3) throw new InvalidOperationException("Expected three authored recon markers.");
                foreach (var marker in markers)
                {
                    if ((marker.lossyScale - Vector3.one).sqrMagnitude > .001f)
                        throw new InvalidOperationException("Recon marker must have unit scale: " + marker.name);
                    if (marker.GetComponents<DamageableTarget>().Any(t => !(t is WatcherTarget)))
                        throw new InvalidOperationException("Existing non-Watcher target: " + marker.name);
                }
                var prefab = BuildVisual();
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install Mission01 Watchers");
                var targets = new WatcherTarget[3];
                for (int i = 0; i < markers.Length; i++)
                {
                    var marker = markers[i];
                    Undo.RecordObject(marker, "Set recon altitude");
                    marker.position = new Vector3(marker.position.x, altitude, marker.position.z);
                    var visual = marker.Cast<Transform>().SingleOrDefault(t => t.name == VisualName);
                    if (visual == null)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, marker);
                        Undo.RegisterCreatedObjectUndo(go, "Create Watcher visual");
                        go.name = VisualName; visual = go.transform;
                    }
                    else Undo.RecordObject(visual, "Align Watcher visual");
                    visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                    var hitbox = marker.GetComponent<BoxCollider>();
                    if (hitbox == null) hitbox = Undo.AddComponent<BoxCollider>(marker.gameObject);
                    Undo.RecordObject(hitbox, "Configure Watcher hitbox");
                    // Three compact boxes, not the empty corners of a full-span rectangle.
                    var boxes = marker.GetComponents<BoxCollider>().ToList();
                    while (boxes.Count < 3) boxes.Add(Undo.AddComponent<BoxCollider>(marker.gameObject));
                    if (boxes.Count != 3) throw new InvalidOperationException("Unexpected Watcher collider count: " + marker.name);
                    var centers = new[] { new Vector3(0f, 0f, .2f), new Vector3(0f, 0f, .05f), new Vector3(0f, -.3f, -6.6f) };
                    var sizes = new[] { new Vector3(3.5f, 5f, 18f), new Vector3(25f, 4f, 3.8f), new Vector3(7.5f, 4f, 3.2f) };
                    for (int j = 0; j < boxes.Count; j++)
                    {
                        Undo.RecordObject(boxes[j], "Configure UAV hit zone");
                        boxes[j].center = centers[j]; boxes[j].size = sizes[j];
                        boxes[j].isTrigger = false; boxes[j].enabled = true;
                        EditorUtility.SetDirty(boxes[j]);
                    }
                    InstallRotor(visual);
                    var target = marker.GetComponent<WatcherTarget>();
                    if (target == null) target = Undo.AddComponent<WatcherTarget>(marker.gameObject);
                    Undo.RecordObject(target, "Configure Watcher patrol");
                    target.ConfigureForAuthoring(visual, new[] { 42f, 52f, 46f }[i], new[] { 32f, 151f, 265f }[i], i == 1);
                    var team = marker.GetComponent<AircraftTeam>();
                    if (team == null) team = Undo.AddComponent<AircraftTeam>(marker.gameObject);
                    Undo.RecordObject(team, "Set Watcher team"); team.SetSide(AircraftSide.Hostile);
                    targets[i] = target;
                    EditorUtility.SetDirty(target); EditorUtility.SetDirty(team); EditorUtility.SetDirty(hitbox);
                }
                var encounter = launch.GetComponent<Mission01ReconEncounter>();
                if (encounter == null) encounter = Undo.AddComponent<Mission01ReconEncounter>(launch.gameObject);
                Undo.RecordObject(encounter, "Bind recon encounter"); encounter.ConfigureForAuthoring(launch, player, targets, floor);
                EditorUtility.SetDirty(encounter); PrefabUtility.RecordPrefabInstancePropertyModifications(encounter);
                Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene); Selection.activeObject = recon.gameObject;
                Debug.Log("RE01: three Watchers on existing recon markers. Save the scene after review.");
            }
            catch (Exception ex)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                Debug.LogError("RE01: " + ex);
            }
        }

        private static void InstallRotor(Transform visual)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "RE01_Rotor.obj");
            var filters = model == null ? Array.Empty<MeshFilter>() : model.GetComponentsInChildren<MeshFilter>(true);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "RE01_Dark.mat");
            if (filters.Length != 1 || filters[0].sharedMesh == null || material == null)
                throw new InvalidOperationException("Missing rotor mesh/material. Generate Watcher OBJs first.");
            var rotor = visual.Find("PusherRotor");
            if (rotor == null)
            {
                var go = new GameObject("PusherRotor", typeof(MeshFilter), typeof(MeshRenderer));
                Undo.RegisterCreatedObjectUndo(go, "Create Watcher rotor");
                Undo.SetTransformParent(go.transform, visual, "Mount Watcher rotor");
                rotor = go.transform;
            }
            Undo.RecordObject(rotor, "Align Watcher rotor");
            rotor.localPosition = new Vector3(0f, .15f, -8.85f);
            rotor.localRotation = Quaternion.identity; rotor.localScale = Vector3.one;
            var filter = rotor.GetComponent<MeshFilter>(); var renderer = rotor.GetComponent<MeshRenderer>();
            Undo.RecordObject(filter, "Set rotor mesh"); Undo.RecordObject(renderer, "Set rotor material");
            filter.sharedMesh = filters[0].sharedMesh; renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }

        private static GameObject BuildVisual()
        {
            var meshes = Parts.Select(part => {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "RE01_" + part + ".obj");
                var filters = model == null ? Array.Empty<MeshFilter>() : model.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length != 1 || filters[0].sharedMesh == null) throw new InvalidOperationException("Missing Watcher mesh: " + part);
                return filters[0].sharedMesh;
            }).ToArray();
            string path = Art + "RE01_Watcher.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit missing.");
            var materials = new Material[Parts.Length];
            for (int i = 0; i < Parts.Length; i++)
            {
                string mp = Art + "RE01_" + Parts[i] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (m == null)
                {
                    m = new Material(shader); m.SetColor("_BaseColor", Palette[i]); m.SetFloat("_Metallic", 0); m.SetFloat("_Smoothness", 0);
                    m.SetFloat("_SpecularHighlights", 0); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    m.SetFloat("_EnvironmentReflections", 0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    AssetDatabase.CreateAsset(m, mp);
                }
                materials[i] = m;
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("RE01_Watcher"); SceneManager.MoveGameObjectToScene(root, preview);
                for (int i = 0; i < Parts.Length; i++)
                {
                    var go = new GameObject(Parts[i], typeof(MeshFilter), typeof(MeshRenderer)); SceneManager.MoveGameObjectToScene(go, preview); go.transform.SetParent(root.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = meshes[i];
                    var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = materials[i]; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new InvalidOperationException("Cannot save Watcher prefab.");
                AssetDatabase.SaveAssets(); return prefab;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
