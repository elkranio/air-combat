using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds two reusable comparison prefabs; never changes Sandbox or its camera.
public static class ConfigureHangarPreview
{
    private const string Folder = "Assets/_Project/Models/Buildings/Hangar-04";
    private const string Name = "Hangar-04";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{Name}.fbx");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (model == null || shader == null)
            throw new InvalidOperationException("Generate Hangar-04 first; URP Lit must be available.");
        var plain = LoadTexture("BaseColor", true);
        var baked = LoadTexture("Baked", true);
        LoadTexture("AO", false); // linear reference texture, not applied twice
        MakePrefab(model, MakeMaterial(shader, "Plain", plain), "Plain");
        MakePrefab(model, MakeMaterial(shader, "Baked", baked), "Baked");
        AssetDatabase.SaveAssets();
        var selection = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{Name}-Baked.prefab");
        Selection.activeObject = selection;
        EditorGUIUtility.PingObject(selection);
        return $"Hangar comparison prefabs ready in {Folder}. Sandbox unchanged.";
    }

    private static Texture2D LoadTexture(string suffix, bool srgb)
    {
        string path = $"{Folder}/{Name}_{suffix}.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing image: " + path);
        if (importer.sRGBTexture != srgb || !importer.mipmapEnabled || importer.maxTextureSize != 1024 ||
            importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidOperationException("Could not import: " + path);
        return texture;
    }

    private static Material MakeMaterial(Shader shader, string suffix, Texture2D texture)
    {
        string path = $"{Folder}/{Name}-{suffix}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = $"{Name}-{suffix}" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void MakePrefab(GameObject model, Material material, string suffix)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject(Name + "-" + suffix);
            SceneManager.MoveGameObjectToScene(root, scene);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            if (instance == null) throw new InvalidOperationException("Cannot instantiate hangar model.");
            instance.transform.SetParent(root.transform, false);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1)
                throw new InvalidOperationException("Expected a single atlas mesh and material slot.");
            renderers[0].sharedMaterial = material;
            // Same real-time lighting for both variants, no real-time building shadow.
            renderers[0].shadowCastingMode = ShadowCastingMode.Off;
            renderers[0].receiveShadows = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderers[0]);
            string path = $"{Folder}/{Name}-{suffix}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || prefab == null) throw new InvalidOperationException("Failed to save " + path);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
