using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Offline color bake: five repeatable textures + broad placement masks.
// No new shader, runtime terrain generation, or changes to Sandbox/v2 assets.
public static class ConfigureArtLabGround
{
    private const string V2 = "Assets/_Project/Scenes/ArtLabV2.unity";
    private const string V3 = "Assets/_Project/Scenes/ArtLabV3.unity";
    private const string Tiles = "Assets/_Project/Art/Terrain/GroundSet";
    private const string Output = "Assets/_Project/Generated/ArtLabV3";
    private const float HalfX = 250f, HalfZ = 210f; // v2 terrain UV extents
    private const float TileWorldSize = 38f;
    private const int Width = 2048, Height = 1720;
    private static readonly string[] Names = { "GrassBase", "GrassDry", "Dirt", "ShoreSand", "AirbaseGround" };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(V2) == null) throw new InvalidOperationException("Missing ArtLabV2 scene.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Missing URP Lit.");
        EnsureFolder(Output);
        var samplers = new Tile[Names.Length];
        var imported = new Texture2D[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            string path = Tiles + "/" + Names[i] + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
            if (!importer.sRGBTexture || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
                importer.filterMode != FilterMode.Trilinear || importer.maxTextureSize != 512 || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear; importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            imported[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (imported[i] == null) throw new InvalidOperationException("Could not import " + path);
            // Decode source bytes independently, without keeping imported textures CPU-readable.
            var cpu = new Texture2D(2, 2, TextureFormat.RGB24, false, false);
            try
            {
                if (!ImageConversion.LoadImage(cpu, File.ReadAllBytes(Path.GetFullPath(path))))
                    throw new InvalidOperationException("Invalid PNG: " + path);
                samplers[i] = new Tile(cpu.GetPixels32(), cpu.width, cpu.height);
            }
            finally { UnityEngine.Object.DestroyImmediate(cpu); }
            MakeMaterial(Tiles + "/Ground_" + Names[i] + ".mat", shader, imported[i], Color.white, Vector2.one);
        }
        var map = BakeMap(samplers);
        var terrainMaterial = MakeMaterial(Output + "/GroundComposite.mat", shader, map, Color.white, Vector2.one);
        var scene = EditorSceneManager.OpenScene(V2, OpenSceneMode.Single);
        // Save as copy before modifying any scene object. Re-running regenerates v3 only.
        if (!EditorSceneManager.SaveScene(scene, V3, true)) throw new InvalidOperationException("Could not copy ArtLabV2 to V3.");
        scene = EditorSceneManager.OpenScene(V3, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single(o => o.name == "ArtLab V2");
        var preview = root.GetComponent<ArtLabLightingPreview>();
        if (preview == null) throw new InvalidOperationException("Missing v2 lighting preview.");
        var renderers = new List<MeshRenderer>(); var originals = new List<Material>(); var variants = new List<Material>();
        MeshRenderer Find(string name) => root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == name);
        void Add(MeshRenderer renderer, Material material)
        { renderers.Add(renderer); originals.Add(renderer.sharedMaterial); variants.Add(material); }
        Add(Find("Terrain mesh"), terrainMaterial);
        // Preserve slab geometry and seam lines. Use world-scaled tiles even on long roads.
        foreach (string name in new[] { "Apron", "Bridge deck", "Access road", "Taxiway north", "Taxiway south" })
        {
            var renderer = Find(name); var scale = renderer.transform.lossyScale;
            var tiling = new Vector2(Mathf.Abs(scale.x) / TileWorldSize, Mathf.Abs(scale.z) / TileWorldSize);
            bool road = name.Contains("road") || name.Contains("Taxiway");
            var tint = road ? new Color32(207, 209, 193, 255) : new Color32(240, 239, 228, 255);
            Add(renderer, MakeMaterial(Output + "/" + name.Replace(' ', '_') + ".mat", shader, imported[4], tint, tiling));
        }
        root.name = "ArtLab V3";
        preview.ConfigureGroundComparison(renderers.ToArray(), originals.ToArray(), variants.ToArray());
        EditorUtility.SetDirty(preview);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save ArtLabV3.");
        Selection.activeGameObject = root;
        return "ArtLab v3 ready. G: original/new ground, L: lighting, H: legend. Sandbox and ArtLabV2 unchanged.";
    }

    private sealed class Tile
    {
        private readonly Color32[] pixels;
        private readonly int w, h;
        public Tile(Color32[] pixels, int w, int h) { this.pixels = pixels; this.w = w; this.h = h; }
        public Color Sample(float u, float v)
        {
            // Half texel offset and wrap both neighbors for continuous bilinear repeat.
            float fx = Mathf.Repeat(u, 1) * w - .5f, fy = Mathf.Repeat(v, 1) * h - .5f;
            int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(fy);
            float tx = fx-x, ty = fy-y;
            int x0=(x+w)%w, x1=(x0+1)%w, y0=(y+h)%h, y1=(y0+1)%h;
            return Color.LerpUnclamped(Color.LerpUnclamped(pixels[y0*w+x0],pixels[y0*w+x1],tx),
                Color.LerpUnclamped(pixels[y1*w+x0],pixels[y1*w+x1],tx),ty);
        }
    }

    private static Texture2D BakeMap(Tile[] tiles)
    {
        var colors = new Color32[Width * Height];
        for (int z = 0; z < Height; z++)
        for (int x = 0; x < Width; x++)
        {
            float wx = Mathf.Lerp(-HalfX, HalfX, (x+.5f)/Width), wz = Mathf.Lerp(-HalfZ, HalfZ, (z+.5f)/Height);
            float u = wx / TileWorldSize, v = wz / TileWorldSize;
            // A few coherent landscape regions instead of high-frequency blotches.
            float broad = Mathf.PerlinNoise(wx*.008f+13,wz*.008f+27);
            float edge = Mathf.PerlinNoise(wx*.029f+51,wz*.029f+9);
            float dry = Smooth(.37f,.73f,broad*.78f+edge*.22f) * .78f;
            Color c = Color.Lerp(tiles[0].Sample(u,v),tiles[1].Sample(u*.89f+.31f,v*.89f+.47f),dry);
            float outside = Mathf.Max(Mathf.Abs(wx+43)-50,Mathf.Abs(wz+7)-75);
            float apronEdge = 1-Smooth(1,9,outside+(edge-.5f)*6);
            float roadEdge = 1-Smooth(4.5f,10,Mathf.Abs(wz-78)+(edge-.5f)*5);
            float bare = Mathf.Max(apronEdge*.8f,roadEdge*.7f);
            // Sparse exposed ground beside the airfield and on the open southern slope.
            bare = Mathf.Max(bare, Hill(wx,wz,-105,-54,17,12)*Smooth(.36f,.68f,edge)*.85f);
            bare = Mathf.Max(bare, Hill(wx,wz,44,-87,18,14)*Smooth(.40f,.68f,edge)*.65f);
            if (bare>.001f) c = Color.Lerp(c,tiles[2].Sample(u+.19f,v+.11f),bare);
            if (apronEdge>.001f) c=Color.Lerp(c,tiles[4].Sample(u+.63f,v+.21f),apronEdge*.35f);
            // Follows the exact v2 river geometry, with irregular width on land.
            float river = 102+9.5f*Mathf.Sin(wz*.025f+.8f)+4.2f*Mathf.Sin(wz*.052f-1.1f);
            float width = 11.8f+2*Mathf.Sin(wz*.031f+1.4f)+1.1f*Mathf.Sin(wz*.084f);
            float shore = Mathf.Abs(wx-river)-width;
            float sand = 1-Smooth(1,7.5f,shore+(edge-.5f)*4.5f);
            if (sand>.001f) c=Color.Lerp(c,tiles[3].Sample(u+.43f,v+.72f),sand);
            // Broad, subdued forest-floor tint; trees remain the primary contrast.
            float woods=Mathf.Max(Hill(wx,wz,-122,25,42,42),Mathf.Max(Hill(wx,wz,69,-66,37,42),Hill(wx,wz,156,4,40,45)));
            float shade=1-.17f*Smooth(.08f,.8f,woods)*(1-sand)*(1-apronEdge);
            c.r*=shade; c.g*=shade; c.b*=Mathf.Lerp(1f,shade,.65f); c.a=1;
            colors[z*Width+x]=c;
        }
        string path=Output+"/GroundComposite.asset";
        var map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool fresh=map==null;
        if(fresh) map=new Texture2D(Width,Height,TextureFormat.RGBA32,true,false);
        else if (map.width!=Width || map.height!=Height) map.Reinitialize(Width,Height,TextureFormat.RGBA32,true);
        map.name="GroundComposite"; map.wrapMode=TextureWrapMode.Clamp; map.filterMode=FilterMode.Trilinear;
        map.SetPixels32(colors);map.Apply(true,false);
        if(fresh) AssetDatabase.CreateAsset(map,path); else EditorUtility.SetDirty(map);
        return map;
    }
    private static float Smooth(float a,float b,float t) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
    private static float Hill(float x,float z,float cx,float cz,float rx,float rz)
    { float u=(x-cx)/rx,v=(z-cz)/rz;return Mathf.Exp(-2*(u*u+v*v)); }
    private static Material MakeMaterial(string path,Shader shader,Texture2D texture,Color tint,Vector2 tiling)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}
        m.shader=shader;m.SetColor("_BaseColor",tint);m.SetTexture("_BaseMap",texture);m.SetTextureScale("_BaseMap",tiling);
        m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.05f);EditorUtility.SetDirty(m);return m;
    }
    private static void EnsureFolder(string path)
    {
        var parts=path.Split('/');string parent=parts[0];
        for(int i=1;i<parts.Length;i++){string next=parent+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(parent,parts[i]);parent=next;}
    }
}
