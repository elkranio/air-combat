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
    // AA-01a: only authored visual geometry. No weapons, movement, radar or damage yet.
    public static class InstallMantisAA01a
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string Art = "Assets/_Project/Art/Ground/AA02_Mantis";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Ground";
        private const string PrefabPath = PrefabFolder + "/AA02_Mantis.prefab";
        private const string Trial = "AA-01a / Mantis - VISUAL TRIAL";
        private static readonly (string Name, string Hex)[] Palette =
        {
            ("Armor", "#68745B"), ("Deck", "#929A77"), ("Track", "#343D3C"),
            ("Steel", "#4D5A57"), ("Dark", "#263331"), ("Accent", "#CE9850")
        };
        private static readonly string[] Sections = {
            "Body_Armor", "Body_Deck", "Body_Track", "Body_Steel", "Body_Dark", "Body_Accent",
            "Yaw_Armor", "Yaw_Deck", "Yaw_Steel", "Yaw_Dark", "Yaw_Accent",
            "Pitch_Steel", "Pitch_Dark"
        };

        [MenuItem("Air Combat/Art/AA-01a - Install Mantis visual trial")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded || scene.isDirty)
                throw new InvalidOperationException("Open and save ArcadeDesertE1 first.");
            var existing = scene.GetRootGameObjects().FirstOrDefault(x => x.name == Trial);
            if (existing != null) { Selection.activeGameObject = existing; return; }
            var session = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<E1MapFlightSession>(true)).FirstOrDefault();
            var player = session != null ? new SerializedObject(session).FindProperty("player").objectReferenceValue as JetAssembly : null;
            if (player == null) throw new InvalidOperationException("E1 player binding missing.");
            var terrain = Terrain.activeTerrains.FirstOrDefault(x => x.gameObject.scene == scene);
            if (terrain == null) throw new InvalidOperationException("Main-map terrain missing.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = BuildPrefab(scene);
            // Static near-spawn comparison in the approximate ground footprint of
            // the oblique camera. User may move the trial without rerunning authoring.
            Vector3 p = player.transform.position + player.transform.forward * 48f;
            p += Quaternion.Euler(0f, -45f, 0f) * Vector3.forward * 55f;
            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;
            p.x = Mathf.Clamp(p.x, origin.x + 15f, origin.x + size.x - 15f);
            p.z = Mathf.Clamp(p.z, origin.z + 15f, origin.z + size.z - 15f);
            p.y = terrain.SampleHeight(p) + origin.y;
            // If the plateau or a prepared lot is higher, sit on its visible surface.
            if (Physics.Raycast(new Vector3(p.x, origin.y + size.y + 200f, p.z), Vector3.down,
                out RaycastHit surface, size.y + 210f, ~0, QueryTriggerInteraction.Ignore))
                p.y = Mathf.Max(p.y, surface.point.y);
            var trial = new GameObject(Trial);
            SceneManager.MoveGameObjectToScene(trial, scene);
            trial.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 25f, 0f));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            visual.transform.SetParent(trial.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            Selection.activeGameObject = trial;
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("AA-01a: Mantis visual trial installed. Static model only; NURS and air combat unchanged.", trial);
        }

        private static GameObject BuildPrefab(Scene scene)
        {
            var models = Sections.Select(x => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/" + x + ".obj")).ToArray();
            for (int i = 0; i < models.Length; i++)
                if (models[i] == null || models[i].GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    throw new InvalidOperationException("Missing imported mesh: " + Sections[i]);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            var mats = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var swatch in Palette)
            {
                string path = Art + "/Mantis_" + swatch.Name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    if (File.Exists(path)) throw new InvalidOperationException("Material not imported: " + path);
                    ColorUtility.TryParseHtmlString(swatch.Hex, out Color color);
                    mat = new Material(shader) { name = "Mantis / " + swatch.Name };
                    mat.SetColor("_BaseColor", color);
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", .06f);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mats.Add(swatch.Name, mat);
            }
            if (!Directory.Exists(PrefabFolder)) { Directory.CreateDirectory(PrefabFolder); AssetDatabase.Refresh(); }
            var root = new GameObject("AA-02 Mantis");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var body = Socket(root.transform, "Body", Vector3.zero);
                var yaw = Socket(root.transform, "TurretYaw", new Vector3(0f, 2.65f, -.25f));
                var pitch = Socket(yaw, "GunPitch", new Vector3(0f, .83f, 1.15f));
                for (int i = 0; i < Sections.Length; i++)
                {
                    string[] bits = Sections[i].Split('_');
                    Transform parent = bits[0] == "Body" ? body : bits[0] == "Yaw" ? yaw : pitch;
                    var part = (GameObject)PrefabUtility.InstantiatePrefab(models[i], scene);
                    part.name = Sections[i];
                    part.transform.SetParent(parent, false);
                    part.transform.localPosition = Vector3.zero;
                    part.transform.localRotation = Quaternion.identity;
                    part.transform.localScale = Vector3.one;
                    foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        renderer.sharedMaterials = Enumerable.Repeat(mats[bits[1]], Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                }
                Socket(pitch, "Muzzle_L", new Vector3(-.98f, 0f, 5.12f));
                Socket(pitch, "Muzzle_R", new Vector3(.98f, 0f, 5.12f));
                // Visual-only initial pose. Future tracking will set this pivot.
                pitch.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Mantis prefab save failed.");
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform Socket(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }
    }
}
#endif
