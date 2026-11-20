using System;
using System.Collections.Generic;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Editor-only, reproducible visual comparison. No Sandbox or shared art mutations.
public static class ConfigureArtLab
{
    private const string ScenePath = "Assets/_Project/Scenes/ArtLab.unity";
    private const string Output = "Assets/_Project/Generated/ArtLab";
    private const string Art = "Assets/_Project/Art/";
    private const string AircraftPath = Art + "Aircraft/XF-01-Kestrel.fbx";
    private const string HangarPath = Art + "Buildings/Hangar-04/Hangar-04-Baked.prefab";
    private const string TurretPath = Art + "Buildings/MT-01-Sentinel/MT-01-Sentinel.prefab";

    // Surface colors are authored as sRGB hex, not global saturation effects.
    private sealed class Palette
    {
        public readonly string id, title;
        public readonly string[] colors;
        public readonly string sun, ambient;
        public readonly float intensity;
        public Palette(string id, string title, string sun, string ambient, float intensity, params string[] colors)
        { this.id = id; this.title = title; this.sun = sun; this.ambient = ambient; this.intensity = intensity; this.colors = colors; }
    }

    // Grass, meadow, deep grass, bank, water, road, apron, runway, markings,
    // foliage, shaded foliage, bark, stone. Geometry is identical in all three studies.
    private static readonly string[] Slots = { "Grass", "Meadow", "DeepGrass", "Bank", "Water", "Road", "Apron", "Runway", "Markings", "Leaf", "LeafDark", "Bark", "Stone" };
    private static readonly Palette[] Palettes = {
        new Palette("A", "A / Emerald Frontier", "FFE6C1", "7995B0", 1.10f,
            "568C48", "78A352", "39734C", "B7A477", "397E94", "878A70", "CCB98E", "38495C", "F3E6C5", "367344", "244C40", "76543A", "8A8A76"),
        new Palette("B", "B / Desert Thunder", "FFE1B0", "A1A3BB", 1.13f,
            "D1AF72", "DFC18A", "B98D52", "A87748", "3C8092", "A48260", "E6D3AC", "3D505A", "FFF0CF", "807B43", "59613E", "91613D", "A47858"),
        new Palette("C", "C / Northern Operations", "FFF1DE", "789AB0", 1.05f,
            "638A78", "85A18B", "456C61", "9D9B84", "346575", "74827D", "A0AEAE", "34445E", "E5EEE5", "35635B", "25454D", "675C4D", "7D8D96")
    };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var aircraft = Require<GameObject>(AircraftPath);
        var hangar = Require<GameObject>(HangarPath);
        var turret = Require<GameObject>(TurretPath);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var effect = Shader.Find("AirCombat/SandboxEffect");
        if (lit == null || effect == null) throw new InvalidOperationException("Missing art shaders.");
        var enemyMats = new Dictionary<string, Material>();
        foreach (var name in new[] { "Kestrel_Hull", "Kestrel_Upper", "Kestrel_Panels", "Kestrel_Amber" })
            enemyMats.Add(name, Require<Material>(Art + "Aircraft/EnemyMaterials/Enemy_" + name + ".mat"));
        EnsureFolder(Output);
        var builder = new Builder(lit, effect);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("ArtLab");
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 80f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 300f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        var rotation = Quaternion.Euler(60f, 0f, 0f);
        camera.transform.SetPositionAndRotation(new Vector3(0, 12f, 0) - rotation * Vector3.forward * (88f / Mathf.Sin(60f * Mathf.Deg2Rad)), rotation);
        var sun = new GameObject("Art Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
        sun.shadows = LightShadows.None; // editor-generated contact shadows instead
        RenderSettings.sun = sun;
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        RenderSettings.reflectionIntensity = 0.25f;
        var environment = Group(root.transform, "Palette studies");
        var studies = new ArtLabPalettePreview.Study[Palettes.Length];
        for (int i = 0; i < Palettes.Length; i++)
        {
            var p = Palettes[i];
            var zone = Group(environment, p.title);
            builder.BuildEnvironment(zone, p);
            studies[i] = new ArtLabPalettePreview.Study { label = p.title, environment = zone.gameObject,
                sunColor = Hex(p.sun), sunIntensity = p.intensity, ambientColor = Hex(p.ambient), backgroundColor = Hex(p.colors[0]) };
            zone.gameObject.SetActive(i == 0);
        }

        // Fixed reference assets: no changes to their FBXs, materials or prefabs.
        var objects = Group(root.transform, "Fixed art references");
        PlaceModel(scene, hangar, objects, "Hangar A", new Vector3(-7, .08f, 32), 30f, -90f);
        PlaceModel(scene, hangar, objects, "Hangar B", new Vector3(-7, .08f, -21), 30f, -90f);
        PlaceModel(scene, turret, objects, "Sentinel A", new Vector3(-94, .10f, 40), 14f, 35f);
        PlaceModel(scene, turret, objects, "Sentinel B", new Vector3(26, .10f, -43), 14f, -35f);
        PlaceModel(scene, aircraft, objects, "Kestrel - blue", new Vector3(24, 12, 19), 11f, 35f, true);
        PlaceModel(scene, aircraft, objects, "Kestrel - red", new Vector3(45, 12, -24), 10.5f, -55f, true, enemyMats);
        var preview = root.AddComponent<ArtLabPalettePreview>();
        preview.Configure(studies, sun, camera);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Failed to save ArtLab.");
        Selection.activeGameObject = root;
        return "ArtLab ready: 1/2/3 palettes, L lighting, H legend. Sandbox and build settings unchanged.";
    }

