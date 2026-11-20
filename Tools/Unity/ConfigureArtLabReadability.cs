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

// Reproducible static visual flight study. Never edits the source scene or shared assets.
public static class ConfigureArtLabReadability
{
    private const string Source = "Assets/_Project/Scenes/Sandbox.unity";
    private const string Destination = "Assets/_Project/Scenes/ArtLabV11Readability.unity";
    private const string Output = "Assets/_Project/Generated/ArtLabV11Readability";
    private const string Pipeline = "Assets/_Project/Generated/ArtLabV8Lighting/ArtLabLighting_URP.asset";
    private const string Trees = "Assets/_Project/Art/Nature/Trees/";
    private const string Ground = "Assets/_Project/Art/Terrain/GroundSet/";
    private const float Half = 600f, Step = 5f, FlightY = 48f;
    private const int N = 240, Sector = 100, Sectors = 12;
    private const int TextureSize = 2048;

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        Require<SceneAsset>(Source);
        var pipeline = Require<UniversalRenderPipelineAsset>(Pipeline);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
        var templates = new[] { "Tree_Round_01", "Tree_Pine_01", "Tree_Cluster_01" }
            .Select(n => Require<GameObject>(Trees + n + ".prefab")).ToArray();
        var grass = new Tile(Ground + "GrassBase.png");
        var dry = new Tile(Ground + "GrassDry.png");
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "ArtLabV11Readability");
        if (!AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Cannot create V11 output.");

        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, Destination, true)) throw new InvalidOperationException("Cannot copy Sandbox.");
        scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var player = Root(scene, "Player");
        var cameraOwner = Root(scene, "Main Camera");
        var lightOwner = Root(scene, "Directional Light");
        var flight = player.GetComponent<FlightController>();
        var boost = player.GetComponent<Afterburner>();
        var follow = cameraOwner.GetComponent<TopDownCameraFollow>();
        var camera = cameraOwner.GetComponent<Camera>();
        var sun = lightOwner.GetComponent<Light>();
        var visual = player.transform.Find("JetVisual");
        if (flight == null || boost == null || follow == null || camera == null || sun == null || visual == null || visual.Find("KestrelModel") == null)
            throw new InvalidOperationException("Unexpected Sandbox flight setup.");
        if (visual.GetComponent<AfterburnerVfx>() == null) throw new InvalidOperationException("Missing afterburner VFX.");

        // Remove combat and flat-ground shadow scripts before discarding their targets.
        foreach (var b in player.GetComponentsInChildren<MonoBehaviour>(true))
            if (b != null && !(b is FlightController) && !(b is Afterburner) && !(b is AfterburnerVfx)) Object.DestroyImmediate(b);
        foreach (var b in cameraOwner.GetComponents<MonoBehaviour>())
            if (b != null && !(b is TopDownCameraFollow) && !(b is UniversalAdditionalCameraData)) Object.DestroyImmediate(b);
        foreach (var go in scene.GetRootGameObjects())
            if (go != player && go != cameraOwner && go != lightOwner) Object.DestroyImmediate(go);
        player.transform.SetPositionAndRotation(new Vector3(0, FlightY, -100), Quaternion.identity);
        foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        BrightenAircraft(visual);
        var settings = new SerializedObject(follow);
        settings.FindProperty("pitch").floatValue = 65f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        camera.rect = new Rect(0, 0, 1, 1);
        camera.orthographic = true; camera.orthographicSize = 80f; camera.farClipPlane = 300f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(105, 141, 80, 255);
        camera.useOcclusionCulling = false;
        var rotation = Quaternion.Euler(65f, 0f, 0f);
        camera.transform.SetPositionAndRotation(player.transform.position - rotation * Vector3.forward * (88f / Mathf.Sin(65f * Mathf.Deg2Rad)), rotation);

        // Same accepted contrast lighting as V9. Only the copy's scene settings change.
        sun.transform.rotation = Quaternion.Euler(38f, -38f, 0f);
        sun.color = new Color32(255, 225, 184, 255); sun.intensity = 1.38f;
        sun.shadows = LightShadows.Soft; sun.shadowStrength = .75f;
        sun.shadowBias = .035f; sun.shadowNormalBias = .18f;
        sun.lightmapBakeType = LightmapBakeType.Realtime;
        RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color32(72, 93, 121, 255);
        RenderSettings.fog = false; RenderSettings.skybox = null;

        var root = new GameObject("ArtLab V11 Readability");
        var field = new HeightField();
        var mesh = SaveMesh("Terrain", field.BuildMesh());
        var color = BakeColor(field, grass, dry);
        var material = SaveMaterial(shader, color);
        AddMesh(root.transform, "Terrain", mesh, material);
        var forest = new GameObject("Instanced forest sectors");
        forest.transform.SetParent(root.transform, false);
        var forestMaterial = MakeForestMaterial();
        var treeTemplates = templates.Select(t => new TreeTemplate(t, forestMaterial)).ToArray();
        int treeCount = BuildForest(field, forest.AddComponent<ArtLabForestInstances>(), camera, treeTemplates, out int batches);
        root.AddComponent<ArtLabLandscapeFlight>().Configure(pipeline, player.transform, camera, flight, boost, Half, "ArtLab v11 / Readability");
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save V11.");
        Selection.activeGameObject = player;
        return $"V11 ready: 1200 x 1200, ground {field.Min:F1}..{field.Max:F1}, {treeCount} trees in {batches} instanced batches. WASD/Space, 1/2/3 camera, R reset, H legend.";
    }

    private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
    private static float Noise(float x, float z, float scale, float ox, float oz) => Mathf.PerlinNoise(x*scale+ox, z*scale+oz);
    private static float Height(float x, float z)
    {
        // Domain warp makes the hills flow instead of reading as a grid of round bumps.
        float wx = x + (Noise(x,z,.0031f,21,92)-.5f)*100f;
        float wz = z + (Noise(x,z,.0034f,68,14)-.5f)*100f;
        float mass = Noise(wx,wz,.0060f,115,37);
        float broad = Smooth(.23f,.77f,mass);
        float shoulders = Noise(wx,wz,.012f,34,71)-.5f;
        float valley = Smooth(.42f,.73f,Noise(wx,wz,.0038f,82,11));
        // Deliberately stronger than the former 500-unit airfield composition.
        return Mathf.Clamp(-4f + 35f*broad + 8f*shoulders - 7f*valley, -5f, 33f);
    }

    private sealed class HeightField
    {
        private readonly Vector3[] vertices = new Vector3[(N+1)*(N+1)];
        public float Min { get; } = float.PositiveInfinity;
        public float Max { get; } = float.NegativeInfinity;
        public HeightField()
        {
            for (int z=0; z<=N; z++) for (int x=0; x<=N; x++)
            {
                float wx = x*Step-Half, wz = z*Step-Half, h = Height(wx,wz);
                vertices[z*(N+1)+x] = new Vector3(wx,h,wz);
                Min = Mathf.Min(Min,h); Max = Mathf.Max(Max,h);
            }
        }
        public float At(float x,float z)
        {
            float gx=Mathf.Clamp((x+Half)/Step,0,N), gz=Mathf.Clamp((z+Half)/Step,0,N);
            int ix=Mathf.Min(N-1,Mathf.FloorToInt(gx)), iz=Mathf.Min(N-1,Mathf.FloorToInt(gz));
            float u=gx-ix,v=gz-iz; int a=iz*(N+1)+ix,b=a+N+1;
            float h00=vertices[a].y,h10=vertices[a+1].y,h01=vertices[b].y,h11=vertices[b+1].y;
            return u+v<=1 ? h00+(h10-h00)*u+(h01-h00)*v : h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
        }
        public float Slope(float x,float z)
        {
            float dx=(At(x+Step,z)-At(x-Step,z))/(2*Step);
            float dz=(At(x,z+Step)-At(x,z-Step))/(2*Step);
            return Mathf.Sqrt(dx*dx+dz*dz);
        }
        public Mesh BuildMesh()
        {
            var uv = new Vector2[vertices.Length]; var tris = new int[N*N*6]; int k=0;
            for(int z=0;z<=N;z++) for(int x=0;x<=N;x++) uv[z*(N+1)+x]=new Vector2(x/(float)N,z/(float)N);
            for(int z=0;z<N;z++) for(int x=0;x<N;x++)
            { int a=z*(N+1)+x,b=a+N+1; tris[k++]=a;tris[k++]=b;tris[k++]=a+1;tris[k++]=a+1;tris[k++]=b;tris[k++]=b+1; }
            var mesh=new Mesh{vertices=vertices,uv=uv,triangles=tris};
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
    }

    private sealed class Tile
    {
        private readonly Color32[] pixels;
        private readonly int width,height;
        public Tile(string path)
        {
            var t=new Texture2D(2,2,TextureFormat.RGB24,false,false);
            try
            {
                if (!ImageConversion.LoadImage(t,File.ReadAllBytes(Path.GetFullPath(path)))) throw new InvalidOperationException("Invalid texture: "+path);
                width=t.width;height=t.height;pixels=t.GetPixels32();
            }
            finally { Object.DestroyImmediate(t); }
        }
        public Color Sample(float u,float v)
        {
            float x=Mathf.Min(Mathf.Repeat(u,1f)*width,width-.0001f), y=Mathf.Min(Mathf.Repeat(v,1f)*height,height-.0001f);
            int ix=Mathf.Min((int)x,width-1),iy=Mathf.Min((int)y,height-1),jx=(ix+1)%width,jy=(iy+1)%height;
            return Color.LerpUnclamped(Color.LerpUnclamped(pixels[iy*width+ix],pixels[iy*width+jx],x-ix),
                Color.LerpUnclamped(pixels[jy*width+ix],pixels[jy*width+jx],x-ix),y-iy);
        }
    }

    private static Texture2D BakeColor(HeightField field,Tile grass,Tile dry)
    {
        var colors=new Color32[TextureSize*TextureSize];
        for(int z=0;z<TextureSize;z++) for(int x=0;x<TextureSize;x++)
        {
            float wx=(x+.5f)*2*Half/TextureSize-Half, wz=(z+.5f)*2*Half/TextureSize-Half;
            float macro=Noise(wx,wz,.009f,27,63), mix=Noise(wx,wz,.018f,9,83);
            // Blend two differently oriented/scaled tile samples; suppress obvious repetition.
            Color a=grass.Sample(wx/31f,wz/31f);
            Color b=grass.Sample((wx*.73f-wz*.68f)/43f+.37f,(wx*.68f+wz*.73f)/43f+.61f);
            Color c=Color.Lerp(a,b,.35f+.3f*mix);
            Color d=dry.Sample((wx*.89f+wz*.45f)/38f+.19f,(-wx*.45f+wz*.89f)/38f+.43f);
            c=Color.Lerp(c,d,Smooth(.42f,.8f,macro*.8f+mix*.2f)*.40f);
            float slope=Smooth(.12f,.55f,field.Slope(wx,wz));
            c=Color.Lerp(c,new Color(.55f,.50f,.32f,1),slope*.10f);
            // Reduce tile-scale noise; retain broad grass variations and the existing relief.
            var muted = Color.Lerp(new Color(.45f,.51f,.31f), new Color(.52f,.55f,.34f), macro);
            c=Color.Lerp(c,muted,.28f);
            c*=Mathf.Lerp(.98f,1.025f,macro);c.a=1;
            colors[z*TextureSize+x]=c;
        }
        var tex=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,false,false);
        try
        {
            tex.SetPixels32(colors);tex.Apply(false,false);
            return ImportPng("GroundColor.png",tex.EncodeToPNG(),TextureSize,true);
        }
        finally { Object.DestroyImmediate(tex); }
    }

    private static Texture2D ImportPng(string filename,byte[] bytes,int size,bool ground)
    {
        string path=Output+"/"+filename;
        File.WriteAllBytes(Path.GetFullPath(path),bytes);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Missing PNG importer: "+path);
        importer.textureType=TextureImporterType.Default;
        importer.alphaSource=TextureImporterAlphaSource.None;
        importer.sRGBTexture=true;importer.isReadable=false;
        importer.maxTextureSize=size;importer.npotScale=TextureImporterNPOTScale.None;
        importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=ground;
        importer.filterMode=ground?FilterMode.Trilinear:FilterMode.Point;
        importer.textureCompression=ground?TextureImporterCompression.Compressed:TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return Require<Texture2D>(path);
    }

    private static Material MakeForestMaterial()
    {
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
        Texture2D palette;
        try
        {
            if(!ImageConversion.LoadImage(tex,File.ReadAllBytes(Path.GetFullPath(Trees+"TreePalette.png"))))
                throw new InvalidOperationException("Invalid tree palette.");
            var pixels=tex.GetPixels32();
            for(int i=0;i<pixels.Length;i++)
            {
                Color c=pixels[i];
                // Compress the foliage tones towards a quiet mid-green, without blurring geometry.
                var target=c.g>c.r?new Color(.36f,.43f,.29f,c.a):new Color(.40f,.36f,.28f,c.a);
                pixels[i]=Color.Lerp(c,target,c.g>c.r?.38f:.22f);
            }
            tex.SetPixels(Array.ConvertAll(pixels,p=>(Color)p));tex.Apply(false,false);
            palette=ImportPng("TreePaletteMuted.png",tex.EncodeToPNG(),Mathf.Max(32,Mathf.NextPowerOfTwo(Mathf.Max(tex.width,tex.height))),false);
        }
        finally { Object.DestroyImmediate(tex); }
        var m=new Material(Require<Material>(Trees+"TreePalette.mat")){name="TreePaletteMuted",enableInstancing=true};
        m.SetTexture("_BaseMap",palette);
        // Very small fill on trees only: prevents black faceted crowns from dominating the player.
        m.SetColor("_EmissionColor",new Color(.018f,.025f,.010f));m.EnableKeyword("_EMISSION");
        m.SetFloat("_Smoothness",.03f);
        return SaveAsset("TreePaletteMuted.mat",m);
    }

    private static void BrightenAircraft(Transform visual)
    {
        var copies=new Dictionary<Material,Material>();
        foreach(var r in visual.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(!r.enabled||!r.gameObject.activeInHierarchy)continue;
            var mats=r.sharedMaterials;bool changed=false;
            for(int i=0;i<mats.Length;i++)
            {
                var source=mats[i];if(source==null)continue;
                Color target;
                switch(source.name)
                {
                    case "Kestrel_Hull":target=new Color32(125,177,218,255);break;
                    case "Kestrel_Upper":target=new Color32(94,148,195,255);break;
                    case "Kestrel_Panels":target=new Color32(78,111,143,255);break;
                    default:continue;
                }
                if(!copies.TryGetValue(source,out var material))
                {
                    material=new Material(source){name="V11_"+source.name};
                    if(material.HasProperty("_BaseColor"))
                    {
                        Color color=Color.Lerp(material.GetColor("_BaseColor"),target,.48f);
                        material.SetColor("_BaseColor",color);
                        if(material.HasProperty("_EmissionColor"))
                        {material.SetColor("_EmissionColor",color*.10f);material.EnableKeyword("_EMISSION");}
                    }
                    else if(material.HasProperty("_Color"))material.SetColor("_Color",Color.Lerp(material.GetColor("_Color"),target,.48f));
                    else throw new InvalidOperationException("Missing Kestrel base color: "+source.name);
                    copies[source]=material=SaveAsset(material.name+".mat",material);
                }
                mats[i]=material;changed=true;
            }
            if(changed){r.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
        }
        if(copies.Count==0)throw new InvalidOperationException("Kestrel hull materials not found.");
    }

    private static Material SaveMaterial(Shader shader,Texture2D texture)
    {
        var m=new Material(shader){name="Ground"};
        m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",texture);
        m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.05f);
        return SaveAsset("Ground.mat",m);
    }

    private sealed class TreeTemplate
    {
        public readonly List<(Mesh mesh,int sub,Material material,Matrix4x4 transform)> Parts = new List<(Mesh,int,Material,Matrix4x4)>();
        public readonly Bounds Bounds;
        public TreeTemplate(GameObject asset, Material material)
        {
            bool hasBounds=false;var bounds=new Bounds();
            foreach(var r in asset.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.enabled) continue;
                var filter=r.GetComponent<MeshFilter>();var m=filter!=null?filter.sharedMesh:null;
                if(m==null) throw new InvalidOperationException("Missing tree mesh: "+asset.name+" / "+r.name);
                var matrix=asset.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
                var b=m.bounds;
                for(int i=0;i<8;i++)
                {
                    var p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(!hasBounds){bounds=new Bounds(p,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(p);
                }
                var mats=r.sharedMaterials;
                for(int sub=0;sub<m.subMeshCount;sub++)
                {
                    if(sub>=mats.Length||mats[sub]==null) throw new InvalidOperationException("Missing tree material: "+asset.name);
                    Parts.Add((m,sub,material,matrix));
                }
            }
            if(!hasBounds||Parts.Count==0||bounds.size.y<.01f||Mathf.Max(bounds.size.x,bounds.size.z)<.01f)
                throw new InvalidOperationException("Invalid tree geometry: "+asset.name);
            Bounds=bounds;
        }
    }
    private static int BuildForest(HeightField field,ArtLabForestInstances forest,Camera camera,TreeTemplate[] trees,out int batchCount)
    {
        var rng=new System.Random(50910);
        float Next(float min,float max)=>Mathf.Lerp(min,max,(float)rng.NextDouble());
        // Store only the placements for each source model and sector, never cloned vertices.
        var buckets=new List<Matrix4x4>[Sectors*Sectors*trees.Length];
        int count=0;const float spacing=6f;
        for(int iz=0;iz<200;iz++) for(int ix=0;ix<200;ix++)
        {
            // Fixed number of random samples per candidate: subsequent density tuning stays deterministic.
            float x=-Half+3+(ix+(iz%2)*.5f)*spacing+Next(-1.5f,1.5f);
            float z=-Half+3+iz*spacing+Next(-1.5f,1.5f);
            float occupancy=Next(0,1),kind=Next(0,1),size=Next(7,10),yaw=Next(0,360);
            if(Mathf.Abs(x)>Half-8||Mathf.Abs(z)>Half-8)continue;
            float h=field.At(x,z),slope=field.Slope(x,z);
            float grove=Noise(x,z,.0085f,39,72)*.82f+Noise(x,z,.025f,82,27)*.18f;
            // More open ground, but still dense at the centers of the forest patches.
            float density=Smooth(.51f,.72f,grove)*(1-Smooth(18f,26f,h))*(1-Smooth(.42f,.75f,slope));
            if(density<.05f||occupancy>density*.78f)continue;
            float species=Noise(x,z,.025f,41,7);
            int variant=species>.61f?(kind<.7f?1:0):(kind<.37f?2:0);
            var t=trees[variant];float width=size*(variant==1?.83f:1f);
            float scale=width/Mathf.Max(t.Bounds.size.x,t.Bounds.size.z);
            float vertical=Mathf.Min(scale,10f/t.Bounds.size.y);
            var position=new Vector3(x,h-t.Bounds.min.y*vertical-.20f,z);
            var matrix=Matrix4x4.TRS(position,Quaternion.Euler(0,yaw,0),new Vector3(scale,vertical,scale));
            int sx=Mathf.Clamp((int)((x+Half)/Sector),0,Sectors-1),sz=Mathf.Clamp((int)((z+Half)/Sector),0,Sectors-1);
            int index=(sz*Sectors+sx)*trees.Length+variant;
            (buckets[index]??(buckets[index]=new List<Matrix4x4>())).Add(matrix);count++;
        }
        var batches=new List<ArtLabForestInstances.Batch>();
        // Conservative limit for nonuniform scales (both object-to-world and world-to-object matrices).
        const int limit=256;
        for(int i=0;i<buckets.Length;i++)
        {
            var bucket=buckets[i];if(bucket==null)continue;
            foreach(var part in trees[i%trees.Length].Parts)
            for(int start=0;start<bucket.Count;start+=limit)
            {
                int n=Mathf.Min(limit,bucket.Count-start);var matrices=new Matrix4x4[n];
                var bounds=new Bounds();bool first=true;var b=part.mesh.bounds;
                for(int j=0;j<n;j++)
                {
                    var m=matrices[j]=bucket[start+j]*part.transform;
                    for(int k=0;k<8;k++)
                    {
                        var point=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));
                        if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                    }
                }
                batches.Add(new ArtLabForestInstances.Batch{mesh=part.mesh,submesh=part.sub,material=part.material,matrices=matrices,bounds=bounds});
            }
        }
        forest.Configure(camera,batches.ToArray());batchCount=batches.Count;return count;
    }

    // Execute only after V11 is generated and playtested. The V10 source remains in Git history.
    public static string RetireV10()
    {
        const string oldScene="Assets/_Project/Scenes/ArtLabV10LandscapeFlight.unity";
        const string oldFolder="Assets/_Project/Generated/ArtLabV10LandscapeFlight";
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty||SceneManager.GetSceneAt(i).path==oldScene)
                throw new InvalidOperationException("Save scenes and open V11 before retirement.");
        Require<SceneAsset>(Destination);
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=Destination||!scene.GetRootGameObjects().Any(g=>g.GetComponentInChildren<ArtLabForestInstances>()!=null))
            throw new InvalidOperationException("Open the generated, successfully playtested V11 first.");
        if(EditorBuildSettings.scenes.Any(x=>x.path==oldScene))throw new InvalidOperationException("V10 is in build settings; cannot retire.");
        // Safety check: no unrelated scene, prefab, material or serialized asset may depend on V10 output.
        foreach(string path in AssetDatabase.GetAllAssetPaths())
        {
            if(!path.StartsWith("Assets/")||path==oldScene||path.StartsWith(oldFolder+"/"))continue;
            if(!path.EndsWith(".unity")&&!path.EndsWith(".prefab")&&!path.EndsWith(".mat")&&!path.EndsWith(".asset"))continue;
            if(AssetDatabase.GetDependencies(path,true).Any(d=>d==oldScene||d.StartsWith(oldFolder+"/")))
                throw new InvalidOperationException("V10 still referenced by "+path);
        }
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(oldScene)!=null&&!AssetDatabase.DeleteAsset(oldScene))
            throw new InvalidOperationException("Could not remove V10 scene.");
        if(AssetDatabase.IsValidFolder(oldFolder)&&!AssetDatabase.DeleteAsset(oldFolder))
            throw new InvalidOperationException("Could not remove V10 generated folder.");
        AssetDatabase.SaveAssets();
        return "V10 scene and generated assets retired. V11, Sandbox and V1-V9 preserved. No Git history rewritten.";
    }
    private static Mesh SaveMesh(string name,Mesh mesh){mesh.name=name;return SaveAsset(name+".asset",mesh);}
    private static T SaveAsset<T>(string filename,T asset) where T:Object
    {
        string path=Output+"/"+filename;var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing==null){AssetDatabase.CreateAsset(asset,path);return asset;}
        EditorUtility.CopySerialized(asset,existing);Object.DestroyImmediate(asset);EditorUtility.SetDirty(existing);return existing;
    }
    private static void AddMesh(Transform parent,string name,Mesh mesh,Material material)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
        r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
        r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
    }
    private static GameObject Root(Scene scene,string name)=>scene.GetRootGameObjects().Single(g=>g.name==name);
    private static T Require<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: "+path);
}
