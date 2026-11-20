using System;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Creates a reusable visual-only prefab and two named Sandbox preview instances.
public static class ConfigureSandboxTurrets
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
    private const string Folder = "Assets/_Project/Art/Buildings/MT-01-Sentinel";
    private const string ModelPath = Folder + "/MT-01-Sentinel.fbx";
    private const string PrefabPath = Folder + "/MT-01-Sentinel.prefab";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Missing Sandbox scene.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Generate the Sentinel FBX first.");
        if (!importer.preserveHierarchy)
        {
            importer.preserveHierarchy = true;
            importer.SaveAndReimport();
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Cannot load Sentinel FBX.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Player");
        if (player == null) throw new InvalidOperationException("Missing Player.");
        var prefab = BuildPrefab(model);
        Place(scene, prefab, player.transform, "Sentinel Preview A", new Vector3(-24f, 0.12f, 46f));
        Place(scene, prefab, player.transform, "Sentinel Preview B", new Vector3(-165f, 0.12f, 20f));
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Sandbox.");
        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        return "Sentinel prefab and two visual tracking previews ready. No combat logic.";
    }

    private static GameObject BuildPrefab(GameObject model)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("MT-01-Sentinel");
            SceneManager.MoveGameObjectToScene(root, preview);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
            if (instance == null) throw new InvalidOperationException("Cannot instantiate Sentinel.");
            instance.transform.SetParent(root.transform, false);
            var head = FindPart(instance.transform, "TurretHead");
            var forward = FindPart(instance.transform, "AimForward");
            var left = FindPart(instance.transform, "LauncherLeft");
            var right = FindPart(instance.transform, "LauncherRight");
            var fixedBase = FindPart(instance.transform, "Base");
            var headMesh = FindPart(instance.transform, "HeadMesh");
            if (!forward.IsChildOf(head) || !left.IsChildOf(head) || !right.IsChildOf(head) ||
                !headMesh.IsChildOf(head) || fixedBase.IsChildOf(head))
                throw new InvalidOperationException("Incorrect Sentinel hierarchy.");
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 2) throw new InvalidOperationException("Expected two mesh renderers.");
            // Normalize source orientation from the imported marker. Handle either FBX forward sign.
            var direction = forward.position - head.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) throw new InvalidOperationException("Invalid Sentinel heading.");
            instance.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(direction, Vector3.forward, Vector3.up), Vector3.up)
                * instance.transform.rotation;
            // Preserve the authored proportions, while making the base approx. 14 game units wide.
            var baseRenderer = fixedBase.GetComponent<MeshRenderer>();
            if (baseRenderer == null) throw new InvalidOperationException("Missing base mesh renderer.");
            var b = baseRenderer.bounds;
            float extent = Mathf.Max(b.size.x, b.size.z);
            if (extent < 0.001f) throw new InvalidOperationException("Invalid Sentinel scale.");
            instance.transform.localScale *= 14f / extent;
            b = baseRenderer.bounds;
            instance.transform.position += root.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var r in renderers)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            var tracker = root.AddComponent<TurretHeadTracker>();
            SetReference(tracker, "head", head);
            SetReference(tracker, "aimForward", forward);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            if (!ok || prefab == null) throw new InvalidOperationException("Failed to save Sentinel prefab.");
            return prefab;
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void Place(Scene scene, GameObject prefab, Transform player, string name, Vector3 position)
    {
        var instance = scene.GetRootGameObjects().FirstOrDefault(o => o.name == name);
        if (instance == null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) throw new InvalidOperationException("Cannot place " + name);
            instance.name = name;
            instance.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }
        var tracker = instance.GetComponent<TurretHeadTracker>();
        if (tracker == null) throw new InvalidOperationException("Missing tracker on " + name);
        SetReference(tracker, "target", player);
        PrefabUtility.RecordPrefabInstancePropertyModifications(tracker);
    }

    private static Transform FindPart(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)
        ?? throw new InvalidOperationException("Missing FBX part: " + name);

    private static void SetReference(Component component, string field, UnityEngine.Object value)
    {
        var settings = new SerializedObject(component);
        var property = settings.FindProperty(field);
        if (property == null) throw new InvalidOperationException("Missing field: " + field);
        property.objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
