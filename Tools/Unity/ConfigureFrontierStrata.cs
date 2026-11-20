using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// V17: replace the four detached V16 FBXs with locally refined, connected terrain.
// Every unchanged cell retains its original geometry; neighbouring cells are stitched
// with collinear edge subdivisions. No overlapping terrain surfaces or world-light bakes.
public static class ConfigureFrontierStrata
{
    private const string Source = "Assets/_Project/Scenes/FrontierEscarpments.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierStrata.unity";
    private const string Output = "Assets/_Project/Generated/FrontierStrata";
    private const string Data = "Resources/Blender/FrontierStrata.json";
    private const string Ground = "Assets/_Project/Generated/FrontierEscarpments/GroundRetouched.png";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const int N = 240, Sub = 4, Fine = N*Sub;
    private const float Half = 600f, Step = 5f, FineStep = Step/Sub;

    [Serializable] private sealed class Manifest { public int version; public float step; public Patch[] fields; }
    [Serializable] private sealed class Patch
    {
        public string name; public int x0,x1,z0,z1,nx,nz; public float[] heights;
        public int I0 => (x0+600)/5; public int I1 => (x1+600)/5;
        public int J0 => (z0+600)/5; public int J1 => (z1+600)/5;
        public bool InCell(int x,int z) => x>=I0 && x<I1 && z>=J0 && z<J1;
        public bool InFine(int x,int z) => x>=I0*Sub && x<=I1*Sub && z>=J0*Sub && z<=J1*Sub;
        public float FineHeight(int x,int z) => heights[(z-J0*Sub)*nx+x-I0*Sub];
        public float At(float x,float z)
        {
            float u=Mathf.Clamp((x-x0)/FineStep,0,nx-1),v=Mathf.Clamp((z-z0)/FineStep,0,nz-1);
            int i=Mathf.Min(nx-2,(int)u),j=Mathf.Min(nz-2,(int)v);
            return Tri(heights[j*nx+i],heights[j*nx+i+1],heights[(j+1)*nx+i],heights[(j+1)*nx+i+1],u-i,v-j);
        }
    }