    private sealed class Builder
    {
        private readonly Shader lit;
        private readonly Material shadow;
        private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        public Builder(Shader lit, Shader effect)
        {
            this.lit = lit;
            shadow = MakeMaterial(Output + "/ContactShadow.mat", effect, Color.white);
        }

        public void BuildEnvironment(Transform zone, Palette palette)
        {
            string folder = Output + "/" + palette.id;
            EnsureFolder(folder);
            var mats = new Material[Slots.Length];
            for (int i = 0; i < Slots.Length; i++) mats[i] = MakeMaterial(folder + "/" + Slots[i] + ".mat", lit, Hex(palette.colors[i]));
            var ground = Group(zone, "Terrain and airfield");
            Rect(ground, "Ground", -0f, 0, 440, 360, 0, mats[0]);
            Patch(ground, "Meadow west", -119, -43, 76, 62, .012f, 31, mats[1]);
            Patch(ground, "Meadow north", 24, 66, 75, 42, .013f, 51, mats[1]);
            Patch(ground, "Deep woodland west", -122, 25, 38, 38, .014f, 81, mats[2]);
            Patch(ground, "Deep woodland east", 69, -72, 30, 52, .015f, 91, mats[2]);
            // The river is a gentle graphic shape with a visible warm/cool shoreline.
            var river = new[] { new Vector2(112,-180), new Vector2(104,-100), new Vector2(94,-42), new Vector2(103,8), new Vector2(116,54), new Vector2(107,112), new Vector2(101,180) };
            Ribbon(ground, "Bank", river, 31f, .019f, mats[3]);
            Ribbon(ground, "Water", river, 23f, .021f, mats[4]);
            Rect(ground, "Access road", -10, 78, 220, 9, .027f, mats[5]);
            Rect(ground, "Bridge", 110, 78, 40, 11, .032f, mats[6]);
            Rect(ground, "Apron", -43, -7, 100, 150, .035f, mats[6]);
            Rect(ground, "Runway", -55, -7, 23, 143, .05f, mats[7]);
            Rect(ground, "Taxiway", -31, 22, 26, 10, .052f, mats[5]);
            Rect(ground, "Taxiway south", -31, -34, 26, 10, .052f, mats[5]);
            for (int z = -55; z <= 38; z += 15) Rect(ground, "Centerline " + z, -55, z, 1.3f, 7.5f, .063f, mats[8]);
            for (int x = -63; x <= -47; x += 4)
            {
                Rect(ground, "Threshold south " + x, x, -73, 2, 7, .063f, mats[8]);
                Rect(ground, "Threshold north " + x, x, 59, 2, 7, .063f, mats[8]);
            }

            var nature = Group(zone, "Temporary nature studies");
            Grove(nature, -122, 25, 30, 14, 42, mats);
            Grove(nature, 69, -64, 25, 11, 56, mats);
            Grove(nature, 49, 61, 18, 6, 65, mats);
            Rock(nature, -105, -46, 7.5f, 36, mats[12]);
            Rock(nature, -119, -56, 5.5f, 81, mats[12]);
            Rock(nature, 81, 34, 7.0f, 18, mats[12]);
            Rock(nature, 87, 25, 4.0f, 56, mats[12]);
            var shadows = Group(zone, "Ground shadows");
            SoftShadow(shadows, -10, 35, 35, 31, 74);
            SoftShadow(shadows, -10, -18, 35, 31, 74);
            SoftShadow(shadows, -97, 43, 18, 18, 74);
            SoftShadow(shadows, 23, -40, 18, 18, 74);
            SoftShadow(shadows, 6, 37, 10, 12, 30);
            SoftShadow(shadows, 27, -6, 10, 12, 30);
        }

