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

// V19: copy the green V17 map. Replace one refinery body and add a local
// detail bake to its north mesa without touching the shared terrain mesh.
public static class ConfigureFrontierPracticalBake
{
    private const string Source = "Assets/_Project/Scenes/FrontierStrata.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierPracticalBake.unity";
    private const string Art = "Assets/_Project/Art/Environment/FrontierPracticalBake";
    private const string Output = "Assets/_Project/Generated/FrontierPracticalBake";
    private const string MesaPath = "Assets/_Project/Generated/FrontierStrata/BrokenMesa.mat";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save open scenes first.");
        Require<SceneAsset>(Source);
        var pipeline = Require<UniversalRenderPipelineAsset>(Pipeline);
        var originalMesa = Require<Material>(MesaPath);
        string marker = Art + "/PracticalBake.ready.txt";
        if (!File.Exists(marker)) throw new InvalidOperationException("Missing completed V19 bake: " + Path.GetFullPath(marker));
        var report = File.ReadAllLines(marker);
        if (report.Length != 3 || report[0].Trim() != "V19_PRACTICAL_BAKE_1" ||
            !report[1].StartsWith("RefineryRoof:", StringComparison.Ordinal) ||
            !report[2].StartsWith("BrokenMesaDetail:", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid V19 bake marker: " + Path.GetFullPath(marker));
        foreach (string f in new[] { "RefineryRoof.fbx", "RefineryRoof_BaseColor.png", "RefineryRoof_Normal.png",
            "RefineryRoof_AO.png", "BrokenMesaDetail_Normal.png", "BrokenMesaDetail_AO.png" })
            if (!File.Exists(Art + "/" + f)) throw new InvalidOperationException("Missing V19 bake output: " + f);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "FrontierPracticalBake");
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit missing.");
        var model = ImportRoof();
        var roofBase = ImportMap("RefineryRoof_BaseColor", false, true);
        var roofNormal = ImportMap("RefineryRoof_Normal", true, false);
        var roofAO = ImportMap("RefineryRoof_AO", false, false);
        var mesaNormal = ImportMap("BrokenMesaDetail_Normal", true, false);
        var mesaAO = ImportMap("BrokenMesaDetail_AO", false, false);
        var sourceScene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        var root = Root(sourceScene);
        var terrain = Terrain(root);
        int slot = RockSlot(terrain, originalMesa);
        var old = MainBlock(root);
        var originalBounds = BoundsOf(old);
        if (Mathf.Abs(originalBounds.size.x - 34) > 2 || Mathf.Abs(originalBounds.size.z - 24) > 2 ||
            originalBounds.size.y < 10 || originalBounds.size.y > 16)
            throw new InvalidOperationException("Unexpected old refinery footprint: " + originalBounds.size);
        var expectedScale = new Vector2(1200f / 145f, 1200f / 80f);
        var expectedOffset = new Vector2(-475f / 145f, -745f / 80f);
        if ((originalMesa.GetTextureScale("_BaseMap") - expectedScale).sqrMagnitude > .00001f ||
            (originalMesa.GetTextureOffset("_BaseMap") - expectedOffset).sqrMagnitude > .00001f)
            throw new InvalidOperationException("BrokenMesa UV transform changed; do not misalign the bake.");

        // Explicit material variants, as in the proven V18 diagnostic. Every mode
        // keeps the exact same albedo/geometry and only changes the two bake maps.
        var buildingTemplate = new Material(shader) { name = "Refinery roof" };
        buildingTemplate.SetColor("_BaseColor", Color.white); buildingTemplate.SetTexture("_BaseMap", roofBase);
        buildingTemplate.SetFloat("_Metallic", 0); buildingTemplate.SetFloat("_Smoothness", .11f);
        var roofModes = Variants("Roof", buildingTemplate, roofNormal, roofAO);
        Object.DestroyImmediate(buildingTemplate);
        var rockModes = Variants("Mesa", originalMesa, mesaNormal, mesaAO);
        var concrete = Solid("Concrete", shader, new Color(.53f,.49f,.42f));
        var dusty = Solid("Dusty footing", shader, new Color(.45f,.40f,.33f));
        var steel = Solid("Dark steel", shader, new Color(.31f,.32f,.30f));
        var pillars = Solid("Pillars", shader, new Color(.60f,.55f,.47f));
        AssetDatabase.SaveAssets();

        if (!EditorSceneManager.SaveScene(sourceScene, Destination, true)) throw new InvalidOperationException("Cannot copy V17.");
        var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        root = Root(scene); root.name = "Frontier practical bake";
        terrain = Terrain(root); slot = RockSlot(terrain, originalMesa);
        old = MainBlock(root);
        var parent = old.parent;
        var center = old.position;
        // Original module is unrotated; nothing may spill outside its old pad.
        var bounds = BoundsOf(old);
        if (Quaternion.Angle(old.rotation, Quaternion.identity) > .1f || Mathf.Abs(center.x - 65) > .1f || Mathf.Abs(center.z - 30) > .1f)
            throw new InvalidOperationException("Unexpected refinery transform.");
        var roof = PlaceBuilding(scene, parent, center, bounds.min.y, model, roofModes[3], concrete, dusty, steel, pillars);
        Object.DestroyImmediate(old.gameObject);
        var materials = terrain.sharedMaterials; materials[slot] = rockModes[3]; terrain.sharedMaterials = materials;
        EditorUtility.SetDirty(terrain);
        // Retire the old comparison only in the copied scene, before adding V19's own.
        var oldPreview = root.GetComponent<FrontierHeroBakePreview>();
        if (oldPreview != null) Object.DestroyImmediate(oldPreview);
        root.AddComponent<FrontierPracticalBakePreview>().Configure(roof, terrain, slot, roofModes, rockModes);
        var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
        var camera = scene.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
        root.GetComponent<ArtLabLandscapeFlight>().Configure(pipeline, player.transform, camera, player.GetComponent<FlightController>(),
            player.GetComponent<Afterburner>(), 600, "V19 / Practical Bake Test");
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save V19.");
        Selection.activeGameObject = player;
        return "V19 ready: same V17 terrain and roads; one roof and BrokenMesa material slot. B cycles the two new bakes. " + report[1] + " | " + report[2];
    }

