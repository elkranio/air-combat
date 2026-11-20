using System;
using System.Collections.Generic;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Visual study only: compare contiguous Blender canopies with the dense individual trees in v5.
public static class ConfigureArtLabForestMasses
{
    private const string Source = "Assets/_Project/Scenes/ArtLabV5Forest.unity";
    private const string Destination = "Assets/_Project/Scenes/ArtLabV6Canopy.unity";
    private const string Folder = "Assets/_Project/Art/Nature/ForestMasses";
    private const string Output = "Assets/_Project/Generated/ArtLabV6Canopy";
    private static readonly string[] Names = { "ForestCore_A", "ForestEdge_A", "ForestEdge_B", "ForestCorner_A", "ForestIsland_A", "ForestFinger_A" };
    private struct Placement
    {
        public int variant;
        public float x,z,width,yaw;
        public Placement(int variant,float x,float z,float width,float yaw)
        { this.variant=variant;this.x=x;this.z=z;this.width=width;this.yaw=yaw; }
    }
    // Curated irregular groups, with overlapping shoulders and open corridors around the airfield.
    // Width is the longest unrotated horizontal extent, in game units.
    private static readonly Placement[] Layout = {
        new Placement(0,-170,35,39,14),new Placement(1,-150,40,34,-13),
        new Placement(3,-179,13,37,31),new Placement(2,-153,19,32,65),
        new Placement(0,-184,-10,41,-19),new Placement(0,-164,-13,35,26),
        new Placement(5,-145,-14,30,18),new Placement(1,-174,-34,32,-20),
        new Placement(3,-192,-34,35,7),new Placement(2,-147,49,27,-18),
        new Placement(0,48,-104,39,12),new Placement(3,59,-96,35,36),
        new Placement(1,40,-120,34,-28),new Placement(2,54,-119,31,8),
        new Placement(5,68,-70,33,-18),new Placement(1,48,-82,31,19),
        new Placement(3,66,-80,26,12),new Placement(2,38,-96,26,58),
        new Placement(0,160,21,38,12),new Placement(1,147,41,30,35),
        new Placement(0,163,-3,40,-24),new Placement(3,178,-25,36,17),
        new Placement(1,151,-22,33,53),new Placement(0,189,-44,39,4),
        new Placement(5,176,-53,31,28),new Placement(2,191,5,32,-20),
        new Placement(4,49,55,24,-18),new Placement(2,61,58,22,34)
    };

