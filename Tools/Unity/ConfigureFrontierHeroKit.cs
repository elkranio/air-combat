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

// V14A: import authored modules, build prefab kit and a separate flight showcase from the tested Frontier.
// Never changes Sandbox, ScorchedFrontier or their shared materials/meshes.
public static class ConfigureFrontierHeroKit
{
    private const string Source = "Assets/_Project/Scenes/ScorchedFrontier.unity";
    private const string Destination = "Assets/_Project/Scenes/FrontierHeroShowcase.unity";
    private const string Art = "Assets/_Project/Art/Environment/FrontierHeroKit";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private const int N = 240;
    private const float Half = 600f, Step = 5f;
    private static readonly (string name, float span, float height)[] Specs = {
        ("Rock_Large_A",18,9),("Rock_Large_B",18,12),("Rock_Large_C",23,7),
        ("Boulder_A",5.5f,3.4f),("Boulder_B",7,3),
        ("Ridge_Chunk_A",36,15),("Ridge_Chunk_B",30,13),("Cliff_Wall_A",28,17),("Cliff_Wall_B",34,14),
        ("Refinery_MainBlock",34,13.6f),("Refinery_Frame_A",25,12.8f),("Refinery_Chimney",7.4f,18.8f),
        ("Refinery_Tank_Large",15,10.3f),("Refinery_Tank_Small",11,7.8f),
        ("Refinery_PipeRack_A",15,7),("Refinery_PipeRack_B",13,7),("Refinery_ControlHut",10,7.6f),
        ("Concrete_Barrier_Set",14,2),("Debris_Concrete_A",9,1.5f),("Debris_Metal_A",7,2)
    };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scenes first.");
        Require<SceneAsset>(Source);
        var pipeline = Require<UniversalRenderPipelineAsset>(Pipeline);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit missing.");
        // Fail before touching the existing scene if the Blender export is incomplete.
        string manifestPath = Art + "/HeroKitManifest.json";
        if (!File.Exists(manifestPath)) throw new InvalidOperationException("Run the Blender hero kit generator to completion first.");
        var manifest = JsonUtility.FromJson<KitManifest>(File.ReadAllText(manifestPath));
        if (manifest == null || manifest.version != 1 || manifest.models == null || !manifest.models.OrderBy(n=>n).SequenceEqual(Specs.Select(s=>s.name).OrderBy(n=>n)))
            throw new InvalidOperationException("Unexpected hero kit manifest; regenerate the complete kit.");
        foreach (var spec in Specs)
            foreach (var suffix in manifest.fullBake ? new[] { ".fbx", "_BaseColor.png", "_AO.png", "_Normal.png" } : new[] { ".fbx", "_BaseColor.png" })
                if (!File.Exists(Art + "/" + spec.name + suffix)) throw new InvalidOperationException("Missing Blender export: " + spec.name + suffix);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureFolder(Art + "/Materials"); EnsureFolder(Art + "/Prefabs");
        var materials = new Dictionary<string, Material>();
        var prefabs = new Dictionary<string, GameObject>();

        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        var oldRoot = scene.GetRootGameObjects().Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
        var oldField = oldRoot.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "Scorched terrain");
        // Validate the existing terrain grid before copying or changing anything.
        var sampler = new GroundSampler(oldField);
        if (!EditorSceneManager.SaveScene(scene, Destination, true)) throw new InvalidOperationException("Cannot copy Scorched Frontier.");
        scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single(g => g.GetComponent<ArtLabLandscapeFlight>() != null);
        sampler = new GroundSampler(root.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "Scorched terrain"));
        var settlements = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Civilization remnants");
        var refinery = settlements.Find("01 - abandoned refinery") ?? throw new InvalidOperationException("Missing refinery group.");
        var nature = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Rock outcrops and dead trees");

        int fullyBaked = 0;
        foreach (var spec in Specs)
        {
            var model = ImportModel(spec.name);
            var material = ImportMaterial(spec.name, shader);
            if (material.GetTexture("_BumpMap") != null && material.GetTexture("_OcclusionMap") != null) fullyBaked++;
            materials.Add(spec.name, material);
            prefabs.Add(spec.name, MakePrefab(scene, spec, model, material));
        }

        // Replace only one V0 site in this copy. Other settlements, roads and terrain remain recognizable.
        Object.DestroyImmediate(refinery.gameObject);
        var hero = Group(settlements, "01 - hero refinery / V14A");
        void Put(string id, float x, float z, float yaw = 0, float scale = 1f)
            => Place(scene, sampler, hero, prefabs[id], x, z, yaw, scale, false);
        Put("Refinery_MainBlock",65,30);
        Put("Refinery_Frame_A",112,39,90,.85f);
        Put("Refinery_Chimney",42,38);
        Put("Refinery_Tank_Large",74,1,12);
        Put("Refinery_Tank_Large",94,1,-8);
        Put("Refinery_Tank_Small",116,3,25);
        Put("Refinery_PipeRack_A",88,19);
        Put("Refinery_PipeRack_B",112,20,90,.90f);
        Put("Refinery_ControlHut",40,1,90);
        Put("Concrete_Barrier_Set",58,-10);
        Put("Debris_Concrete_A",44,19,18);
        Put("Debris_Metal_A",105,53,-24);

        // Three distinct test formations NE/NW of refinery, with open space between them.
        var rocks = Group(root.transform, "V14A rock formations");
        void Stone(string id, float x, float z, float yaw = 0, float scale = 1f)
        {
            // Avoid a double layer of old scatter among the authored formations.
            foreach (var old in nature.Cast<Transform>().Where(t => Vector2.Distance(new Vector2(t.position.x,t.position.z),new Vector2(x,z)) < 14f).ToArray())
                Object.DestroyImmediate(old.gameObject);
            Place(scene,sampler,rocks,prefabs[id],x,z,yaw,scale,true);
        }
        Stone("Ridge_Chunk_A",26,104,25);
        Stone("Ridge_Chunk_B",-4,117,40,.94f);
        Stone("Rock_Large_A",47,92,20,.96f);
        Stone("Rock_Large_B",12,126,75,.85f);
        Stone("Boulder_A",49,110,36,1.10f);
        Stone("Boulder_B",-15,99,-40);
        Stone("Cliff_Wall_A",-77,119,-28);
        Stone("Cliff_Wall_B",-110,103,-28);
        Stone("Rock_Large_C",-60,107,100,.9f);
        Stone("Boulder_B",-92,84,24);
        Stone("Rock_Large_A",143,112,-24,1.17f);
        Stone("Rock_Large_B",158,124,45);
        Stone("Rock_Large_C",130,136,-15);
        Stone("Boulder_A",153,101,40);
        Stone("Boulder_B",114,130,70,1.1f);

        var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
        var camera = scene.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
        var boost = player.GetComponent<Afterburner>();
        var flight = player.GetComponent<FlightController>();
        if (camera == null || boost == null || flight == null) throw new InvalidOperationException("Missing visual flight controls.");
        var offset = camera.transform.position - player.transform.position;
        player.transform.SetPositionAndRotation(new Vector3(82,48,-45), Quaternion.identity);
        camera.transform.position = player.transform.position + offset;
        root.name = "Frontier hero showcase";
        root.GetComponent<ArtLabLandscapeFlight>().Configure(pipeline,player.transform,camera,flight,boost,Half,"V14A / Frontier hero kit");
        var renderers = hero.GetComponentsInChildren<MeshRenderer>(true).Concat(rocks.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        root.AddComponent<FrontierHeroBakePreview>().Configure(materials.Values.ToArray(),renderers);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save hero showcase.");
        Selection.activeGameObject = player;
        return $"V14A ready: {prefabs.Count} reusable prefabs, {fullyBaked} with both AO/normal maps. Start over the refinery; rocks north of it. B compares maps; H hides legends. Frontier and Sandbox untouched.";
    }

    [Serializable] private sealed class KitManifest { public int version; public bool fullBake; public string[] models; }

    private static GameObject ImportModel(string id)
    {
        string path = Art + "/" + id + ".fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter ?? throw new InvalidOperationException("Missing model importer: " + path);
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.isReadable = false; importer.addCollider = false;
        importer.SaveAndReimport();
        return Require<GameObject>(path);
    }

    private static Texture2D ImportMap(string id, string suffix, TextureImporterType type, bool srgb, bool required)
    {
        string path = Art + "/" + id + "_" + suffix + ".png";
        if (!File.Exists(path))
        {
            if (required) throw new InvalidOperationException("Missing map: " + path);
            return null;
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new InvalidOperationException("Missing texture importer: " + path);
        importer.textureType = type; importer.sRGBTexture = srgb;
        importer.isReadable = false; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.SaveAndReimport();
        return Require<Texture2D>(path);
    }

    private static Material ImportMaterial(string id, Shader shader)
    {
        var color = ImportMap(id,"BaseColor",TextureImporterType.Default,true,true);
        var ao = ImportMap(id,"AO",TextureImporterType.Default,false,false);
        var normal = ImportMap(id,"Normal",TextureImporterType.NormalMap,false,false);
        string path = Art + "/Materials/" + id + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader) { name = id }; AssetDatabase.CreateAsset(m,path); }
        m.shader = shader; m.SetColor("_BaseColor",Color.white); m.SetTexture("_BaseMap",color);
        m.SetFloat("_Metallic",0); m.SetFloat("_Smoothness",.12f);
        m.SetTexture("_OcclusionMap",ao); m.SetFloat("_OcclusionStrength",ao != null ? .72f : 0);
        m.SetTexture("_BumpMap",normal); m.SetFloat("_BumpScale",normal != null ? .74f : 0);
        if (ao != null) m.EnableKeyword("_OCCLUSIONMAP"); else m.DisableKeyword("_OCCLUSIONMAP");
        if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
        m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }

    private static GameObject MakePrefab(Scene scene,(string name,float span,float height) spec,GameObject model,Material material)
    {
        var temp = new GameObject(spec.name);
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model,scene);
            if (instance == null) throw new InvalidOperationException("Cannot instantiate " + spec.name);
            instance.transform.SetParent(temp.transform,false);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Missing mesh: " + spec.name);
            foreach (var r in renderers)
            {
                // Assign the same baked atlas to every original FBX slot; no shared source-material edits.
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) throw new InvalidOperationException("Missing FBX mesh on " + spec.name);
                r.sharedMaterials = Enumerable.Repeat(material,mesh.subMeshCount).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            Bounds b = BoundsOf(renderers);
            float span = Mathf.Max(b.size.x,b.size.z);
            if (span < .001f || b.size.y < .001f) throw new InvalidOperationException("Empty FBX bounds: " + spec.name);
            // Most exports are in metres. Guard against an importer unit mismatch without silently shrinking all assets.
            if (span < spec.span * .5f || span > spec.span * 2f)
            {
                float correction = spec.span / span;
                instance.transform.localScale *= correction;
                Debug.LogWarning($"V14A {spec.name}: corrected FBX unit scale by {correction:F3}");
                b = BoundsOf(renderers);
            }
            if (b.size.y < spec.height*.35f || b.size.y > spec.height*2.0f)
                throw new InvalidOperationException("Unexpected FBX up-axis/height: " + spec.name + " " + b.size);
            instance.transform.position -= Vector3.up * b.min.y;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            var asset = PrefabUtility.SaveAsPrefabAsset(temp,Art + "/Prefabs/" + spec.name + ".prefab");
            if (asset == null) throw new InvalidOperationException("Cannot save prefab: " + spec.name);
            return asset;
        }
        finally { Object.DestroyImmediate(temp); }
    }

    private static Bounds BoundsOf(MeshRenderer[] renderers)
    {
        var result = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) result.Encapsulate(r.bounds);
        return result;
    }

    private static void Place(Scene scene,GroundSampler field,Transform parent,GameObject prefab,float x,float z,float yaw,float scale,bool rock)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        if (instance == null) throw new InvalidOperationException("Cannot place " + prefab.name);
        var t = instance.transform; t.SetParent(parent,false);
        t.SetPositionAndRotation(new Vector3(x,field.At(x,z),z),Quaternion.Euler(0,yaw,0));
        t.localScale = Vector3.one*scale;
        if (rock)
        {
            // Sink rock bases on slopes and don't reach the 48-unit aircraft plane.
            var b=BoundsOf(instance.GetComponentsInChildren<MeshRenderer>(true));
            float min=field.At(x,z);
            foreach(var a in new[]{b.min.x,b.max.x})foreach(var c in new[]{b.min.z,b.max.z})min=Mathf.Min(min,field.At(a,c));
            var p=t.position;p.y=Mathf.Min(p.y-.28f,min);t.position=p;
            b=BoundsOf(instance.GetComponentsInChildren<MeshRenderer>(true));
            if (b.max.y>43f)
            {
                var s=t.localScale;s.y*=Mathf.Clamp((43f-p.y)/Mathf.Max(.1f,b.max.y-p.y),.25f,1f);t.localScale=s;
            }
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }

    private sealed class GroundSampler
    {
        private readonly Vector3[] vertices;
        private readonly Transform transform;
        public GroundSampler(MeshFilter filter)
        {
            transform=filter.transform;
            if(filter.sharedMesh==null||!filter.sharedMesh.isReadable)throw new InvalidOperationException("Frontier terrain mesh is not readable.");
            vertices=filter.sharedMesh.vertices;
            if(vertices.Length!=(N+1)*(N+1)||Mathf.Abs(vertices[0].x+Half)>.01f||Mathf.Abs(vertices[0].z+Half)>.01f||
                Mathf.Abs(vertices[vertices.Length-1].x-Half)>.01f||Mathf.Abs(vertices[vertices.Length-1].z-Half)>.01f)
                throw new InvalidOperationException("Unexpected Frontier terrain grid.");
        }
        public float At(float x,float z)
        {
            var p=transform.InverseTransformPoint(new Vector3(x,0,z));
            float gx=Mathf.Clamp((p.x+Half)/Step,0,N),gz=Mathf.Clamp((p.z+Half)/Step,0,N);
            int ix=Mathf.Min(N-1,Mathf.FloorToInt(gx)),iz=Mathf.Min(N-1,Mathf.FloorToInt(gz));
            float u=gx-ix,v=gz-iz;int a=iz*(N+1)+ix,b=a+N+1;
            float h00=vertices[a].y,h10=vertices[a+1].y,h01=vertices[b].y,h11=vertices[b+1].y;
            float h=u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
            return transform.TransformPoint(new Vector3(p.x,h,p.z)).y;
        }
    }
    private static Transform Group(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    private static T Require<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);
    private static void EnsureFolder(string path)
    {
        var parts=path.Split('/');string parent=parts[0];
        for(int i=1;i<parts.Length;i++){string next=parent+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(parent,parts[i]);parent=next;}
    }
}
