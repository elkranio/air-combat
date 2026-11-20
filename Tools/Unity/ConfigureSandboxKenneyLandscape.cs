using System;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ConfigureSandboxKenneyLandscape
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
    private const string Nature = "Assets/ThirdParty/Kenney/NatureKit/";
    private const string SkyPath = "Assets/ThirdParty/Kenney/Skyboxes/skybox-day.png";
    private const string MaterialPath = "Assets/_Project/Art/Terrain/SkyboxDay.mat";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes first.");
        var trees = Load("Trees/", "tree_default", "tree_oak", "tree_detailed", "tree_pineRoundA", "tree_pineRoundB", "tree_pineTallA");
        var rocks = Load("Rocks/", "rock_largeA", "rock_largeB");
        var bushes = Load("Bushes/", "plant_bushLarge");
        var sky = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyPath);
        if (sky == null) throw new InvalidOperationException("Missing sky texture: " + SkyPath);
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var root = GameObject.Find("Landscape");
        var landscape = root != null ? root.GetComponent<ProceduralLandscape>() : null;
        if (landscape == null) throw new InvalidOperationException("Missing ProceduralLandscape in Sandbox.");
        var serialized = new SerializedObject(landscape);
        SetArray(serialized, "treeModels", trees);
        SetArray(serialized, "rockModels", rocks);
        SetArray(serialized, "bushModels", bushes);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // A top-down camera will not see much sky. Do not interpret a non-panorama as one.
        bool panorama = Mathf.Abs((float)sky.width / sky.height - 2f) < 0.06f;
        var shader = Shader.Find("Skybox/Panoramic");
        if (panorama && shader != null)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "SkyboxDay" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_MainTex", sky);
            material.SetFloat("_Exposure", 0.9f);
            EditorUtility.SetDirty(material);
            RenderSettings.skybox = material;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.66f, 0.71f, 0.70f);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Sandbox.");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        return "Kenney landscape configured. " + (panorama && shader != null ? "Day skybox assigned." : "Skybox skipped: texture layout or shader not supported.");
    }

    private static GameObject[] Load(string folder, params string[] names)
    {
        var result = new GameObject[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var path = Nature + folder + names[i] + ".obj";
            result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (result[i] == null) throw new InvalidOperationException("Missing model: " + path);
        }
        return result;
    }

    private static void SetArray(SerializedObject target, string name, GameObject[] values)
    {
        var array = target.FindProperty(name);
        if (array == null) throw new InvalidOperationException("Missing property: " + name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
