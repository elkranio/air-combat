#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // D1 / purely visual EF-01 enemy fighter. No gameplay wiring yet.
    // Sections are authored by Tools/Blender/build_enemy_tiger_d1.py.
    // On safe editor import: OBJ -> URP mats -> JetChassis prefab -> parked A/B trial.
    [InitializeOnLoad]
    public static class InstallEnemyTigerD1
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string Art = "Assets/_Project/Art/Aircraft/EF01_Tiger_D1";
        private const string PrefabPath = "Assets/_Project/Prefabs/Aircraft/EF01_Tiger_D1_Chassis.prefab";
        private const string TrialName = "D1 / EF-01 Tiger - VISUAL TRIAL (toggle)";

        private static readonly (string Mesh, string Mat, string Hex)[] Sections =
        {
            ("Tiger_Fuselage",      "Fuselage", "#5D6E6B"),
            ("Tiger_MainWings",     "Upper",    "#798B85"),
            ("Tiger_TailSurfaces",  "Upper",    "#798B85"),
            ("Tiger_TwinEngines",   "Underside","#415557"),
            ("Tiger_Intakes",       "Steel",    "#32464B"),
            ("Tiger_IntakeShadows", "Steel",    "#32464B"),
            ("Tiger_Exhaust",       "Steel",    "#32464B"),
            ("Tiger_Canopy",        "Glass",    "#134156"),
            ("Tiger_Trim",          "Steel",    "#32464B"),
            ("Tiger_Identification","Accent", "#CD543B"),
        };

        private static double nextPoll;
        private static bool notifiedDirty;
        private static bool errorLogged;

        static InstallEnemyTigerD1()
        {
            // No installation menu required. Wait for model import and a clean open scene.
            EditorApplication.update += AutoInstall;
        }

        private static void AutoInstall()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (scene.GetRootGameObjects().Any(x => x.name == TrialName))
            {
                EditorApplication.update -= AutoInstall;
                return;
            }
            if (Sections.Any(x => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/" + x.Mesh + ".obj") == null))
                return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                if (!notifiedDirty)
                {
                    Debug.Log("D1 Tiger: waiting for open scenes to be saved; no edits made.");
                    notifiedDirty = true;
                }
                return;
            }
            try
            {
                Debug.Log(Install());
                EditorApplication.update -= AutoInstall;
            }
            catch (Exception ex)
            {
                if (!errorLogged) Debug.LogError("D1 Tiger auto-install failed: " + ex);
                errorLogged = true; // prevent console spam; fallback menu remains available
                EditorApplication.update -= AutoInstall;
            }
        }

        [MenuItem("Air Combat/Art/D1 - Verify/Install Tiger (fallback)")]
        private static void VerifyOrInstall()
        {
            try { Debug.Log(Install()); }
            catch (Exception ex) { Debug.LogError("D1 Tiger install: " + ex); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard all open scene changes first.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open ArcadeDesertE1 to compare Tiger and Kestrel.");
            if (scene.GetRootGameObjects().Any(x => x.name == TrialName))
                return "D1 Tiger already placed; skipped to preserve manual edits.";

            var models = Sections.Select(x => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/" + x.Mesh + ".obj")).ToArray();
            for (int i = 0; i < models.Length; i++)
                if (models[i] == null || models[i].GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    throw new InvalidOperationException("Missing imported Tiger mesh: " + Sections[i].Mesh);

            var airfield = scene.GetRootGameObjects().FirstOrDefault(x => x.name.StartsWith("B1 / Airfield"));
            if (airfield == null)
                throw new InvalidOperationException("B1 airfield not found; D1 does not create or move it.");
            var parked = airfield.GetComponentsInChildren<Transform>(true)
                .Where(x => x.name.StartsWith("Parked Kestrel "))
                .OrderBy(x => x.name).ToArray();
            if (parked.Length < 2)
                throw new InvalidOperationException("Two parked B1e Kestrels not found; cannot safely place comparison.");
            Vector3 position = (parked[0].position + parked[1].position) * .5f;
            Quaternion rotation = parked[0].rotation;
            if (Mathf.Abs(position.x) > 850f || Mathf.Abs(position.z) > 850f)
                throw new InvalidOperationException("Tiger trial is too close to the map boundary.");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");
            var mats = new Dictionary<string, Material>();
            foreach (var part in Sections)
            {
                if (mats.ContainsKey(part.Mat)) continue;
                var path = Art + "/D1_" + part.Mat + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    if (File.Exists(path)) throw new InvalidOperationException("Material not imported: " + path);
                    if (!ColorUtility.TryParseHtmlString(part.Hex, out var color))
                        throw new InvalidOperationException("Bad colour: " + part.Hex);
                    m = new Material(shader) { name = "D1 Tiger / " + part.Mat };
                    m.SetColor("_BaseColor", color);
                    m.SetFloat("_Metallic", 0f);
                    m.SetFloat("_Smoothness", 0f);
                    m.enableInstancing = true;
                    AssetDatabase.CreateAsset(m, path);
                }
                mats.Add(part.Mat, m);
            }

            GameObject chassisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (chassisPrefab == null)
            {
                if (File.Exists(PrefabPath)) throw new InvalidOperationException("Prefab not imported: " + PrefabPath);
                var root = new GameObject("EF-01 Tiger / Visual Chassis - NO AI");
                SceneManager.MoveGameObjectToScene(root, scene);
                try
                {
                    for (int i = 0; i < Sections.Length; i++)
                    {
                        var part = (GameObject)PrefabUtility.InstantiatePrefab(models[i], scene);
                        part.name = Sections[i].Mesh;
                        part.transform.SetParent(root.transform, false);
                        part.transform.localPosition = Vector3.zero;
                        part.transform.localRotation = Quaternion.identity;
                        part.transform.localScale = Vector3.one;
                        foreach (var rend in part.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            int count = Mathf.Max(1, rend.sharedMaterials.Length);
                            rend.sharedMaterials = Enumerable.Repeat(mats[Sections[i].Mat], count).ToArray();
                            rend.shadowCastingMode = ShadowCastingMode.On;
                            rend.receiveShadows = true;
                        }
                    }

                    // Scene-neutral mount points for later D3/D4 EnemyJet integration.
                    // No Gauss weapon is equipped; the socket exists for shared JetChassis contract.
                    var gunsL = Socket(root.transform, "Gun L", new Vector3(-.58f,-.05f,5.8f));
                    var gunsR = Socket(root.transform, "Gun R", new Vector3(.58f,-.05f,5.8f));
                    var gauss = Socket(root.transform, "Unused Gauss", new Vector3(0f,-.03f,6.65f));
                    var missile = Socket(root.transform, "Missile", new Vector3(0f,-.45f,-.15f));
                    var engineL = Socket(root.transform, "Engine L", new Vector3(-.98f,-.19f,-6.83f));
                    var engineR = Socket(root.transform, "Engine R", new Vector3(.98f,-.19f,-6.83f));
                    var chassis = root.AddComponent<JetChassis>();
                    chassis.SetAuthoringSockets(gunsL,gunsR,gauss,missile,engineL,engineR);
                    if (!chassis.HasRequiredSockets) throw new InvalidOperationException("Tiger sockets incomplete.");
                    chassisPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { Object.DestroyImmediate(root); }
                if (chassisPrefab == null) throw new InvalidOperationException("Could not save Tiger chassis prefab.");
            }

            // Static comparison only: won't chase, shoot or affect PlayerJet.
            var trial = new GameObject(TrialName);
            SceneManager.MoveGameObjectToScene(trial, scene);
            trial.transform.SetPositionAndRotation(position,rotation);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(chassisPrefab, scene);
            visual.name = "EF-01 Tiger / FIRST WAVE - static scale comparison";
            visual.transform.SetParent(trial.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene,ScenePath))
                throw new InvalidOperationException("Couldn't save D1 trial to main scene.");
            return "D1 Tiger ready: matte URP materials, editable JetChassis prefab and static comparison " +
                "between parked Kestrels. Original Kestrel, EnemyJet, labs, flight, camera and combat unchanged.";
        }

        private static Transform Socket(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition = position;
            return go.transform;
        }
    }
}
#endif
