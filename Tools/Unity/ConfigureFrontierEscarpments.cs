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

// V16: replace four local hill cores with actual cliff geometry. Reuse the green V14B
// scene, road meshes, flight controls and ground albedo; only retouch the replaced ground.
public static class ConfigureFrontierEscarpments
{
    private const string Source = "Assets/_Project/Scenes/FrontierSurfacePass.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierEscarpments.unity";
    private const string Art = "Assets/_Project/Art/Environment/FrontierEscarpments";
    private const string Output = "Assets/_Project/Generated/FrontierEscarpments";
    private const string GroundPng = "Assets/_Project/Generated/FrontierSurfacePass/GroundSurface.png";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const int N = 240, Size = 4096;
    private const float Half = 600, Step = 5, Floor = 2.2f, Burial = 5f;
    private static readonly (string name,float x,float z,float width,float depth,float height)[] Sites = {
        ("BrokenMesa",-52,185,152,80,30), ("EastEscarpment",365,118,84,142,32),
        ("SplitRidge",172,-194,152,80,28), ("WesternButte",-220,65,80,105,29)
    };
    private static readonly Vector4[] Pads = {
        new Vector4(82,22,49,37), new Vector4(-119,-116,42,31), new Vector4(226,230,48,38),
        new Vector4(-255,202,43,30), new Vector4(334,-243,36,25)
    };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save open scenes first.");
        Require<SceneAsset>(Source);
        var pipeline=Require<UniversalRenderPipelineAsset>(Pipeline);
        if(!File.Exists(GroundPng)||!File.Exists(Art+"/EscarpmentPalette.png"))
            throw new InvalidOperationException("Missing V14B ground or V16 Blender export.");
        foreach(var site in Sites) if(!File.Exists(Art+"/"+site.name+".fbx"))
            throw new InvalidOperationException("Missing FBX: "+site.name);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if(!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated","FrontierEscarpments");
        var sourceScene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
        var sourceRoot=Root(sourceScene);
        var original=FieldOf(sourceRoot);
        var protection=ProtectedFootprints(sourceRoot);
        var next=BuildField(original,protection);
        ValidateSites(next);
        // All placement masks are checked before copying the scene, not inferred from a screenshot.
        ValidateRoadsAndSites(sourceRoot,original,next);
        var palette=ImportPalette();
        var shader=Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader missing.");
        var rockMaterial=new Material(shader){name="Escarpment sandstone"};
        rockMaterial.SetColor("_BaseColor",Color.white);rockMaterial.SetTexture("_BaseMap",palette);
        rockMaterial.SetFloat("_Metallic",0);rockMaterial.SetFloat("_Smoothness",.04f);rockMaterial.enableInstancing=true;
        rockMaterial=SaveAsset("Sandstone.mat",rockMaterial);
        var models=Sites.Select(s=>Require<GameObject>(Art+"/"+s.name+".fbx")).ToArray();
        var sourceGround=FindTerrain(sourceRoot).GetComponent<MeshRenderer>();
        try
        {
            EditorUtility.DisplayProgressBar("V16","Retouching only replaced hill colors",.2f);
            var groundColor=RetouchGround(original,next,protection);
            var groundMaterial=new Material(sourceGround.sharedMaterial){name="V16 ground"};
            groundMaterial.SetTexture("_BaseMap",groundColor);
            groundMaterial=SaveAsset("Ground.mat",groundMaterial);
            // Clone the original mesh, keeping its exact UVs, index format and triangle topology.
            var mesh=Object.Instantiate(FindTerrain(sourceRoot).sharedMesh);mesh.name="V16 terrain";
            mesh.vertices=next.Vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            mesh=SaveAsset("Terrain.asset",mesh);
            if(!EditorSceneManager.SaveScene(sourceScene,Destination,true)) throw new InvalidOperationException("Cannot copy V14B.");
            var scene=EditorSceneManager.OpenScene(Destination,OpenSceneMode.Single);
            var root=Root(scene);root.name="Frontier escarpments";
            var terrain=FindTerrain(root);terrain.sharedMesh=mesh;terrain.GetComponent<MeshRenderer>().sharedMaterial=groundMaterial;
            EditorUtility.SetDirty(terrain);EditorUtility.SetDirty(terrain.GetComponent<MeshRenderer>());
            // Resolve both group owners before destroying any of their children.
            // Unity's hierarchy snapshot otherwise retains destroyed Transform references.
            var owners=new[]{"Rock outcrops and dead trees","V14A rock formations"}
                .Select(name=>root.GetComponentsInChildren<Transform>(true).Single(t=>t!=null&&t.name==name)).ToArray();
            int removed=0,moved=0;
            foreach(var owner in owners)
            {
                foreach(var instance in owner.Cast<Transform>().ToArray())
                {
                    if(instance==null)continue;
                    Vector3 p=instance.position;
                    // Remove only loose nature that conflicts with the new cliff footprint.
                    if(Sites.Any(s=>InFootprint(p.x,p.z,s,1.12f))) {Object.DestroyImmediate(instance.gameObject);removed++;continue;}
                    float dy=next.At(p.x,p.z)-original.At(p.x,p.z);
                    var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
                    if(renderers.Length>0)
                    {
                        var b=BoundsOf(renderers);
                        // Do not leave the edge of a wide rock floating on a receding slope.
                        foreach(float x in new[]{b.min.x,b.max.x})foreach(float z in new[]{b.min.z,b.max.z})
                            dy=Mathf.Min(dy,next.At(x,z)-original.At(x,z));
                    }
                    if(Mathf.Abs(dy)>.001f){instance.position+=Vector3.up*dy;PrefabUtility.RecordPrefabInstancePropertyModifications(instance);moved++;}
                }
            }
            var formationRoot=new GameObject("V16 coherent escarpments").transform;formationRoot.SetParent(root.transform,false);
            for(int i=0;i<Sites.Length;i++) Place(scene,formationRoot,models[i],rockMaterial,Sites[i]);
            var player=scene.GetRootGameObjects().Single(g=>g.name=="Player");
            var camera=scene.GetRootGameObjects().Single(g=>g.name=="Main Camera").GetComponent<Camera>();
            root.GetComponent<ArtLabLandscapeFlight>().Configure(pipeline,player.transform,camera,player.GetComponent<FlightController>(),
                player.GetComponent<Afterburner>(),Half,"V16 / Rocky escarpments");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save V16.");
            Selection.activeGameObject=player;
            return $"V16 ready: four continuous cliff masses; {removed} conflicting loose props removed, {moved} individually regrounded. Roads and settlement heights validated. Fly north from refinery, then east/south/west.";
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    private static void Place(Scene scene,Transform parent,GameObject model,Material material,
        (string name,float x,float z,float width,float depth,float height) site)
    {
        var holder=new GameObject(site.name).transform;holder.SetParent(parent,false);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);
        if(instance==null)throw new InvalidOperationException("Cannot instantiate "+site.name);
        instance.transform.SetParent(holder,false);
        var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
        var b=BoundsOf(renderers);
        if(b.size.x<.01f||b.size.y<.01f||b.size.z<.01f)throw new InvalidOperationException("Empty cliff bounds.");
        float sx=site.width/b.size.x,sy=site.height/b.size.y,sz=site.depth/b.size.z;
        if(Mathf.Max(sx,Mathf.Max(sy,sz))/Mathf.Min(sx,Mathf.Min(sy,sz))>1.15f)
            throw new InvalidOperationException("Unexpected FBX axis or proportions: "+site.name+" "+b.size);
        // Account for FBX unit/axis conversion, then anchor the actual bounds to the buried base.
        holder.localScale=new Vector3(sx,sy,sz);
        b=BoundsOf(renderers);
        holder.position+=new Vector3(site.x-b.center.x,Floor-Burial-b.min.y,site.z-b.center.z);
        b=BoundsOf(renderers);
        if(b.max.y>40||Mathf.Abs(b.center.x-site.x)>.02f||Mathf.Abs(b.center.z-site.z)>.02f)
            throw new InvalidOperationException("Cliff placement/flight clearance invalid: "+site.name);
        foreach(var r in renderers)
        {
            var mf=r.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)throw new InvalidOperationException("Missing cliff mesh.");
            r.sharedMaterials=Enumerable.Repeat(material,mf.sharedMesh.subMeshCount).ToArray();
            r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
    }
    private static Bounds BoundsOf(MeshRenderer[] renderers)
    {
        if(renderers.Length==0)throw new InvalidOperationException("FBX has no renderer.");
        var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;
    }

    private static Field BuildField(Field old,Vector4[] protection)
    {
        var v=(Vector3[])old.Vertices.Clone();
        for(int i=0;i<v.Length;i++)
        {
            var p=v[i];float w=ChangeWeight(p.x,p.z,protection);
            p.y=Mathf.Lerp(p.y,Floor,w);v[i]=p;
        }
        return new Field(v);
    }
    private static bool InFootprint(float x,float z,(string name,float x,float z,float width,float depth,float height) s,float margin)
        => Mathf.Abs(x-s.x)<=s.width*.5f*margin&&Mathf.Abs(z-s.z)<=s.depth*.5f*margin;
    private static float ChangeWeight(float x,float z,Vector4[] protection)
    {
        float best=0;
        foreach(var s in Sites)
        {
            float d=Mathf.Max(Mathf.Abs(x-s.x)/(s.width*.5f+8),Mathf.Abs(z-s.z)/(s.depth*.5f+8));
            if(d<1.75f)best=Mathf.Max(best,1-Smooth(1,1.75f,d));
        }
        if(best<=0)return 0;
        float road=Smooth(19,42,RoadDistance(x,z));float pad=1;
        foreach(var p in Pads)pad=Mathf.Min(pad,Smooth(9,36,Mathf.Max(Mathf.Abs(x-p.x)-p.z,Mathf.Abs(z-p.y)-p.w)));
        if(road*pad<=0)return 0;
        foreach(var p in protection)pad=Mathf.Min(pad,Smooth(8,25,Mathf.Max(Mathf.Abs(x-p.x)-p.z,Mathf.Abs(z-p.y)-p.w)));
        return best*road*pad;
    }
    private static void ValidateSites(Field field)
    {
        foreach(var s in Sites)
        {
            // The entire bounding rectangle must have a flat, buried footing and clear road/POI space.
            for(float z=s.z-s.depth/2;z<=s.z+s.depth/2+.01f;z+=2)
            for(float x=s.x-s.width/2;x<=s.x+s.width/2+.01f;x+=2)
                if(RoadDistance(x,z)<18||Mathf.Abs(field.At(x,z)-Floor)>.9f)
                    throw new InvalidOperationException("Unprotected cliff footprint: "+s.name+" at "+x+","+z);
        }
    }
    private static Vector4[] ProtectedFootprints(GameObject root)
    {
        var sites=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Civilization remnants");
        var bounds=new System.Collections.Generic.List<Vector4>();
        foreach(Transform site in sites)
        foreach(Transform instance in site)
        {
            var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length==0)continue;
            var b=BoundsOf(renderers);
            bounds.Add(new Vector4(b.center.x,b.center.z,b.extents.x,b.extents.z));
        }
        return bounds.ToArray();
    }
    private static void ValidateRoadsAndSites(GameObject root,Field old,Field next)
    {
        var roads=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Abandoned roads");
        foreach(var f in roads.GetComponentsInChildren<MeshFilter>(true))
        {
            foreach(var v in f.sharedMesh.vertices)
            {var p=f.transform.TransformPoint(v);if(Mathf.Abs(next.At(p.x,p.z)-old.At(p.x,p.z))>.001f)throw new InvalidOperationException("Road elevation changed: "+f.name);}
        }
        var sites=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Civilization remnants");
        foreach(Transform site in sites)
        foreach(Transform instance in site)
        {
            var p=instance.position;
            if(Sites.Any(s=>InFootprint(p.x,p.z,s,1.1f))||Mathf.Abs(next.At(p.x,p.z)-old.At(p.x,p.z))>.001f)
                throw new InvalidOperationException("Settlement/utility placement would be affected: "+instance.name);
            foreach(var r in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b=r.bounds;
                foreach(float x in new[]{b.min.x,b.center.x,b.max.x})foreach(float z in new[]{b.min.z,b.center.z,b.max.z})
                    if(Mathf.Abs(next.At(x,z)-old.At(x,z))>.01f)throw new InvalidOperationException("Settlement footprint changed: "+instance.name);
            }
        }
    }