    private static GameObject Root(Scene scene) => scene.GetRootGameObjects().Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
    private static MeshRenderer Terrain(GameObject root) => root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Scorched terrain");
    private static Transform MainBlock(GameObject root)
    {
        var hero = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "01 - hero refinery / V14A");
        // The existing main block is a prefab root; tolerate Unity's optional name suffix.
        return hero.Cast<Transform>().Single(t => t.name.StartsWith("Refinery_MainBlock", StringComparison.Ordinal) ||
            (Mathf.Abs(t.position.x - 65) < .1f && Mathf.Abs(t.position.z - 30) < .1f));
    }
    private static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Missing renderer on " + root.name);
        var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    private static int RockSlot(MeshRenderer r, Material expected)
    {
        var m = r.GetComponent<MeshFilter>().sharedMesh;
        if (m == null || !m.isReadable || m.uv.Length != m.vertexCount || m.tangents.Length != m.vertexCount)
            throw new InvalidOperationException("V17 terrain needs UVs and tangents for normal mapping.");
        var slots = r.sharedMaterials;
        if (slots.Length != m.subMeshCount || slots.Count(s => s == expected) != 1)
            throw new InvalidOperationException("Expected exactly one BrokenMesa terrain material slot.");
        return Array.IndexOf(slots, expected);
    }
    private static GameObject ImportRoof()
    {
        string path = Art + "/RefineryRoof.fbx";
        var imp = AssetImporter.GetAtPath(path) as ModelImporter ?? throw new InvalidOperationException("Missing roof FBX importer.");
        imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.importNormals = ModelImporterNormals.Import;
        imp.importTangents = ModelImporterTangents.CalculateMikk; imp.importAnimation = false; imp.importCameras = false;
        imp.importLights = false; imp.isReadable = true; imp.addCollider = false;
        imp.meshCompression = ModelImporterMeshCompression.Off; imp.SaveAndReimport();
        var model = Require<GameObject>(path); var filter = model.GetComponentsInChildren<MeshFilter>(true).Single(); var mesh = filter.sharedMesh;
        if (mesh == null || mesh.triangles.Length != 6 || mesh.uv.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount)
            throw new InvalidOperationException("Expected a two-triangle roof with valid UVs and tangent basis.");
        return model;
    }
    private static Texture2D ImportMap(string id, bool normal, bool srgb)
    {
        string path = Art + "/" + id + ".png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new InvalidOperationException("Missing importer: " + path);
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.convertToNormalmap = false; imp.sRGBTexture = srgb; imp.mipmapEnabled = true; imp.isReadable = false;
        imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Trilinear; imp.anisoLevel = 4;
        imp.alphaSource = TextureImporterAlphaSource.None; imp.maxTextureSize = 2048;
        imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.SaveAndReimport();
        return Require<Texture2D>(path);
    }
    private static Material[] Variants(string name, Material source, Texture2D normal, Texture2D ao)
    {
        var variants = new Material[4]; string[] suffix = { "Base", "AO", "Normal", "Full" };
        for (int i = 0; i < variants.Length; i++)
        {
            bool useAO = (i & 1) != 0, useNormal = (i & 2) != 0;
            var m = new Material(source) { name = name + "_" + suffix[i] };
            m.SetTexture("_OcclusionMap", useAO ? ao : null); m.SetFloat("_OcclusionStrength", useAO ? 1f : 0f);
            m.SetTexture("_BumpMap", useNormal ? normal : null); m.SetFloat("_BumpScale", useNormal ? 1f : 0f);
            // URP Lit shares the base UV transform. Match it explicitly for both maps.
            foreach (string p in new[] { "_BumpMap", "_OcclusionMap" })
            { m.SetTextureScale(p, source.GetTextureScale("_BaseMap")); m.SetTextureOffset(p, source.GetTextureOffset("_BaseMap")); }
            if (useAO) m.EnableKeyword("_OCCLUSIONMAP"); else m.DisableKeyword("_OCCLUSIONMAP");
            if (useNormal) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            variants[i] = SaveMaterial(m);
        }
        return variants;
    }
    private static Material Solid(string name, Shader shader, Color color)
    {
        var m = new Material(shader) { name = name }; m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .07f);
        return SaveMaterial(m);
    }
    private static Material SaveMaterial(Material m)
    {
        string path = Output + "/" + m.name + ".mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (old == null) { AssetDatabase.CreateAsset(m, path); return m; }
        EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); EditorUtility.SetDirty(old); return old;
    }
    private static MeshRenderer PlaceBuilding(Scene scene, Transform parent, Vector3 center, float baseY,
        GameObject model, Material roofMaterial, Material concrete, Material dusty, Material steel, Material pillars)
    {
        var group = new GameObject("V19 refinery process building").transform; group.SetParent(parent, false);
        group.position = new Vector3(center.x, baseY, center.z);
        Cube(group, "Footing", new Vector3(0,.35f,0), new Vector3(34,.7f,24), dusty);
        Cube(group, "Process hall", new Vector3(0,5.3f,0), new Vector3(30,9.2f,20), concrete);
        Cube(group, "Roof slab", new Vector3(0,10.15f,0), new Vector3(30,.5f,20), concrete);
        foreach (float z in new[] { -10.15f, 10.15f })
        {
            Cube(group, "Dark upper window band", new Vector3(0,7.4f,z), new Vector3(28,2.2f,.38f), steel);
            foreach (float x in new[] { -12f,-6f,0f,6f,12f })
                Cube(group, "Concrete buttress", new Vector3(x,5.2f,z), new Vector3(1.15f,9.0f,1.15f), pillars);
        }
        foreach (float x in new[] { -15.1f,15.1f })
            Cube(group, "End service doors", new Vector3(x,3f,0), new Vector3(.4f,5.4f,5.8f), steel);
        var ob = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        if (ob == null) throw new InvalidOperationException("Cannot instantiate roof FBX.");
        ob.transform.SetParent(group, false);
        var r = ob.GetComponentsInChildren<MeshRenderer>(true).Single(); var b = r.bounds;
        if (b.size.x < .001f || b.size.z < .001f) throw new InvalidOperationException("Invalid roof axes.");
        ob.transform.localScale *= 30f / b.size.x; b = r.bounds;
        if (Mathf.Abs(b.size.z - 20) > .1f || b.size.y > .05f) throw new InvalidOperationException("Expected horizontal 30 x 20 roof: " + b.size);
        var mf = r.GetComponent<MeshFilter>();
        if (mf.transform.TransformDirection(mf.sharedMesh.normals[0]).y < .98f) throw new InvalidOperationException("Expected upward roof normals.");
        ob.transform.position += new Vector3(center.x - b.center.x, baseY + 10.43f - b.center.y, center.z - b.center.z);
        r.sharedMaterial = roofMaterial; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(r); PrefabUtility.RecordPrefabInstancePropertyModifications(ob.transform);
        return r;
    }
    private static void Cube(Transform parent, string name, Vector3 local, Vector3 scale, Material material)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name; o.transform.SetParent(parent, false);
        o.transform.localPosition = local; o.transform.localScale = scale;
        var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
        Object.DestroyImmediate(o.GetComponent<Collider>());
    }
    private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
}
