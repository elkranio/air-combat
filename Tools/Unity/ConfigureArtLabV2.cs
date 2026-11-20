using System;
using System.Collections.Generic;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Independent visual study. Does not modify shared art, pipeline settings or Sandbox.
public static class ConfigureArtLabV2
{
    private const string ScenePath = "Assets/_Project/Scenes/ArtLabV2.unity";
    private const string Output = "Assets/_Project/Generated/ArtLabV2";
    private const string Art = "Assets/_Project/Art/";
    private const float HalfX = 250f, HalfZ = 210f;
    private const float WaterY = -.70f;

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        var aircraft = Require<GameObject>(Art + "Aircraft/XF-01-Kestrel.fbx");
        var hangar = Require<GameObject>(Art + "Buildings/Hangar-04/Hangar-04-Baked.prefab");
        var turret = Require<GameObject>(Art + "Buildings/MT-01-Sentinel/MT-01-Sentinel.prefab");
        var enemyMats = new Dictionary<string, Material>();
        foreach (string name in new[] { "Kestrel_Hull", "Kestrel_Upper", "Kestrel_Panels", "Kestrel_Amber" })
            enemyMats.Add(name, Require<Material>(Art + "Aircraft/EnemyMaterials/Enemy_" + name + ".mat"));
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var effect = Shader.Find("AirCombat/SandboxEffect");
        if (lit == null || unlit == null || effect == null) throw new InvalidOperationException("Missing study shaders.");
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "ArtLabV2");
        if (!AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Could not create ArtLabV2 output.");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("ArtLab V2");
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 80f;
        camera.nearClipPlane = .3f; camera.farClipPlane = 300f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("698D50");
        var rotation = Quaternion.Euler(60, 0, 0);
        camera.transform.SetPositionAndRotation(new Vector3(0, 12, 0) - rotation * Vector3.forward * (88f / Mathf.Sin(60 * Mathf.Deg2Rad)), rotation);
        var sun = new GameObject("Art Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(48, -38, 0);
        sun.shadows = LightShadows.None; // static contact shadows below; no shared URP changes
        RenderSettings.sun = sun; RenderSettings.skybox = null;
        RenderSettings.fog = false; RenderSettings.reflectionIntensity = .15f;
        var builder = new Builder(lit, unlit, effect);
        builder.Build(root.transform);
        var models = Group(root.transform, "Fixed art references");
        PlaceModel(scene, hangar, models, "Hangar A", new Vector3(-7, .08f, 32), 30, -90);
        PlaceModel(scene, hangar, models, "Hangar B", new Vector3(-7, .08f, -21), 30, -90);
        PlaceModel(scene, turret, models, "Sentinel A", new Vector3(-94, Height(-94,40)+.12f, 40), 14, 35);
        PlaceModel(scene, turret, models, "Sentinel B", new Vector3(26, Height(26,-43)+.12f, -43), 14, -35);
        PlaceModel(scene, aircraft, models, "Kestrel - blue", new Vector3(24, 12, 19), 11, 35, true);
        PlaceModel(scene, aircraft, models, "Kestrel - red", new Vector3(45, 12, -24), 10.5f, -55, true, enemyMats);
        root.AddComponent<ArtLabLightingPreview>().Configure(sun);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save ArtLabV2.");
        Selection.activeGameObject = root;
        return "ArtLab v2 ready. L: compare lighting, H: hide legend. Original ArtLab and Sandbox unchanged.";
    }

    private static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a,b,t));
    private static float Hill(float x,float z,float cx,float cz,float rx,float rz)
    { float u=(x-cx)/rx,v=(z-cz)/rz; return Mathf.Exp(-2.0f*(u*u+v*v)); }
    private static float RiverX(float z) => 102f + 9.5f*Mathf.Sin(z*.025f+.8f) + 4.2f*Mathf.Sin(z*.052f-1.1f);
    private static float RiverWidth(float z) => 11.8f+2.0f*Mathf.Sin(z*.031f+1.4f)+1.1f*Mathf.Sin(z*.084f);
    private static float Shore(float x,float z) => Mathf.Abs(x-RiverX(z))-RiverWidth(z);
    private static float Height(float x,float z)
    {
        float hills=5.6f*Hill(x,z,-144,-34,65,68)+4.2f*Hill(x,z,-142,30,56,44)
            +4.2f*Hill(x,z,58,-87,35,51)+5.4f*Hill(x,z,153,32,45,68)
            +2.5f*Hill(x,z,52,54,40,38);
        hills+=Mathf.PerlinNoise(x*.025f+23,z*.025f+11)*1.25f;
        // Runway and apron are flat. Smoothly reconnect them to the foothills.
        float outside=Mathf.Max(Mathf.Abs(x+43)-51,Mathf.Abs(z+7)-76);
        hills*=Smooth(0,20,outside)*Smooth(6,18,Mathf.Abs(z-78));
        float shore=Shore(x,z);
        return Mathf.Lerp(-2.9f,hills,Smooth(-5,5,shore));
    }
    private static float SurfaceY(float x,float z)
    {
        float y = Height(x,z);
        if (Mathf.Abs(x+43)<=50 && Mathf.Abs(z+7)<=75) y=Mathf.Max(y,.04f);
        if (Mathf.Abs(x+55)<=11.5f && Mathf.Abs(z+7)<=71.5f) y=Mathf.Max(y,.064f);
        if (Mathf.Abs(z-78)<5.8f) y=Mathf.Max(y,.16f);
        return y;
    }

    private sealed class Builder
    {
        private readonly Shader lit, unlit;
        private readonly Material shadow;
        private readonly Dictionary<string,Mesh> meshes = new Dictionary<string, Mesh>();
        private readonly List<Vector3> shadowVerts = new List<Vector3>();
        private readonly List<Color32> shadowColors = new List<Color32>();
        private readonly List<int> shadowTris = new List<int>();
        private Material trunk,leaf,leafLight,leafDark,rock,rockDark;
        public Builder(Shader lit,Shader unlit,Shader effect)
        { this.lit=lit;this.unlit=unlit;shadow=Mat("ContactShadow",effect,"FFFFFF"); }

        public void Build(Transform root)
        {
            var terrain = Group(root,"Terrain / soft foothills");
            var groundTex=Texture("GroundColor",1024,1024,GroundColor);
            var groundMat=Mat("Ground",lit,"FFFFFF",groundTex);
            Add(terrain,"Terrain mesh",MeshAsset("TerrainMesh",TerrainMesh()),groundMat,Vector3.zero,Vector3.one);
            var waterTex=Texture("WaterColor",128,1024,WaterColor);
            // Painted water avoids specular white wash and gives an identifiable shallow edge.
            Add(terrain,"River",MeshAsset("RiverMesh",RiverMesh()),Mat("Water",unlit,"FFFFFF",waterTex),Vector3.zero,Vector3.one);
            var pavement=Mat("Apron",lit,"B8B19A",Texture("ConcreteColor",256,256,ConcreteColor));
            var asphalt=Mat("Runway",lit,"424E57");
            var road=Mat("Road",lit,"888C77");
            var seam=Mat("ConcreteSeam",lit,"AAA38D");
            var curb=Mat("RunwayShoulder",lit,"9C9987");
            var paint=Mat("Markings",lit,"EEE7CB");
            var airfield=Group(root,"Airfield / worn concrete");
            Rect(airfield,"Apron",-43,-7,100,150,.04f,pavement);
            Rect(airfield,"Runway shoulder",-55,-7,27,145,.052f,curb);
            Rect(airfield,"Runway",-55,-7,23,143,.064f,asphalt);
            Rect(airfield,"Taxiway north",-31,22,26,10,.07f,road);
            Rect(airfield,"Taxiway south",-31,-34,26,10,.07f,road);
            // Restrained seams on the large apron, sized to survive the game camera.
            for(int z=-74;z<=66;z+=14) Rect(airfield,"Apron seam",-16,z,42,.22f,.052f,seam);
            for(int x=-34;x<=3;x+=13) Rect(airfield,"Apron seam",x,-7,.22f,148,.052f,seam);
            for(int z=-55;z<=38;z+=15) Rect(airfield,"Centerline",-55,z,1.3f,7.5f,.078f,paint);
            for(int x=-63;x<=-47;x+=4)
            { Rect(airfield,"Threshold",x,-73,2,7,.078f,paint);Rect(airfield,"Threshold",x,59,2,7,.078f,paint); }
            // Keep the bridge visibly separate from the bank and water.
            Rect(airfield,"Access road",0,78,400,9,.09f,road);
            Rect(airfield,"Bridge deck",RiverX(78),78,34,11,.16f,pavement);
            for(int side=-1;side<=1;side+=2)
                Rect(airfield,"Bridge parapet",RiverX(78),78+side*5.2f,34,.55f,.32f,curb);

            trunk=Mat("Bark",lit,"77513A");leaf=Mat("Leaf",lit,"427B42");
            leafLight=Mat("LeafLight",lit,"639442");leafDark=Mat("LeafDark",lit,"2E5D40");
            rock=Mat("Rock",lit,"9A9581");rockDark=Mat("RockDark",lit,"777E73");
            var nature=Group(root,"Nature studies / layered crowns");
            Grove(nature,-122,25,31,19,42);
            Grove(nature,69,-66,27,17,56);
            Grove(nature,49,61,17,8,65);
            Grove(nature,156,4,31,15,71);
            Rocks(nature,-134,-47,7,36); Rocks(nature,-111,-57,5,81);
            Rocks(nature,78,34,6,18); Rocks(nature,155,-42,8,76);
            // fixed model shadows match the direction of the sun; no shadow maps needed.
            Shadow(-7,32,28,30,125);Shadow(-10,35,34,33,72);
            Shadow(-7,-21,28,30,125);Shadow(-10,-18,34,33,72);
            Shadow(-94,40,15,15,115);Shadow(-97,43,19,19,60);
            Shadow(26,-43,15,15,115);Shadow(23,-40,19,19,60);
            Shadow(6,37,10,12,72);Shadow(27,-6,10,12,72);
            var sm=new Mesh { vertices=shadowVerts.ToArray(), triangles=shadowTris.ToArray(), colors32=shadowColors.ToArray() };
            sm.RecalculateBounds();
            Add(root,"Conforming contact shadows",MeshAsset("ContactShadowMesh",sm),shadow,Vector3.zero,Vector3.one);
        }

        // Cached color constants for the million-pixel terrain bake.
        private static readonly Color C608749 = Hex("608749");
        private static readonly Color C92A650 = Hex("92A650");
        private static readonly Color C477849 = Hex("477849");
        private static readonly Color C426A43 = Hex("426A43");
        private static readonly Color CB3A371 = Hex("B3A371");
        private static readonly Color CBBA47B = Hex("BBA47B");
        private static readonly Color C286B7B = Hex("286B7B");
        private static readonly Color C63A99D = Hex("63A99D");
        private static readonly Color C89C1AD = Hex("89C1AD");
        private static readonly Color CB5D4B2 = Hex("B5D4B2");

        private Color GroundColor(float u,float v)
        {
            float x=Mathf.Lerp(-HalfX,HalfX,u), z=Mathf.Lerp(-HalfZ,HalfZ,v);
            float n=Mathf.PerlinNoise(x*.013f+17,z*.013f+42), m=Mathf.PerlinNoise(x*.037f+56,z*.037f+14);
            float d=Mathf.PerlinNoise(x*.18f+12,z*.18f+5);
            Color c=Color.Lerp(C608749,C92A650,Smooth(.28f,.72f,n));
            c=Color.Lerp(c,C477849,Smooth(.48f,.84f,m)*.68f);
            float wood=Mathf.Max(Hill(x,z,-122,25,42,42),Mathf.Max(Hill(x,z,69,-66,37,42),Hill(x,z,156,4,40,45)));
            c=Color.Lerp(c,C426A43,Smooth(.04f,.84f,wood)*.78f);
            c=Color.Lerp(c,CB3A371,Smooth(.75f,.93f,m)*.42f);
            c=Color.Lerp(c,CBBA47B,1-Smooth(1.5f,8f,Shore(x,z)+(m-.5f)*4));
            c*=.96f+.08f*d; c.a=1; return c;
        }
        private Color WaterColor(float u,float v)
        {
            float edge=Mathf.Abs(u*2-1);
            float noise=Mathf.PerlinNoise(u*5+19,v*26+4);
            float ripple=Mathf.PerlinNoise(u*9+4,v*96+17);
            Color c=Color.Lerp(C286B7B,C63A99D,Smooth(.38f,.93f,edge));
            c=Color.Lerp(c,C89C1AD,Smooth(.62f,.92f,noise)*.14f);
            c=Color.Lerp(c,CB5D4B2,Smooth(.82f,.96f,ripple)*.22f);
            return c;
        }
        private Color ConcreteColor(float u,float v)
        {
            float n=Mathf.PerlinNoise(u*10+7,v*15+5), fine=Mathf.PerlinNoise(u*60+20,v*60+8);
            float f=.93f+.07f*n+.035f*fine;return new Color(f,f,f,1);
        }

        private void Grove(Transform owner,float cx,float cz,float radius,int count,int seed)
        {
            var rng=new System.Random(seed);
            float Next(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)rng.NextDouble());
            var crown=Get("Crown",()=>Ring(new[]{.24f,.47f,.49f,.38f,.17f},new[]{0f,.21f,.44f,.72f,.91f},9,.10f));
            var pine=Get("PineTier",()=>Ring(new[]{.40f,.49f,.30f,.07f},new[]{0f,.12f,.55f,.93f},8,.06f));
            var barkMesh=Get("Trunk",()=>Ring(new[]{.5f,.37f},new[]{0f,1f},6,0));
            for(int i=0;i<count;i++)
            {
                float a=i*2.399963f+Next(-.13f,.13f),r=radius*Mathf.Sqrt((i+.5f)/count);
                float x=cx+Mathf.Cos(a)*r,z=cz+Mathf.Sin(a)*r;
                if(Shore(x,z)<5||Mathf.Abs(z-78)<8|| (Mathf.Abs(x+43)<53&&Mathf.Abs(z+7)<78))continue;
                // Keep the fixed turret and both aircraft silhouettes clear.
                if(Vector2.Distance(new Vector2(x,z),new Vector2(-94,40))<10 || Vector2.Distance(new Vector2(x,z),new Vector2(26,-43))<10)continue;
                float s=Next(6.2f,9.2f), y=Height(x,z);
                var tree=Group(owner,"Tree");tree.localPosition=new Vector3(x,y,z);tree.localRotation=Quaternion.Euler(0,Next(0,360),0);
                Add(tree,"Trunk",barkMesh,trunk,Vector3.zero,new Vector3(s*.15f,s*.47f,s*.15f));
                if(i%4==1)
                {
                    Add(tree,"Lower tier",pine,leafDark,new Vector3(0,s*.18f,0),new Vector3(s*.78f,s*.59f,s*.78f));
                    Add(tree,"Middle tier",pine,leaf,new Vector3(0,s*.41f,0),new Vector3(s*.61f,s*.57f,s*.61f));
                    Add(tree,"Top tier",pine,leafLight,new Vector3(0,s*.64f,0),new Vector3(s*.44f,s*.45f,s*.44f));
                }
                else
                {
                    Add(tree,"Main crown",crown,i%3==0?leafDark:leaf,new Vector3(0,s*.25f,0),new Vector3(s*.85f,s*.62f,s*.78f));
                    Add(tree,"Sunward crown",crown,leafLight,new Vector3(s*.22f,s*.40f,-s*.12f),new Vector3(s*.59f,s*.46f,s*.58f));
                    Add(tree,"Side crown",crown,leaf,new Vector3(-s*.27f,s*.23f,s*.16f),new Vector3(s*.58f,s*.58f,s*.59f));
                }
                Shadow(x-1.4f,z+1.4f,s*1.35f,s*1.35f,100);
                Shadow(x-3,z+3,s*1.7f,s*1.5f,53);
            }
        }
        private void Rocks(Transform owner,float cx,float cz,float size,int seed)
        {
            var rng=new System.Random(seed);
            var mesh=Get("Rock",()=>Ring(new[]{.35f,.5f,.39f,.17f},new[]{0f,.19f,.58f,.84f},7,.24f));
            for(int i=0;i<4;i++)
            {
                float a=i*2.399963f, r=i==0?0:size*.64f;
                float x=cx+Mathf.Cos(a)*r,z=cz+Mathf.Sin(a)*r,s=size*(i==0?1:.28f+.26f*(float)rng.NextDouble());
                var t=Add(owner,"Rock",mesh,i%2==0?rock:rockDark,new Vector3(x,Height(x,z)-.15f,z),new Vector3(s,s*.55f,s*.83f));
                t.localRotation=Quaternion.Euler(0,(float)rng.NextDouble()*360f,0);
                Shadow(x-1,z+1,s*1.4f,s*1.1f,94);
            }
        }
        private void Shadow(float x,float z,float w,float d,int opacity)
        {
            const int n=24;
            int first=shadowVerts.Count;
            void Vertex(float px,float pz,int alpha)
            { shadowVerts.Add(new Vector3(px,SurfaceY(px,pz)+.13f,pz));shadowColors.Add(new Color32(34,48,61,(byte)alpha)); }
            Vertex(x,z,opacity);
            for(int ring=0;ring<3;ring++)
            for(int i=0;i<n;i++)
            { float a=i*Mathf.PI*2/n,r=new[]{.21f,.38f,.59f}[ring];Vertex(x+Mathf.Cos(a)*w*r,z+Mathf.Sin(a)*d*r,(int)(opacity*new[]{.86f,.46f,0f}[ring])); }
            for(int i=0;i<n;i++)
            {
                int a=first+1+i,b=first+1+(i+1)%n;shadowTris.AddRange(new[]{first,b,a});
                for(int ring=0;ring<2;ring++){shadowTris.AddRange(new[]{a,b,b+n,a,b+n,a+n});a+=n;b+=n;}
            }
        }
        private void Rect(Transform owner,string name,float x,float z,float w,float d,float y,Material mat)
        { Add(owner,name,Get("Quad",Quad),mat,new Vector3(x,y,z),new Vector3(w,1,d)); }
        private Mesh Get(string name,Func<Mesh> create)
        { if(!meshes.TryGetValue(name,out var m)){m=MeshAsset(name,create());meshes.Add(name,m);}return m; }
        private Material Mat(string name,Shader shader,string hex,Texture2D texture=null)
        {
            string path=Output+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",Hex(hex));
            if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",texture);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.05f);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(m);return m;
        }
    }

    private static Texture2D Texture(string name,int w,int h,Func<float,float,Color> sample)
    {
        string path=Output+"/"+name+".asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool fresh=tex==null;if(fresh)tex=new Texture2D(w,h,TextureFormat.RGBA32,true,false);
        else if(tex.width!=w||tex.height!=h)tex.Reinitialize(w,h,TextureFormat.RGBA32,true);
        var colors=new Color32[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)colors[y*w+x]=sample(x/(float)(w-1),y/(float)(h-1));
        tex.name=name;tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Trilinear;
        tex.SetPixels32(colors);tex.Apply(true,false);
        if(fresh)AssetDatabase.CreateAsset(tex,path);else EditorUtility.SetDirty(tex);return tex;
    }
    private static Mesh TerrainMesh()
    {
        const int nx=200,nz=168;var v=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[v.Length];var tris=new int[nx*nz*6];int k=0;
        for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
        { int i=z*(nx+1)+x;float u=x/(float)nx,t=z/(float)nz;float wx=Mathf.Lerp(-HalfX,HalfX,u),wz=Mathf.Lerp(-HalfZ,HalfZ,t);v[i]=new Vector3(wx,Height(wx,wz),wz);uv[i]=new Vector2(u,t); }
        for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
        { int a=z*(nx+1)+x,b=a+nx+1;tris[k++]=a;tris[k++]=b;tris[k++]=a+1;tris[k++]=a+1;tris[k++]=b;tris[k++]=b+1; }
        var m=new Mesh { vertices=v,uv=uv,triangles=tris };m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    private static Mesh RiverMesh()
    {
        const int steps=168;var v=new Vector3[(steps+1)*2];var uv=new Vector2[v.Length];var t=new int[steps*6];
        for(int i=0;i<=steps;i++)
        { float f=i/(float)steps,z=Mathf.Lerp(-HalfZ,HalfZ,f),x=RiverX(z),w=RiverWidth(z)+4.5f;v[2*i]=new Vector3(x-w,WaterY,z);v[2*i+1]=new Vector3(x+w,WaterY,z);uv[2*i]=new Vector2(0,f);uv[2*i+1]=new Vector2(1,f);if(i==steps)continue;int a=2*i,k=6*i;t[k]=a;t[k+1]=a+2;t[k+2]=a+1;t[k+3]=a+1;t[k+4]=a+2;t[k+5]=a+3; }
        var m=new Mesh{vertices=v,uv=uv,triangles=t};m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    private static Mesh Quad()
    { var m=new Mesh{vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)},uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right},triangles=new[]{0,1,2,0,2,3}};m.RecalculateNormals();m.RecalculateBounds();return m; }
    private static Mesh Ring(float[] radii,float[] heights,int n,float irregular)
    {
        var p=new List<Vector3>();var v=new List<Vector3>();
        for(int r=0;r<radii.Length;r++)for(int i=0;i<n;i++)
        { float a=i*Mathf.PI*2/n+(r%2)*.14f,rad=radii[r]*(1+irregular*Mathf.Sin(i*2.7f+r));p.Add(new Vector3(Mathf.Cos(a)*rad,heights[r],Mathf.Sin(a)*rad)); }
        void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 outward){if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0){var tmp=b;b=c;c=tmp;}v.Add(a);v.Add(b);v.Add(c);}
        for(int r=0;r<radii.Length-1;r++)for(int i=0;i<n;i++)
        {int a=r*n+i,b=r*n+(i+1)%n;var outward=p[a]+p[b];outward.y=0;Tri(p[a],p[b],p[b+n],outward);Tri(p[a],p[b+n],p[a+n],outward);}
        int end=(radii.Length-1)*n;
        for(int i=0;i<n;i++){int j=(i+1)%n;Tri(Vector3.up*heights[0],p[i],p[j],Vector3.down);Tri(Vector3.up*(heights[heights.Length-1]+.04f),p[end+i],p[end+j],Vector3.up);}
        var m=new Mesh{vertices=v.ToArray(),triangles=Enumerable.Range(0,v.Count).ToArray()};m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    private static Mesh MeshAsset(string name,Mesh fresh)
    {
        string path=Output+"/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);fresh.name=name;
        if(m==null){AssetDatabase.CreateAsset(fresh,path);return fresh;}EditorUtility.CopySerialized(fresh,m);UnityEngine.Object.DestroyImmediate(fresh);EditorUtility.SetDirty(m);return m;
    }
    private static Transform Add(Transform root,string name,Mesh mesh,Material mat,Vector3 position,Vector3 scale)
    {var t=Group(root,name);t.localPosition=position;t.localScale=scale;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return t;}
    private static void PlaceModel(Scene scene,GameObject asset,Transform owner,string name,Vector3 position,float size,float yaw,bool aircraft=false,Dictionary<string,Material> overrides=null)
    {
        var pivot=Group(owner,name);pivot.localPosition=position;
        var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);if(model==null)throw new InvalidOperationException("Cannot instantiate "+name);
        model.transform.SetParent(pivot,false);
        if(aircraft)
        {
            var parts=model.GetComponentsInChildren<Transform>(true);
            Vector3 Center(string part)=>BoundsOf(parts.First(t=>t.name==part).GetComponentsInChildren<MeshRenderer>(true)).center;
            var forward=Center("Canopy")-(Center("Nozzle_L")+Center("Nozzle_R"))*.5f;forward.y=0;
            if(forward.sqrMagnitude<.001f)throw new InvalidOperationException("Invalid Kestrel heading.");
            model.transform.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(forward,Vector3.forward,Vector3.up),Vector3.up)*model.transform.rotation;
        }
        var renderers=model.GetComponentsInChildren<MeshRenderer>(true);var bounds=BoundsOf(renderers);
        float extent=Mathf.Max(bounds.size.x,bounds.size.z);if(extent<.001f)throw new InvalidOperationException("Invalid model bounds.");
        model.transform.localScale*=size/extent;bounds=BoundsOf(renderers);var center=bounds.center;if(!aircraft)center.y=bounds.min.y;
        model.transform.position+=position-center;pivot.localRotation=Quaternion.Euler(0,yaw,0);
        foreach(var r in renderers){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;if(overrides!=null)r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&overrides.TryGetValue(m.name,out var replacement)?replacement:m).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
    }
    private static Bounds BoundsOf(MeshRenderer[] renderers)
    {if(renderers.Length==0)throw new InvalidOperationException("No model mesh.");var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
    private static Transform Group(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    private static Color Hex(string hex){if(!ColorUtility.TryParseHtmlString("#"+hex,out var c))throw new InvalidOperationException("Invalid hex: "+hex);return c;}
    private static T Require<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing asset: "+path);
}
