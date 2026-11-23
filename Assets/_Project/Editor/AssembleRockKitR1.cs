#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // R1: very small authored rock kit study. No terrain generation, no scene cloning,
    // no combat logic changes. The currently open scene is used as the flight stage.
    public static class AssembleRockKitR1
    {
        private const string ArtFolder = "Assets/_Project/Art/Environment/RockKitR1";
        private const string GeneratedFolder = "Assets/_Project/Generated/RockKitR1";
        private const string ModelPath = ArtFolder + "/RockKitR1.fbx";
        private const string MarkerPath = ArtFolder + "/RockKitR1.ready.txt";
        private const string RootName = "R1 / Rock kit study";


        [MenuItem("Air Combat/Art/R1 Assemble Rock Kit Into Open Scene")]
        private static void FromMenu()
        {
            try { Debug.Log(Assemble()); }
            catch (Exception exception) { Debug.LogError("R1 rock kit: " + exception); }
        }

        public static string Assemble()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before assembling R1 rock kit.");

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Open a saved flight scene first, then run R1 assembly.");
            if (!File.Exists(MarkerPath) || File.ReadLines(MarkerPath).FirstOrDefault()?.Trim() != "ROCKKIT_R1_READY")
                throw new InvalidOperationException("Missing Blender export marker: " + MarkerPath);

            EnsureFolder(GeneratedFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportModel();

            var kitPrefab = Require<GameObject>(ModelPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No Lit shader found.");

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var player = GameObject.Find("Player");
            var anchor = player != null ? player.transform.position + new Vector3(0f, 0f, 120f) : new Vector3(0f, 0f, 0f);

            var root = new GameObject(RootName);
            root.transform.position = anchor;
            Undo.RegisterCreatedObjectUndo(root, "Create R1 rock kit root");

            var instance = PrefabUtility.InstantiatePrefab(kitPrefab) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate " + ModelPath);
            Undo.RegisterCreatedObjectUndo(instance, "Create R1 rock kit");
            instance.name = "RockKitR1";
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                string materialName;
                Color tint;
                ResolveMaterial(renderer.gameObject.name, out materialName, out tint);
                var material = EnsureMaterial(shader, materialName, tint);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    var collider = renderer.GetComponent<MeshCollider>();
                    if (collider == null) collider = renderer.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                }
            }

            // Spread the three modules so the pilot can compare their scale quickly.
            SetLocalPosition(instance.transform, "Cliff_A", new Vector3(-60f, 0f, 0f));
            SetLocalPosition(instance.transform, "Ridge_B", new Vector3(35f, 0f, 24f));
            SetLocalPosition(instance.transform, "Talus_C", new Vector3(88f, 0f, -18f));

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            return "R1 ready in open scene: three rock modules placed ahead of the player. Fly by Cliff_A first.";
        }


        private static void ResolveMaterial(string rendererName, out string materialName, out Color tint)
        {
            if (rendererName.IndexOf("Cliff_A", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                materialName = "R1_Cliff_A";
                tint = new Color(0.44f, 0.37f, 0.31f);
                return;
            }
            if (rendererName.IndexOf("Ridge_B", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                materialName = "R1_Ridge_B";
                tint = new Color(0.48f, 0.41f, 0.34f);
                return;
            }
            if (rendererName.IndexOf("Talus_C", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                materialName = "R1_Talus_C";
                tint = new Color(0.57f, 0.49f, 0.41f);
                return;
            }
            materialName = "R1_Rock";
            tint = new Color(0.45f, 0.39f, 0.33f);
        }

        private static void SetLocalPosition(Transform root, string childName, Vector3 localPosition)
        {
            var child = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == childName);
            if (child != null) child.localPosition = localPosition;
        }

        private static void ImportModel()
        {
            if (!File.Exists(ModelPath))
                throw new InvalidOperationException("Missing Blender export: " + ModelPath);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter
                ?? throw new InvalidOperationException("Unity did not import the FBX: " + ModelPath);
            bool changed = false;
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.importNormals != ModelImporterNormals.Import) { importer.importNormals = ModelImporterNormals.Import; changed = true; }
            if (importer.globalScale != 1f) { importer.globalScale = 1f; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        private static Material EnsureMaterial(Shader shader, string name, Color tint)
        {
            string path = GeneratedFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            if (slash < 0) throw new InvalidOperationException("Bad folder " + folder);
            EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }

        private static T Require<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing asset: " + path);
    }
}
#endif
