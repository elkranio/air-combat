using System;
using System.Collections.Generic;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Composition study: reuse the authored trees, change only their placement and shadows.
public static class ConfigureArtLabForest
{
    private const string Source = "Assets/_Project/Scenes/ArtLabV4Trees.unity";
    private const string Destination = "Assets/_Project/Scenes/ArtLabV5Forest.unity";
    private const string Output = "Assets/_Project/Generated/ArtLabV5Forest";
    private const string Trees = "Assets/_Project/Art/Nature/Trees/";
    private static readonly string[] Names = { "Tree_Round_01", "Tree_Pine_01", "Tree_Cluster_01" };
    private const float Spacing = 5.4f;
    private struct Plant
    {
        public Vector2 point;
        public float width, yaw;
        public int variant;
    }

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null) throw new InvalidOperationException("Generate ArtLab v4 first.");
        var prefabs = Names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Trees + n + ".prefab")).ToArray();
        if (prefabs.Any(p => p == null)) throw new InvalidOperationException("Missing tree pack prefabs.");
        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, Destination, true)) throw new InvalidOperationException("Cannot copy ArtLab v4.");
        scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single(g => g.name == "ArtLab V4 Trees");
        var preview = root.GetComponent<ArtLabLightingPreview>();
        if (preview == null) throw new InvalidOperationException("Missing ArtLab preview.");
        // The slash is part of the object name, not a hierarchy separator.
        var nature = root.transform.Cast<Transform>().SingleOrDefault(t => t.name == "Nature studies / layered crowns");
        if (nature == null) throw new InvalidOperationException("Missing nature group.");
        var scattered = nature.Find("Blender tree pack");
        var procedural = nature.Find("Original procedural trees");
        var oldShadow = root.transform.Find("Conforming contact shadows");
        var terrain = root.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "Terrain mesh");
        if (scattered == null || procedural == null || oldShadow == null) throw new InvalidOperationException("Unexpected v4 nature hierarchy.");
        var shadowFilter = oldShadow.GetComponent<MeshFilter>();
        var shadowRenderer = oldShadow.GetComponent<MeshRenderer>();
        var staticShadow = NonTreeShadows(shadowFilter.sharedMesh, procedural.Cast<Transform>().ToArray());
        var ground = new GroundSampler(terrain.sharedMesh);
        var plants = Layout();
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "ArtLabV5Forest");
        if (!AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Cannot create forest output folder.");

        // Each side of the comparison owns its matching shadows. Rocks, aircraft and buildings stay fixed.
        var before = Group(nature, "A - Scattered trees");
        scattered.SetParent(before, true);
        scattered.gameObject.SetActive(true);
        oldShadow.SetParent(before, true);
        UnityEngine.Object.DestroyImmediate(procedural.gameObject);
        var after = Group(nature, "B - Forest masses");
        var west = Group(after, "Western woodland");
        var south = Group(after, "Southern woodland");
        var east = Group(after, "East bank woodland");
        var groves = Group(after, "Groves and edge trees");
        foreach (var p in plants)
        {
            var owner = p.point.x < -90 && p.point.y < 65 ? west : p.point.x > 125 ? east : p.point.y < -45 ? south : groves;
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[p.variant], scene);
            if (obj == null) throw new InvalidOperationException("Cannot instantiate tree.");
            var t = obj.transform;
            t.SetParent(owner, false);
            var r = obj.GetComponentInChildren<MeshRenderer>();
            var b = r.bounds;
            float extent = Mathf.Max(b.size.x, b.size.z);
            if (extent < .001f || b.size.y < .001f) throw new InvalidOperationException("Invalid tree bounds.");
            float scale = p.width / extent;
            // Preserve the ground pivot; cap height below the 12-unit flight plane where practical.
            float y = ground.At(p.point.x, p.point.y);
            float vertical = Mathf.Min(scale, Mathf.Max(3.6f, 10.5f-y) / b.size.y);
            t.localScale = new Vector3(scale, vertical, scale);
            t.SetPositionAndRotation(new Vector3(p.point.x, y, p.point.y), Quaternion.Euler(0,p.yaw,0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        AddMesh(after, "Fixed object shadows", SaveMesh("FixedObjectShadows", staticShadow), shadowRenderer.sharedMaterial);
        AddMesh(after, "Continuous woodland shade", SaveMesh("WoodlandShade", ForestShade(plants, ground)), shadowRenderer.sharedMaterial);
        root.name = "ArtLab V5 Forest";
        preview.ConfigureTreeComparison(before.gameObject, after.gameObject, true);
        EditorUtility.SetDirty(preview);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save forest study.");
        Selection.activeGameObject = after.gameObject;
        return $"ArtLab v5 ready: {plants.Count} trees; T scattered/forest, G/L/H unchanged. Sandbox and v4 unchanged.";
    }

    // Union of overlapping lobes gives a continuous, irregular forest outline rather than radial scatter.
    private static float Lobe(float x,float z,float cx,float cz,float rx,float rz)
    {
        float u=(x-cx)/rx, v=(z-cz)/rz;
        return 1-Mathf.Sqrt(u*u+v*v);
    }
    private static float ForestField(float x,float z)
    {
        float west=Mathf.Max(Lobe(x,z,-153,29,42,36), Lobe(x,z,-175,-1,36,44));
        west=Mathf.Max(west,Lobe(x,z,-143,-12,25,25));
        // A bite into the edge instead of a uniformly oval patch.
        west=Mathf.Min(west,-Lobe(x,z,-124,12,12,13));
        float south=Mathf.Max(Lobe(x,z,64,-85,37,39),Lobe(x,z,46,-111,32,34));
        south=Mathf.Max(south,Lobe(x,z,77,-53,18,15));
        float east=Mathf.Max(Lobe(x,z,157,4,35,48),Lobe(x,z,181,-29,32,39));
        float grove=Lobe(x,z,51,56,17,12);
        float field=Mathf.Max(Mathf.Max(west,south),Mathf.Max(east,grove));
        return field + (Mathf.PerlinNoise(x*.11f+13,z*.11f+31)-.5f)*.24f;
    }
    private static float Shore(float x,float z)
    {
        float river=102+9.5f*Mathf.Sin(z*.025f+.8f)+4.2f*Mathf.Sin(z*.052f-1.1f);
        float width=11.8f+2*Mathf.Sin(z*.031f+1.4f)+1.1f*Mathf.Sin(z*.084f);
        return Mathf.Abs(x-river)-width;
    }
    private static bool ClearGround(float x,float z,float margin)
    {
        if (Shore(x,z)<4+margin || Mathf.Abs(z-78)<5+margin) return false;
        if (Mathf.Abs(x+43)<50+margin && Mathf.Abs(z+7)<75+margin) return false;
        return true;
    }
    private static bool Near(float x,float z,float cx,float cz,float radius)
        => (x-cx)*(x-cx)+(z-cz)*(z-cz)<radius*radius;
    private static bool CanPlant(float x,float z)
    {
        if (!ClearGround(x,z,4)) return false;
        if (Near(x,z,-94,40,15)||Near(x,z,26,-43,15)) return false;
        if (Near(x,z,-134,-47,11)||Near(x,z,-111,-57,9)||Near(x,z,78,34,9)||Near(x,z,155,-42,12)) return false;
        return true;
    }
    private static List<Plant> Layout()
    {
        var rng=new System.Random(50901);
        float Next(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)rng.NextDouble());
        var result=new List<Plant>();
        for(int iz=0;iz<48;iz++)
        for(int ix=0;ix<80;ix++)
        {
            float x=-215+(ix+(iz%2)*.5f)*Spacing+Next(-1.35f,1.35f);
            float z=-155+iz*Spacing*.866f+Next(-1.35f,1.35f);
            float field=ForestField(x,z);
            if(field<0||!CanPlant(x,z)) continue;
            // Coherent conifer pockets, not a uniform alternating pattern.
            float species=Mathf.PerlinNoise(x*.035f+41,z*.035f+7);
            int variant=species>.60f ? (Next(0,1)<.73f?1:0) : (Next(0,1)<.36f?2:0);
            float width=Next(7.0f,10.0f)*(variant==1?.83f:1f);
            if(field<.13f)width*=Next(.80f,.96f);
            result.Add(new Plant{point=new Vector2(x,z),width=width,yaw=Next(0,360),variant=variant});
        }
        // A handful of deliberate outliers outside the masses, not a new layer of freckles.
        foreach(var point in new[]{new Vector2(-115,64),new Vector2(28,60),new Vector2(35,-58),new Vector2(82,-9)})
            if(CanPlant(point.x,point.y)&&result.All(p=>(p.point-point).sqrMagnitude>80))
                result.Add(new Plant{point=point,width=Next(6.5f,8f),yaw=Next(0,360),variant=0});
        return result;
    }

    // V2 appended two 73-vertex rings per tree before its rocks and fixed-object shadows.
    // Validate the prefix against all original tree positions before removing it.
    private static Mesh NonTreeShadows(Mesh source,Transform[] trees)
    {
        const int ring=73;
        var v=source.vertices;var c=source.colors32;var tris=source.triangles;
        int prefix=trees.Length*2*ring;
        if(trees.Length==0||v.Length!=c.Length||v.Length%ring!=0||prefix>=v.Length)
            throw new InvalidOperationException("Unexpected v2 shadow layout.");
        var unmatched=new List<Transform>(trees);
        for(int i=0;i<prefix;i+=2*ring)
        {
            var a=v[i];var b=v[i+ring];
            int index=unmatched.FindIndex(t=>Near(a.x,a.z,t.position.x-1.4f,t.position.z+1.4f,.025f)
                &&Near(b.x,b.z,t.position.x-3,t.position.z+3,.025f));
            if(index<0)throw new InvalidOperationException("Cannot identify original tree shadow block.");
            unmatched.RemoveAt(index);
        }
        var kept=new List<int>();
        for(int i=0;i<tris.Length;i+=3)
        {
            bool a=tris[i]>=prefix,b=tris[i+1]>=prefix,d=tris[i+2]>=prefix;
            if(a!=b||a!=d)throw new InvalidOperationException("Shadow geometry crosses tree/non-tree blocks.");
            if(a)kept.AddRange(new[]{tris[i]-prefix,tris[i+1]-prefix,tris[i+2]-prefix});
        }
        var m=new Mesh{vertices=v.Skip(prefix).ToArray(),colors32=c.Skip(prefix).ToArray(),triangles=kept.ToArray()};
        m.RecalculateBounds();return m;
    }

    private static Mesh ForestShade(List<Plant> plants,GroundSampler ground)
    {
        // One connected, terrain-following canopy shadow. Max coverage avoids stacking into black blobs.
        const int nx=200,nz=168;const float step=2.5f;
        var v=new Vector3[(nx+1)*(nz+1)];var c=new Color32[v.Length];var t=new List<int>();
        var color=new Color32(32,49,35,0);
        for(int iz=0;iz<=nz;iz++)for(int ix=0;ix<=nx;ix++)
        {
            int i=iz*(nx+1)+ix;float x=-250+ix*step,z=-210+iz*step;
            v[i]=new Vector3(x,ground.At(x,z)+.13f,z);
            float coverage=0;
            if(ClearGround(x,z,.25f))foreach(var p in plants)
            {
                float dx=x-p.point.x+1.8f,dz=z-p.point.y-1.8f;
                float r=p.width*.90f;
                if(Mathf.Abs(dx)>=r||Mathf.Abs(dz)>=r)continue;
                float d=(dx*dx+dz*dz)/(r*r);
                coverage=Mathf.Max(coverage,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,1,d)));
            }
            color.a=(byte)Mathf.RoundToInt(coverage*157);c[i]=color;
        }
        for(int iz=0;iz<nz;iz++)for(int ix=0;ix<nx;ix++)
        {
            int a=iz*(nx+1)+ix,b=a+nx+1;
            if(c[a].a==0&&c[b].a==0&&c[a+1].a==0&&c[b+1].a==0)continue;
            t.AddRange(new[]{a,b,a+1,a+1,b,b+1});
        }
        var m=new Mesh{vertices=v,colors32=c,triangles=t.ToArray()};m.RecalculateBounds();return m;
    }
    private sealed class GroundSampler
    {
        private const int NX=200,NZ=168;
        private readonly Vector3[] vertices;
        public GroundSampler(Mesh mesh)
        {
            vertices=mesh.vertices;
            if(vertices.Length!=(NX+1)*(NZ+1)||Mathf.Abs(vertices[0].x+250)>.01f||Mathf.Abs(vertices[0].z+210)>.01f
                ||Mathf.Abs(vertices[vertices.Length-1].x-250)>.01f||Mathf.Abs(vertices[vertices.Length-1].z-210)>.01f)
                throw new InvalidOperationException("Unexpected ArtLab terrain grid.");
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
    private static Transform Group(Transform parent,string name)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    private static void AddMesh(Transform parent,string name,Mesh mesh,Material mat)
    {
        var t=Group(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
    }
    private static Mesh SaveMesh(string name,Mesh mesh)
    {
        string path=Output+"/"+name+".asset";mesh.name=name;
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
    }
}