        private void Grove(Transform owner, float cx, float cz, float radius, int count, int seed, Material[] mats)
        {
            var random = new System.Random(seed);
            var treeA = GetMesh("TreeBroad", () => RingMesh(new[] { .20f, .48f, .44f, .25f }, new[] { 0f, .25f, .65f, .90f }, 7, 0.12f));
            var treeB = GetMesh("TreePine", () => RingMesh(new[] { .18f, .50f, .32f, .12f }, new[] { 0f, .12f, .49f, .85f }, 7, .0f));
            var trunk = GetMesh("Trunk", () => RingMesh(new[] { .5f, .45f }, new[] { 0f, 1f }, 6, 0));
            for (int i = 0; i < count; i++)
            {
                float a = i * 2.399963f + (float)(random.NextDouble() * .22 - .11);
                float r = radius * Mathf.Sqrt((i + .5f) / count);
                float x = cx + Mathf.Cos(a) * r;
                float z = cz + Mathf.Sin(a) * r;
                float size = 6f + (float)random.NextDouble() * 3.5f;
                bool pine = i % 3 == 1;
                float height = pine ? size * 1.04f : size * .74f;
                var tree = Group(owner, "Tree study " + i);
                tree.localPosition = new Vector3(x, 0, z);
                tree.localRotation = Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
                Add(tree, "Trunk", trunk, mats[11], new Vector3(0, 0, 0), new Vector3(size * .20f, height * .5f, size * .20f));
                Add(tree, "Crown", pine ? treeB : treeA, i % 3 == 0 ? mats[10] : mats[9], new Vector3(0, height * .22f, 0), new Vector3(size, height * .8f, size));
                SoftShadow(owner, x - 1.8f, z + 1.8f, size * 1.3f, size * 1.3f, 68);
            }
        }

        private void Rock(Transform owner, float x, float z, float size, float yaw, Material mat)
        {
            var mesh = GetMesh("Rock", () => RingMesh(new[] { .33f, .50f, .35f, .17f }, new[] { 0f, .24f, .62f, .85f }, 7, .24f));
            var obj = Add(owner, "Rock study", mesh, mat, new Vector3(x, 0, z), new Vector3(size, size * .49f, size * .83f));
            obj.localRotation = Quaternion.Euler(0, yaw, 0);
            SoftShadow(owner, x - 1, z + 1, size * 1.4f, size, 65);
        }

        private void Rect(Transform owner, string name, float x, float z, float w, float d, float y, Material mat)
        {
            var quad = GetMesh("Quad", () => FlatMesh(new[] { new Vector3(-.5f,0,-.5f), new Vector3(-.5f,0,.5f), new Vector3(.5f,0,.5f), new Vector3(.5f,0,-.5f) }, new[] { 0,1,2,0,2,3 }));
            Add(owner, name, quad, mat, new Vector3(x,y,z), new Vector3(w,1,d));
        }

