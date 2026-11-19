using System;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run after importing/compiling the runtime scripts. The debug grid remains in the scene.
public static class ConfigureSandboxLandscape
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Missing scene: " + ScenePath);
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes before configuring Sandbox.");

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ground = RequireRoot(scene, "Ground");
        var grid = RequireRoot(scene, "Grid");
        var camera = RequireRoot(scene, "Main Camera").GetComponent<Camera>();
        if (camera == null || !camera.orthographic)
            throw new InvalidOperationException("Sandbox needs an orthographic Main Camera.");

        var root = FindRoot(scene, "Landscape");
        if (root == null)
        {
            root = new GameObject("Landscape");
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        var landscape = root.GetComponent<ProceduralLandscape>();
        if (landscape == null)
            landscape = root.AddComponent<ProceduralLandscape>();
        var serialized = new SerializedObject(landscape);
        serialized.FindProperty("viewCamera").objectReferenceValue = camera;
        serialized.FindProperty("debugGround").objectReferenceValue = ground;
        serialized.FindProperty("debugGrid").objectReferenceValue = grid;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Failed to save " + ScenePath);
        Selection.activeGameObject = root;
        return "Sandbox landscape configured. Enter Play Mode to generate terrain.";
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static GameObject RequireRoot(Scene scene, string name)
    {
        var root = FindRoot(scene, name);
        if (root == null) throw new InvalidOperationException("Missing Sandbox root: " + name);
        return root;
    }
}