    public static string Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save current scene changes first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Require<SceneAsset>(Source);
        var models=Names.Select(n=>Require<GameObject>(Folder+"/"+n+".fbx")).ToArray();
        var material=MakeMaterial();
        var prefabs=Names.Select((n,i)=>MakePrefab(models[i],n,material)).ToArray();
        AssetDatabase.SaveAssets();
        if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder("Assets/_Project/Generated","ArtLabV6Canopy");
        if(!AssetDatabase.IsValidFolder(Output))throw new InvalidOperationException("Could not create canopy output.");
        var scene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
        if(!EditorSceneManager.SaveScene(scene,Destination,true))throw new InvalidOperationException("Cannot copy ArtLab v5.");
        scene=EditorSceneManager.OpenScene(Destination,OpenSceneMode.Single);
        var root=scene.GetRootGameObjects().Single(g=>g.name=="ArtLab V5 Forest");
        var preview=root.GetComponent<ArtLabLightingPreview>();
        var nature=root.transform.Cast<Transform>().SingleOrDefault(t=>t.name=="Nature studies / layered crowns");
        if(nature==null||preview==null)throw new InvalidOperationException("Missing ArtLab v5 hierarchy.");
        var scattered=nature.Find("A - Scattered trees");
        var dense=nature.Find("B - Forest masses");
        if(scattered==null||dense==null)throw new InvalidOperationException("Missing v5 comparison groups.");
        var fixedShadow=dense.Find("Fixed object shadows");
        if(fixedShadow==null)throw new InvalidOperationException("Missing fixed shadows.");
        var terrain=root.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Terrain mesh");
        var ground=new GroundSampler(terrain.sharedMesh);
        var canopy=Group(nature,"C - Continuous canopy modules");
        var west=Group(canopy,"Western woodland");var south=Group(canopy,"Southern woodland");
        var east=Group(canopy,"East bank woodland");var grove=Group(canopy,"Small grove and isolated trees");
        var shadeSources=new List<Bounds>();
        foreach(var p in Layout)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[p.variant],scene);
            if(obj==null)throw new InvalidOperationException("Cannot place canopy module.");
            var t=obj.transform;t.SetParent(p.x < -100 ? west : p.x > 120 ? east : p.z < -45 ? south : grove,false);
            var r=obj.GetComponentInChildren<MeshRenderer>(true);var b=r.bounds;
            float extent=Mathf.Max(b.size.x,b.size.z);
            if(extent<.001f||b.size.y<.001f)throw new InvalidOperationException("Invalid imported canopy bounds.");
            float scale=p.width/extent;
            float y=ground.At(p.x,p.z);
            // Slightly sink the skirt into softly sloped terrain; keep canopy tops below aircraft height.
            t.localScale=new Vector3(scale,Mathf.Min(scale,Mathf.Max(3.5f,10.5f-y)/b.size.y),scale);
            t.SetPositionAndRotation(new Vector3(p.x,y-.6f,p.z),Quaternion.Euler(0,p.yaw,0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            shadeSources.Add(r.bounds);
        }
        // Keep the actual deliberate outliers from v5, rather than a fresh layer of scattered points.
        int singles=0;
        var isolated=new[]{new Vector2(-115,64),new Vector2(28,60),new Vector2(35,-58),new Vector2(82,-9)};
        foreach(var p in isolated)
        {
            var old=dense.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>
                t.name.StartsWith("Tree_Round_01",StringComparison.Ordinal)&&
                (new Vector2(t.position.x,t.position.z)-p).sqrMagnitude<.1f);
            if(old==null)continue;
            var tree=Require<GameObject>("Assets/_Project/Art/Nature/Trees/Tree_Round_01.prefab");
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(tree,scene);
            if(obj==null)throw new InvalidOperationException("Cannot place edge tree.");
            obj.transform.SetParent(grove,false);obj.transform.SetPositionAndRotation(old.position,old.rotation);
            obj.transform.localScale=old.lossyScale;PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            shadeSources.Add(obj.GetComponentInChildren<MeshRenderer>(true).bounds);singles++;
        }
        // Copy the unchanged building/rock/aircraft shadows, then create a continuous shade for the new layout.
        var fixedCopy=UnityEngine.Object.Instantiate(fixedShadow.gameObject,canopy,false);fixedCopy.name="Fixed object shadows";
        var shadowMat=fixedShadow.GetComponent<MeshRenderer>().sharedMaterial;
        AddShadow(canopy,SaveMesh("CanopyShade",MakeShade(shadeSources,ground)),shadowMat);
        preview.ConfigureTreeComparison(dense.gameObject,canopy.gameObject,true,true);
        UnityEngine.Object.DestroyImmediate(scattered.gameObject);
        root.name="ArtLab V6 Canopy";EditorUtility.SetDirty(preview);EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save canopy study.");
        Selection.activeGameObject=canopy.gameObject;
        return $"ArtLab v6: {Layout.Length} canopy modules, {singles} isolated trees. T: v5 dense trees / canopy. G/L/H unchanged. Sandbox untouched.";
    }

    private static Material MakeMaterial()
    {
        string path=Folder+"/ForestRamp.png";
        var imp=AssetImporter.GetAtPath(path) as TextureImporter;
        if(imp==null)throw new InvalidOperationException("Generate the Blender forest mass pack first.");
        if(!imp.sRGBTexture||imp.mipmapEnabled||imp.wrapMode!=TextureWrapMode.Clamp||imp.filterMode!=FilterMode.Bilinear||imp.textureCompression!=TextureImporterCompression.Uncompressed)
        {imp.sRGBTexture=true;imp.mipmapEnabled=false;imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Bilinear;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();}
        var tex=Require<Texture2D>(path);
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Missing URP Lit.");
        path=Folder+"/ForestCanopy.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.shader=shader;mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BaseMap",tex);
        mat.SetTextureScale("_BaseMap",Vector2.one);mat.SetTextureOffset("_BaseMap",Vector2.zero);
        mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.04f);EditorUtility.SetDirty(mat);return mat;
    }
    private static GameObject MakePrefab(GameObject model,string name,Material mat)
    {
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);
            if(obj==null)throw new InvalidOperationException("Cannot instantiate "+name);
            obj.transform.SetParent(root.transform,false);
            var rs=obj.GetComponentsInChildren<MeshRenderer>(true);
            if(rs.Length!=1||rs[0].sharedMaterials.Length!=1)throw new InvalidOperationException("Expected one canopy mesh, one material on "+name);
            var r=rs[0];r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            obj.transform.position-=Vector3.up*r.bounds.min.y;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab",out bool ok);
            if(!ok||prefab==null)throw new InvalidOperationException("Cannot save "+name);
            return prefab;
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    private static Mesh MakeShade(List<Bounds> sources,GroundSampler ground)
    {
        const int nx=200,nz=168;const float step=2.5f;
        var v=new Vector3[(nx+1)*(nz+1)];var c=new Color32[v.Length];var tris=new List<int>();
        for(int iz=0;iz<=nz;iz++)for(int ix=0;ix<=nx;ix++)
        {
            int i=iz*(nx+1)+ix;float x=-250+ix*step,z=-210+iz*step;
            v[i]=new Vector3(x,ground.At(x,z)+.13f,z);
            float coverage=0;
            if(ClearGround(x,z))foreach(var b in sources)
            {
                float dx=(x-b.center.x+1.8f)/(b.extents.x+4),dz=(z-b.center.z-1.8f)/(b.extents.z+4);
                float dist=dx*dx+dz*dz;
                coverage=Mathf.Max(coverage,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.30f,1.10f,dist)));
            }
            c[i]=new Color32(32,49,35,(byte)Mathf.RoundToInt(coverage*155));
        }
        for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
        {int a=z*(nx+1)+x,b=a+nx+1;if(c[a].a==0&&c[b].a==0&&c[a+1].a==0&&c[b+1].a==0)continue;tris.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
        var m=new Mesh{vertices=v,colors32=c,triangles=tris.ToArray()};m.RecalculateBounds();return m;
    }
    private static bool ClearGround(float x,float z)
    {
        float river=102+9.5f*Mathf.Sin(z*.025f+.8f)+4.2f*Mathf.Sin(z*.052f-1.1f);
        float w=11.8f+2*Mathf.Sin(z*.031f+1.4f)+1.1f*Mathf.Sin(z*.084f);
        return Mathf.Abs(x-river)>w+4 && Mathf.Abs(z-78)>5 && !(Mathf.Abs(x+43)<50&&Mathf.Abs(z+7)<75);
    }
    private sealed class GroundSampler
    {
        private const int NX=200,NZ=168;private readonly Vector3[] vertices;
        public GroundSampler(Mesh mesh)
        {
            vertices=mesh.vertices;
            if(vertices.Length!=(NX+1)*(NZ+1)||Mathf.Abs(vertices[0].x+250)>.01f||Mathf.Abs(vertices[0].z+210)>.01f||
                Mathf.Abs(vertices[vertices.Length-1].x-250)>.01f||Mathf.Abs(vertices[vertices.Length-1].z-210)>.01f)
                throw new InvalidOperationException("Unexpected terrain mesh.");
        }
        public float At(float x,float z)
        {
            float u=Mathf.Clamp((x+250)/2.5f,0,NX),v=Mathf.Clamp((z+210)/2.5f,0,NZ);
            int ix=Mathf.Min(NX-1,Mathf.FloorToInt(u)),iz=Mathf.Min(NZ-1,Mathf.FloorToInt(v));
            float fx=u-ix,fz=v-iz;int a=iz*(NX+1)+ix,b=a+NX+1;
            float h00=vertices[a].y,h10=vertices[a+1].y,h01=vertices[b].y,h11=vertices[b+1].y;
            return fx+fz<=1?h00+(h10-h00)*fx+(h01-h00)*fz:h11+(h01-h11)*(1-fx)+(h10-h11)*(1-fz);
        }
    }
    private static void AddShadow(Transform parent,Mesh mesh,Material material)
    {
        var t=Group(parent,"Continuous canopy shade");t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
    }
    private static Mesh SaveMesh(string name,Mesh mesh)
    {
        string path=Output+"/"+name+".asset";mesh.name=name;var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
    }
    private static Transform Group(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    private static T Require<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing asset: "+path);
}
