using System;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run after the runtime scripts compile. Does not rebuild the scene or touch player tuning.
public static class ConfigureSandboxEnemy
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
    private const string ModelPath = "Assets/ThirdParty/PolyPizza/LowPolyFighter/model.obj";
    private const string MaterialPath = "Assets/_Project/Materials/EnemyFighter.mat";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Missing scene: " + ScenePath);
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes first.");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (model == null || shader == null)
            throw new InvalidOperationException("Missing enemy model or URP Lit shader.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var player = FindRoot(scene, "Player");
        var cameraObject = FindRoot(scene, "Main Camera");
        if (player == null || player.GetComponent<FlightController>() == null || cameraObject == null || cameraObject.GetComponent<Camera>() == null)
            throw new InvalidOperationException("Sandbox is missing the configured player or camera.");

        var enemy = FindRoot(scene, "Enemy");
        if (enemy == null)
        {
            enemy = new GameObject("Enemy");
            SceneManager.MoveGameObjectToScene(enemy, scene);
            enemy.transform.SetPositionAndRotation(player.transform.position + new Vector3(32f, 0f, 73f), Quaternion.identity);
        }

        var visual = enemy.transform.Find("EnemyVisual");
        if (visual == null)
        {
            visual = new GameObject("EnemyVisual").transform;
            visual.SetParent(enemy.transform, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            instance.name = "FighterModel";
            instance.transform.SetParent(visual, false);
            // Source OBJ nose points along +Z; match the gameplay root's forward direction.
            instance.transform.localRotation = Quaternion.identity;
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("Enemy model has no mesh renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            var width = Mathf.Max(bounds.size.x, bounds.size.z);
            if (width <= 0.001f)
                throw new InvalidOperationException("Enemy model has invalid bounds.");
            instance.transform.localScale *= 10.5f / width;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            instance.transform.position += visual.position - bounds.center;
            var material = GetMaterial(shader);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            foreach (var renderer in renderers)
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        var controller = GetOrAdd<EnemyFlightController>(enemy);
        SetReference(controller, "target", player.transform);
        SetReference(controller, "visual", visual);
        var indicator = GetOrAdd<EnemyIndicator>(cameraObject);
        SetReference(indicator, "player", player.transform);
        SetReference(indicator, "enemy", enemy.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Failed to save " + ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = enemy;
        return "Sandbox enemy configured. Enter Play Mode to test pursuit.";
    }

    private static Material GetMaterial(Shader shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        material = new Material(shader) { name = "EnemyFighter" };
        material.SetColor("_BaseColor", new Color32(170, 76, 65, 255));
        material.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static T GetOrAdd<T>(GameObject owner) where T : Component
    {
        var component = owner.GetComponent<T>();
        return component != null ? component : owner.AddComponent<T>();
    }

    private static void SetReference(Component component, string propertyName, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("Missing serialized property: " + propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
