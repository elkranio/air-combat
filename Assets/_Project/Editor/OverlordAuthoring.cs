using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class OverlordAuthoring
    {
        private const string Art = "Assets/_Project/Art/Aircraft/Dominion/AE01_Overlord/";
        private const string PreviewName = "AE01 / Overlord visual trial (remove before save)";
        private static readonly string[] Parts = { "Hull", "Upper", "Dark", "Gold", "Glass", "Dome", "DomeBand", "GlowL", "GlowR" };
        private static readonly Color32[] Palette = {
            new Color32(82,95,82,255), new Color32(111,124,110,255), new Color32(43,53,57,255),
            new Color32(210,174,62,255), new Color32(48,83,94,255), new Color32(105,119,110,255),
            new Color32(49,62,64,255), new Color32(47,124,141,255), new Color32(47,124,141,255)
        };

        [MenuItem("Air Combat/Art/Build AE01 Overlord Prefab v0")]
        private static void BuildOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Selection.activeObject = BuildVisual(); }
            catch (Exception ex) { Debug.LogError("AE01: " + ex); }
        }

        [MenuItem("Air Combat/Art/Preview AE01 Overlord v0")]
        private static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int group = -1;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.name != "Mission01_FirstSortie")
                    throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var roots = scene.GetRootGameObjects();
                var launch = roots.SelectMany(g => g.GetComponentsInChildren<Mission01LaunchSequence>(true)).Single();
                var config = new SerializedObject(launch);
                var player = config.FindProperty("flight").objectReferenceValue as FlightController;
                if (player == null) throw new InvalidOperationException("Missing mission player.");
                float altitude = config.FindProperty("cruiseAltitude").floatValue;
                float distance = player.CruiseSpeed * (.5f * Mathf.Max(.1f, config.FindProperty("rollSeconds").floatValue)
                    + Mathf.Max(.1f, config.FindProperty("climbSeconds").floatValue));
                Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 p = player.transform.position + forward * (distance + 40f) + right * 22f;
                p.y = altitude;
                var prefab = BuildVisual();
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Preview AE01 Overlord");
                foreach (var root in roots) if (root.name == PreviewName) Undo.DestroyObjectImmediate(root);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Preview Overlord");
                instance.name = PreviewName;
                instance.transform.SetPositionAndRotation(p, Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 22f, 0f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = instance;
                Debug.Log("AE01: visual trial near debug takeoff endpoint. Frame selected (F) or Play with debug air start. Remove trial before saving mission.");
            }
            catch (Exception ex)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                Debug.LogError("AE01: " + ex);
            }
        }

        [MenuItem("Air Combat/Art/Remove AE01 Overlord Preview")]
        private static void RemovePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == PreviewName) { Undo.DestroyObjectImmediate(root); EditorSceneManager.MarkSceneDirty(scene); }
        }

        private static GameObject BuildVisual()
        {
            var meshes = Parts.Select(part => {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "AE01_" + part + ".obj");
                var filters = model == null ? Array.Empty<MeshFilter>() : model.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length != 1 || filters[0].sharedMesh == null)
                    throw new InvalidOperationException("Missing Overlord mesh: " + part);
                return filters[0].sharedMesh;
            }).ToArray();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit missing.");
            var materials = new Material[Parts.Length];
            for (int i = 0; i < Parts.Length; i++)
            {
                string path = Art + "AE01_" + Parts[i] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    m.SetColor("_BaseColor", Palette[i]);
                    m.SetFloat("_Metallic", 0); m.SetFloat("_Smoothness", .05f);
                    m.SetFloat("_SpecularHighlights", 0); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    m.SetFloat("_EnvironmentReflections", 0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    if (Parts[i].StartsWith("Glow", StringComparison.Ordinal))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", new Color(.035f, .12f, .16f));
                    }
                    AssetDatabase.CreateAsset(m, path);
                }
                materials[i] = m;
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("AE01_Overlord"); SceneManager.MoveGameObjectToScene(root, preview);
                var pivot = new GameObject("RadarPivot"); SceneManager.MoveGameObjectToScene(pivot, preview);
                pivot.transform.SetParent(root.transform, false); pivot.transform.localPosition = new Vector3(0f, 4.6f, -1.35f);
                for (int i = 0; i < Parts.Length; i++)
                {
                    var go = new GameObject(Parts[i], typeof(MeshFilter), typeof(MeshRenderer));
                    SceneManager.MoveGameObjectToScene(go, preview);
                    go.transform.SetParent(Parts[i].StartsWith("Dome", StringComparison.Ordinal) ? pivot.transform : root.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = meshes[i];
                    var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = materials[i];
                    r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
                }
                var visual = root.AddComponent<OverlordVisual>();
                var serialized = new SerializedObject(visual);
                serialized.FindProperty("radarPivot").objectReferenceValue = pivot.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // Empty transforms for later charge/escape VFX (forward = exhaust direction).
                foreach (int side in new[] {-1, 1})
                {
                    var socket = new GameObject(side < 0 ? "EscapeExhaustL" : "EscapeExhaustR");
                    SceneManager.MoveGameObjectToScene(socket, preview); socket.transform.SetParent(root.transform, false);
                    socket.transform.localPosition = new Vector3(side * 5.15f, .28f, -11.72f);
                    socket.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Art + "AE01_Overlord.prefab");
                if (prefab == null) throw new InvalidOperationException("Cannot save Overlord prefab.");
                AssetDatabase.SaveAssets(); return prefab;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
