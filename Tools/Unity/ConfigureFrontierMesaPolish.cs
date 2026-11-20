using System;
using System.IO;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Local art experiment. Compare identical mesh/albedo with different normal detail.
public static class ConfigureFrontierMesaPolish
{
    private const string Source = "Assets/_Project/Scenes/FrontierPracticalBake.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierMesaPolish.unity";
    private const string Art = "Assets/_Project/Art/Environment/FrontierMesaPolish";
    private const string Output = "Assets/_Project/Generated/FrontierMesaPolish";
    private const string V19 = "Assets/_Project/Generated/FrontierPracticalBake/";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save open scenes first.");
        Require<SceneAsset>(Source);
        var pipeline = Require<UniversalRenderPipelineAsset>(Pipeline);
        string marker = Art + "/MesaPolish.ready.txt";
        string normalPath = Art + "/BrokenMesaPolish_Normal.png";
        if (!File.Exists(marker) || !File.Exists(normalPath)) throw new InvalidOperationException("Missing completed V20 bake: " + Path.GetFullPath(marker));
        var report = File.ReadAllLines(marker);
        if (report.Length != 2 || report[0].Trim() != "V20_MESA_POLISH_1" || !report[1].StartsWith("Normal XY", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid V20 marker: " + Path.GetFullPath(marker));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var normal = ImportNormal(normalPath);
        var baseline = Require<Material>(V19 + "Mesa_Base.mat");
        var previous = Require<Material>(V19 + "Mesa_Normal.mat");
        var full = Require<Material>(V19 + "Mesa_Full.mat");
        var previousMap = previous.GetTexture("_BumpMap");
        if (previousMap == null || baseline.GetTexture("_BaseMap") == null || previous.GetTexture("_BaseMap") != baseline.GetTexture("_BaseMap"))
            throw new InvalidOperationException("Missing V19 normal or mismatched base albedo.");
        var expectedScale = new Vector2(1200f / 145f, 1200f / 80f);
        var expectedOffset = new Vector2(-475f / 145f, -745f / 80f);
        if ((baseline.GetTextureScale("_BaseMap") - expectedScale).sqrMagnitude > .00001f ||
            (baseline.GetTextureOffset("_BaseMap") - expectedOffset).sqrMagnitude > .00001f)
            throw new InvalidOperationException("BrokenMesa UV transform changed.");
        var originalScene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        var sourceRoot = Root(originalScene);
        int slot = RockSlot(Terrain(sourceRoot), full);
        if (sourceRoot.GetComponent<FrontierPracticalBakePreview>() == null) throw new InvalidOperationException("Missing V19 comparison component.");
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "FrontierMesaPolish");
        var modes = new[] {baseline, previous,
            Variant("V19_Normal_040", baseline, previousMap, .4f),
            Variant("V20_Beds_045", baseline, normal, .45f),
            Variant("V20_Beds_080", baseline, normal, .8f)};
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(originalScene, Destination, true)) throw new InvalidOperationException("Cannot copy V19.");
        var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var root = Root(scene); root.name = "Frontier mesa polish";
        var terrain = Terrain(root); slot = RockSlot(terrain, full);
        var old = root.GetComponent<FrontierPracticalBakePreview>();
        Object.DestroyImmediate(old);
        var slots = terrain.sharedMaterials; slots[slot] = modes[3]; terrain.sharedMaterials = slots;
        EditorUtility.SetDirty(terrain);
        root.AddComponent<FrontierMesaPolishPreview>().Configure(terrain, slot, modes);
        var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
        var camera = scene.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
        // Start next to the test mesa for immediate comparison; preserve altitude and camera offset.
        var offset = camera.transform.position - player.transform.position;
        var start = new Vector3(27, player.transform.position.y, 185);
        player.transform.position = start; camera.transform.position = start + offset;
        root.GetComponent<ArtLabLandscapeFlight>().Configure(pipeline, player.transform, camera, player.GetComponent<FlightController>(),
            player.GetComponent<Afterburner>(), 600, "V20 / Mesa Polish");
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save V20.");
        Selection.activeGameObject = player;
        return "V20 ready: BrokenMesa only, B compares base/V19 1.0/V19 .4/V20 .45/V20 .8. " + report[1];
    }
    private static GameObject Root(Scene scene) => scene.GetRootGameObjects().Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
    private static MeshRenderer Terrain(GameObject root) => root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Scorched terrain");
    private static int RockSlot(MeshRenderer r, Material full)
    {
        var mesh = r.GetComponent<MeshFilter>().sharedMesh; var slots = r.sharedMaterials;
        if (mesh == null || !mesh.isReadable || mesh.uv.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount ||
            slots.Length != mesh.subMeshCount || slots.Count(m => m == full) != 1)
            throw new InvalidOperationException("Expected exactly one V19 BrokenMesa slot with valid UVs/tangents.");
        return Array.IndexOf(slots, full);
    }
    private static Texture2D ImportNormal(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new InvalidOperationException("Missing V20 importer.");
        imp.textureType = TextureImporterType.NormalMap; imp.convertToNormalmap = false; imp.sRGBTexture = false;
        imp.mipmapEnabled = true; imp.isReadable = false; imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Trilinear; imp.anisoLevel = 4; imp.alphaSource = TextureImporterAlphaSource.None;
        imp.maxTextureSize = 2048; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.SaveAndReimport();
        return Require<Texture2D>(path);
    }
    private static Material Variant(string name, Material source, Texture normal, float strength)
    {
        var m = new Material(source) { name = name };
        m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", strength); m.EnableKeyword("_NORMALMAP");
        m.SetTextureScale("_BumpMap", source.GetTextureScale("_BaseMap")); m.SetTextureOffset("_BumpMap", source.GetTextureOffset("_BaseMap"));
        m.SetTexture("_OcclusionMap", null); m.SetFloat("_OcclusionStrength", 0); m.DisableKeyword("_OCCLUSIONMAP");
        string path = Output + "/" + name + ".mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (old == null) { AssetDatabase.CreateAsset(m, path); return m; }
        EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); EditorUtility.SetDirty(old); return old;
    }
    private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
}
