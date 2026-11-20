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

// V14B: surface-only experiment on the green V14A showcase. No edits to source scenes or assets.
public static class ConfigureFrontierSurfacePass
{
    private const string Source = "Assets/_Project/Scenes/FrontierHeroShowcase.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierSurfacePass.unity";
    private const string Output = "Assets/_Project/Generated/FrontierSurfacePass";
    private const string SourceColor = "Assets/_Project/Generated/ScorchedFrontier/GroundColor.png";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const int GroundSize = 4096, RoadWidth = 256, RoadLength = 2048;
    private const float Half = 600f;
    private static readonly string[] Roads = { "Main highway", "Industrial spur", "Main highway shoulder", "Industrial spur shoulder" };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        Require<SceneAsset>(Source);
        var pipeline = Require<UniversalRenderPipelineAsset>(Pipeline);
        if (!File.Exists(SourceColor)) throw new InvalidOperationException("Missing baseline ground PNG: " + SourceColor);
        if (!AssetDatabase.IsValidFolder(Output))
            AssetDatabase.CreateFolder("Assets/_Project/Generated", "FrontierSurfacePass");
        if (!AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Missing output folder.");

        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        var sourceRoot = scene.GetRootGameObjects().Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
        Validate(sourceRoot);
        if (!EditorSceneManager.SaveScene(scene, Destination, true)) throw new InvalidOperationException("Cannot copy V14A showcase.");
        scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
        root.name = "Frontier surface pass";
        var ground = FindRenderer(root, "Scorched terrain");
        var preview = root.GetComponent<ArtLabLandscapeFlight>();
        var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
        var camera = scene.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
        preview.Configure(pipeline, player.transform, camera, player.GetComponent<FlightController>(),
            player.GetComponent<Afterburner>(), Half, "V14B / Surface & composition");
        EditorUtility.SetDirty(preview);

        try
        {
            EditorUtility.DisplayProgressBar("V14B", "Baking terrain, yards and worn surfaces", .15f);
            var color = BakeGround();
            ground.sharedMaterial = SaveMaterial("Ground", ground.sharedMaterial, color);
            EditorUtility.SetDirty(ground);
            EditorUtility.DisplayProgressBar("V14B", "Baking asphalt and shoulders", .7f);
            var asphalt = BakeRoad(false);
            var shoulder = BakeRoad(true);
            var roadMaterial = SaveMaterial("WornAsphalt", FindRenderer(root, Roads[0]).sharedMaterial, asphalt);
            var edgeMaterial = SaveMaterial("DustShoulder", FindRenderer(root, Roads[2]).sharedMaterial, shoulder);
            foreach (string name in Roads)
            {
                var renderer = FindRenderer(root, name);
                var filter = renderer.GetComponent<MeshFilter>();
                bool edge = name.EndsWith(" shoulder", StringComparison.Ordinal);
                bool branch = name.StartsWith("Industrial", StringComparison.Ordinal);
                filter.sharedMesh = SaveRoadUV(filter.sharedMesh, name, edge, branch);
                renderer.sharedMaterial = edge ? edgeMaterial : roadMaterial;
                EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save surface study.");
            Selection.activeGameObject = player;
            return "V14B ready. V14A hero models and bake comparison retained; 4K color PNG, concrete yard, cracked asphalt and dusty shoulders. Dynamic shadows unchanged.";
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void Validate(GameObject root)
    {
        if (root.GetComponent<FrontierHeroBakePreview>() == null ||
            !root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "01 - hero refinery / V14A"))
            throw new InvalidOperationException("Expected green V14A showcase. Generate hero kit first.");
        var ground = FindRenderer(root, "Scorched terrain");
        var mesh = ground.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null || mesh.vertexCount != 241 * 241 || mesh.uv.Length != mesh.vertexCount)
            throw new InvalidOperationException("Unexpected map terrain mesh/UVs.");
        foreach (var name in Roads)
        {
            var road = FindRenderer(root, name);
            var m = road.GetComponent<MeshFilter>()?.sharedMesh;
            int stride = name.EndsWith(" shoulder", StringComparison.Ordinal) ? 6 : 5;
            if (m == null || !m.isReadable || m.vertexCount % stride != 0 || road.sharedMaterial == null)
                throw new InvalidOperationException("Unexpected road ribbon: " + name);
        }
        if (ground.sharedMaterial == null) throw new InvalidOperationException("Missing ground material.");
    }

    // Preserve the original unique macro color map (terrain/slope/crater information).
    // Add world-scale detail, desaturation and integrated yard surfaces, not floating patch meshes.
    private static Texture2D BakeGround()
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        try
        {
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(SourceColor))) throw new InvalidOperationException("Invalid baseline ground PNG.");
            var old = source.GetPixels32(); int width = source.width, height = source.height;
            var colors = new Color32[GroundSize * GroundSize];
            Color concrete = new Color32(145, 139, 129, 255);
            Color joint = new Color32(99, 91, 81, 255);
            Color oil = new Color32(87, 77, 66, 255);
            Color dust = new Color32(178, 145, 110, 255);
            for (int z = 0; z < GroundSize; z++)
            {
                if (z % 256 == 0) EditorUtility.DisplayProgressBar("V14B", "Baking terrain color", .15f + .50f * z / GroundSize);
                float v = (z + .5f) / GroundSize, wz = v * 2 * Half - Half;
                for (int x = 0; x < GroundSize; x++)
                {
                    float u = (x + .5f) / GroundSize, wx = u * 2 * Half - Half;
                    Color c = Sample(old, width, height, u, v);
                    float luminance = .2126f * c.r + .7152f * c.g + .0722f * c.b;
                    c = Color.Lerp(c, new Color(luminance, luminance, luminance, 1), .13f);
                    float mid = Noise(wx, wz, .09f, 49, 12), grit = Noise(wx, wz, .55f, 93, 4);
                    c *= Mathf.Lerp(.92f, 1.08f, mid) * Mathf.Lerp(.96f, 1.04f, grit);
                    // Broken crusts are sparse and larger than subpixel scratches.
                    float crust = Smooth(.58f, .79f, Noise(wx,wz,.18f,23,61));
                    c = Color.Lerp(c, c * .84f, crust * .34f);
                    float rd = Mathf.Min(Mathf.Abs(wz - RoadZ(wx)), wz >= BranchStart - 6 ? Mathf.Abs(wx - BranchX(wz)) : 9999f);
                    float roadDust = (1 - Smooth(5.2f, 11f, rd)) * (.38f + .22f * mid);
                    c = Color.Lerp(c, dust, roadDust);

                    // Main refinery yard: actual V14A footprint x~35..130, z~-12..60.
                    // Other settlements get quiet compacted dirt only; terrain remains dominant.
                    float yard = (1 - Smooth(-1.5f, 6f, RectDistance(wx, wz, 82, 23, 49, 33) + (mid-.5f)*3.5f)) * Smooth(4.8f, 6.8f, rd);
                    float access = (1 - Smooth(3.5f, 5.5f, Mathf.Abs(wx - 82))) * Smooth(RoadZ(82)-1, RoadZ(82)+3, wz) * (1-Smooth(-9,-5,wz));
                    yard = Mathf.Max(yard, access * Smooth(4.5f, 6f, rd));
                    if (yard > .001f)
                    {
                        float sx = wx + .65f * (mid - .5f), sz = wz + .65f * (mid - .5f);
                        float dx = Mathf.Abs(Mathf.Repeat(sx + 6, 12) - 6), dz = Mathf.Abs(Mathf.Repeat(sz + 5, 10) - 5);
                        float seam = 1 - Smooth(.20f, .65f, Mathf.Min(dx,dz));
                        float slab = Noise(Mathf.Floor((wx+6)/12), Mathf.Floor((wz+5)/10), .37f, 19, 42);
                        Color slabColor = concrete * Mathf.Lerp(.90f, 1.09f, slab) * Mathf.Lerp(.94f, 1.06f, grit);
                        slabColor = Color.Lerp(slabColor, joint, seam * (.4f + .35f * mid));
                        // Local weathering and oil stains, not baked occlusion or sun shadows.
                        float stain = Mathf.Max(Spot(wx,wz,74,1,7,5), Mathf.Max(Spot(wx,wz,94,1,7,5), Spot(wx,wz,116,3,6,5)));
                        stain = Mathf.Max(stain, Spot(wx,wz,92,19,10,3)*.7f);
                        slabColor = Color.Lerp(slabColor, oil, stain * (.32f+.18f*mid));
                        slabColor = Color.Lerp(slabColor, dust, Smooth(.58f,.8f,mid)*.30f);
                        c = Color.Lerp(c, slabColor, yard * .94f);
                    }
                    else
                    {
                        float pad = Mathf.Max(Pad(wx,wz,-119,-116,41,29), Mathf.Max(Pad(wx,wz,226,230,46,36),
                            Mathf.Max(Pad(wx,wz,-255,202,41,29),Pad(wx,wz,334,-243,34,24))));
                        c = Color.Lerp(c, dust * .88f, pad * .24f * (.65f+.35f*mid));
                    }
                    c.a = 1; colors[z*GroundSize+x] = c;
                }
            }
            return ImportPng("GroundSurface.png", colors, GroundSize, GroundSize, TextureWrapMode.Clamp);
        }
        finally { Object.DestroyImmediate(source); }
    }

    // Long thin repeating strip. The longitudinal coordinate wraps seamlessly;
    // dust eats into the outer edge and cracks are coarse enough for the flight camera.
    private static Texture2D BakeRoad(bool shoulder)
    {
        var colors = new Color32[RoadWidth * RoadLength];
        Color asphalt = new Color32(102, 98, 91, 255), dust = new Color32(156, 127, 99, 255), crack = new Color32(60, 57, 53, 255);
        for (int z=0;z<RoadLength;z++) for(int x=0;x<RoadWidth;x++)
        {
            float u=(x+.5f)/RoadWidth,v=(z+.5f)/RoadLength;
            float wx=(u-.5f)*8.5f, wz=v*80f;
            float macro=LoopNoise(wx,wz,.13f, 51, 92), grit=LoopNoise(wx,wz,1.3f, 12, 84);
            float edge=Smooth(.69f,.99f,Mathf.Abs(u*2-1)+(macro-.5f)*.28f);
            Color c=asphalt*Mathf.Lerp(.88f,1.12f,macro)*Mathf.Lerp(.94f,1.06f,grit);
            float warp=LoopNoise(wx,wz,.41f,37,11)-.5f;
            float split=Mathf.Abs(Mathf.Repeat(wz+warp*1.6f+wx*.5f,16f)-8f);
            float fracture=(1-Smooth(.09f,.32f,split))*Smooth(.30f,.62f,macro);
            // Thin branching cracks and patches, rather than a repeating bright checkerboard.
            float across=Mathf.Abs(wx-1.0f-2.0f*(macro-.5f));
            fracture=Mathf.Max(fracture,(1-Smooth(.08f,.27f,across))*Smooth(.53f,.77f,macro)*.65f);
            c=Color.Lerp(c,crack,fracture*.65f);
            c=Color.Lerp(c,dust,edge*.83f+(.10f*(1-edge))*Smooth(.49f,.77f,macro));
            if(shoulder)c=Color.Lerp(dust*.90f,dust,Mathf.Clamp01(edge*.65f+macro*.35f))*Mathf.Lerp(.92f,1.08f,grit);
            c.a=1;colors[z*RoadWidth+x]=c;
        }
        return ImportPng(shoulder?"RoadShoulder.png":"WornAsphalt.png",colors,RoadWidth,RoadLength,TextureWrapMode.Repeat);
    }

    private static Mesh SaveRoadUV(Mesh source, string name, bool shoulder, bool branch)
    {
        // V13 Ribbon emits rows of across+1 vertices, even for gaps. Preserve every triangle.
        var mesh = Object.Instantiate(source);
        var verts = mesh.vertices; int stride=shoulder?6:5;
        var uv = new Vector2[verts.Length];
        for(int i=0;i<verts.Length;i++)
        {
            int row=i/stride, start=row*stride;
            Vector3 p=(verts[start]+verts[start+stride-1])*.5f;
            uv[i]=new Vector2((i%stride)/(float)(stride-1),(branch?p.z:p.x)/80f);
        }
        mesh.uv=uv; mesh.RecalculateTangents();mesh.name=name.Replace(' ','_')+"_UV";
        string path=Output+"/"+mesh.name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
    }
    private static Texture2D ImportPng(string name,Color32[] pixels,int width,int height,TextureWrapMode wrap)
    {
        var tex=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
        string path=Output+"/"+name;
        try { tex.SetPixels32(pixels);tex.Apply(false,false);File.WriteAllBytes(path,ImageConversion.EncodeToPNG(tex)); }
        finally { Object.DestroyImmediate(tex); }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter ?? throw new InvalidOperationException("Missing importer: "+path);
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.None;
        importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.wrapMode=wrap;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=Mathf.Max(width,height);importer.isReadable=false;
        importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();return Require<Texture2D>(path);
    }
    private static Material SaveMaterial(string name, Material baseline, Texture2D texture)
    {
        var m=new Material(baseline){name=name};m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",texture);
        m.SetTextureScale("_BaseMap",Vector2.one);m.SetTextureOffset("_BaseMap",Vector2.zero);
        string path=Output+"/"+name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(old==null){AssetDatabase.CreateAsset(m,path);return m;}
        EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;
    }
    private static Color Sample(Color32[] a,int w,int h,float u,float v)
    {
        float px=Mathf.Clamp(u*w-.5f,0,w-1),py=Mathf.Clamp(v*h-.5f,0,h-1);
        int x=(int)px,y=(int)py,x1=Mathf.Min(x+1,w-1),y1=Mathf.Min(y+1,h-1);
        return Color.LerpUnclamped(Color.LerpUnclamped(a[y*w+x],a[y*w+x1],px-x),Color.LerpUnclamped(a[y1*w+x],a[y1*w+x1],px-x),py-y);
    }
    private static float Noise(float x,float z,float f,float a,float b)=>Mathf.PerlinNoise(x*f+a,z*f+b);
    private static float Smooth(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
    private static float LoopNoise(float x,float z,float f,float a,float b)=>Mathf.Lerp(Noise(x,z,f,a,b),Noise(x,z-80,f,a,b),Smooth(0,80,z));
    private static float RectDistance(float x,float z,float cx,float cz,float hx,float hz)=>Mathf.Max(Mathf.Abs(x-cx)-hx,Mathf.Abs(z-cz)-hz);
    private static float Pad(float x,float z,float cx,float cz,float hx,float hz)=>1-Smooth(-3,8,RectDistance(x,z,cx,cz,hx,hz));
    private static float Spot(float x,float z,float cx,float cz,float rx,float rz){float u=(x-cx)/rx,v=(z-cz)/rz;return Mathf.Exp(-2*(u*u+v*v));}
    private static float RoadZ(float x)=>-43f+23f*Mathf.Sin(x*.011f+.25f)+10f*Mathf.Sin(x*.021f+.7f);
    private static float BranchX(float z)=>190f+17f*Mathf.Sin(z*.0085f+.4f);
    private static float BranchStart=>RoadZ(BranchX(-20f))+2f;
    private static MeshRenderer FindRenderer(GameObject root,string name)=>root.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name==name);
    private static T Require<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing asset: "+path);
}
