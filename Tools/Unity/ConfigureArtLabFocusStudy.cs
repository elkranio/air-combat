using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Copy the tested V11 scene and share its mesh and instance data. No landscape rebake.
public static class ConfigureArtLabFocusStudy
{
    private const string Source = "Assets/_Project/Scenes/ArtLabV11Readability.unity";
    private const string Destination = "Assets/_Project/Scenes/ArtLabV12FocusStudy.unity";
    private const string Input = "Assets/_Project/Generated/ArtLabV11Readability/";
    private const string Output = "Assets/_Project/Generated/ArtLabV12FocusStudy";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        Require<SceneAsset>(Source);
        var treeMaterial = Require<Material>(Input + "TreePaletteMuted.mat");
        var outlineShader = Require<Shader>("Assets/_Project/Shaders/ArtLabAircraftOutline.shader");
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "ArtLabV12FocusStudy");
        if (!AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Cannot create V12 output.");

        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, Destination, true)) throw new InvalidOperationException("Cannot copy V11.");
        scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var root = Root(scene, "ArtLab V11 Readability");
        var player = Root(scene, "Player");
        var sun = Root(scene, "Directional Light").GetComponent<Light>();
        var visual = player.transform.Find("JetVisual");
        var ground = root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Terrain");
        var forest = root.GetComponentInChildren<ArtLabForestInstances>(true);
        var flightStudy = root.GetComponent<ArtLabLandscapeFlight>();
        if (visual == null || visual.Find("KestrelModel") == null || sun == null || forest == null || flightStudy == null || ground.sharedMaterial == null)
            throw new InvalidOperationException("Unexpected V11 scene setup.");

        var softGround = new Material(ground.sharedMaterial) { name = "GroundSoft" };
        softGround.SetTexture("_BaseMap", SoftGroundTexture());
        softGround = SaveMaterial(softGround);
        var softTrees = new Material(treeMaterial) { name = "TreePaletteSoft", enableInstancing = true };
        softTrees.SetTexture("_BaseMap", SoftTreeTexture());
        softTrees.SetColor("_EmissionColor", new Color(.038f, .046f, .020f));
        softTrees.EnableKeyword("_EMISSION");
        softTrees = SaveMaterial(softTrees);
        var outlineMaterial = new Material(outlineShader) { name = "AircraftOutline" };
        outlineMaterial.SetColor("_OutlineColor", new Color(.055f, .075f, .095f, 1f));
        outlineMaterial.SetFloat("_OutlineWidth", .22f);
        outlineMaterial = SaveMaterial(outlineMaterial);

        // Snapshot the existing renderers before adding any outline children; never recurse into a new copy.
        var renderers = visual.Find("KestrelModel").GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var outlines = new List<GameObject>();
        var materials = new List<ArtLabFocusStudy.AircraftMaterials>();
        var accentCopies = new Dictionary<Material, Material>();
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var original = renderer.sharedMaterials;
            var accent = (Material[])original.Clone();
            bool changed = false;
            for (int i = 0; i < original.Length; i++)
            {
                var mat = original[i];
                if (mat == null || !mat.name.StartsWith("V11_Kestrel_", StringComparison.Ordinal) || !mat.HasProperty("_BaseColor")) continue;
                if (!accentCopies.TryGetValue(mat, out var copy))
                {
                    copy = new Material(mat) { name = "V12_" + mat.name.Substring(4) };
                    var col = Color.Lerp(mat.GetColor("_BaseColor"), new Color(.80f,.90f,1f), .20f);
                    copy.SetColor("_BaseColor", col);
                    accentCopies[mat] = copy = SaveMaterial(copy);
                }
                accent[i] = copy; changed = true;
            }
            if (changed) materials.Add(new ArtLabFocusStudy.AircraftMaterials { renderer = renderer, baseline = original, accent = accent });

            var shell = new GameObject("V12 Outline");
            shell.transform.SetParent(renderer.transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var r = shell.AddComponent<MeshRenderer>();
            r.sharedMaterials = Enumerable.Repeat(outlineMaterial, filter.sharedMesh.subMeshCount).ToArray();
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shell.SetActive(false); outlines.Add(shell);
        }
        if (outlines.Count == 0 || materials.Count == 0) throw new InvalidOperationException("Missing Kestrel meshes or V11 hull materials.");

        root.AddComponent<ArtLabFocusStudy>().Configure(sun, ground, softGround, softTrees, forest, visual, outlines.ToArray(), materials.ToArray());
        var settings = new SerializedObject(flightStudy);
        settings.FindProperty("studyTitle").stringValue = "ArtLab v12 / Focus study";
        settings.ApplyModifiedPropertiesWithoutUndo();
        root.name = "ArtLab V12 Focus Study";
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save V12.");
        Selection.activeGameObject = player;
        return "V12 ready. B: V11 baseline / soft nature / aircraft focus. WASD/Space; 1/2/3 camera; R reset; H legend.";
    }

    // Decode PNG bytes independently of importer Read/Write settings. Box filter 4x4 into a small PNG.
    private static Texture2D SoftGroundTexture()
    {
        var src = Decode(Input + "GroundColor.png");
        var result = new Texture2D(512, 512, TextureFormat.RGBA32, false, false);
        try
        {
            var input = src.GetPixels32(); var pixels = new Color32[512*512];
            for (int z=0; z<512; z++) for (int x=0; x<512; x++)
            {
                int x0=x*src.width/512, x1=Mathf.Max(x0+1,(x+1)*src.width/512);
                int z0=z*src.height/512, z1=Mathf.Max(z0+1,(z+1)*src.height/512);
                Color sum=Color.clear; int count=0;
                for(int j=z0;j<z1;j++) for(int i=x0;i<x1;i++) {sum+=(Color)input[j*src.width+i];count++;}
                var c=Color.Lerp(sum/count,new Color(.49f,.53f,.33f,1),.22f);c.a=1;
                pixels[z*512+x]=c;
            }
            result.SetPixels32(pixels);result.Apply(false,false);
            return ImportPng("GroundSoft.png",result.EncodeToPNG(),512,true);
        }
        finally { Object.DestroyImmediate(src);Object.DestroyImmediate(result); }
    }

    private static Texture2D SoftTreeTexture()
    {
        var tex=Decode(Input+"TreePaletteMuted.png");
        try
        {
            var pixels=tex.GetPixels32();
            for(int i=0;i<pixels.Length;i++)
            {
                Color c=pixels[i];
                var target=c.g>c.r?new Color(.40f,.45f,.31f,1):new Color(.40f,.37f,.29f,1);
                c=Color.Lerp(c,target,.42f);c.a=1;pixels[i]=c;
            }
            tex.SetPixels32(pixels);tex.Apply(false,false);
            return ImportPng("TreePaletteSoft.png",tex.EncodeToPNG(),Mathf.NextPowerOfTwo(Mathf.Max(tex.width,tex.height)),false);
        }
        finally { Object.DestroyImmediate(tex); }
    }

    private static Texture2D Decode(string path)
    {
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
        if(ImageConversion.LoadImage(tex,File.ReadAllBytes(Path.GetFullPath(path)))) return tex;
        Object.DestroyImmediate(tex);throw new InvalidOperationException("Invalid PNG: "+path);
    }

    private static Texture2D ImportPng(string filename, byte[] bytes, int size, bool ground)
    {
        string path=Output+"/"+filename;
        File.WriteAllBytes(Path.GetFullPath(path),bytes);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Missing PNG importer: "+path);
        importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.None;
        importer.sRGBTexture=true;importer.isReadable=false;importer.maxTextureSize=Mathf.Max(32,size);
        importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;
        importer.mipmapEnabled=ground;importer.filterMode=ground?FilterMode.Trilinear:FilterMode.Point;
        importer.textureCompression=ground?TextureImporterCompression.Compressed:TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();return Require<Texture2D>(path);
    }

    private static Material SaveMaterial(Material material)
    {
        string path=Output+"/"+material.name+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing==null){AssetDatabase.CreateAsset(material,path);return material;}
        EditorUtility.CopySerialized(material,existing);Object.DestroyImmediate(material);EditorUtility.SetDirty(existing);return existing;
    }
    private static GameObject Root(Scene scene,string name)=>scene.GetRootGameObjects().Single(g=>g.name==name);
    private static T Require<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing asset: "+path);
}
