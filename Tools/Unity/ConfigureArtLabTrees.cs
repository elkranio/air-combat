using System;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds reusable tree prefabs and a copy of the anti-tiling study. Never edits Sandbox.
public static class ConfigureArtLabTrees
{
    private const string SourceScene = "Assets/_Project/Scenes/ArtLabV3AntiTiling.unity";
    private const string StudyScene = "Assets/_Project/Scenes/ArtLabV4Trees.unity";
    private const string Folder = "Assets/_Project/Art/Nature/Trees";
    private static readonly string[] Names = { "Tree_Round_01", "Tree_Pine_01", "Tree_Cluster_01" };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Require<SceneAsset>(SourceScene);
        var models = Names.Select(name => Require<GameObject>(Folder + "/" + name + ".fbx")).ToArray();
        var material = MakePaletteMaterial();
        var prefabs = new GameObject[Names.Length];
        for (int i = 0; i < Names.Length; i++)
            prefabs[i] = MakePrefab(models[i], Names[i], material);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        // Save a copy, then open that copy before touching any scene objects.
        if (!EditorSceneManager.SaveScene(scene, StudyScene, true))
            throw new InvalidOperationException("Could not copy ArtLab v3.1.");
        scene = EditorSceneManager.OpenScene(StudyScene, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single(o => o.name == "ArtLab V3 Anti-Tiling");
        var preview = root.GetComponent<ArtLabLightingPreview>();
        if (preview == null) throw new InvalidOperationException("Missing lighting preview.");
        var nature = root.transform.Cast<Transform>().SingleOrDefault(t => t.name == "Nature studies / layered crowns");
        if (nature == null) throw new InvalidOperationException("Missing nature study group.");
        var trees = nature.Cast<Transform>().Where(t => t.name == "Tree").ToArray();
        if (trees.Length == 0) throw new InvalidOperationException("No original trees found.");
        var original = Group(nature, "Original procedural trees");
        var authored = Group(nature, "Blender tree pack");
        var counts = new int[Names.Length];
        foreach (var tree in trees)
        {
            var oldBounds = BoundsOf(tree.GetComponentsInChildren<MeshRenderer>(true));
            bool pine = tree.Find("Top tier") != null;
            // Keep the existing pine placements; alternate broadleaf forms deterministically.
            var position = tree.position;
            int hash = unchecked(Mathf.RoundToInt(position.x * 100f) * 73856093 ^ Mathf.RoundToInt(position.z * 100f) * 19349663);
            int variant = pine ? 1 : (hash & 3) == 0 ? 2 : 0;
            counts[variant]++;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[variant], scene);
            if (instance == null) throw new InvalidOperationException("Could not place " + Names[variant]);
            var t = instance.transform;
            t.SetParent(authored, false);
            t.SetPositionAndRotation(position, tree.rotation);
            var bounds = BoundsOf(instance.GetComponentsInChildren<MeshRenderer>(true));
            float extent = Mathf.Max(bounds.size.x, bounds.size.z);
            if (extent < .001f || bounds.size.y < .001f) throw new InvalidOperationException("Invalid imported tree bounds.");
            float width = Mathf.Max(oldBounds.size.x, oldBounds.size.z);
            float ratio = width / extent;
            // Preserve footprint, ground contact and practical height at the game camera.
            t.localScale = new Vector3(ratio, Mathf.Min(ratio, 10f / bounds.size.y), ratio);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            tree.SetParent(original, true);
        }
        preview.ConfigureTreeComparison(original.gameObject, authored.gameObject);
        root.name = "ArtLab V4 Trees";
        EditorUtility.SetDirty(preview);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save tree study.");
        Selection.activeGameObject = authored.gameObject;
        return $"ArtLab v4: {trees.Length} trees ({counts[0]} round, {counts[1]} pine, {counts[2]} cluster). T: old/new; G/L/H unchanged. Sandbox unchanged.";
    }

    private static Material MakePaletteMaterial()
    {
        string path = Folder + "/TreePalette.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Generate the Blender tree pack first.");
        if (!importer.sRGBTexture || importer.mipmapEnabled || importer.filterMode != FilterMode.Point ||
            importer.wrapMode != TextureWrapMode.Clamp || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.sRGBTexture = true;
            // UVs select solid palette swatches. No mipmaps prevents mixing swatches at distance.
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var texture = Require<Texture2D>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Missing URP Lit.");
        path = Folder + "/TreePalette.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "TreePalette" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", texture);
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", .05f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject MakePrefab(GameObject model, string name, Material material)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            if (instance == null) throw new InvalidOperationException("Cannot instantiate " + name);
            instance.transform.SetParent(root.transform, false);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1)
                throw new InvalidOperationException("Expected one mesh and one material slot: " + name);
            var r = renderers[0];
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false; // same existing contact-shadow mesh in both comparison modes
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            // The generator sets its origin at the trunk foot. Only correct import height,
            // not the horizontal pivot of the deliberately asymmetric canopy.
            instance.transform.position -= Vector3.up * r.bounds.min.y;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab", out bool success);
            if (!success || prefab == null) throw new InvalidOperationException("Failed to save " + name);
            return prefab;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }
    private static Bounds BoundsOf(MeshRenderer[] renderers)
    {
        if (renderers.Length == 0) throw new InvalidOperationException("Missing tree mesh.");
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }
    private static T Require<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
}
