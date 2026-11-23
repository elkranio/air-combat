#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Small, disposable comparison scene using unmodified BrokenVector source prefabs.
    // Authoring isolation: never save changes into FrontierStrata or combat scenes.
    public static class BuildCliffPackReferenceStudy
    {
        private const string Source = "Assets/_Project/Scenes/FrontierStrata.unity";
        private const string Destination = "Assets/_Project/Scenes/FrontierCliffReference.unity";
        private const string Prefabs = "Assets/BrokenVector/LowPolyCliffPack/Prefabs/";
        private const string Textures = "Assets/BrokenVector/LowPolyCliffPack/Textures/";
        private const string Materials = "Assets/_Project/Generated/CliffReference/";
        private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
        private const string RootName = "Cliff reference / BrokenVector (study only)";

        // A deliberately small set of existing prefabs. They include MeshColliders.
        // Note: there is no Straight 3 PREFAB in the imported package.
        private static readonly string[] Left =
        {
            "Clifftile Straight 1", "Clifftile Straight 2",
            "Clifftile Diagonal", "Clifftile Pass 1"
        };
        private static readonly string[] Right =
        {
            "Clifftile Straight 2", "Clifftile Straight 1",
            "Clifftile Connector 1", "Clifftile Concave"
        };

        [MenuItem("Air Combat/Art/Build Cliff Pack Reference Study")]
        private static void BuildFromMenu()
        {
            try { Debug.Log(Build()); }
            catch (Exception e) { Debug.LogError("Cliff pack reference: " + e); }
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating the reference scene.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard open scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null || File.Exists(Destination))
                throw new InvalidOperationException("Reference scene already exists; do not overwrite it: " + Destination);

            Require<SceneAsset>(Source);
            Require<UniversalRenderPipelineAsset>(Pipeline);
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP Lit shader not available.");
            foreach (var name in Left.Concat(Right).Distinct()) Require<GameObject>(PrefabPath(name));
            var yellow = Require<Texture2D>(Textures + "Colorscheme Yellow.png");
            var grey = Require<Texture2D>(Textures + "Colorscheme Grey.png");
            MakeFolder(Materials.TrimEnd('/'));
            var materialWarm = MakeMaterial(shader, "Reference_Warm", new Color(.91f, .87f, .80f), yellow);
            var materialGrey = MakeMaterial(shader, "Reference_Grey", new Color(.91f, .90f, .88f), grey);
            var materialGround = MakeMaterial(shader, "Reference_Ground", new Color(.58f, .49f, .39f), null);
            AssetDatabase.SaveAssets();

            // Save as a NEW scene. Never mutate V17; it also contains the old R1 test.
            var sourceScene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(sourceScene, Destination, true))
                throw new InvalidOperationException("Cannot write the new scene. Check Windows folder permissions.");
            var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
            var player = scene.GetRootGameObjects().Single(x => x.name == "Player");
            var cameraRoot = scene.GetRootGameObjects().Single(x => x.name == "Main Camera");
            var sunRoot = scene.GetRootGameObjects().Single(x => x.name == "Directional Light");
            var camera = cameraRoot.GetComponent<Camera>();
            var sun = sunRoot.GetComponent<Light>();
            var flight = player.GetComponent<FlightController>();
            var boost = player.GetComponent<Afterburner>();
            if (camera == null || sun == null || flight == null || boost == null)
                throw new InvalidOperationException("V17 camera / aircraft / light contract has changed.");
            foreach (var root in scene.GetRootGameObjects())
                if (root != player && root != cameraRoot && root != sunRoot)
                    Object.DestroyImmediate(root);

            var study = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(study, scene);
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Neutral ground (no generated terrain)";
            plane.transform.SetParent(study.transform, false);
            plane.transform.position = new Vector3(0, -1.5f, 0);
            plane.transform.localScale = new Vector3(120, 1, 120);
            plane.GetComponent<Renderer>().sharedMaterial = materialGround;

            // Scale from actual imported renderer bounds; Blender's DAE transforms
            // cannot be assumed to use the same axis layout as other study FBXs.
            var left = PlaceBank(scene, study.transform, "Left wall / warm", Left, -105f, -160f, 90f, materialWarm);
            var right = PlaceBank(scene, study.transform, "Right wall / grey", Right, 105f, -160f, -90f, materialGrey);
            if (left.Count != Left.Length || right.Count != Right.Length)
                throw new InvalidOperationException("Some source modules failed to instantiate.");

            Vector3 offset = camera.transform.position - player.transform.position;
            player.transform.SetPositionAndRotation(new Vector3(0f, 48f, -280f), Quaternion.identity);
            camera.transform.position = player.transform.position + offset;
            camera.orthographic = true;
            camera.orthographicSize = 80f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 650f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.42f, .42f, .43f);

            study.AddComponent<ArtLabLandscapeFlight>().Configure(
                Require<UniversalRenderPipelineAsset>(Pipeline),
                player.transform, camera, flight, boost, 600f,
                "CLIFF PACK / Original mesh comparison / W: warm, G: grey");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save new reference study scene.");
            Selection.activeGameObject = study;
            return "Cliff reference ready: " + Destination + ". 8 original modules, scaled to ~80m cliffs. " +
                "Play, fly between the walls (WASD / Space), 1/2/3 camera. No terrain-crash reset in this visual pass.";
        }

        private static List<GameObject> PlaceBank(Scene scene, Transform parent, string label,
            string[] names, float centreX, float startZ, float yaw, Material mat)
        {
            var bank = new GameObject(label);
            bank.transform.SetParent(parent, false);
            var output = new List<GameObject>();
            float nextMinZ = startZ;
            for (int i = 0; i < names.Length; i++)
            {
                var prefab = Require<GameObject>(PrefabPath(names[i]));
                var item = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject
                    ?? throw new InvalidOperationException("Failed to instantiate " + names[i]);
                item.name = (i + 1) + " - " + names[i];
                item.transform.SetParent(bank.transform, false);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                item.transform.localScale = Vector3.one;
                var renderer = item.GetComponentInChildren<MeshRenderer>(true)
                    ?? throw new InvalidOperationException("Prefab missing renderer: " + names[i]);
                var nativeHeight = renderer.bounds.size.y;
                if (nativeHeight < .05f)
                    throw new InvalidOperationException("Prefab height invalid: " + names[i]);
                float targetHeight = i == 0 ? 82f : (i == names.Length - 1 ? 73f : 80f);
                float scale = Mathf.Clamp(targetHeight / nativeHeight, 2.5f, 9f);
                item.transform.localScale = Vector3.one * scale;
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                // Re-read bounds after scaling and rotating to align each tile.
                var bounds = renderer.bounds;
                item.transform.position += new Vector3(centreX - bounds.center.x,
                    -0.4f - bounds.min.y, nextMinZ - bounds.min.z);
                nextMinZ = renderer.bounds.max.z - 4f; // overlap to mask open seams
                output.Add(item);
            }
            return output;
        }

        private static string PrefabPath(string name) => Prefabs + name + ".prefab";

        private static Material MakeMaterial(Shader shader, string name, Color tint, Texture2D palette)
        {
            string path = Materials + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var material = existing != null ? existing : new Material(shader) { name = name };
            material.shader = shader;
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseMap", palette);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", .02f);
            if (existing == null) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private static void MakeFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            if (slash <= 0) throw new InvalidOperationException("Invalid asset folder: " + folder);
            MakeFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }

        private static T Require<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing imported asset: " + path);
    }
}
#endif