        private void Patch(Transform owner, string name, float x, float z, float rx, float rz, float y, int seed, Material mat)
        {
            var mesh = GetMesh("Patch" + seed, () => {
                const int n = 20;
                var random = new System.Random(seed);
                var v = new List<Vector3> { Vector3.zero };
                var t = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2 / n;
                    float r = .82f + .18f * (float)random.NextDouble();
                    v.Add(new Vector3(Mathf.Cos(a)*r, 0, Mathf.Sin(a)*r));
                    t.AddRange(new[]{ 0, 1+(i+1)%n, 1+i });
                }
                return FlatMesh(v.ToArray(), t.ToArray());
            });
            Add(owner, name, mesh, mat, new Vector3(x,y,z), new Vector3(rx,1,rz));
        }

        private void Ribbon(Transform owner, string name, Vector2[] points, float width, float y, Material mat)
        {
            var mesh = GetMesh("Ribbon" + name, () => {
                var v = new List<Vector3>();
                var tris = new List<int>();
                for (int i = 0; i < points.Length; i++)
                {
                    var f = (points[Mathf.Min(points.Length-1,i+1)] - points[Mathf.Max(0,i-1)]).normalized;
                    var n = new Vector2(-f.y,f.x) * (width*.5f);
                    var a = points[i]+n; var b = points[i]-n;
                    v.Add(new Vector3(a.x,0,a.y)); v.Add(new Vector3(b.x,0,b.y));
                    if (i>0) { int k=i*2; tris.AddRange(new[] { k-2,k,k-1,k-1,k,k+1 }); }
                }
                for (int i=0; i<tris.Count; i+=3)
                    if (Vector3.Cross(v[tris[i+1]]-v[tris[i]],v[tris[i+2]]-v[tris[i]]).y<0)
                    { int k=tris[i+1]; tris[i+1]=tris[i+2]; tris[i+2]=k; }
                return FlatMesh(v.ToArray(),tris.ToArray());
            });
            Add(owner,name,mesh,mat,new Vector3(0,y,0),Vector3.one);
        }