    // Retain every V14B pixel outside the local replacement masks. Inside, subtract the old
    // periodic rock tint and use a non-periodic slope tint. Preserve roads, yard, and grit.
    private static Texture2D RetouchGround(Field old,Field next,Vector4[] protection)
    {
        var read=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
        try
        {
            if(!ImageConversion.LoadImage(read,File.ReadAllBytes(GroundPng))||read.width!=Size||read.height!=Size)
                throw new InvalidOperationException("Expected V14B 4096 ground PNG.");
            var colors=read.GetPixels32();
            Color clay=new Color32(175,121,78,255),dust=new Color32(199,154,109,255),dark=new Color32(127,92,69,255),rock=new Color32(130,106,87,255);
            for(int z=0;z<Size;z++)
            {
                float wz=(z+.5f)*1200/Size-Half;
                if(z%256==0)EditorUtility.DisplayProgressBar("V16","Retouching terrain",.2f+.5f*z/Size);
                for(int x=0;x<Size;x++)
                {
                    float wx=(x+.5f)*1200/Size-Half,w=ChangeWeight(wx,wz,protection);if(w<.001f)continue;
                    float broad=Noise(wx,wz,.006f,11,43),mid=Noise(wx,wz,.019f,83,61);
                    Color plain=Color.Lerp(clay,dust,Smooth(.29f,.74f,broad*.72f+mid*.28f));
                    plain=Color.Lerp(plain,dark,Smooth(.58f,.81f,Noise(wx,wz,.033f,79,16))*.32f);
                    float oldTint=Smooth(.17f,.55f,old.Slope(wx,wz))*(.72f+.28f*Mathf.Sin(old.At(wx,wz)*1.3f+mid*2.5f))*.68f;
                    float newTint=Smooth(.17f,.55f,next.Slope(wx,wz))*.42f;
                    Color delta=(rock-plain)*(newTint-oldTint);
                    float lum=.2126f*delta.r+.7152f*delta.g+.0722f*delta.b;
                    delta=Color.Lerp(delta,new Color(lum,lum,lum,0),.13f);
                    Color c=(Color)colors[z*Size+x]+delta*w;c.a=1;colors[z*Size+x]=c;
                }
            }
            read.SetPixels32(colors);read.Apply(false,false);
            string path=Output+"/GroundRetouched.png";File.WriteAllBytes(path,ImageConversion.EncodeToPNG(read));
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Default;imp.sRGBTexture=true;
            imp.alphaSource=TextureImporterAlphaSource.None;imp.isReadable=false;imp.maxTextureSize=Size;imp.mipmapEnabled=true;
            imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=4;imp.textureCompression=TextureImporterCompression.Compressed;imp.SaveAndReimport();
            return Require<Texture2D>(path);
        }
        finally{Object.DestroyImmediate(read);}
    }
    private static Texture2D ImportPalette()
    {
        string path=Art+"/EscarpmentPalette.png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType=TextureImporterType.Default;imp.sRGBTexture=true;imp.alphaSource=TextureImporterAlphaSource.None;
        imp.mipmapEnabled=false;imp.filterMode=FilterMode.Point;imp.wrapMode=TextureWrapMode.Clamp;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
        return Require<Texture2D>(path);
    }
    private sealed class Field
    {
        public readonly Vector3[] Vertices;
        public Field(Vector3[] vertices){Vertices=vertices;}
        public float At(float x,float z)
        {
            float gx=Mathf.Clamp((x+Half)/Step,0,N),gz=Mathf.Clamp((z+Half)/Step,0,N);
            int ix=Mathf.Min(N-1,Mathf.FloorToInt(gx)),iz=Mathf.Min(N-1,Mathf.FloorToInt(gz));
            float u=gx-ix,v=gz-iz;int a=iz*(N+1)+ix,b=a+N+1;
            float h00=Vertices[a].y,h10=Vertices[a+1].y,h01=Vertices[b].y,h11=Vertices[b+1].y;
            return u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
        }
        public float Slope(float x,float z){float a=(At(x+Step,z)-At(x-Step,z))/(2*Step),b=(At(x,z+Step)-At(x,z-Step))/(2*Step);return Mathf.Sqrt(a*a+b*b);}
    }
    private static Field FieldOf(GameObject root)
    {
        var mf=FindTerrain(root);var mesh=mf.sharedMesh;
        if(mesh==null||!mesh.isReadable||mesh.vertexCount!=(N+1)*(N+1)||mesh.uv.Length!=mesh.vertexCount)
            throw new InvalidOperationException("Unexpected V14B terrain.");
        var v=mesh.vertices;
        for(int z=0;z<=N;z++)for(int x=0;x<=N;x++)
        {var p=v[z*(N+1)+x];if(Mathf.Abs(p.x-(x*Step-Half))>.01f||Mathf.Abs(p.z-(z*Step-Half))>.01f||(mf.transform.TransformPoint(p)-p).sqrMagnitude>.0001f)throw new InvalidOperationException("Terrain needs identity world-space grid.");}
        var tris=mesh.triangles;if(tris.Length!=N*N*6)throw new InvalidOperationException("Unexpected terrain triangles.");
        int k=0;for(int z=0;z<N;z++)for(int x=0;x<N;x++){int a=z*(N+1)+x,b=a+N+1;if(tris[k++]!=a||tris[k++]!=b||tris[k++]!=a+1||tris[k++]!=a+1||tris[k++]!=b||tris[k++]!=b+1)throw new InvalidOperationException("Unexpected terrain triangle ordering.");}
        return new Field(v);
    }
    private static MeshFilter FindTerrain(GameObject root)=>root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Scorched terrain");
    private static GameObject Root(Scene scene)=>scene.GetRootGameObjects().Single(g=>g.GetComponent<ArtLabLandscapeFlight>()!=null);
    private static float RoadZ(float x)=>-43+23*Mathf.Sin(x*.011f+.25f)+10*Mathf.Sin(x*.021f+.7f);
    private static float BranchX(float z)=>190+17*Mathf.Sin(z*.0085f+.4f);
    private static float RoadDistance(float x,float z)=>Mathf.Min(Mathf.Abs(z-RoadZ(x)),z>=RoadZ(BranchX(-20))+2-6?Mathf.Abs(x-BranchX(z)):9999);
    private static float Smooth(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
    private static float Noise(float x,float z,float f,float a,float b)=>Mathf.PerlinNoise(x*f+a,z*f+b);
    private static T Require<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing "+path);
    private static T SaveAsset<T>(string file,T obj) where T:Object
    {
        string path=Output+"/"+file;var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old==null){AssetDatabase.CreateAsset(obj,path);return obj;}
        EditorUtility.CopySerialized(obj,old);Object.DestroyImmediate(obj);EditorUtility.SetDirty(old);return old;
    }
}
