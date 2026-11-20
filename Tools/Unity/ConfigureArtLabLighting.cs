using System;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Separate experiment. No changes to Sandbox, quality settings or shared materials/meshes.
public static class ConfigureArtLabLighting
{
    private const string Source = "Assets/_Project/Scenes/ArtLabV5Forest.unity";
    private const string Destination = "Assets/_Project/Scenes/ArtLabV8Lighting.unity";
    private const string Output = "Assets/_Project/Generated/ArtLabV8Lighting";
    private const string PipelineSource = "Assets/Settings/PC_RPAsset.asset";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null) throw new InvalidOperationException("Missing ArtLab v5.");
        var pipeline = MakePipeline();
        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, Destination, true)) throw new InvalidOperationException("Cannot copy ArtLab v5.");
        scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single(g => g.name == "ArtLab V5 Forest");
        var previous = root.GetComponent<ArtLabLightingPreview>();
        if (previous == null) throw new InvalidOperationException("Missing v5 comparison controller.");
        var settings = new SerializedObject(previous);
        var sun = (Light)settings.FindProperty("sun").objectReferenceValue;
        var ground = References<MeshRenderer>(settings, "groundRenderers");
        var original = References<Material>(settings, "originalGround");
        var current = References<Material>(settings, "texturedGround");
        if (sun == null || ground.Length != 6 || original.Length != ground.Length || current.Length != ground.Length ||
            ground.Any(r => r == null) || original.Any(m => m == null) || current.Any(m => m == null))
            throw new InvalidOperationException("Unexpected v5 lighting or ground references.");
        var nature = root.transform.Cast<Transform>().Single(t => t.name == "Nature studies / layered crowns");
        var dense = nature.Find("B - Forest masses");
        var scattered = nature.Find("A - Scattered trees");
        var fixedArt = root.transform.Find("Fixed art references");
        var terrain = root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Terrain mesh");
        if (dense == null || scattered == null || fixedArt == null) throw new InvalidOperationException("Unexpected v5 hierarchy.");
        var fixedShadows = dense.Find("Fixed object shadows");
        var woodlandShade = dense.Find("Continuous woodland shade");
        if (fixedShadows == null || woodlandShade == null) throw new InvalidOperationException("Missing v5 shadow meshes.");
        var blobs = new[] { fixedShadows.gameObject, woodlandShade.gameObject };
        UnityEngine.Object.DestroyImmediate(previous);
        UnityEngine.Object.DestroyImmediate(scattered.gameObject);
        dense.gameObject.SetActive(true);

        int casters = 0, receivers = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (blobs.Any(b => r.transform.IsChildOf(b.transform))) continue;
            // The painted river is unlit. Thin runway/road/marking meshes receive shadows but must not cast them.
            bool natureCaster = r.transform.IsChildOf(nature);
            bool modelCaster = r.transform.IsChildOf(fixedArt);
            bool cast = r == terrain || natureCaster || modelCaster;
            r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = r.sharedMaterials.Any(m => m != null && m.shader.name.Contains("/Lit"));
            if (cast) casters++;
            if (r.receiveShadows) receivers++;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        var camera = scene.GetRootGameObjects().Select(o=>o.GetComponent<Camera>()).FirstOrDefault(c=>c!=null);
        if (camera == null || !camera.orthographic) throw new InvalidOperationException("Missing ArtLab camera.");
        root.AddComponent<ArtLabDirectionalLightingPreview>().Configure(pipeline, sun, blobs, ground, original, current);
        root.name = "ArtLab V8 Lighting";
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save lighting study.");
        Selection.activeGameObject = root;
        return $"ArtLab v8 ready: {casters} caster renderers, {receivers} lit receivers. Play: 1/2/3, L, G, H. Pipeline override is Play Mode only.";
    }

    private static UniversalRenderPipelineAsset MakePipeline()
    {
        var source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineSource);
        if (source == null) throw new InvalidOperationException("Missing PC URP asset.");
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "ArtLabV8Lighting");
        string path = Output + "/ArtLabLighting_URP.asset";
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (asset == null)
        {
            asset = UnityEngine.Object.Instantiate(source);
            asset.name = "ArtLabLighting_URP";
            AssetDatabase.CreateAsset(asset, path);
        }
        else EditorUtility.CopySerialized(source, asset);
        asset.name = "ArtLabLighting_URP";
        // These are the actual URP 17.x serialized fields of the checked-in PC asset.
        // Only this isolated copy is modified; the renderer data stays shared and unchanged.
        var so = new SerializedObject(asset);
        Required(so, "m_MainLightShadowsSupported").boolValue = true;
        Required(so, "m_SoftShadowsSupported").boolValue = true;
        Required(so, "m_MainLightShadowmapResolution").intValue = 4096;
        Required(so, "m_ShadowDistance").floatValue = 230f;
        Required(so, "m_ShadowCascadeCount").intValue = 2;
        Required(so, "m_Cascade2Split").floatValue = .48f;
        Required(so, "m_ShadowDepthBias").floatValue = .06f;
        Required(so, "m_ShadowNormalBias").floatValue = .22f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static SerializedProperty Required(SerializedObject so, string field) =>
        so.FindProperty(field) ?? throw new InvalidOperationException("Missing URP field: " + field);
    private static T[] References<T>(SerializedObject so, string field) where T : UnityEngine.Object
    {
        var p = Required(so, field);
        return Enumerable.Range(0,p.arraySize).Select(i => p.GetArrayElementAtIndex(i).objectReferenceValue as T).ToArray();
    }
}
