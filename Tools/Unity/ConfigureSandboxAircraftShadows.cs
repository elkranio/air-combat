using System;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConfigureSandboxAircraftShadows
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
    private const string MaterialPath = "Assets/_Project/Generated/Battlefield/Shadows.mat";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes first.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null || material.shader == null || material.shader.name != "AirCombat/SandboxEffect")
            throw new InvalidOperationException("Missing Battlefield shadow material: " + MaterialPath);
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = RequireRoot(scene, "Player");
        var enemy = RequireRoot(scene, "Enemy");
        Attach(player, material, 8f, 11f);
        Attach(enemy, material, 8f, 11f);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Failed to save Sandbox.");
        AssetDatabase.SaveAssets();
        return "Configured aircraft ground shadows.";
    }

    private static GameObject RequireRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        throw new InvalidOperationException("Missing Sandbox root: " + name);
    }

    private static void Attach(GameObject owner, Material material, float width, float length)
    {
        var component = owner.GetComponent<AircraftGroundShadow>();
        if (component == null) component = owner.AddComponent<AircraftGroundShadow>();
        var serialized = new SerializedObject(component);
        serialized.FindProperty("shadowMaterial").objectReferenceValue = material;
        serialized.FindProperty("baseWidth").floatValue = width;
        serialized.FindProperty("baseLength").floatValue = length;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