    public static string Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first.");
        Require<SceneAsset>(Source);var pipeline=Require<UniversalRenderPipelineAsset>(Pipeline);
        var manifest=ReadManifest(Data);
        ValidateManifest(manifest);var patches=manifest.fields;
        if(!File.Exists(Ground))throw new InvalidOperationException("Missing V16 ground PNG.");
        var originalScene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
        var originalRoot=Root(originalScene);
        var originalTerrain=Terrain(originalRoot);
        var oldMesh=originalTerrain.sharedMesh;
        ValidateTerrain(originalTerrain);
        var before=oldMesh.vertices;
        var oldMaterial=originalTerrain.GetComponent<MeshRenderer>().sharedMaterial;
        if(oldMaterial==null||oldMaterial.GetTexture("_BaseMap")==null)throw new InvalidOperationException("Missing V16 ground material.");
        Group(originalRoot,"V16 coherent escarpments");
        ValidateInfrastructure(originalRoot,patches);
        if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder("Assets/_Project/Generated","FrontierStrata");
        try
        {
            EditorUtility.DisplayProgressBar("V17","Stitching continuous terrain",.1f);
            var mesh=Save("Terrain.asset",BuildMesh(before,patches));
            var materials=new Material[patches.Length+1];materials[0]=oldMaterial;
            var input=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
            try
            {
                if(!ImageConversion.LoadImage(input,File.ReadAllBytes(Ground))||input.width!=4096||input.height!=4096)
                    throw new InvalidOperationException("Expected the V16 4K ground texture.");
                var pixels=input.GetPixels32();
                for(int i=0;i<patches.Length;i++)
                {
                    EditorUtility.DisplayProgressBar("V17","Texturing "+patches[i].name,.25f+.55f*i/patches.Length);
                    materials[i+1]=BuildMaterial(oldMaterial,pixels,input.width,patches[i]);
                }
            }
            finally{Object.DestroyImmediate(input);}
            // All assets and geometry checks have succeeded before editing a scene copy.
            if(!EditorSceneManager.SaveScene(originalScene,Destination,true))throw new InvalidOperationException("Cannot copy V16.");
            var scene=EditorSceneManager.OpenScene(Destination,OpenSceneMode.Single);
            var root=Root(scene);root.name="Frontier strata";
            var terrain=Terrain(root);terrain.sharedMesh=mesh;
            terrain.GetComponent<MeshRenderer>().sharedMaterials=materials;
            Object.DestroyImmediate(Group(root,"V16 coherent escarpments").gameObject);
            // V16 already removed nature inside these footprints. Guard against rare overhangs
            // at their margins; keep every unrelated loose asset and its existing height.
            int removed=0;
            var natureOwners=new[]{Group(root,"Rock outcrops and dead trees"),Group(root,"V14A rock formations")};
            foreach(var owner in natureOwners)foreach(var child in owner.Cast<Transform>().ToArray())
            {
                if(child==null)continue;
                var renderers=child.GetComponentsInChildren<MeshRenderer>(true);
                if(renderers.Any(r=>IntersectsRaised(patches,r.bounds,.5f)))
                {Object.DestroyImmediate(child.gameObject);removed++;}
            }
            var player=scene.GetRootGameObjects().Single(g=>g.name=="Player");
            var camera=scene.GetRootGameObjects().Single(g=>g.name=="Main Camera").GetComponent<Camera>();
            root.GetComponent<ArtLabLandscapeFlight>().Configure(pipeline,player.transform,camera,player.GetComponent<FlightController>(),
                player.GetComponent<Afterburner>(),Half,"V17 / Integrated rock strata");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save V17.");
            Selection.activeGameObject=player;
            return $"V17 ready: {mesh.vertexCount} terrain vertices, four connected landforms, {removed} conflicting loose props removed. V16 source, roads, flight and real shadows preserved.";
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    private static float Tri(float a,float b,float c,float d,float u,float v)
        => u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
    private static float OldHeight(Vector3[] v,int x,int z)
    {
        int i=Mathf.Min(N-1,x/Sub),j=Mathf.Min(N-1,z/Sub);float u=(x-i*Sub)/(float)Sub,w=(z-j*Sub)/(float)Sub;
        int a=j*(N+1)+i,b=a+N+1;return Tri(v[a].y,v[a+1].y,v[b].y,v[b+1].y,u,w);
    }
    private static Mesh BuildMesh(Vector3[] original,Patch[] patches)
    {
        var verts=new List<Vector3>(original.Length+30000);var uv=new List<Vector2>(original.Length+30000);
        var ids=new Dictionary<int,int>(original.Length+30000);
        var sections=Enumerable.Range(0,patches.Length+1).Select(_=>new List<int>()).ToArray();
        var cells=new int[N,N];
        for(int p=0;p<patches.Length;p++)for(int z=patches[p].J0;z<patches[p].J1;z++)for(int x=patches[p].I0;x<patches[p].I1;x++)cells[x,z]=p+1;
        int Region(int x,int z)=>x>=0&&x<N&&z>=0&&z<N?cells[x,z]:0;
        int Vertex(int x,int z)
        {
            int key=z*(Fine+1)+x;if(ids.TryGetValue(key,out int id))return id;
            float h=OldHeight(original,x,z);
            foreach(var p in patches)if(p.InFine(x,z)){h+=p.FineHeight(x,z);break;}
            if(h>40f||float.IsNaN(h)||float.IsInfinity(h))throw new InvalidOperationException("Invalid terrain height/flight clearance.");
            id=verts.Count;verts.Add(new Vector3(x*FineStep-Half,h,z*FineStep-Half));uv.Add(new Vector2(x/(float)Fine,z/(float)Fine));ids.Add(key,id);return id;
        }
        void Quad(List<int> t,int x,int z,int d)
        {
            int a=Vertex(x,z),b=Vertex(x,z+d),c=Vertex(x+d,z),e=Vertex(x+d,z+d);
            t.Add(a);t.Add(b);t.Add(c);t.Add(c);t.Add(b);t.Add(e);
        }
        for(int z=0;z<N;z++)for(int x=0;x<N;x++)
        {
            int ix=x*Sub,iz=z*Sub,region=Region(x,z);
            if(region!=0)
            {
                for(int j=0;j<Sub;j++)for(int i=0;i<Sub;i++)Quad(sections[region],ix+i,iz+j,1);
                continue;
            }
            bool left=Region(x-1,z)!=0,upper=Region(x,z+1)!=0,right=Region(x+1,z)!=0,lower=Region(x,z-1)!=0;
            if(!(left||upper||right||lower)){Quad(sections[0],ix,iz,Sub);continue;}
            // Transition cell: the centre lies on the OLD diagonal, so a fan around it
            // remains on the original two planes even when an edge is subdivided.
            var ring=new List<int>(16);
            void Edge(int a,int b,int dx,int dz,bool refine)
            {int increment=refine?1:Sub;for(int k=0;k<Sub;k+=increment)ring.Add(Vertex(a+dx*k,b+dz*k));}
            Edge(ix,iz,0,1,left);Edge(ix,iz+Sub,1,0,upper);Edge(ix+Sub,iz+Sub,0,-1,right);Edge(ix+Sub,iz,-1,0,lower);
            int center=Vertex(ix+Sub/2,iz+Sub/2);
            for(int k=0;k<ring.Count;k++){sections[0].Add(ring[k]);sections[0].Add(ring[(k+1)%ring.Count]);sections[0].Add(center);}
        }
        ValidateTopology(verts,sections);
        var mesh=new Mesh{name="V17 stitched terrain",indexFormat=IndexFormat.UInt32};
        mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.subMeshCount=sections.Length;
        for(int i=0;i<sections.Length;i++)mesh.SetTriangles(sections[i],i,false);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }

    // Detect flipped/degenerate triangles, interior open edges, and accidental overlaps
    // before saving. Only the outer 1200 x 1200 rectangle may have boundary edges.
    private static void ValidateTopology(List<Vector3> v,List<int>[] sections)
    {
        var edges=new Dictionary<ulong,int>();double area=0;
        void Edge(int a,int b)
        {uint lo=(uint)Math.Min(a,b),hi=(uint)Math.Max(a,b);ulong k=((ulong)lo<<32)|hi;edges.TryGetValue(k,out int c);edges[k]=c+1;}
        foreach(var s in sections)for(int i=0;i<s.Count;i+=3)
        {
            int a=s[i],b=s[i+1],c=s[i+2];float y=Vector3.Cross(v[b]-v[a],v[c]-v[a]).y;
            if(y<=0)throw new InvalidOperationException("Terrain triangle has wrong winding or zero area.");
            area+=y*.5;Edge(a,b);Edge(b,c);Edge(c,a);
        }
        if(Math.Abs(area-1200d*1200d)>.01)throw new InvalidOperationException("Terrain projected area mismatch: "+area);
        foreach(var e in edges)
        {
            if(e.Value==2)continue;
            var a=v[(int)(e.Key>>32)];var b=v[(int)(e.Key&0xffffffffUL)];
            bool border=(a.x==b.x&&Mathf.Abs(a.x)==Half)||(a.z==b.z&&Mathf.Abs(a.z)==Half);
            if(e.Value!=1||!border)throw new InvalidOperationException("Terrain has an interior hole/overlap.");
        }
    }

    private static Material BuildMaterial(Material baseline,Color32[] ground,int size,Patch p)
    {
        // A separate local albedo per submesh, sampled from the ORIGINAL V16 ground.
        // Exactly the same terrain UV layout; no duplicated 4K map or overlay decal.
        int width=Mathf.NextPowerOfTwo(Mathf.CeilToInt((p.x1-p.x0)*3.5f));
        int height=Mathf.NextPowerOfTwo(Mathf.CeilToInt((p.z1-p.z0)*3.5f));
        var pixels=new Color32[width*height];
        Color sandstone=new Color32(169,143,118,255),mineral=new Color32(143,117,94,255),pale=new Color32(184,159,131,255);
        for(int j=0;j<height;j++)for(int i=0;i<width;i++)
        {
            float x=Mathf.Lerp(p.x0,p.x1,(i+.5f)/width),z=Mathf.Lerp(p.z0,p.z1,(j+.5f)/height);
            float h=p.At(x,z);
            float dx=(p.At(x+FineStep,z)-p.At(x-FineStep,z))/(2*FineStep);
            float dz=(p.At(x,z+FineStep)-p.At(x,z-FineStep))/(2*FineStep);
            float slope=Mathf.Sqrt(dx*dx+dz*dz);
            float warp=Noise(x,z,.07f,14,83),grain=Noise(x,z,.45f,61,17);
            // Sparse irregular bedding exposed on cliffs, no sinusoidal contour rings.
            float strata=Noise(x*.12f+warp*.7f,h*.25f+z*.035f,1f,35,17); // world/height variation
            float exposed=Smooth(1.3f,10,h)*(.58f+.42f*Smooth(.23f,.9f,slope));
            Color rock=Color.Lerp(sandstone,pale,warp*.42f);
            rock=Color.Lerp(rock,mineral,Smooth(.54f,.82f,strata)*.43f);
            rock*=Mathf.Lerp(.93f,1.07f,grain);
            Color c=Color.Lerp(Sample(ground,size,(x+Half)/1200,(z+Half)/1200),rock,exposed*.86f);
            c.a=1;pixels[j*width+i]=c;
        }
        string path=Output+"/"+p.name+"_Albedo.png";
        var tex=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
        try{tex.SetPixels32(pixels);tex.Apply(false,false);File.WriteAllBytes(path,ImageConversion.EncodeToPNG(tex));}
        finally{Object.DestroyImmediate(tex);}
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType=TextureImporterType.Default;imp.sRGBTexture=true;imp.alphaSource=TextureImporterAlphaSource.None;
        imp.mipmapEnabled=true;imp.filterMode=FilterMode.Trilinear;imp.wrapMode=TextureWrapMode.Clamp;imp.anisoLevel=4;
        imp.maxTextureSize=Mathf.Max(width,height);imp.isReadable=false;imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();
        var m=new Material(baseline){name=p.name+" sandstone"};m.SetTexture("_BaseMap",Require<Texture2D>(path));
        m.SetTextureScale("_BaseMap",new Vector2(1200f/(p.x1-p.x0),1200f/(p.z1-p.z0)));
        m.SetTextureOffset("_BaseMap",new Vector2(-(p.x0+Half)/(p.x1-p.x0),-(p.z0+Half)/(p.z1-p.z0)));
        return Save(p.name+".mat",m);
    }
    private static Color Sample(Color32[] a,int size,float u,float v)
    {float x=Mathf.Clamp(u*size-.5f,0,size-1),z=Mathf.Clamp(v*size-.5f,0,size-1);int i=(int)x,j=(int)z,i1=Mathf.Min(i+1,size-1),j1=Mathf.Min(j+1,size-1);
        return Color.Lerp(Color.Lerp(a[j*size+i],a[j*size+i1],x-i),Color.Lerp(a[j1*size+i],a[j1*size+i1],x-i),z-j);}
    private static float Noise(float x,float z,float f,float a,float b)=>Mathf.PerlinNoise(x*f+a,z*f+b);
    private static float Smooth(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));