        private void SoftShadow(Transform owner, float x, float z, float w, float d, int opacity)
        {
            var mesh = GetMesh("Shadow" + opacity, () => {
                const int n=24;
                var v=new List<Vector3>{Vector3.zero};
                var c=new List<Color32>{new Color32(25,37,29,(byte)opacity)};
                var t=new List<int>();
                for (int ring=0;ring<2;ring++)
                for (int i=0;i<n;i++)
                {
                    float a=i*Mathf.PI*2/n; float r=ring==0?.24f:.5f;
                    v.Add(new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));
                    c.Add(new Color32(25,37,29,(byte)(ring==0?opacity*.70f:0)));
                }
                for (int i=0;i<n;i++)
                { int a=1+i,b=1+(i+1)%n; t.AddRange(new[]{0,b,a,a,b,b+n,a,b+n,a+n}); }
                var result=FlatMesh(v.ToArray(),t.ToArray()); result.colors32=c.ToArray(); return result;
            });
            Add(owner,"Soft shadow",mesh,shadow,new Vector3(x,.09f,z),new Vector3(w,1,d));
        }

        private Mesh GetMesh(string name, Func<Mesh> create)
        {
            if (meshes.TryGetValue(name,out var mesh)) return mesh;
            var fresh = create(); fresh.name = name;
            string path = Output + "/" + name + ".asset";
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh=fresh; AssetDatabase.CreateAsset(mesh,path); }
            else { EditorUtility.CopySerialized(fresh,mesh); UnityEngine.Object.DestroyImmediate(fresh); EditorUtility.SetDirty(mesh); }
            meshes.Add(name,mesh);
            return mesh;
        }
    }

    private static Mesh RingMesh(float[] radii, float[] heights, int sides, float irregular)
    {
        // Split vertices per triangle for deliberately flat shading of placeholder forms.
        var points=new List<Vector3>();
        for (int r=0;r<radii.Length;r++)
        for (int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2/sides + (r%2)*.15f;
            float rad=radii[r]*(1+irregular*Mathf.Sin(i*2.7f+r));
            points.Add(new Vector3(Mathf.Cos(a)*rad,heights[r],Mathf.Sin(a)*rad));
        }
        var v=new List<Vector3>();
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0) { var tmp=b;b=c;c=tmp; }
            v.Add(a);v.Add(b);v.Add(c);
        }
        for (int r=0;r<radii.Length-1;r++)
        for (int i=0;i<sides;i++)
        {
            int a=r*sides+i,b=r*sides+(i+1)%sides;
            var outward=points[a]+points[b];outward.y=0;
            Triangle(points[a],points[b],points[b+sides],outward);
            Triangle(points[a],points[b+sides],points[a+sides],outward);
        }
        int end=(radii.Length-1)*sides;
        for (int i=0;i<sides;i++)
        {
            int j=(i+1)%sides;
            Triangle(Vector3.up*heights[0],points[i],points[j],Vector3.down);
            Triangle(Vector3.up*(heights[heights.Length-1]+.09f),points[end+i],points[end+j],Vector3.up);
        }
        return FlatMesh(v.ToArray(),Enumerable.Range(0,v.Count).ToArray());
    }

    private static Mesh FlatMesh(Vector3[] vertices, int[] triangles)
    {
        var mesh=new Mesh { vertices=vertices, triangles=triangles };
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }

    private static Transform Add(Transform owner,string name,Mesh mesh,Material mat,Vector3 position,Vector3 scale)
    {
        var t=Group(owner,name);t.localPosition=position;t.localScale=scale;
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        return t;
    }

    private static void PlaceModel(Scene scene,GameObject asset,Transform owner,string name,Vector3 position,float size,float yaw,bool aircraft=false,Dictionary<string,Material> overrides=null)
    {
        var pivot=Group(owner,name);pivot.localPosition=position;
        var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
        if (model==null) throw new InvalidOperationException("Cannot instantiate "+name);
        model.transform.SetParent(pivot,false);
        if (aircraft)
        {
            var parts=model.GetComponentsInChildren<Transform>(true);
            Vector3 Center(string part) => BoundsOf(parts.First(t=>t.name==part).GetComponentsInChildren<MeshRenderer>(true)).center;
            var forward=Center("Canopy")-(Center("Nozzle_L")+Center("Nozzle_R"))*.5f;
            forward.y=0;
            if (forward.sqrMagnitude<.001f) throw new InvalidOperationException("Invalid Kestrel forward.");
            model.transform.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(forward,Vector3.forward,Vector3.up),Vector3.up)*model.transform.rotation;
        }
        var renderers=model.GetComponentsInChildren<MeshRenderer>(true);
        var bounds=BoundsOf(renderers);
        float extent=Mathf.Max(bounds.size.x,bounds.size.z);
        if (extent<.001f) throw new InvalidOperationException("Invalid art bounds: "+name);
        model.transform.localScale*=size/extent;
        bounds=BoundsOf(renderers);
        var center=bounds.center;if(!aircraft)center.y=bounds.min.y;
        model.transform.position+=position-center;
        pivot.localRotation=Quaternion.Euler(0,yaw,0);
        foreach(var r in renderers)
        {
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            if(overrides!=null)r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&overrides.TryGetValue(m.name,out var replacement)?replacement:m).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
    }

    private static Bounds BoundsOf(MeshRenderer[] renderers)
    { if(renderers.Length==0)throw new InvalidOperationException("No art mesh renderers.");var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b; }
    private static Transform Group(Transform owner,string name)
    { var t=new GameObject(name).transform;t.SetParent(owner,false);return t; }
    private static Color Hex(string value)
    { if(!ColorUtility.TryParseHtmlString("#"+value,out var c))throw new InvalidOperationException("Invalid color: "+value);return c; }
    private static Material MakeMaterial(string path,Shader shader,Color color)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}
        m.shader=shader;if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
        if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",0f);
        if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0f);
        EditorUtility.SetDirty(m);return m;
    }
    private static T Require<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing art asset: "+path);
    private static void EnsureFolder(string path)
    { var pieces=path.Split('/');string parent=pieces[0];for(int i=1;i<pieces.Length;i++){string next=parent+"/"+pieces[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(parent,pieces[i]);parent=next;} }
}
