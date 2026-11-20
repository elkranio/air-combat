using System;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Compare v5 dense trees, v6 smooth canopy, and v7 hybrid crown modules.
// Preserve terrain, lighting, old assets and the playable Sandbox.
public static class ConfigureArtLabForestHybrid
{
    private const string Source = "Assets/_Project/Scenes/ArtLabV6Canopy.unity";
    private const string Destination = "Assets/_Project/Scenes/ArtLabV7Hybrid.unity";
    private const string OldFolder = "Assets/_Project/Art/Nature/ForestMasses/";
    private const string Folder = "Assets/_Project/Art/Nature/ForestHybrid";
    private static readonly string[] Names = { "ForestCore_A", "ForestEdge_A", "ForestEdge_B", "ForestCorner_A", "ForestIsland_A", "ForestFinger_A" };
    private static readonly string[] Groups = { "Western woodland", "Southern woodland", "East bank woodland", "Small grove and isolated trees" };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i=0;i<SceneManager.sceneCount;i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Require<SceneAsset>(Source);
        var models = Names.Select(n => Require<GameObject>(Folder+"/"+n+".fbx")).ToArray();
        var material = MakeMaterial();
        var prefabs = Names.Select((n,i) => MakePrefab(models[i],n,material)).ToArray();
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene,Destination,true)) throw new InvalidOperationException("Cannot copy ArtLab v6.");
        scene = EditorSceneManager.OpenScene(Destination,OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single(g => g.name == "ArtLab V6 Canopy");
        var preview = root.GetComponent<ArtLabLightingPreview>();
        // Literal slash in the name: do not use Transform.Find on this name.
        var nature = root.transform.Cast<Transform>().SingleOrDefault(t => t.name == "Nature studies / layered crowns");
        if (nature == null || preview == null) throw new InvalidOperationException("Missing v6 study hierarchy.");
        var dense = Child(nature,"B - Forest masses");
        var canopy = Child(nature,"C - Continuous canopy modules");
        // Validate placement types before constructing the third comparison group.
        var modules = Groups.SelectMany(n => Child(canopy,n).Cast<Transform>()).Where(t => Variant(t) >= 0).ToArray();
        if (modules.Length != 28) throw new InvalidOperationException($"Expected 28 v6 modules, got {modules.Length}.");
        if (canopy.Find("Fixed object shadows") == null || canopy.Find("Continuous canopy shade") == null)
            throw new InvalidOperationException("Missing source shadows.");
        canopy.gameObject.SetActive(true);
        var hybrid = Group(nature,"D - Hybrid crown clusters");
        int replaced=0;
        foreach (Transform sourceGroup in canopy)
        {
            if (!Groups.Contains(sourceGroup.name))
            {
                // Copy v6's woodland and fixed-object shadows for a controlled comparison.
                Copy(sourceGroup,hybrid);
                continue;
            }
            var group=Group(hybrid,sourceGroup.name);
            foreach (Transform old in sourceGroup)
            {
                int variant=Variant(old);
                if (variant<0) { Copy(old,group); continue; } // isolated edge trees
                var oldRenderer=old.GetComponentInChildren<MeshRenderer>(true);
                if(oldRenderer==null) throw new InvalidOperationException("Missing old canopy renderer.");
                var oldBounds=oldRenderer.bounds;
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[variant],scene);
                if(obj==null) throw new InvalidOperationException("Cannot instantiate hybrid module.");
                var t=obj.transform;
                t.SetParent(group,false);
                t.SetPositionAndRotation(old.position,old.rotation);
                t.localScale=old.localScale;
                var renderer=obj.GetComponentInChildren<MeshRenderer>(true);
                if(renderer==null) throw new InvalidOperationException("Missing hybrid renderer.");
                var b=renderer.bounds;
                float oldWidth=Mathf.Max(oldBounds.size.x,oldBounds.size.z), newWidth=Mathf.Max(b.size.x,b.size.z);
                if(oldWidth<.001f||newWidth<.001f||b.size.y<.001f||old.localScale.y<=0)
                    throw new InvalidOperationException("Invalid canopy bounds.");
                // Match the existing footprint and retain the ground pivot. The taller
                // crown relief is capped below the original 12-unit flight plane.
                float ratio=oldWidth/newWidth;
                float unitHeight=b.size.y/old.localScale.y;
                float vertical=Mathf.Min(old.localScale.y*ratio,Mathf.Max(1.0f,10.5f-t.position.y)/unitHeight);
                t.localScale=new Vector3(old.localScale.x*ratio,vertical,old.localScale.z*ratio);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                replaced++;
            }
        }
        preview.ConfigureHybridComparison(dense.gameObject,canopy.gameObject,hybrid.gameObject);
        root.name="ArtLab V7 Hybrid";
        EditorUtility.SetDirty(preview);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save hybrid study.");
        Selection.activeGameObject=hybrid.gameObject;
        return $"ArtLab v7: {replaced} hybrid modules. T: dense v5 / smooth v6 / hybrid v7. G/L/H unchanged. Sandbox untouched.";
    }

    private static int Variant(Transform t)
    {
        string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
        return Array.FindIndex(Names,n=> path==OldFolder+n+".prefab");
    }
    private static Transform Child(Transform parent,string name)
    {
        return parent.Cast<Transform>().SingleOrDefault(t=>t.name==name)
            ?? throw new InvalidOperationException("Missing group: "+name);
    }
    private static void Copy(Transform source,Transform parent)
    {
        var obj=UnityEngine.Object.Instantiate(source.gameObject,parent,false);
        obj.name=source.name;
    }
    private static Material MakeMaterial()
    {
        string path=Folder+"/ForestHybridRamp.png";
        var imp=AssetImporter.GetAtPath(path) as TextureImporter;
        if(imp==null)throw new InvalidOperationException("Generate the Blender forest hybrid pack first.");
        if(!imp.sRGBTexture||imp.mipmapEnabled||imp.wrapMode!=TextureWrapMode.Clamp||imp.filterMode!=FilterMode.Bilinear||imp.textureCompression!=TextureImporterCompression.Uncompressed)
        {imp.sRGBTexture=true;imp.mipmapEnabled=false;imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Bilinear;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();}
        var tex=Require<Texture2D>(path);
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Missing URP Lit.");
        path=Folder+"/ForestHybrid.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
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
    private static Transform Group(Transform parent,string name)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    private static T Require<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing asset: "+path);
}