    // The CLI compiles this editor script dynamically. Do not rely on Unity's native
    // serializer registering its nested DTOs. Read the deterministic Python export
    // with managed code and retain all geometric validation below.
    private static Manifest ReadManifest(string path)
    {
        if(!File.Exists(path))throw new InvalidOperationException("Missing strata data: "+path+". Run Blender generator.");
        string json=File.ReadAllText(path);int pos=0;
        var culture=CultureInfo.InvariantCulture;
        Match Take(string pattern,string label)
        {
            var m=new Regex(@"\G\s*"+pattern).Match(json,pos);
            if(!m.Success)throw new InvalidOperationException("Invalid strata JSON: "+label+" at character "+pos+" in "+path);
            pos=m.Index+m.Length;return m;
        }
        int Int(string value)=>int.Parse(value,NumberStyles.Integer,culture);
        float Float(string value)=>float.Parse(value,NumberStyles.Float,culture);
        var header=Take(@"\{\s*""version""\s*:\s*(\d+)\s*,\s*""step""\s*:\s*([0-9.]+)\s*,\s*""fields""\s*:\s*\[","header");
        var result=new Manifest{version=Int(header.Groups[1].Value),step=Float(header.Groups[2].Value)};
        var fields=new List<Patch>();
        for(int k=0;k<4;k++)
        {
            if(k>0)Take(@",","field separator");
            var m=Take(@"\{\s*""name""\s*:\s*""([A-Za-z0-9_]+)""\s*,\s*""x0""\s*:\s*(-?\d+)\s*,\s*""x1""\s*:\s*(-?\d+)\s*,\s*""z0""\s*:\s*(-?\d+)\s*,\s*""z1""\s*:\s*(-?\d+)\s*,\s*""nx""\s*:\s*(\d+)\s*,\s*""nz""\s*:\s*(\d+)\s*,\s*""heights""\s*:\s*\[([^\]]*)\]\s*\}","field "+k);
            var p=new Patch{name=m.Groups[1].Value,x0=Int(m.Groups[2].Value),x1=Int(m.Groups[3].Value),
                z0=Int(m.Groups[4].Value),z1=Int(m.Groups[5].Value),nx=Int(m.Groups[6].Value),nz=Int(m.Groups[7].Value)};
            string[] values=m.Groups[8].Value.Split(',');
            if(p.nx<=0||p.nz<=0||(long)p.nx*p.nz!=values.Length)
                throw new InvalidOperationException("Invalid strata sample count: "+p.name+" ("+values.Length+").");
            p.heights=new float[values.Length];
            for(int i=0;i<values.Length;i++)
                if(!float.TryParse(values[i],NumberStyles.Float,culture,out p.heights[i]))
                    throw new InvalidOperationException("Invalid strata number: "+p.name+" sample "+i);
            fields.Add(p);
        }
        Take(@"\]\s*\}\s*\z","end of manifest");
        result.fields=fields.ToArray();
        return result;
    }

