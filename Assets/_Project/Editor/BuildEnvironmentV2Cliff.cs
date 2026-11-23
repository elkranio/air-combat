#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// V2A5: ONE hand-authored cliff in its own playable scene. V2A4 and combat scenes
// are not modified by this installer. The existing V2A ground is a neutral stage.
public static class BuildEnvironmentV2Cliff
{
    private const string Source = "Assets/_Project/Scenes/FrontierV2Ground.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierV2Cliff.unity";
    private const string Art = "Assets/_Project/Art/Environment/FrontierV2Cliff";
    private const string Generated = "Assets/_Project/Generated/EnvironmentV2Cliff";
    private const string ModelPath = Art + "/V2A5_Cliff.fbx";
    private const string AoPath = Art + "/V2A5_Cliff_AO.png";
    private const string Marker = Art + "/V2A5.ready.txt";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";

    // Exact order of source Blender materials, never alphabetical sorting.
    private static readonly string[] MaterialNames = { "Cap", "WarmFace", "CoolFace", "Fracture", "Talus" };
    private static readonly Color[] MaterialColors = {
        new Color(.59f, .51f, .405f),
        new Color(.43f, .365f, .30f),
        new Color(.37f, .372f, .365f),
        new Color(.285f, .29f, .285f),
        new Color(.53f, .44f, .35f)
    };

    [MenuItem("Air Combat/Art V2/V2A5 Assemble Single Cliff")]
    private static void FromMenu()
    {
        try { Debug.Log(Configure()); }
        catch (Exception exception) { Debug.LogError("V2A5: " + exception); }
    }

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before assembling V2A5.");
        for (int i = 0; i < SceneManager.sceneCount; ++i)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scenes before V2A5 assembly.");
        Require<SceneAsset>(Source);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null)
            throw new InvalidOperationException("V2A5 scene already exists. No silent overwrite.");
        if (!File.Exists(Marker) || File.ReadLines(Marker).FirstOrDefault()?.Trim() != "V2A5_CLIFF_1")
            throw new InvalidOperationException("Missing completed Blender V2A5 export marker.");
        Require<UniversalRenderPipelineAsset>(Pipeline);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ImportModel();
        var cliffModel = Require<GameObject>(ModelPath);
        var shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? throw new InvalidOperationException("URP Lit shader missing.");
        EnsureFolder(Generated);

        Texture2D ao = File.Exists(AoPath) ? ImportAO() : null;
        var mats = new Material[MaterialNames.Length];
        for (int i = 0; i < mats.Length; ++i)
            mats[i] = EnsureMaterial(shader, MaterialNames[i], MaterialColors[i], ao);
        AssetDatabase.SaveAssets();

        // A clean V2A study, not the rejected trench/mountain geometry of V2A4.
        var source = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(source, Destination, true))
            throw new InvalidOperationException("Failed copying V2A study.");
        var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
        var camRoot = scene.GetRootGameObjects().Single(g => g.name == "Main Camera");
        var art = scene.GetRootGameObjects().Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
        var camera = camRoot.GetComponent<Camera>();
        var flight = player.GetComponent<FlightController>();
        var afterburner = player.GetComponent<Afterburner>();
        if (camera == null || flight == null || afterburner == null)
            throw new InvalidOperationException("V2A flight setup changed.");

        var cliffRoot = new GameObject("V2A5 / single authored escarpment");
        SceneManager.MoveGameObjectToScene(cliffRoot, scene);
        var instance = PrefabUtility.InstantiatePrefab(cliffModel, scene) as GameObject
            ?? throw new InvalidOperationException("Could not instantiate authored cliff FBX.");
        instance.transform.SetParent(cliffRoot.transform, false);
        instance.transform.localPosition = new Vector3(0f, 0f, 10f);
        instance.name = "Hero cliff / manually defined fracture masses";
        var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != 1)
            throw new InvalidOperationException("V2A5 model must have ONE MeshRenderer. Found " + renderers.Length);
        var renderer = renderers[0];
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.subMeshCount != mats.Length)
            throw new InvalidOperationException("V2A5 FBX submesh/material mismatch (expect 5). Inspect Blender export.");
        renderer.sharedMaterials = mats;
        renderer.receiveShadows = true;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        var collider = renderer.gameObject.GetComponent<MeshCollider>();
        if (collider == null) collider = renderer.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = filter.sharedMesh;
        collider.convex = false;

        // Spawn INSIDE the 600-halfMap safety boundary. V2A4's -425 spawn was
        // outside the camera-aware +/-418 clamp and caused a reset loop.
        Vector3 offset = camera.transform.position - player.transform.position;
        player.transform.SetPositionAndRotation(new Vector3(0f, 48f, -235f), Quaternion.identity);
        camera.transform.position = player.transform.position + offset;
        camera.orthographic = true;
        camera.orthographicSize = 80f;
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, 650f);
        art.GetComponent<ArtLabLandscapeFlight>().Configure(
            Require<UniversalRenderPipelineAsset>(Pipeline),
            player.transform, camera, flight, afterburner, 600f,
            "V2A5 / SINGLE FRACTURED CLIFF — scale and silhouette trial");
        cliffRoot.AddComponent<ArtLabTerrainCollision>().Configure(
            player.transform, new[] { collider });

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Failed saving FrontierV2Cliff.");
        Selection.activeGameObject = instance;
        return "V2A5 ready: FrontierV2Cliff.unity. Fly forward into the cliff. " +
            "1/2/3 change pitch; R reset; real rock MeshCollider. V2A/V2A4 unchanged.";
    }

    private static void ImportModel()
    {
        if (!File.Exists(ModelPath))
            throw new InvalidOperationException("Missing Blender export: " + ModelPath);
        var imp = AssetImporter.GetAtPath(ModelPath) as ModelImporter
            ?? throw new InvalidOperationException("Unity did not import the FBX: " + ModelPath);
        if (!imp.isReadable || imp.importNormals != ModelImporterNormals.Import)
        {
            imp.isReadable = true; // Required by the nonconvex study collider.
            imp.importNormals = ModelImporterNormals.Import;
            imp.SaveAndReimport();
        }
    }

    private static Texture2D ImportAO()
    {
        var imp = AssetImporter.GetAtPath(AoPath) as TextureImporter
            ?? throw new InvalidOperationException("Missing AO texture importer.");
        if (imp.textureType != TextureImporterType.Default || imp.sRGBTexture ||
            !imp.mipmapEnabled || imp.maxTextureSize != 1024)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
            imp.mipmapEnabled = true;
            imp.isReadable = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        return Require<Texture2D>(AoPath);
    }

    private static Material EnsureMaterial(Shader shader, string label, Color tint, Texture2D ao)
    {
        string path = Generated + "/V2A5_" + label + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "V2A5_" + label };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", .055f);
        material.SetTexture("_OcclusionMap", ao);
        material.SetFloat("_OcclusionStrength", ao == null ? 0f : .53f);
        if (ao != null) material.EnableKeyword("_OCCLUSIONMAP");
        else material.DisableKeyword("_OCCLUSIONMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        int slash = folder.LastIndexOf('/');
        if (slash < 0) throw new InvalidOperationException("Bad folder " + folder);
        EnsureFolder(folder.Substring(0, slash));
        AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
    }

    private static T Require<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException("Missing asset: " + path);
}
#endif
