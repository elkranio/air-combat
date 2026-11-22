#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat.EditorTools
{
    // AA1's one-shot authoring command. Creates a REAL prefab, not runtime art
    // tied to GroundAttackLab. Running the accepted scene never regenerates it.
    internal static class InstallAntiAircraftTrial
    {
        private const string ScenePath = "Assets/_Project/Scenes/GroundAttackLab.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/AA1Autocannon.prefab";
        private const string MaterialsFolder = "Assets/_Project/Art/AA1";
        private const string SceneName = "AA-01 Autocannon";

        [MenuItem("Air Combat/AA1/Install Autocannon Trial")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("AA1: Exit Play Mode first.");
                return;
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || SceneManager.sceneCount != 1)
            {
                Debug.LogError("AA1: Open only the saved GroundAttackLab scene in Edit Mode first.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ||
                (AssetDatabase.IsValidFolder(MaterialsFolder) &&
                 AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }).Length > 0))
            {
                Debug.LogError("AA1: AA prefab/materials already exist; refusing to overwrite.");
                return;
            }
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == SceneName)
                {
                    Debug.LogError("AA1: This scene already contains the AA trial.");
                    return;
                }
            var sessions = UnityEngine.Object.FindObjectsByType<GroundAttackLabSession>();
            if (sessions.Length != 1)
            {
                Debug.LogError("AA1: Expected exactly one GroundAttackLabSession in the active scene.");
                return;
            }
            if (Shader.Find("Universal Render Pipeline/Lit") == null ||
                Shader.Find("AirCombat/SandboxEffect") == null)
            {
                Debug.LogError("AA1: Required URP Lit / AirCombat/SandboxEffect shaders missing.");
                return;
            }

            EnsureFolder("Assets/_Project/Prefabs/Enemies");
            EnsureFolder(MaterialsFolder);
            var baseMat = MakeMaterial("Base", new Color(.30f, .37f, .33f));
            var armor = MakeMaterial("Armor", new Color(.19f, .27f, .27f));
            var upper = MakeMaterial("Upper", new Color(.40f, .49f, .43f));
            var dark = MakeMaterial("Dark", new Color(.085f, .12f, .13f));
            var stripe = MakeMaterial("Signal", new Color(.88f, .45f, .10f));
            var tracer = new Material(Shader.Find("AirCombat/SandboxEffect")) { name = "AA1_Tracer" };
            AssetDatabase.CreateAsset(tracer, MaterialsFolder + "/AA1_Tracer.mat");
            var spark = MakeMaterial("Spark", new Color(1f, .71f, .21f));

            // Preview scene authoring prevents transient build objects from being
            // serialized into the accepted GroundAttackLab scene.
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(SceneName);
                SceneManager.MoveGameObjectToScene(root, preview);
                BuildModel(root.transform, baseMat, armor, upper, dark, stripe,
                    out Transform yaw, out Transform elevation, out Transform left,
                    out Transform right);
                var hitbox = root.AddComponent<BoxCollider>();
                hitbox.center = new Vector3(0f, 2.25f, .7f);
                hitbox.size = new Vector3(9.5f, 4.5f, 9.5f);
                root.AddComponent<AntiAircraftTurret>().ConfigureArt(yaw, elevation, left,
                    right, tracer, spark);
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out saved);
                if (!saved) throw new InvalidOperationException("AA1: Failed to create turret prefab.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException("AA1: Failed to place prefab.");
            instance.transform.position = new Vector3(85f, 0f, 300f);
            sessions[0].ConfigureAntiAircraft(instance.GetComponent<AntiAircraftTurret>());
            EditorUtility.SetDirty(sessions[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("AA1: Could not save GroundAttackLab scene.");
            AssetDatabase.SaveAssets();
            Selection.activeObject = instance;
            Debug.Log("AA1: Installed one autocannon at (85,0,300), linked to GroundAttackLabSession. " +
                "Press Play; commit the scene, AA prefab, materials and generated .meta files after green.");
        }

        private static Material MakeMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { name = "AA1_" + name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .12f);
            AssetDatabase.CreateAsset(material, MaterialsFolder + "/AA1_" + name + ".mat");
            return material;
        }

        private static void BuildModel(Transform root, Material baseMat, Material armor,
            Material upper, Material dark, Material stripe, out Transform yaw,
            out Transform elevation, out Transform leftMuzzle, out Transform rightMuzzle)
        {
            // Deliberately broad low-poly silhouette for ortho size 80: stepped
            // emplacement, rotating armored head, independently elevating twin tubes.
            Part(root, "Octagonal foundation", PrimitiveType.Cylinder,
                new Vector3(0f, .22f, 0f), new Vector3(10.5f, .22f, 10.5f), baseMat);
            Part(root, "Armored turntable", PrimitiveType.Cylinder,
                new Vector3(0f, .64f, 0f), new Vector3(8.7f, .22f, 8.7f), dark);
            Part(root, "Pedestal", PrimitiveType.Cylinder,
                new Vector3(0f, 1.37f, 0f), new Vector3(5.5f, .65f, 5.5f), armor);
            for (int side = -1; side <= 1; side += 2)
                for (int row = -1; row <= 1; row += 2)
                    Part(root, "Ground anchor", PrimitiveType.Cube,
                        new Vector3(side * 3.55f, .53f, row * 3.35f),
                        new Vector3(1.65f, .8f, 1.85f), upper);

            yaw = Node(root, "Yaw pivot", new Vector3(0f, 2.05f, 0f));
            // 3D parts all inherit yaw; pitch only affects tubes, not turret housing.
            Part(yaw, "Turret ring", PrimitiveType.Cylinder,
                new Vector3(0f, .05f, 0f), new Vector3(5.2f, .3f, 5.2f), dark);
            Part(yaw, "Armored cabin", PrimitiveType.Cube,
                new Vector3(0f, .85f, -.65f), new Vector3(4.0f, 1.6f, 3.75f), armor);
            Part(yaw, "Cabin roof", PrimitiveType.Cube,
                new Vector3(0f, 1.71f, -.83f), new Vector3(3.55f, .28f, 3.3f), upper);
            Part(yaw, "Mantlet", PrimitiveType.Cube,
                new Vector3(0f, .89f, 1.42f), new Vector3(3.35f, 1.55f, 1.15f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                var cheek = Part(yaw, "Angled side plate", PrimitiveType.Cube,
                    new Vector3(side * 2.15f, .8f, -.5f), new Vector3(.6f, 1.55f, 3.45f), upper);
                cheek.localRotation = Quaternion.Euler(0f, side * -12f, 0f);
                Part(yaw, "Top identification stripe", PrimitiveType.Cube,
                    new Vector3(side * 1.23f, 1.88f, -.85f),
                    new Vector3(.52f, .10f, 2.15f), stripe);
            }
            elevation = Node(yaw, "Pitch pivot", new Vector3(0f, 1.06f, 1.22f));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 1.08f;
                Part(elevation, "Recoiling cannon breech", PrimitiveType.Cube,
                    new Vector3(x, 0f, .72f), new Vector3(1.2f, 1.2f, 2.55f), upper);
                Transform barrel = Part(elevation, "Long autocannon barrel", PrimitiveType.Cylinder,
                    new Vector3(x, 0f, 3.36f), new Vector3(.63f, 2.72f, .63f), dark);
                barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Transform brake = Part(elevation, "Muzzle brake", PrimitiveType.Cylinder,
                    new Vector3(x, 0f, 6.04f), new Vector3(.92f, .38f, .92f), armor);
                brake.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Transform band = Part(elevation, "Cannon warning band", PrimitiveType.Cylinder,
                    new Vector3(x, 0f, 3.0f), new Vector3(.73f, .10f, .73f), stripe);
                band.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            leftMuzzle = Node(elevation, "Muzzle L", new Vector3(-1.08f, 0f, 6.48f));
            rightMuzzle = Node(elevation, "Muzzle R", new Vector3(1.08f, 0f, 6.48f));
        }

        private static Transform Node(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name);
            SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            return child.transform;
        }

        private static Transform Part(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            var child = GameObject.CreatePrimitive(type);
            SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            var collider = child.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return child.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
#endif
