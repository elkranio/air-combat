#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

// V2A4: a NEW scene, from the known V17 flight/camera composition only.
// All modeled canyon and mountain geometry is imported from authored Blender FBX.
// No runtime terrain generation and no changes to the accepted gameplay scenes.
public static class BuildEnvironmentV2Hero
{
    private const string Source = "Assets/_Project/Scenes/FrontierStrata.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierV2Hero.unity";
    private const string Art = "Assets/_Project/Art/Environment/FrontierV2Hero";
    private const string Output = "Assets/_Project/Generated/EnvironmentV2Hero";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const string Marker = "V2A4_HERO_TERRAIN_1";
    private static readonly string[] Rocks = { "WestMassif", "EastMassif", "NorthSpine" };
    private static readonly Vector3[] RockPositions = {
        new Vector3(-397f, 0f, -169f),
        new Vector3(397f, 0f, 252f),
        new Vector3(-347f, 0f, 362f)
    };

    [MenuItem("Air Combat/Art V2/V2A4 Assemble Hero Gorge")]
    private static void FromMenu()
    {
        try { Debug.Log(Configure()); }
        catch (Exception e) { Debug.LogError("V2A4: " + e); }
    }

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before V2A4 assembly.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scenes before assembly.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null)
            throw new InvalidOperationException("V2A4 already exists. Do not overwrite the new study.");
        Require<SceneAsset>(Source);
        var urp = Require<UniversalRenderPipelineAsset>(Pipeline);
        if (!File.Exists(Art + "/V2A4.ready.txt") ||
            File.ReadLines(Art + "/V2A4.ready.txt").FirstOrDefault()?.Trim() != Marker)
            throw new InvalidOperationException("Blender export not finished. Run the V2A4 generator first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? throw new InvalidOperationException("URP Lit shader missing.");
        EnsureFolder(Output);

        // Preflight all imported models before copying and mutating the destination.
        ImportModel(Art + "/HeroGorgeGround.fbx");
        foreach (var name in Rocks) ImportModel(Art + "/" + name + ".fbx");
        var gorgePrefab = Require<GameObject>(Art + "/HeroGorgeGround.fbx");
        var rockPrefabs = Rocks.Select(name => Require<GameObject>(Art + "/" + name + ".fbx")).ToArray();

        var groundMats = new[] {
            MaterialAsset(shader, "OpenGround", new Color(.69f, .55f, .41f)),
            MaterialAsset(shader, "CanyonWall", new Color(.45f, .38f, .31f)),
            MaterialAsset(shader, "CanyonBed", new Color(.43f, .38f, .34f)),
            MaterialAsset(shader, "CanyonApron", new Color(.56f, .46f, .37f))
        };
        var rockMaterials = new Material[Rocks.Length][];
        for (int i = 0; i < Rocks.Length; i++)
        {
            // AO is a LOCAL crevice bake from the hero rock, never a baked sun.
            string aoPath = Art + "/" + Rocks[i] + "_AO.png";
            Texture2D ao = File.Exists(aoPath) ? ImportAO(aoPath) : null;
            rockMaterials[i] = new[] {
                MaterialAsset(shader, Rocks[i] + "_Cap", new Color(.62f, .55f, .44f), ao),
                MaterialAsset(shader, Rocks[i] + "_Face", new Color(.43f, .42f, .39f), ao),
                MaterialAsset(shader, Rocks[i] + "_Talus", new Color(.51f, .47f, .41f), ao)
            };
        }
        AssetDatabase.SaveAssets();

        // Save as a COPY. The approved V17 scene is never edited.
        var source = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(source, Destination, true))
            throw new InvalidOperationException("Cannot copy V17 into V2A4.");
        var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects().Single(x => x.name == "Player");
        var cameraRoot = scene.GetRootGameObjects().Single(x => x.name == "Main Camera");
        var lightRoot = scene.GetRootGameObjects().Single(x => x.name == "Directional Light");
        var camera = cameraRoot.GetComponent<Camera>();
        var sun = lightRoot.GetComponent<Light>();
        var flight = player.GetComponent<FlightController>();
        var boost = player.GetComponent<Afterburner>();
        if (camera == null || sun == null || flight == null || boost == null)
            throw new InvalidOperationException("V17 camera / player / sunlight contract changed.");
        foreach (var root in scene.GetRootGameObjects())
            if (root != player && root != cameraRoot && root != lightRoot)
                Object.DestroyImmediate(root);
        var container = new GameObject("V2A4 / authored rock canyon + flight hazards");
        SceneManager.MoveGameObjectToScene(container, scene);
        var colliders = new List<MeshCollider>();
        var gorge = Place(gorgePrefab, scene, container.transform, Vector3.zero,
            groundMats, colliders);
        gorge.name = "Hero gorge / authored cross-sections";
        for (int i = 0; i < Rocks.Length; i++)
        {
            var mass = Place(rockPrefabs[i], scene, container.transform,
                RockPositions[i], rockMaterials[i], colliders);
            mass.name = Rocks[i] + " / collision hazard";
        }
        if (colliders.Count < 4)
            throw new InvalidOperationException("Expected canyon and 3 mountain MeshColliders.");

        // Same aircraft and ArtLab camera controls as V17/V2A.
        // Spawn over the canyon: both steep banks fit into ortho size 80 at 16:9.
        Vector3 cameraOffset = camera.transform.position - player.transform.position;
        player.transform.position = new Vector3(120f, 48f, -300f);
        player.transform.rotation = Quaternion.identity;
        camera.transform.position = player.transform.position + cameraOffset;
        camera.orthographic = true;
        camera.orthographicSize = 80f;
        camera.farClipPlane = Mathf.Max(650f, camera.farClipPlane);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.59f, .52f, .45f);
        sun.transform.rotation = Quaternion.Euler(38f, -42f, 0f);
        sun.intensity = 1.32f;
        sun.color = new Color(1f, .94f, .87f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .8f;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.37f, .39f, .43f);
        RenderSettings.fog = false;
        RenderSettings.skybox = null;
        container.AddComponent<ArtLabLandscapeFlight>().Configure(urp,
            player.transform, camera, flight, boost, 600f, "V2A4 / HERO GORGE + MOUNTAIN COLLISION");
        container.AddComponent<ArtLabTerrainCollision>().Configure(player.transform, colliders.ToArray());
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Cannot save V2A4 scene.");
        Selection.activeGameObject = gorge;
        return "V2A4 built: FrontierV2Hero.unity; 1 FBX canyon + 3 rock masses, " +
               colliders.Count + " real mesh colliders. Use 1/2/3, WASD, Space, R; fly into a mountain.";
    }

    private static GameObject Place(GameObject prefab, Scene scene, Transform parent,
        Vector3 position, Material[] mats, List<MeshCollider> colliders)
    {
        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject
            ?? throw new InvalidOperationException("Cannot instantiate " + prefab.name);
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != 1)
            throw new InvalidOperationException(prefab.name + ": expected one model MeshRenderer, got " + renderers.Length);
        var renderer = renderers[0];
        var meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null ||
            meshFilter.sharedMesh.subMeshCount != mats.Length)
            throw new InvalidOperationException(prefab.name + ": FBX material/submesh contract changed. " +
                "Expected " + mats.Length + " submeshes.");
        renderer.sharedMaterials = mats;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        var collider = renderer.GetComponent<MeshCollider>();
        if (collider == null) collider = renderer.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = meshFilter.sharedMesh;
        collider.convex = false;
        colliders.Add(collider);
        return instance;
    }

    private static void ImportModel(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("Missing Blender model: " + path);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter
            ?? throw new InvalidOperationException("Unity did not import FBX: " + path);
        if (!importer.isReadable || importer.importNormals != ModelImporterNormals.Import)
        {
            importer.isReadable = true; // Nonconvex MeshCollider requires readable meshes.
            importer.importNormals = ModelImporterNormals.Import;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D ImportAO(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter
            ?? throw new InvalidOperationException("Missing imported AO: " + path);
        if (importer.sRGBTexture || importer.textureType != TextureImporterType.Default ||
            !importer.mipmapEnabled)
        {
            importer.sRGBTexture = false;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return Require<Texture2D>(path);
    }

    private static Material MaterialAsset(Shader shader, string name, Color color, Texture2D ao = null)
    {
        var material = new Material(shader) { name = "V2A4_" + name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", .07f);
        if (ao != null)
        {
            material.SetTexture("_OcclusionMap", ao);
            material.SetFloat("_OcclusionStrength", .62f);
            material.EnableKeyword("_OCCLUSIONMAP");
        }
        var path = Output + "/" + material.name + ".mat";
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash < 0) throw new InvalidOperationException("Invalid folder " + path);
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    private static T Require<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException("Missing asset: " + path);
}
#endif
