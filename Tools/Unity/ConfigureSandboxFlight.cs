using System;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run after the runtime scripts compile. Only updates the existing Sandbox scene.
public static class ConfigureSandboxFlight
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Missing scene: " + ScenePath);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes before configuring Sandbox.");
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var player = FindRoot(scene, "Player");
        var cameraObject = FindRoot(scene, "Main Camera");
        var ground = FindRoot(scene, "Ground");
        var grid = FindRoot(scene, "Grid");
        var visual = player.transform.Find("JetVisual");
        if (visual == null || cameraObject.GetComponent<Camera>() == null)
            throw new InvalidOperationException("Sandbox is missing JetVisual or Camera.");

        // Get or add makes the setup safe to repeat without duplicating components
        // or overwriting their Inspector tuning values.
        var flight = GetOrAdd<FlightController>(player);
        SetReference(flight, "visual", visual);
        var cameraFollow = GetOrAdd<TopDownCameraFollow>(cameraObject);
        SetReference(cameraFollow, "target", player.transform);
        var surface = GetOrAdd<SandboxSurface>(ground);
        SetReference(surface, "cameraTarget", cameraObject.transform);
        SetReference(surface, "grid", grid.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Failed to save " + ScenePath);
        Selection.activeGameObject = player;
        return "Sandbox flight configured and saved. WASD ready in Play Mode.";
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        throw new InvalidOperationException("Missing Sandbox root: " + name);
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
