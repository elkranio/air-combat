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

// Rebuild the visual playground from Sandbox without changing gameplay or shared assets.
public static class ConfigureArtLabScorchedFrontier
{
    private const string Source = "Assets/_Project/Scenes/Sandbox.unity";
    private const string Destination = "Assets/_Project/Scenes/ScorchedFrontier.unity";
    private const string Output = "Assets/_Project/Generated/ScorchedFrontier";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const float Half = 600f, Step = 5f;
    private const int N = 240, TextureSize = 2048;
    // Pads define the actual placement and flattening mask. Keep them well away from major ridges.
    private static readonly Vector4[] Pads = {
        new Vector4(82, 22, 49, 37), new Vector4(-119,-116,42,31),
        new Vector4(226,230,48,38), new Vector4(-255,202,43,30), new Vector4(334,-243,36,25)
    };
    private static readonly Vector3[] Craters = { new Vector3(-50,0,-181),new Vector3(98,0,-183),new Vector3(-213,0,52),new Vector3(302,0,87),new Vector3(-104,0,330) };
    private static readonly float[] CraterRadii = { 17,23,25,19,28 };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save current scene changes first.");
        Require<SceneAsset>(Source);
        var pipeline = Require<UniversalRenderPipelineAsset>(Pipeline);
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null) throw new InvalidOperationException("URP Lit shader missing.");
        EnsureFolder(Output);
        EnsureFolder(Output+"/Modules");
        // Load all accepted tuning assets before modifying the destination scene.
        var aircraftMaterials = new Dictionary<string,Material>();
        foreach(var slot in new[]{"Hull","Upper","Panels"})
            aircraftMaterials.Add("Kestrel_"+slot,Require<Material>("Assets/_Project/Art/Aircraft/FrontierMaterials/Kestrel_"+slot+".mat"));
        var scene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
        if(!EditorSceneManager.SaveScene(scene,Destination,true)) throw new InvalidOperationException("Cannot copy Sandbox.");
        scene=EditorSceneManager.OpenScene(Destination,OpenSceneMode.Single);
        var player=Root(scene,"Player");var cameraOwner=Root(scene,"Main Camera");var lightOwner=Root(scene,"Directional Light");
        var camera=cameraOwner.GetComponent<Camera>();var follow=cameraOwner.GetComponent<TopDownCameraFollow>();
        var sun=lightOwner.GetComponent<Light>();var flight=player.GetComponent<FlightController>();var boost=player.GetComponent<Afterburner>();
        var visual=player.transform.Find("JetVisual");
        if(camera==null||follow==null||sun==null||flight==null||boost==null||visual==null||visual.Find("KestrelModel")==null||visual.GetComponent<AfterburnerVfx>()==null)
            throw new InvalidOperationException("Missing Sandbox flight setup.");
        foreach(var b in player.GetComponentsInChildren<MonoBehaviour>(true))
            if(b!=null&&!(b is FlightController)&&!(b is Afterburner)&&!(b is AfterburnerVfx))Object.DestroyImmediate(b);
        // Remove target HUD before TargetLock (the HUD requires it).
        foreach(var b in cameraOwner.GetComponents<MonoBehaviour>().OrderBy(b=>b is TargetLock?1:0))
            if(b!=null&&!(b is TopDownCameraFollow)&&!(b is UniversalAdditionalCameraData))Object.DestroyImmediate(b);
        foreach(var go in scene.GetRootGameObjects())
            if(go!=player&&go!=cameraOwner&&go!=lightOwner)Object.DestroyImmediate(go);
        player.transform.SetPositionAndRotation(new Vector3(0,48,-100),Quaternion.identity);
        foreach(var r in visual.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(!r.enabled||!r.gameObject.activeInHierarchy)continue;
            r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
            r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&aircraftMaterials.TryGetValue(m.name,out var replacement)?replacement:m).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        var settings=new SerializedObject(follow);
        settings.FindProperty("pitch").floatValue=65f;settings.ApplyModifiedPropertiesWithoutUndo();
        camera.rect=new Rect(0,0,1,1);camera.orthographic=true;camera.orthographicSize=80f;camera.farClipPlane=300f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(160,116,79,255);camera.useOcclusionCulling=false;
        var rotation=Quaternion.Euler(65f,0,0);
        camera.transform.SetPositionAndRotation(player.transform.position-rotation*Vector3.forward*(88f/Mathf.Sin(65f*Mathf.Deg2Rad)),rotation);
        sun.transform.rotation=Quaternion.Euler(38f,-38f,0);
        sun.color=new Color32(255,239,219,255);sun.intensity=1.38f;sun.shadowStrength=.75f;sun.shadows=LightShadows.Soft;
        sun.shadowBias=.035f;sun.shadowNormalBias=.18f;sun.lightmapBakeType=LightmapBakeType.Realtime;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color32(83,100,122,255);
        RenderSettings.sun=sun;RenderSettings.fog=false;RenderSettings.skybox=null;
        var root=new GameObject("ArtLab V13 Scorched Frontier");
        var preview=root.AddComponent<ArtLabLandscapeFlight>();
        preview.Configure(pipeline,player.transform,camera,flight,boost,Half,"ArtLab v13 / Scorched Frontier");
        EditorUtility.SetDirty(preview);

        var field=new HeightField();
        var terrainMaterial=MaterialAsset(shader,"Ground",Color.white);
        var tex=BakeGround(field);
        terrainMaterial.SetTexture("_BaseMap",tex);EditorUtility.SetDirty(terrainMaterial);
        AddMesh(root.transform,"Scorched terrain",SaveMesh("Terrain",field.BuildMesh()),terrainMaterial);
        var palette=new Palette(shader);
        BuildRoads(field,Group(root.transform,"Abandoned roads"),palette);
        var modules=new Modules(scene,palette);
        var settlements=Group(root.transform,"Civilization remnants");
        PlaceSettlements(scene,field,settlements,modules);
        int rocks=ScatterNature(scene,field,Group(root.transform,"Rock outcrops and dead trees"),modules);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save V13.");
        Selection.activeGameObject=player;
        return $"V13 ready: 1200 x 1200 scorched frontier, 5 sites, {rocks} rocks/stumps. WASD/Space, 1/2/3 camera, R recenter, H legend. Sandbox unchanged.";
    }

    private static float Smooth(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
    private static float Noise(float x,float z,float scale,float ox,float oz)=>Mathf.PerlinNoise(x*scale+ox,z*scale+oz);
    private static float Hill(float x,float z,float cx,float cz,float rx,float rz)
    {float u=(x-cx)/rx,v=(z-cz)/rz;return Mathf.Exp(-2f*(u*u+v*v));}
    private static float RoadZ(float x)=>-43f+23f*Mathf.Sin(x*.011f+.25f)+10f*Mathf.Sin(x*.021f+.7f);
    private static float BranchX(float z)=>190f+17f*Mathf.Sin(z*.0085f+.4f);
    private static float BranchStart=>RoadZ(BranchX(-20f))+2f;
    private static float RoadDistance(float x,float z)
    {float a=Mathf.Abs(z-RoadZ(x));float b=z>=BranchStart-6?Mathf.Abs(x-BranchX(z)):9999;return Mathf.Min(a,b);}
    private static float PadDistance(float x,float z)
    {
        float d=9999f;
        foreach(var p in Pads)d=Mathf.Min(d,Mathf.Max(Mathf.Abs(x-p.x)-p.z,Mathf.Abs(z-p.y)-p.w));
        return d;
    }
    private static float Crater(float x,float z,out float stain)
    {
        float h=0;stain=0;
        for(int i=0;i<Craters.Length;i++)
        {
            float d=Vector2.Distance(new Vector2(x,z),new Vector2(Craters[i].x,Craters[i].z))/CraterRadii[i];
            if(d>1.6f)continue;
            h+=-5f*(1-Smooth(.26f,.89f,d))+2.3f*Mathf.Exp(-Mathf.Pow((d-.88f)/.16f,2f));
            stain=Mathf.Max(stain,(1-Smooth(.6f,1.4f,d))*.42f);
        }
        return h;
    }
    private static float Height(float x,float z)
    {
        float wx=x+(Noise(x,z,.006f,22,81)-.5f)*60f;
        float wz=z+(Noise(x,z,.006f,81,16)-.5f)*70f;
        // Elongated ridges framing broad open basins; no uniform field of noise bumps.
        float mass=Mathf.Max(Hill(wx,wz,-205,-25,90,153),Hill(wx,wz,182,-194,135,65));
        mass=Mathf.Max(mass,Hill(wx,wz,-52,185,140,66));
        mass=Mathf.Max(mass,Hill(wx,wz,350,118,62,128));
        mass=Mathf.Max(mass,Hill(wx,wz,-405,310,122,88));
        mass=Mathf.Max(mass,Hill(wx,wz,48,-400,155,95));
        mass=Mathf.Max(mass,Hill(wx,wz,348,376,145,103));
        float ridge=Noise(wx,wz,.028f,37,65);
        float baseHeight=1.6f+(Noise(wx,wz,.004f,41,90)-.5f)*5f;
        float h=baseHeight+26f*Smooth(.12f,.86f,mass)+mass*(ridge-.5f)*10f;
        h+=Crater(x,z,out _);
        float clear=Smooth(0,29,PadDistance(x,z))*Smooth(6,27,RoadDistance(x,z));
        return Mathf.Clamp(Mathf.Lerp(1.3f,h,clear),-7f,31f);
    }

    private sealed class HeightField
    {
        private readonly Vector3[] vertices=new Vector3[(N+1)*(N+1)];
        public HeightField(){for(int z=0;z<=N;z++)for(int x=0;x<=N;x++){
            float wx=x*Step-Half,wz=z*Step-Half;vertices[z*(N+1)+x]=new Vector3(wx,Height(wx,wz),wz);}}
        public float At(float x,float z)
        {
            float gx=Mathf.Clamp((x+Half)/Step,0,N),gz=Mathf.Clamp((z+Half)/Step,0,N);
            int ix=Mathf.Min(N-1,Mathf.FloorToInt(gx)),iz=Mathf.Min(N-1,Mathf.FloorToInt(gz));
            float u=gx-ix,v=gz-iz;int a=iz*(N+1)+ix,b=a+N+1;
            float h00=vertices[a].y,h10=vertices[a+1].y,h01=vertices[b].y,h11=vertices[b+1].y;
            return u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
        }
        public float Slope(float x,float z){float a=(At(x+Step,z)-At(x-Step,z))/(2*Step),b=(At(x,z+Step)-At(x,z-Step))/(2*Step);return Mathf.Sqrt(a*a+b*b);}
        public Mesh BuildMesh()
        {
            var uv=new Vector2[vertices.Length];var tris=new int[N*N*6];int k=0;
            for(int z=0;z<=N;z++)for(int x=0;x<=N;x++)uv[z*(N+1)+x]=new Vector2(x/(float)N,z/(float)N);
            for(int z=0;z<N;z++)for(int x=0;x<N;x++){int a=z*(N+1)+x,b=a+N+1;tris[k++]=a;tris[k++]=b;tris[k++]=a+1;tris[k++]=a+1;tris[k++]=b;tris[k++]=b+1;}
            var m=new Mesh{vertices=vertices,uv=uv,triangles=tris};m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;
        }
    }
    private static Texture2D BakeGround(HeightField field)
    {
        // Use unique macro color, subtle fine variation, subdued strata and sparse impact scars.
        // Ground has no painted directional shadows: real lighting is authoritative.
        var colors=new Color32[TextureSize*TextureSize];
        Color clay=new Color32(175,121,78,255),dust=new Color32(199,154,109,255),dark=new Color32(127,92,69,255),rock=new Color32(130,106,87,255);
        for(int iz=0;iz<TextureSize;iz++)for(int ix=0;ix<TextureSize;ix++)
        {
            float x=(ix+.5f)*2*Half/TextureSize-Half,z=(iz+.5f)*2*Half/TextureSize-Half;
            float broad=Noise(x,z,.006f,11,43),medium=Noise(x,z,.019f,83,61),fine=Noise(x,z,.12f,66,10);
            float h=field.At(x,z),s=field.Slope(x,z);
            Color c=Color.Lerp(clay,dust,Smooth(.29f,.74f,broad*.72f+medium*.28f));
            c=Color.Lerp(c,dark,Smooth(.58f,.81f,Noise(x,z,.033f,79,16))*.32f);
            float strata=.72f+.28f*Mathf.Sin(h*1.3f+medium*2.5f);
            c=Color.Lerp(c,rock,Smooth(.17f,.55f,s)*strata*.68f);
            c*=Mathf.Lerp(.95f,1.05f,fine);
            Crater(x,z,out float stain);c=Color.Lerp(c,dark,stain*.55f);
            float road=1-Smooth(5f,12f,RoadDistance(x,z));c=Color.Lerp(c,dust,road*.35f);
            float pad=1-Smooth(-6f,10f,PadDistance(x,z));c=Color.Lerp(c,new Color32(164,142,119,255),pad*.40f);
            c.a=1;colors[iz*TextureSize+ix]=c;
        }
        var tex=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,false,false);
        byte[] png;
        try{tex.SetPixels32(colors);tex.Apply(false,false);png=ImageConversion.EncodeToPNG(tex);}finally{Object.DestroyImmediate(tex);}
        string path=Output+"/GroundColor.png";
        File.WriteAllBytes(Path.GetFullPath(path),png);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.isReadable=false;
        importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=TextureSize;
        importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
        return Require<Texture2D>(path);
    }

    private static void BuildRoads(HeightField field,Transform parent,Palette pal)
    {
        // Road masks and ribbons share the same centerlines. Gaps expose the ground instead of adding debris everywhere.
        Func<float,Vector2> main=t=>new Vector2(t,RoadZ(t));
        Func<float,Vector2> branch=t=>new Vector2(BranchX(t),t);
        foreach(var item in new[]{(name:"Main highway",curve:main,lo:-Half+7,hi:Half-7),(name:"Industrial spur",curve:branch,lo:BranchStart,hi:Half-7)})
        {
            string id=item.name.Replace(' ','_');
            bool Gap(float t)=>item.name=="Main highway"?(t> -303&&t< -284)||(t>365&&t<381):(t>331&&t<347);
            AddMesh(parent,item.name+" shoulder",SaveMesh(id+"_Shoulder",Ribbon(field,item.curve,item.lo,item.hi,5.3f,.30f,3f,_=>true)),pal.roadEdge,false);
            AddMesh(parent,item.name,SaveMesh(id,Ribbon(field,item.curve,item.lo,item.hi,4.25f,.43f,3f,t=>!Gap(t))),pal.asphalt,false);
            bool Mark(float t){var p=item.curve(t);if(Gap(t))return false;if(Mathf.Abs(p.x-BranchX(p.y))<12&&Mathf.Abs(p.y-RoadZ(p.x))<12)return false;
                return Mathf.Repeat(t+1000,15)<6&&Noise(p.x,p.y,.09f,21,17)>.38f;}
            AddMesh(parent,item.name+" faded markings",SaveMesh(id+"_Markings",Ribbon(field,item.curve,item.lo,item.hi,.15f,.56f,1.5f,Mark)),pal.marking,false);
        }
    }
    private static Mesh Ribbon(HeightField field,Func<float,Vector2> curve,float lo,float hi,float halfWidth,float lift,float spacing,Func<float,bool> visible)
    {
        var verts=new List<Vector3>();var tris=new List<int>();int count=Mathf.CeilToInt((hi-lo)/spacing);
        // Subdivide laterally as well, keeping ribbons close to the actual triangulated ground.
        int across=Mathf.Max(1,Mathf.CeilToInt(halfWidth*2/2.5f));
        for(int i=0;i<=count;i++)
        {
            float t=Mathf.Lerp(lo,hi,i/(float)count);var p=curve(t);var delta=(curve(t+.1f)-curve(t-.1f)).normalized;
            var normal=new Vector2(-delta.y,delta.x);
            for(int j=0;j<=across;j++)
            {var q=p+normal*Mathf.Lerp(-halfWidth,halfWidth,j/(float)across);verts.Add(new Vector3(q.x,field.At(q.x,q.y)+lift,q.y));}
            if(i==0||!visible(Mathf.Lerp(lo,hi,(i-.5f)/count)))continue;
            for(int j=0;j<across;j++){int a=(i-1)*(across+1)+j,b=i*(across+1)+j;tris.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
        }
        var m=new Mesh{vertices=verts.ToArray(),triangles=tris.ToArray()};m.RecalculateNormals();m.RecalculateBounds();return m;
    }

    private sealed class Palette
    {
        public readonly Material concrete,lightConcrete,broken,metal,rust,roof,dark,tank,asphalt,roadEdge,marking,stone,stoneDark,charred;
        public Palette(Shader s)
        {
            concrete=MaterialAsset(s,"Concrete",new Color32(158,148,133,255));lightConcrete=MaterialAsset(s,"PaleConcrete",new Color32(187,173,153,255));
            broken=MaterialAsset(s,"FracturedConcrete",new Color32(118,108,98,255));metal=MaterialAsset(s,"Steel",new Color32(86,83,77,255));
            rust=MaterialAsset(s,"Rust",new Color32(136,83,52,255));roof=MaterialAsset(s,"Roof",new Color32(119,112,100,255));
            dark=MaterialAsset(s,"DarkInterior",new Color32(74,66,60,255));tank=MaterialAsset(s,"Tank",new Color32(164,126,90,255));
            asphalt=MaterialAsset(s,"Asphalt",new Color32(105,99,92,255));roadEdge=MaterialAsset(s,"RoadEdge",new Color32(137,119,100,255));marking=MaterialAsset(s,"FadedMarking",new Color32(180,164,128,255));
            stone=MaterialAsset(s,"Sandstone",new Color32(156,116,83,255));stoneDark=MaterialAsset(s,"DarkSandstone",new Color32(129,100,79,255));charred=MaterialAsset(s,"CharredWood",new Color32(87,68,54,255));
        }
    }
    // Small reusable prefab kit: every instance references shared primitive/rock meshes and materials.
    private sealed class Modules
    {
        private readonly Palette p;private readonly Mesh cube,cylinder;
        public readonly GameObject hall,house,frame,tank,stack,pylon,barrier,stump;
        public readonly GameObject[] rocks;
        public Modules(Scene scene,Palette palette)
        {
            p=palette;cube=PrimitiveMesh(PrimitiveType.Cube);cylinder=PrimitiveMesh(PrimitiveType.Cylinder);
            hall=Prefab("RuinedFactory",Hall);house=Prefab("RuinedHouse",House);frame=Prefab("SteelFrame",Frame);
            tank=Prefab("FuelTank",Tank);stack=Prefab("Chimney",Stack);pylon=Prefab("PowerPylon",Pylon);barrier=Prefab("ConcreteBarrier",Barrier);stump=Prefab("BurntTree",Stump);
            rocks=new GameObject[3];
            for(int i=0;i<rocks.Length;i++)
            {
                int id=i;var m=SaveMesh("Rock"+i,RockMesh(907+i*73));
                rocks[i]=Prefab("Rock"+i,t=>AddMesh(t,"Stone",m,id==1?p.stoneDark:p.stone));
            }
        }
        private GameObject Prefab(string name,Action<Transform> build)
        {
            var go=new GameObject(name);
            try{build(go.transform);var asset=PrefabUtility.SaveAsPrefabAsset(go,Output+"/Modules/"+name+".prefab");if(asset==null)throw new InvalidOperationException("Cannot save module "+name);return asset;}
            finally{Object.DestroyImmediate(go);}
        }
        private void Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,float yaw=0)
        {var t=AddMesh(parent,name,cube,mat).transform;t.localPosition=pos;t.localScale=size;t.localRotation=Quaternion.Euler(0,yaw,0);}
        private void Tube(Transform parent,string name,Vector3 pos,float diameter,float height,Material mat)
        {var t=AddMesh(parent,name,cylinder,mat).transform;t.localPosition=pos;t.localScale=new Vector3(diameter,height*.5f,diameter);}
        private void Beam(Transform parent,string name,Vector3 a,Vector3 b,float width,Material mat)
        {var d=b-a;var t=AddMesh(parent,name,cube,mat).transform;t.localPosition=(a+b)*.5f;t.localRotation=Quaternion.FromToRotation(Vector3.up,d.normalized);t.localScale=new Vector3(width,d.magnitude,width);}
        private void Hall(Transform t)
        {
            Box(t,"Slab",new Vector3(0,.25f,0),new Vector3(34,.5f,24),p.concrete);
            Box(t,"Dark floor",new Vector3(0,.56f,0),new Vector3(29,.15f,19),p.broken);
            // Solid end wall, broken bays along the sides and a missing roof section.
            Box(t,"End wall",new Vector3(-16,5.5f,0),new Vector3(1.4f,11,23),p.concrete);
            foreach(float z in new[]{-11f,11f})
            {
                for(int i=0;i<5;i++)
                {
                    float x=-14+i*7f;float height=(z>0&&i>=3)?(i==3?5f:3f):10.5f;
                    Box(t,"Column",new Vector3(x,height*.5f,z),new Vector3(1.65f,height,1.4f),p.lightConcrete);
                    if(i==4)continue;
                    float sill=(i==2&&z<0)?1.2f:3.2f;
                    Box(t,"Broken lower wall",new Vector3(x+3.5f,sill*.5f,z),new Vector3(5.5f,sill,1.0f),p.concrete);
                    if(height>8&&!(z<0&&i==2))Box(t,"Lintel",new Vector3(x+3.5f,9.4f,z),new Vector3(5.5f,1.5f,1.1f),p.concrete);
                }
            }
            Box(t,"Surviving roof slab",new Vector3(-8,11.1f,0),new Vector3(17,1.05f,24),p.roof);
            Box(t,"Roof step",new Vector3(-9,11.9f,-4),new Vector3(14,.6f,7),p.lightConcrete);
            for(int i=0;i<3;i++){float x=2+i*6;Beam(t,"Exposed roof beam",new Vector3(x,10.6f,-10.5f),new Vector3(x,10.6f,10.5f),.85f,p.metal);}
            Box(t,"Broken equipment",new Vector3(7,1.6f,3),new Vector3(5,2,4),p.dark,15);
            Box(t,"Collapsed panel",new Vector3(18,.9f,5),new Vector3(5,1.4f,3),p.broken,26);
        }
        private void House(Transform t)
        {
            Box(t,"Foundation",new Vector3(0,.2f,0),new Vector3(16,.4f,13),p.concrete);
            Box(t,"Back wall",new Vector3(0,3.6f,6),new Vector3(15,7.2f,1),p.concrete);
            Box(t,"Left wall",new Vector3(-7.2f,3.5f,.5f),new Vector3(1,7,11),p.lightConcrete);
            Box(t,"Right fragment",new Vector3(7.2f,1.8f,2.5f),new Vector3(1,3.6f,7),p.concrete);
            foreach(float x in new[]{-6f,0f,6f})Box(t,"Front pier",new Vector3(x,3.4f,-5.8f),new Vector3(1.55f,6.8f,1),p.lightConcrete);
            Box(t,"Front lintel",new Vector3(-3,6.6f,-5.8f),new Vector3(7,1,1),p.concrete);
            Box(t,"Roof remnant",new Vector3(-4,7.2f,2),new Vector3(8,.8f,9),p.roof);
            Box(t,"Rubble slab",new Vector3(4,.8f,1),new Vector3(4,1,5),p.broken,32);
        }
        private void Frame(Transform t)
        {
            Box(t,"Slab",new Vector3(0,.22f,0),new Vector3(26,.44f,20),p.broken);
            for(int i=0;i<4;i++)
            {
                float x=-11+i*7.3f;
                foreach(float z in new[]{-8f,8f})Box(t,"Upright",new Vector3(x,5.5f,z),new Vector3(.9f,11,.9f),p.metal);
                Beam(t,"Roof span",new Vector3(x,10.8f,-8),new Vector3(x,10.8f,8),.75f,p.rust);
                if(i==3)continue;
                foreach(float z in new[]{-8f,8f})
                {Beam(t,"Longitudinal beam",new Vector3(x,10.8f,z),new Vector3(x+7.3f,10.8f,z),.65f,p.metal);
                 if(i!=1)Beam(t,"Brace",new Vector3(x,1,z),new Vector3(x+7.3f,10.8f,z),.38f,p.rust);}
            }
            Box(t,"Roof fragment",new Vector3(-7.5f,11.35f,1.5f),new Vector3(7.4f,.45f,14),p.roof);
        }
        private void Tank(Transform t)
        {
            Tube(t,"Concrete ring",new Vector3(0,.3f,0),15,.6f,p.concrete);
            Tube(t,"Tank shell",new Vector3(0,4.9f,0),13,9.2f,p.tank);
            foreach(float y in new[]{1.0f,5.1f,9.25f})Tube(t,"Rust band",new Vector3(0,y,0),13.25f,.44f,p.rust);
            Tube(t,"Lid",new Vector3(0,9.8f,0),13.1f,.75f,p.rust);
            Tube(t,"Vent",new Vector3(2,10.8f,-1),1.5f,1.8f,p.metal);
            Box(t,"Service walkway",new Vector3(7,8.9f,0),new Vector3(2.8f,.45f,5f),p.metal);
            for(int i=0;i<5;i++)Box(t,"Ladder rung",new Vector3(6.8f,1.6f+i*1.5f,0),new Vector3(.65f,.18f,1.6f),p.metal);
        }
        private void Stack(Transform t)
        {
            Tube(t,"Base",new Vector3(0,1,0),7,2,p.concrete);Tube(t,"Stack",new Vector3(0,9,0),4.3f,17,p.broken);
            Tube(t,"Weathered band",new Vector3(0,12.5f,0),4.45f,2,p.rust);Tube(t,"Rim",new Vector3(0,17.5f,0),4.6f,.65f,p.concrete);
            Tube(t,"Dark opening",new Vector3(0,17.85f,0),3.2f,.12f,p.dark);
        }
        private void Pylon(Transform t)
        {
            foreach(float x in new[]{-2.3f,2.3f})foreach(float z in new[]{-2.3f,2.3f})
            {Box(t,"Foot",new Vector3(x,.25f,z),new Vector3(1.4f,.5f,1.4f),p.concrete);Beam(t,"Leg",new Vector3(x,.5f,z),new Vector3(x*.25f,13,z*.25f),.36f,p.metal);}
            for(int i=0;i<3;i++){float y=1+i*4;float a=Mathf.Lerp(2.3f,.58f,y/13);float b=Mathf.Lerp(2.3f,.58f,(y+4)/13);
                foreach(float z in new[]{-1f,1f}){Beam(t,"Cross brace",new Vector3(-a,y,z*a),new Vector3(b,y+4,z*b),.22f,p.rust);Beam(t,"Cross brace",new Vector3(a,y,z*a),new Vector3(-b,y+4,z*b),.22f,p.metal);}}
            Box(t,"Crossbar",new Vector3(0,12.5f,0),new Vector3(11,.45f,.65f),p.metal);
            foreach(float x in new[]{-4.5f,4.5f})Box(t,"Insulator",new Vector3(x,11.9f,0),new Vector3(.6f,1.4f,.6f),p.lightConcrete);
        }
        private void Barrier(Transform t){Box(t,"Base",new Vector3(0,.45f,0),new Vector3(8,.9f,2),p.broken);Box(t,"Barrier",new Vector3(0,1.3f,0),new Vector3(7.5f,1.8f,1.05f),p.concrete);}
        private void Stump(Transform t)
        {Beam(t,"Trunk",Vector3.zero,new Vector3(.2f,4.3f,0),.65f,p.charred);Beam(t,"Broken branch",new Vector3(.1f,2.8f,0),new Vector3(-1.8f,4.4f,.3f),.32f,p.charred);Beam(t,"Broken branch",new Vector3(.1f,2.1f,0),new Vector3(1.5f,3.8f,-.6f),.28f,p.charred);}
    }
    private static Mesh RockMesh(int seed)
    {
        const int sides=7;var rng=new System.Random(seed);float Next(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
        var rings=new Vector3[sides*3];float[] radius={.49f,.53f,.30f};float[] height={-.09f,.38f,.81f};
        for(int j=0;j<3;j++)for(int i=0;i<sides;i++){float a=(i+j*.15f)*Mathf.PI*2/sides;float r=radius[j]*Next(.78f,1.18f);rings[j*sides+i]=new Vector3(Mathf.Cos(a)*r,height[j]+Next(-.09f,.09f),Mathf.Sin(a)*r);}
        var verts=new List<Vector3>();
        void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 direction){if(Vector3.Dot(Vector3.Cross(b-a,c-a),direction)<0){var t=b;b=c;c=t;}verts.Add(a);verts.Add(b);verts.Add(c);}
        for(int i=0;i<sides;i++)
        {
            int next=(i+1)%sides;for(int j=0;j<2;j++){var a=rings[j*sides+i];var b=rings[j*sides+next];var c=rings[(j+1)*sides+i];var d=rings[(j+1)*sides+next];var dir=new Vector3(a.x+b.x,0,a.z+b.z);Tri(a,b,c,dir);Tri(b,d,c,dir);}
            Tri(rings[2*sides+i],rings[2*sides+next],new Vector3(.07f,.91f,-.05f),Vector3.up);
            Tri(rings[i],rings[next],new Vector3(0,-.18f,0),Vector3.down);
        }
        var m=new Mesh{vertices=verts.ToArray(),triangles=Enumerable.Range(0,verts.Count).ToArray()};m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    private static void PlaceSettlements(Scene scene,HeightField f,Transform owner,Modules m)
    {
        Transform Site(string name)=>Group(owner,name);
        void Put(Transform site,GameObject prefab,float x,float z,float yaw=0,float scale=1f)=>Place(scene,f,site,prefab,x,z,yaw,new Vector3(scale,scale,scale));
        var s=Site("01 - abandoned refinery");Put(s,m.hall,65,30,0);Put(s,m.frame,110,39,90,.82f);Put(s,m.stack,44,36);Put(s,m.tank,75,1);Put(s,m.tank,92,1);Put(s,m.tank,113,9,30,.76f);
        Put(s,m.barrier,53,-9,0);Put(s,m.barrier,64,-9,0);Put(s,m.house,39,1,90,.76f);
        s=Site("02 - ruined roadside village");Put(s,m.house,-142,-101,4);Put(s,m.house,-118,-101,180,.9f);Put(s,m.house,-95,-103,-8,1.06f);Put(s,m.house,-140,-129,90,.92f);Put(s,m.house,-112,-133,185);Put(s,m.barrier,-91,-124,90);
        s=Site("03 - industrial yard");Put(s,m.frame,210,240,0);Put(s,m.hall,244,235,0,.88f);Put(s,m.tank,208,213,0,.9f);Put(s,m.tank,225,207,30,.7f);Put(s,m.stack,257,213,0,.85f);Put(s,m.barrier,260,253,90);
        s=Site("04 - transmission station");Put(s,m.frame,-260,206,90,.9f);Put(s,m.house,-285,187,0,.75f);Put(s,m.pylon,-230,185);Put(s,m.pylon,-228,220,5);Put(s,m.barrier,-283,217,90);
        s=Site("05 - abandoned checkpoint");Put(s,m.house,320,-244,0,.95f);Put(s,m.house,347,-233,180,.7f);Put(s,m.barrier,339,-256,0);Put(s,m.barrier,349,-256,0);Put(s,m.pylon,360,-248,0,.85f);
        // Rare infrastructure landmarks along the highway, outside the road itself.
        var line=Site("Dismantled power line");foreach(float x in new[]{-380f,-305f,-225f,-55f,135f,415f})Put(line,m.pylon,x,RoadZ(x)+17,12,.85f);
    }
    private static int ScatterNature(Scene scene,HeightField f,Transform owner,Modules m)
    {
        var rng=new System.Random(130509);float Next(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());int count=0;
        // Loose clusters at ridge shoulders, with the low ground left mostly empty.
        for(int i=0;i<1600&&count<125;i++)
        {
            float x=Next(-560,560),z=Next(-560,560);if(PadDistance(x,z)<8||RoadDistance(x,z)<13)continue;
            float h=f.At(x,z),s=f.Slope(x,z);if(h<7||s<.12f||s>.85f||Noise(x,z,.028f,7,61)<.56f)continue;
            float w=Next(2.7f,9.5f);Place(scene,f,owner,m.rocks[rng.Next(m.rocks.Length)],x,z,Next(0,360),new Vector3(w,w*Next(.50f,.96f),w*Next(.7f,1.15f)));count++;
        }
        for(int i=0;i<350&&count<141;i++)
        {
            float x=Next(-500,500),z=Next(-500,500);if(PadDistance(x,z)<10||RoadDistance(x,z)<12||f.Slope(x,z)>.2f||Noise(x,z,.015f,60,4)<.64f)continue;
            float scale=Next(.70f,1.18f);Place(scene,f,owner,m.stump,x,z,Next(0,360),Vector3.one*scale);count++;
        }
        return count;
    }
    private static void Place(Scene scene,HeightField field,Transform parent,GameObject prefab,float x,float z,float yaw,Vector3 scale)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);if(go==null)throw new InvalidOperationException("Cannot instantiate "+prefab.name);
        var t=go.transform;t.SetParent(parent,false);t.SetPositionAndRotation(new Vector3(x,field.At(x,z),z),Quaternion.Euler(0,yaw,0));t.localScale=scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }
    private static Mesh PrimitiveMesh(PrimitiveType type)
    {var go=GameObject.CreatePrimitive(type);try{return go.GetComponent<MeshFilter>().sharedMesh;}finally{Object.DestroyImmediate(go);}}
    private static Transform Group(Transform owner,string name){var t=new GameObject(name).transform;t.SetParent(owner,false);return t;}
    private static MeshRenderer AddMesh(Transform owner,string name,Mesh mesh,Material mat,bool cast=true)
    {
        var t=Group(owner,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
        r.shadowCastingMode=cast?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;return r;
    }
    private static Material MaterialAsset(Shader shader,string name,Color color)
    {
        var m=new Material(shader){name=name,enableInstancing=true};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.045f);return SaveAsset(name+".mat",m);
    }
    private static Mesh SaveMesh(string name,Mesh mesh){mesh.name=name;return SaveAsset(name+".asset",mesh);}
    private static T SaveAsset<T>(string file,T asset) where T:Object
    {string path=Output+"/"+file;var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(asset,path);return asset;}EditorUtility.CopySerialized(asset,old);Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);return old;}
    private static void EnsureFolder(string path){var parent=Path.GetDirectoryName(path).Replace('\\','/');if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,Path.GetFileName(path));if(!AssetDatabase.IsValidFolder(path))throw new InvalidOperationException("Cannot create "+path);}
    private static GameObject Root(Scene scene,string name)=>scene.GetRootGameObjects().Single(g=>g.name==name);
    private static T Require<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing "+path);
}
