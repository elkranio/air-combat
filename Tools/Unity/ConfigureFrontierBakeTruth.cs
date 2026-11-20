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

// Creates a separate diagnostic scene; no references to the live map or shared hero materials.
public static class ConfigureFrontierBakeTruth
{
    private const string Art = "Assets/_Project/Art/Environment/FrontierBakeTruth";
    private const string Output = "Assets/_Project/Generated/FrontierBakeTruth";
    private const string Destination = "Assets/_Project/Scenes/FrontierBakeTruth.unity";
    private const string Pipeline = "Assets/Settings/ScorchedFrontier_URP.asset";
    private static readonly string[] Maps = { "AO", "Normal", "NormalRaw", "AOStrong", "ControlAO", "ControlNormal" };

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save open scenes first.");
        var pipeline=Require<UniversalRenderPipelineAsset>(Pipeline);
        string ready=Art+"/BakeTruth.ready.txt";
        if(!File.Exists(ready))
            throw new InvalidOperationException("Missing completed V18 bake marker: "+Path.GetFullPath(ready)+". Check Blender output for BAKE TRUTH READY.");
        string[] reportLines=File.ReadAllLines(ready);
        if(reportLines.Length<2||reportLines[0].Trim()!="V18_BAKE_TRUTH_1"||string.IsNullOrWhiteSpace(reportLines[1]))
            throw new InvalidOperationException("Invalid V18 bake marker: "+Path.GetFullPath(ready)+". Expected version header and bake report.");
        foreach(string name in Maps) if(!File.Exists(Art+"/"+name+".png"))
            throw new InvalidOperationException("Missing diagnostic map: "+name);
        string fbx=Art+"/IndustrialTestPanel.fbx";
        if(!File.Exists(fbx))throw new InvalidOperationException("Missing test mesh. Run Blender generator.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if(!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/_Project/Generated","FrontierBakeTruth");
        var shader=Shader.Find("Universal Render Pipeline/Lit")??throw new InvalidOperationException("URP Lit not found.");
        var unlit=Shader.Find("Universal Render Pipeline/Unlit")??throw new InvalidOperationException("URP Unlit not found.");
        var model=ImportMesh(fbx);
        var ao=ImportMap("AO",false);var normal=ImportMap("Normal",true);
        var raw=ImportMap("NormalRaw",false);var strong=ImportMap("AOStrong",false);
        var controlAO=ImportMap("ControlAO",false);var controlNormal=ImportMap("ControlNormal",true);
        var variants=new[]{
            Lit("01_Base",shader,null,null,0f), Lit("02_AO",shader,ao,null,0f),
            Lit("03_Normal",shader,null,normal,1f), Lit("04_Full",shader,ao,normal,1f),
            Lit("05_StrongAO",shader,strong,null,0f), Lit("06_NormalX4",shader,null,normal,4f),
            Lit("07_StrongBoth",shader,strong,normal,4f), Unlit("08_RawAO",unlit,ao),
            Unlit("09_RawNormal",unlit,raw), Lit("10_ControlBoth",shader,controlAO,controlNormal,1f),
            Lit("11_ControlAO",shader,controlAO,null,0f), Lit("12_ControlNormal",shader,null,controlNormal,1f)
        };
        var body=Solid("PanelBody",shader,new Color(.36f,.36f,.36f));
        var ground=Solid("NeutralFloor",shader,new Color(.27f,.27f,.27f));
        // All imports/validation above precede scene creation. Nothing is edited in any existing scene.
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("V18 - Bake Truth Test");
        Cube(root.transform,"Floor",new Vector3(0,-.35f,0),new Vector3(260,.7f,260),ground);
        var left=MakePanel(scene,root.transform,model,"LEFT - immutable base",-18.5f,variants[0],body);
        var right=MakePanel(scene,root.transform,model,"RIGHT - test material",18.5f,variants[3],body);
        if(left.GetComponent<MeshFilter>().sharedMesh!=right.GetComponent<MeshFilter>().sharedMesh)
            throw new InvalidOperationException("The two comparison meshes must be identical.");
        var lightObject=new GameObject("Directional Light");var sun=lightObject.AddComponent<Light>();
        sun.type=LightType.Directional;sun.color=Color.white;sun.intensity=1.25f;
        lightObject.transform.rotation=Quaternion.Euler(38f,-38f,0f);
        sun.shadows=LightShadows.Soft;sun.shadowStrength=.75f;sun.shadowBias=.035f;sun.shadowNormalBias=.18f;
        sun.lightmapBakeType=LightmapBakeType.Realtime;
        RenderSettings.sun=sun;RenderSettings.skybox=null;RenderSettings.fog=false;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.52f,.52f,.52f,1);
        RenderSettings.ambientIntensity=1f;
        var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";
        var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=22f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.24f,.24f,.24f);
        camera.nearClipPlane=.1f;camera.farClipPlane=300f;
        var rot=Quaternion.Euler(65,0,0);
        camera.transform.SetPositionAndRotation(new Vector3(0,2.2f,0)-rot*Vector3.forward*115f,rot);
        var data=cameraObject.GetComponent<UniversalAdditionalCameraData>() ?? cameraObject.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;
        root.AddComponent<FrontierBakeTruthPreview>().Configure(pipeline,camera,sun,right,variants);
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene,Destination))throw new InvalidOperationException("Cannot save V18 scene.");
        Selection.activeGameObject=root;
        return "V18 ready. LEFT fixed base, RIGHT 1-9/0 or T, C camera, L light. Blender report: "+File.ReadAllText(ready).Trim();
    }

    private static GameObject ImportMesh(string path)
    {
        var imp=AssetImporter.GetAtPath(path) as ModelImporter??throw new InvalidOperationException("Missing FBX importer.");
        imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.importNormals=ModelImporterNormals.Import;
        imp.importTangents=ModelImporterTangents.CalculateMikk;imp.importAnimation=false;imp.importLights=false;imp.importCameras=false;
        imp.isReadable=true;imp.addCollider=false;imp.meshCompression=ModelImporterMeshCompression.Off;imp.SaveAndReimport();
        var model=Require<GameObject>(path);
        var filters=model.GetComponentsInChildren<MeshFilter>(true);
        if(filters.Length!=1)throw new InvalidOperationException("Expected one planar low-poly mesh.");
        var mesh=filters[0].sharedMesh;
        if(mesh==null||mesh.vertexCount<4||mesh.vertexCount>8||mesh.triangles.Length!=6||
            mesh.uv.Length!=mesh.vertexCount||mesh.tangents.Length!=mesh.vertexCount||mesh.normals.Length!=mesh.vertexCount)
            throw new InvalidOperationException("Low mesh must have two triangles, UVs, normals and Mikk tangents.");
        return model;
    }

    private static Texture2D ImportMap(string name,bool normal)
    {
        string path=Art+"/"+name+".png";
        var imp=AssetImporter.GetAtPath(path) as TextureImporter??throw new InvalidOperationException("Missing importer: "+path);
        imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        imp.convertToNormalmap=false;imp.sRGBTexture=false;imp.mipmapEnabled=true;imp.isReadable=false;
        imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=4;
        imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=2048;
        imp.alphaSource=TextureImporterAlphaSource.None;imp.SaveAndReimport();
        return Require<Texture2D>(path);
    }

    private static Material Lit(string name,Shader shader,Texture2D ao,Texture2D normal,float strength)
    {
        var m=new Material(shader){name=name};m.SetColor("_BaseColor",new Color(.57f,.57f,.57f,1));
        m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.18f);
        m.SetTexture("_OcclusionMap",ao);m.SetFloat("_OcclusionStrength",ao!=null?1f:0f);
        m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",normal!=null?strength:0f);
        if(ao!=null)m.EnableKeyword("_OCCLUSIONMAP");else m.DisableKeyword("_OCCLUSIONMAP");
        if(normal!=null)m.EnableKeyword("_NORMALMAP");else m.DisableKeyword("_NORMALMAP");
        return SaveMaterial(name,m);
    }
    private static Material Unlit(string name,Shader shader,Texture2D map)
    {var m=new Material(shader){name=name};m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",map);return SaveMaterial(name,m);}
    private static Material Solid(string name,Shader shader,Color color)
    {var m=new Material(shader){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.05f);return SaveMaterial(name,m);}
    private static Material SaveMaterial(string name,Material value)
    {
        string path=Output+"/"+name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
        EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
    }

    private static MeshRenderer MakePanel(Scene scene,Transform parent,GameObject model,string name,float x,Material top,Material body)
    {
        var group=new GameObject(name).transform;group.SetParent(parent,false);group.position=new Vector3(x,0,0);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);
        if(instance==null)throw new InvalidOperationException("Cannot instantiate test panel.");
        instance.transform.SetParent(group,false);
        var r=instance.GetComponentsInChildren<MeshRenderer>(true).Single();
        var b=r.bounds;
        if(b.size.x<.001f||b.size.z<.001f)throw new InvalidOperationException("Unexpected FBX axis.");
        float scale=30f/b.size.x;
        instance.transform.localScale*=scale;b=r.bounds;
        if(Mathf.Abs(b.size.z-18f)>.1f||b.size.y>.05f)throw new InvalidOperationException("Expected horizontal 30 x 18 plane: "+b.size);
        var filter=r.GetComponent<MeshFilter>();
        if(filter.transform.TransformDirection(filter.sharedMesh.normals[0]).y<.98f)
            throw new InvalidOperationException("Expected upward facing test surface.");
        instance.transform.position+=new Vector3(x-b.center.x,3.4f-b.center.y,-b.center.z);
        r.sharedMaterial=top;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
        r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        // Identical solid support under each planar test surface. Top is only the exported FBX plane.
        Cube(group,"Foundation",new Vector3(x,1.65f,0),new Vector3(30,3.3f,18),body);
        return r;
    }
    private static void Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);
        o.transform.position=position;o.transform.localScale=scale;
        var r=o.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
        r.reflectionProbeUsage=ReflectionProbeUsage.Off;Object.DestroyImmediate(o.GetComponent<Collider>());
    }
    private static T Require<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing asset: "+path);
}
