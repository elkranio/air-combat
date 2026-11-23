#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    // B1c: deliberately separate HQ, entrance, planters, plaza and flag prefabs.
    // One-time scene placement, no terrain rebuilding and no shared combat edits.
    public static class InstallHeadquartersB1C
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string ModelFolder = "Assets/_Project/Art/Buildings/Base/HQ_B1c";
        private const string BarracksFolder = "Assets/_Project/Art/Buildings/Base/Barracks_B1a";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Environment/Base";
        private const string SiteName = "B1 / HQ and courtyard";
        private const float DefaultAltitude = 78f;
        // Adjacent to the existing barracks row at (-485, -270), not at the plateau.
        // Entrance faces -Z, toward the fighter approach (-362, -393).
        private static readonly Vector3 SiteCenter = new Vector3(-422f, 0f, -320f);

        private static readonly string[] MeshNames =
        {
            "Body", "Roof", "Openings", "Trim", "Accent",
            "Entry_Concrete", "Entry_Dark", "Entry_Accent", "Plaza",
            "Planter_Concrete", "Planter_Soil", "Planter_Green",
            "Flag_Pole", "Flag_Cloth"
        };

        private static readonly (string Key, string Hex)[] ExtraColors =
        {
            ("Accent", "#C48150"),      // restrained command/service marking
            ("Concrete", "#9BA39F"),    // cool grey, separate from muted sand
            ("Soil", "#6C6458"),
            ("Green", "#627564")
        };

        [MenuItem("Air Combat/Art/B1c - Install HQ Courtyard + Y78")]
        private static void RunFromMenu()
        {
            try { Debug.Log(Install()); }
            catch (Exception ex) { Debug.LogError("B1c HQ installation failed: " + ex); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before B1c installation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save/discard open scene edits first. B1c opens the persistent main map.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("The authored E1 scene is missing; never regenerate it.");

            var rendererShader = Shader.Find("Universal Render Pipeline/Lit");
            if (rendererShader == null)
                throw new InvalidOperationException("B1c requires URP Lit.");

            // Validate inputs and output collisions BEFORE writing any assets.
            var source = new Dictionary<string, GameObject>();
            foreach (string name in MeshNames)
            {
                string path = ModelFolder + "/HQ_B1c_" + name + ".obj";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || model.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    throw new InvalidOperationException("Missing/unimported HQ OBJ: " + path);
                source.Add(name, model);
            }
            foreach (var color in ExtraColors)
            {
                string path = ModelFolder + "/B1c_" + color.Key + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null || File.Exists(path))
                    throw new InvalidOperationException("Existing authored material must not be overwritten: " + path);
            }
            foreach (string name in new[] { "Command", "Entrance", "Plaza", "Planter", "Flag" })
            {
                string path = PrefabFolder + "/HQ_B1c_" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null || File.Exists(path))
                    throw new InvalidOperationException("HQ prefab already exists; do not rerun installer: " + path);
            }
            var materials = new Dictionary<string, Material>();
            foreach (var existing in new[] { "Walls", "Roof", "Openings", "Trim" })
            {
                string path = BarracksFolder + "/B1a_" + existing + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                    throw new InvalidOperationException("Missing accepted barracks palette material: " + path);
                materials.Add(existing, mat);
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.name == SiteName))
                throw new InvalidOperationException("B1c already placed. Edit the HQ prefabs and scene objects directly.");
            var barracks = roots.SingleOrDefault(r => r.name == "B1 / Base sector - barracks trial");
            var plateau = roots.SingleOrDefault(r => r.name.StartsWith("E1C / Main plateau"));
            var player = roots.SingleOrDefault(r => r.name.StartsWith("E1 / PlayerJet"));
            var camera = roots.SingleOrDefault(r => r.name == "E1 / Main Camera");
            var session = roots.Select(r => r.GetComponent<E1MapFlightSession>()).FirstOrDefault(s => s != null);
            if (barracks == null || barracks.transform.childCount < 3 ||
                plateau == null || player == null || camera == null || session == null ||
                player.GetComponent<JetAssembly>() == null || camera.GetComponent<ArcadeCameraFollow>() == null)
                throw new InvalidOperationException("Expected accepted B1a/E1 scene composition not found. Review the scene before B1c.");
            if (Vector2.Distance(new Vector2(plateau.transform.position.x, plateau.transform.position.z),
                    new Vector2(SiteCenter.x, SiteCenter.z)) < 380f)
                throw new InvalidOperationException("HQ too close to the existing plateau; site must stay separate.");
            float oldAltitude = player.transform.position.y;
            if (Mathf.Abs(oldAltitude - 150f) > .05f && Mathf.Abs(oldAltitude - DefaultAltitude) > .05f)
                throw new InvalidOperationException("Unexpected player start height. Preserve manually tuned spawn rather than overwrite it.");

            EnsureFolder(PrefabFolder);
            foreach (var c in ExtraColors)
            {
                if (!ColorUtility.TryParseHtmlString(c.Hex, out var color))
                    throw new InvalidOperationException("Invalid palette entry " + c.Key);
                var material = new Material(rendererShader) { name = "B1c / " + c.Key };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0f);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, ModelFolder + "/B1c_" + c.Key + ".mat");
                materials.Add(c.Key, material);
            }

            var command = MakePrefab(scene, "Command", source, materials,
                new[] { ("Body", "Walls"), ("Roof", "Roof"), ("Openings", "Openings"),
                        ("Trim", "Trim"), ("Accent", "Accent") },
                new Vector3(0f, 4.6f, 0f), new Vector3(29.6f, 9.5f, 19.5f));
            var entrance = MakePrefab(scene, "Entrance", source, materials,
                new[] { ("Entry_Concrete", "Concrete"), ("Entry_Dark", "Roof"),
                        ("Entry_Accent", "Accent") },
                new Vector3(0f, 2.55f, -3.93f), new Vector3(13.2f, 5.2f, 8.9f));
            var plaza = MakePrefab(scene, "Plaza", source, materials,
                new[] { ("Plaza", "Concrete") },
                new Vector3(0f, 0f, -27f), new Vector3(38f, .17f, 22f));
            var planter = MakePrefab(scene, "Planter", source, materials,
                new[] { ("Planter_Concrete", "Concrete"), ("Planter_Soil", "Soil"),
                        ("Planter_Green", "Green") },
                new Vector3(0f, .6f, 0f), new Vector3(7.2f, 1.3f, 2.9f));
            var flag = MakePrefab(scene, "Flag", source, materials,
                new[] { ("Flag_Pole", "Roof"), ("Flag_Cloth", "Accent") },
                new Vector3(0f, 6.2f, 0f), new Vector3(4.1f, 12.5f, 1.8f));

            var site = new GameObject(SiteName);
            SceneManager.MoveGameObjectToScene(site, scene);
            site.transform.position = SiteCenter;
            Place(scene, site.transform, command, "01 / Command building", Vector3.zero);
            Place(scene, site.transform, entrance, "02 / Entrance + steps", new Vector3(0f, 0f, -9f));
            Place(scene, site.transform, plaza, "03 / Front courtyard paving", Vector3.zero);
            Place(scene, site.transform, planter, "04 / Planter - left", new Vector3(-11.5f, 0f, -22.5f));
            Place(scene, site.transform, planter, "05 / Planter - right", new Vector3(11.5f, 0f, -22.5f));
            Place(scene, site.transform, flag, "06 / Flagpole", new Vector3(-8.5f, .0f, -32f));

            // Keep existing X/Z spawn and forward heading. Lower aircraft and
            // camera by exactly the same amount: preserves approved framing.
            var p = player.transform.position;
            player.transform.position = new Vector3(p.x, DefaultAltitude, p.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            var cpos = camera.transform.position;
            camera.transform.position = new Vector3(cpos.x, cpos.y + DefaultAltitude - oldAltitude, cpos.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Couldn't save the authored main map.");
            Selection.activeGameObject = site;
            return "B1c HQ installed: five independent prefabs, 6 courtyard objects, B1 barracks/plateau intact. " +
                   "Flight spawn is now Y78 (also R reset); temporary 8/9 height tests remain. " +
                   "Commit generated materials/prefabs and their .meta files with the revised main scene.";
        }

        private static GameObject MakePrefab(Scene scene, string name,
            Dictionary<string, GameObject> models, Dictionary<string, Material> materials,
            (string Model, string Material)[] sections, Vector3 colliderCenter, Vector3 colliderSize)
        {
            var root = new GameObject("HQ_B1c_" + name);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var section in sections)
            {
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(models[section.Model], scene);
                imported.name = section.Model;
                imported.transform.SetParent(root.transform, false);
                imported.transform.localPosition = Vector3.zero;
                imported.transform.localRotation = Quaternion.identity;
                imported.transform.localScale = Vector3.one;
                foreach (var renderer in imported.GetComponentsInChildren<MeshRenderer>(true))
                {
                    int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                    renderer.sharedMaterials = Enumerable.Repeat(materials[section.Material], count).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = colliderCenter;
            collider.size = colliderSize;
            var path = PrefabFolder + "/HQ_B1c_" + name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (saved == null) throw new InvalidOperationException("Couldn't create prefab: " + path);
            return saved;
        }

        private static void Place(Scene scene, Transform site, GameObject prefab, string name, Vector3 local)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            instance.transform.SetParent(site, false);
            instance.transform.localPosition = local;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            foreach (string name in folder.Substring("Assets/".Length).Split('/'))
            {
                string next = current + "/" + name;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, name);
                current = next;
            }
        }
    }
}
#endif
