using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Run in the open editor using Unity Pipeline: unity command script run --file Tools/Unity/BuildSandbox.cs
// Editor-only tooling deliberately lives outside Assets/.
public static class BuildSandbox
{
    private const string ModelPath = "Assets/ThirdParty/PolyPizza/Jet/Jet_01.obj";
    private const string SceneFolder = "Assets/_Project/Scenes";
    private const string ScenePath = SceneFolder + "/Sandbox.unity";
    private const string MaterialFolder = "Assets/_Project/Materials";
    private const int GroundSize = 400;
    private const int GridSpacing = 20;

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Sandbox already exists. Open it instead of rebuilding.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes before building Sandbox.");
        }

        var jet = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (jet == null)
            throw new InvalidOperationException("Missing model: " + ModelPath);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP Lit shader not found.");

        EnsureFolder(SceneFolder);
        EnsureFolder(MaterialFolder);
        var groundMaterial = GetMaterial("SandboxGround", shader, new Color32(112, 139, 113, 255));
        var gridMaterial = GetMaterial("SandboxGrid", shader, new Color32(156, 177, 151, 255));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        var grid = new GameObject("Grid");
        for (int offset = -GroundSize / 2 + GridSpacing; offset < GroundSize / 2; offset += GridSpacing)
        {
            AddGridLine(grid.transform, gridMaterial, "X " + offset,
                new Vector3(0f, 0.025f, offset), new Vector3(GroundSize, 0.05f, 0.22f));
            AddGridLine(grid.transform, gridMaterial, "Z " + offset,
                new Vector3(offset, 0.025f, 0f), new Vector3(0.22f, 0.05f, GroundSize));
        }

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 12f, 0f);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(jet, scene);
        visual.name = "JetVisual";
        visual.transform.SetParent(player.transform, false);
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            visual.transform.position += player.transform.position - bounds.center;
        }

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 100f, 0f), Quaternion.Euler(90f, 0f, 0f));
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 55f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 300f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(112, 139, 113, 255);
        cameraObject.AddComponent<AudioListener>();

        var lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save " + ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        return "Sandbox saved: " + ScenePath;
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var child = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(child))
                AssetDatabase.CreateFolder(parent, parts[i]);
            parent = child;
        }
    }

    private static Material GetMaterial(string name, Shader shader, Color color)
    {
        var path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void AddGridLine(Transform parent, Material material, string name, Vector3 position, Vector3 scale)
    {
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = name;
        line.transform.SetParent(parent, false);
        line.transform.localPosition = position;
        line.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());
        var renderer = line.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }
}
