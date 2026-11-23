#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // B1f: automatic, one-shot OBJ -> URP materials -> prefab -> authored scene.
    // The Blender generator owns mesh geometry; Unity never rebuilds the meshes.
    // Only runs when the main map is open and every open scene is clean.
    // B1e and all other authored objects are never regenerated.
    [InitializeOnLoad]
    public static class InstallArchHangarB1F
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string Art = "Assets/_Project/Art/Buildings/Base/ArchHangar_B1f";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/Base/B1f_ArchHangar.prefab";
        private const string TrialName = "B1 / B1f Arch hangar - STYLE TRIAL (toggle)";

        private static readonly (string Section, string MaterialName, string Hex)[] Parts =
        {
            ("Arch_Roof",        "Roof",      "#506E76"),
            ("Arch_Walls",       "Structure", "#9EA296"),
            ("Arch_FrontPortal", "Structure", "#9EA296"),
            ("Arch_Ribs",        "Metal",     "#38555E"),
            ("Arch_SideAnnex",   "Structure", "#9EA296"),
            ("Arch_DarkDetails", "Dark",      "#273B43"),
            ("Arch_RoofVents",   "Metal",     "#38555E"),
            ("Arch_Accent",      "Accent",    "#C17A4D")
        };

        private static double nextPoll;
        private static bool reportedDirty;

        static InstallArchHangarB1F()
        {
            // No menu, no user click: the editor checks after imports/compilation.
            EditorApplication.update += AutoInstallWhenSafe;
        }

        private static void AutoInstallWhenSafe()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return; // don't switch scenes behind the user
            if (scene.GetRootGameObjects().Any(go => go.name == TrialName))
            {
                EditorApplication.update -= AutoInstallWhenSafe;
                return; // installed and hand-editable; do not overwrite on recompile
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            if (Parts.Any(p => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/" + p.Section + ".obj") == null))
                return; // wait for Unity's model importer
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                if (!reportedDirty)
                {
                    reportedDirty = true;
                    Debug.Log("B1f / Blender -> Unity: waiting for open scenes to be saved; no changes made.");
                }
                return;
            }
            EditorApplication.update -= AutoInstallWhenSafe;
            try { Debug.Log(Install()); }
            catch (Exception e) { Debug.LogError("B1f automatic installation FAILED (no existing content was intentionally replaced): " + e); }
        }

        [MenuItem("Air Combat/Art/B1f - Verify/Install (fallback)")]
        private static void ManualInstall()
        {
            try { Debug.Log(Install()); }
            catch (Exception e) { Debug.LogError("B1f verify/install: " + e); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before B1f installation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard open scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("The authored main map is missing.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");

            // Resolve and validate ALL models before touching scene/assets.
            var sources = Parts.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/" + p.Section + ".obj"))
                .ToArray();
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] == null || sources[i].GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    throw new InvalidOperationException("Missing imported OBJ: " + Parts[i].Section);
            // Idempotent: can reuse an existing generated prefab/materials after a
            // local reset, but never overwrite manually edited prefab assets.
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open ArcadeDesertE1 before installing B1f.");
            var roots = scene.GetRootGameObjects();
            if (roots.Any(x => x.name == TrialName))
                return "B1f already present; skipped, no duplication.";
            var airfield = roots.SingleOrDefault(x => x.name.StartsWith("B1 / Airfield"));
            if (airfield == null) throw new InvalidOperationException("B1e airfield missing; don't regenerate it.");
            var original = airfield.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.name.StartsWith("Hangar / maintenance"));
            if (original == null)
                throw new InvalidOperationException("Cannot find original B1e hangar for safe side-by-side comparison.");
            var site = original.position + original.rotation * new Vector3(0, 0, -70f);
            if (Mathf.Abs(site.x) > 850f || Mathf.Abs(site.z) > 850f)
                throw new InvalidOperationException("Test hangar would be too close to map boundary.");

            var materialNames = Parts.Select(p => p.MaterialName).Distinct().ToArray();
            var mats = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (string name in materialNames)
            {
                var colorHex = Parts.First(p => p.MaterialName == name).Hex;
                if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
                    throw new InvalidOperationException("Invalid B1f color: " + colorHex);
                string matPath = Art + "/B1f_" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null)
                {
                    if (File.Exists(matPath))
                        throw new InvalidOperationException("Unimported material: " + matPath);
                    material = new Material(shader) { name = "B1f / " + name };
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", 0f);
                    material.enableInstancing = true;
                    AssetDatabase.CreateAsset(material, matPath);
                }
                mats.Add(name, material);
            }

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (saved == null)
            {
                if (File.Exists(Prefab))
                    throw new InvalidOperationException("Unimported prefab: " + Prefab);
                var master = new GameObject("B1f / Arch hangar - editable model sections");
                SceneManager.MoveGameObjectToScene(master, scene);
                try
                {
                    for (int i = 0; i < Parts.Length; i++)
                    {
                        var part = (GameObject)PrefabUtility.InstantiatePrefab(sources[i], scene);
                        part.name = Parts[i].Section;
                        part.transform.SetParent(master.transform, false);
                        part.transform.localPosition = Vector3.zero;
                        part.transform.localRotation = Quaternion.identity;
                        part.transform.localScale = Vector3.one;
                        foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                            renderer.sharedMaterials = Enumerable.Repeat(mats[Parts[i].MaterialName], count).ToArray();
                            renderer.shadowCastingMode = ShadowCastingMode.On;
                            renderer.receiveShadows = true;
                        }
                    }
                    // Approximate solid walls only; open entrance is not blocked.
                    var physics = new GameObject("Collision - walls only (front is open)");
                    physics.transform.SetParent(master.transform, false);
                    Box(physics, new Vector3(0f, 2.4f, -15.6f), new Vector3(28.4f, 4.8f, 2.2f));
                    Box(physics, new Vector3(0f, 2.4f, 15.6f), new Vector3(28.4f, 4.8f, 2.2f));
                    Box(physics, new Vector3(14f, 2.5f, 0f), new Vector3(1.2f, 5f, 31.7f));
                    Box(physics, new Vector3(-14.8f, 3.1f, -15.8f), new Vector3(2.2f, 6.3f, 2.8f));
                    Box(physics, new Vector3(-14.8f, 3.1f, 15.8f), new Vector3(2.2f, 6.3f, 2.8f));
                    Box(physics, new Vector3(7.7f, 2.8f, 19.5f), new Vector3(12.4f, 5.6f, 8.1f));
                    saved = PrefabUtility.SaveAsPrefabAsset(master, Prefab);
                }
                finally
                {
                    Object.DestroyImmediate(master);
                }
                if (saved == null) throw new InvalidOperationException("Unable to save B1f prefab.");
            }

            var trial = new GameObject(TrialName);
            SceneManager.MoveGameObjectToScene(trial, scene);
            trial.transform.position = site;
            trial.transform.rotation = original.rotation;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(saved, scene);
            instance.name = "New faceted arch hangar - compare with old B1e";
            instance.transform.SetParent(trial.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Unable to save B1f scene.");
            return "B1f Blender pipeline GREEN at import/installation stage: generated matte URP " +
                   "materials + prefab + scene instance. Old B1e is untouched. " +
                   "Check visuals in Play Mode, then commit generated assets if accepted.";
        }

        private static void Box(GameObject target, Vector3 center, Vector3 size)
        {
            var collider = target.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }
    }
}
#endif
