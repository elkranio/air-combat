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
public static class ConfigureArtLabLandscapeFlight
{
    private const string Source = "Assets/_Project/Scenes/Sandbox.unity";
    private const string Destination = "Assets/_Project/Scenes/ArtLabV10LandscapeFlight.unity";
    private const string Output = "Assets/_Project/Generated/ArtLabV10LandscapeFlight";
    private const string Pipeline = "Assets/_Project/Generated/ArtLabV8Lighting/ArtLabLighting_URP.asset";
    private const string Trees = "Assets/_Project/Art/Nature/Trees/";
    private const string Ground = "Assets/_Project/Art/Terrain/GroundSet/";
    private const float Half = 600f, Step = 5f, FlightY = 48f;
    private const int N = 240, Sector = 100, Sectors = 12;
    private const int TextureSize = 3072;

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
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "ArtLabV10LandscapeFlight");
        if (!AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Cannot create V10 output.");

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

        var root = new GameObject("ArtLab V10 Landscape Flight");
        var field = new HeightField();
        var mesh = SaveMesh("Terrain", field.BuildMesh());
        var color = BakeColor(field, grass, dry);
        var material = SaveMaterial(shader, color);
        AddMesh(root.transform, "Terrain", mesh, material);
        var forest = new GameObject("Forest sectors").transform;
        forest.SetParent(root.transform, false);
        int treeCount, batches;
        // The FBX importers intentionally disable Read/Write. Make editor-only readable clones.
        var copies = new Dictionary<Mesh, Mesh>();
        try
        {
            var treeTemplates = templates.Select(t => new TreeTemplate(t, copies)).ToArray();
            treeCount = BuildForest(field, forest, treeTemplates, out batches);
        }
        finally
        {
            foreach (var copy in copies.Values) Object.DestroyImmediate(copy);
        }
        root.AddComponent<ArtLabLandscapeFlight>().Configure(pipeline, player.transform, camera, flight, boost, Half);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save V10.");
        Selection.activeGameObject = player;
        return $"V10 ready: 1200 x 1200, ground {field.Min:F1}..{field.Max:F1}, {treeCount} trees in {batches} renderers. WASD/Space, 1/2/3 camera, R reset, H legend.";
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
            c*=Mathf.Lerp(.96f,1.045f,macro);c.a=1;
            colors[z*TextureSize+x]=c;
        }
        var tex=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,true,false) { name="GroundColor",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear };
        tex.SetPixels32(colors);tex.Apply(true,false);
        return SaveAsset("GroundColor.asset",tex);
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
        public TreeTemplate(GameObject asset, Dictionary<Mesh, Mesh> copies)
        {
            bool hasBounds=false;var bounds=new Bounds();
            foreach(var r in asset.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.enabled) continue;
                var filter=r.GetComponent<MeshFilter>();var m=filter!=null?filter.sharedMesh:null;
                if(m==null) throw new InvalidOperationException("Missing tree mesh: "+asset.name+" / "+r.name);
                if (!m.isReadable)
                {
                    if (!copies.TryGetValue(m, out var readable)) copies[m] = readable = ReadableCopy(m);
                    m = readable;
                }
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
                    Parts.Add((m,sub,mats[sub],matrix));
                }
            }
            if(!hasBounds||Parts.Count==0||bounds.size.y<.01f||Mathf.Max(bounds.size.x,bounds.size.z)<.01f)
                throw new InvalidOperationException("Invalid tree geometry: "+asset.name);
            Bounds=bounds;
        }
    }
    private static Mesh ReadableCopy(Mesh source)
    {
        // Editor API can read imported meshes without toggling ModelImporter.isReadable.
        using (var reads = MeshUtility.AcquireReadOnlyMeshData(source))
        {
            var src = reads[0];
            var writes = Mesh.AllocateWritableMeshData(1);
            bool applied = false;
            try
            {
                var dst = writes[0];
                dst.SetVertexBufferParams(src.vertexCount, source.GetVertexAttributes());
                for (int stream = 0; stream < src.vertexBufferCount; stream++)
                    dst.GetVertexData<byte>(stream).CopyFrom(src.GetVertexData<byte>(stream));
                if (src.indexFormat == IndexFormat.UInt16)
                {
                    var indices = src.GetIndexData<ushort>();
                    dst.SetIndexBufferParams(indices.Length, src.indexFormat);
                    dst.GetIndexData<ushort>().CopyFrom(indices);
                }
                else
                {
                    var indices = src.GetIndexData<uint>();
                    dst.SetIndexBufferParams(indices.Length, src.indexFormat);
                    dst.GetIndexData<uint>().CopyFrom(indices);
                }
                dst.subMeshCount = src.subMeshCount;
                for (int i = 0; i < src.subMeshCount; i++)
                    dst.SetSubMesh(i, src.GetSubMesh(i), MeshUpdateFlags.DontRecalculateBounds);
                var copy = new Mesh { name = source.name + " (editor copy)" };
                Mesh.ApplyAndDisposeWritableMeshData(writes, copy, MeshUpdateFlags.DontRecalculateBounds);
                applied = true;
                copy.bounds = source.bounds;
                return copy;
            }
            finally { if (!applied) writes.Dispose(); }
        }
    }
    private static int BuildForest(HeightField field,Transform owner,TreeTemplate[] trees,out int batchCount)
    {
        var rng=new System.Random(50910);
        float Next(float min,float max)=>Mathf.Lerp(min,max,(float)rng.NextDouble());
        var buckets=new Dictionary<Material,List<CombineInstance>>[Sectors*Sectors];
        int count=0;const float spacing=6f;
        for(int iz=0;iz<200;iz++) for(int ix=0;ix<200;ix++)
        {
            float x=-Half+3+(ix+(iz%2)*.5f)*spacing+Next(-1.5f,1.5f);
            float z=-Half+3+iz*spacing+Next(-1.5f,1.5f);
            if(Mathf.Abs(x)>Half-8||Mathf.Abs(z)>Half-8)continue;
            float h=field.At(x,z),slope=field.Slope(x,z);
            // Preserve actual open hilltops; broad coherent patches, with scattered edges.
            float grove=Noise(x,z,.0085f,39,72)*.76f+Noise(x,z,.025f,82,27)*.24f;
            float density=Smooth(.48f,.70f,grove)*(1-Smooth(20f,28f,h))*(1-Smooth(.48f,.8f,slope));
            if(density<.05f||Next(0,1)>density*.88f)continue;
            float species=Noise(x,z,.025f,41,7);
            int variant=species>.61f ? (Next(0,1)<.7f?1:0) : (Next(0,1)<.37f?2:0);
            var t=trees[variant];float width=Next(7f,10f)*(variant==1?.83f:1f);
            float scale=width/Mathf.Max(t.Bounds.size.x,t.Bounds.size.z);
            float vertical=Mathf.Min(scale,10f/t.Bounds.size.y);
            // Sink the base a little into the real rendered terrain, including slopes.
            var position=new Vector3(x,h-t.Bounds.min.y*vertical-.20f,z);
            var matrix=Matrix4x4.TRS(position,Quaternion.Euler(0,Next(0,360),0),new Vector3(scale,vertical,scale));
            int sx=Mathf.Clamp((int)((x+Half)/Sector),0,Sectors-1),sz=Mathf.Clamp((int)((z+Half)/Sector),0,Sectors-1);
            int index=sz*Sectors+sx;
            var bucket=buckets[index]??(buckets[index]=new Dictionary<Material,List<CombineInstance>>());
            foreach(var part in t.Parts)
            {
                if(!bucket.TryGetValue(part.material,out var list))bucket[part.material]=list=new List<CombineInstance>();
                list.Add(new CombineInstance{mesh=part.mesh,subMeshIndex=part.sub,transform=matrix*part.transform});
            }
            count++;
        }
        batchCount=0;
        for(int i=0;i<buckets.Length;i++)
        {
            if(buckets[i]==null)continue;
            int j=0;
            foreach(var batch in buckets[i].OrderBy(k=>AssetDatabase.GetAssetPath(k.Key)).ThenBy(k=>k.Key.name))
            {
                string name=$"Trees_{i:D3}_{j++:D2}";
                var m=new Mesh{indexFormat=IndexFormat.UInt32};
                m.CombineMeshes(batch.Value.ToArray(),true,true,false);m.RecalculateBounds();
                AddMesh(owner,name,SaveMesh(name,m),batch.Key);batchCount++;
            }
        }
        return count;
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