    private static void ValidateManifest(Manifest m)
    {
        if(m==null||m.version!=1||Mathf.Abs(m.step-FineStep)>.0001f||m.fields==null||m.fields.Length!=4)
            throw new InvalidOperationException($"Invalid strata manifest: version={m?.version}, step={m?.step}, fields={m?.fields?.Length}; expected 1 / {FineStep} / 4.");
        var expected=new[]{"BrokenMesa","EastEscarpment","SplitRidge","WesternButte"};
        for(int k=0;k<m.fields.Length;k++)
        {
            var p=m.fields[k];if(p==null||p.name!=expected[k]||p.x0<=-600||p.x1>=600||p.z0<=-600||p.z1>=600||p.x1<=p.x0||p.z1<=p.z0||
                p.x0%5!=0||p.x1%5!=0||p.z0%5!=0||p.z1%5!=0||p.nx!=(p.I1-p.I0)*Sub+1||p.nz!=(p.J1-p.J0)*Sub+1||p.heights==null||p.heights.Length!=p.nx*p.nz)
                throw new InvalidOperationException("Invalid strata grid.");
            for(int z=0;z<p.nz;z++)for(int x=0;x<p.nx;x++)
            {float h=p.heights[z*p.nx+x];if(float.IsNaN(h)||float.IsInfinity(h)||h<0||h>30||((x==0||z==0||x==p.nx-1||z==p.nz-1)&&h!=0))throw new InvalidOperationException("Invalid strata elevation: "+p.name);}
            for(int j=0;j<k;j++) {var q=m.fields[j];if(p.x0<=q.x1&&p.x1>=q.x0&&p.z0<=q.z1&&p.z1>=q.z0)throw new InvalidOperationException("Overlapping strata grids.");}
        }
    }
    private static void ValidateTerrain(MeshFilter f)
    {
        var m=f.sharedMesh;if(m==null||!m.isReadable||m.vertexCount!=(N+1)*(N+1)||m.subMeshCount!=1)throw new InvalidOperationException("Expected V16 terrain grid.");
        var v=m.vertices;var uv=m.uv;var tris=m.triangles;
        if(uv.Length!=v.Length||tris.Length!=N*N*6)throw new InvalidOperationException("Unexpected V16 mesh layout.");
        int k=0;
        for(int z=0;z<=N;z++)for(int x=0;x<=N;x++)
        {
            int i=z*(N+1)+x;var p=v[i];if(Mathf.Abs(p.x-(x*Step-Half))>.001f||Mathf.Abs(p.z-(z*Step-Half))>.001f||(f.transform.TransformPoint(p)-p).sqrMagnitude>.000001f||(uv[i]-new Vector2(x/(float)N,z/(float)N)).sqrMagnitude>.000001f)throw new InvalidOperationException("Unexpected terrain coordinates.");
            if(x<N&&z<N){int b=i+N+1;if(tris[k++]!=i||tris[k++]!=b||tris[k++]!=i+1||tris[k++]!=i+1||tris[k++]!=b||tris[k++]!=b+1)throw new InvalidOperationException("Unexpected terrain triangles.");}
        }
    }
    private static bool IntersectsRaised(Patch[] patches,Bounds b,float limit)
    {
        foreach(var p in patches)
        {
            int x0=Mathf.Max(0,Mathf.FloorToInt((b.min.x-p.x0)/FineStep)),x1=Mathf.Min(p.nx-1,Mathf.CeilToInt((b.max.x-p.x0)/FineStep));
            int z0=Mathf.Max(0,Mathf.FloorToInt((b.min.z-p.z0)/FineStep)),z1=Mathf.Min(p.nz-1,Mathf.CeilToInt((b.max.z-p.z0)/FineStep));
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)if(p.heights[z*p.nx+x]>limit)return true;
        }
        return false;
    }
    private static void ValidateInfrastructure(GameObject root,Patch[] patches)
    {
        foreach(var f in Group(root,"Abandoned roads").GetComponentsInChildren<MeshFilter>(true))foreach(var v in f.sharedMesh.vertices)
        {
            var q=f.transform.TransformPoint(v);foreach(var p in patches)if(q.x>=p.x0&&q.x<=p.x1&&q.z>=p.z0&&q.z<=p.z1&&p.At(q.x,q.z)>.001f)
                throw new InvalidOperationException("Strata would alter road: "+f.name);
        }
        foreach(var r in Group(root,"Civilization remnants").GetComponentsInChildren<MeshRenderer>(true))if(IntersectsRaised(patches,r.bounds,.001f))throw new InvalidOperationException("Strata would alter infrastructure: "+r.name);
    }
    private static GameObject Root(Scene s)=>s.GetRootGameObjects().Single(g=>g.GetComponent<ArtLabLandscapeFlight>()!=null);
    private static MeshFilter Terrain(GameObject root)=>root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Scorched terrain");
    private static Transform Group(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t!=null&&t.name==name);
    private static T Require<T>(string path)where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing "+path);
    private static T Save<T>(string file,T asset)where T:Object
    {string path=Output+"/"+file;var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(asset,path);return asset;}EditorUtility.CopySerialized(asset,old);Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);return old;}
}
