using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // ENV04 is entirely visual. Existing launchers, AI, radar and map are untouched.
    internal static class Mission01FieldSites
    {
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/";
        private const string Library = Art + "ENV04_FieldSites/";
        private const string PicketArt = "Assets/_Project/Art/Environment/Dominion/SAM01_Picket/";
        private const string LayerName = "Mission01 / ENV04 field sites";
        private static readonly string[] Names = {
            "ENV04_A_FieldRadar", "ENV04_B_SupplyCrate", "ENV04_C_LongCase", "ENV04_D_Generator",
            "ENV04_E_Pallet", "ENV04_F_Hardstand", "ENV04_G_HardstandLong", "ENV04_H_Tracks"
        };
        private static readonly string[][] Parts = {
            new[]{"Hull","Equipment","Metal","Caps"}, new[]{"Hull","Equipment","Metal","Caps"},
            new[]{"Hull","Equipment","Metal","Caps"}, new[]{"Hull","Equipment","Metal","Caps"},
            new[]{"Hull","Equipment","Metal","Caps"}, new[]{"Pad"}, new[]{"Pad"}, new[]{"Track"}
        };
        private static GameObject Root(Scene scene,string name)=>scene.GetRootGameObjects().SingleOrDefault(g=>g.name==name);
        private static Renderer[] Renderers(GameObject go)=>go.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled).ToArray();
        private static Bounds Combine(Renderer[] rs)
        {
            if(rs.Length==0)throw new InvalidOperationException("Missing renderer bounds.");
            var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
        }
        private static Rect Footprint(Bounds b,float pad=0)=>Rect.MinMaxRect(b.min.x-pad,b.min.z-pad,b.max.x+pad,b.max.z+pad);
        private static Bounds TransformBounds(Bounds b,Matrix4x4 m)
        {
            var result=new Bounds(m.MultiplyPoint3x4(b.center),Vector3.zero);
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                result.Encapsulate(m.MultiplyPoint3x4(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));
            return result;
        }
        private static Bounds LocalBounds(GameObject prefab)
        {
            var filters=prefab.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length==0||filters.Any(f=>f.sharedMesh==null))throw new InvalidOperationException("Invalid prefab "+prefab.name);
            var inv=prefab.transform.worldToLocalMatrix;
            var b=TransformBounds(filters[0].sharedMesh.bounds,inv*filters[0].transform.localToWorldMatrix);
            foreach(var f in filters)b.Encapsulate(TransformBounds(f.sharedMesh.bounds,inv*f.transform.localToWorldMatrix));return b;
        }
        private sealed class Placement
        {
            public int model;public Vector3 position;public float yaw;public Vector3 scale;public Rect rect;
        }
        private static Placement At(int model,Bounds[] bounds,Vector3 origin,float heading,float x,float z,float yaw,float scale=1,float y=0)
        {
            var p=new Placement{model=model,position=origin+Quaternion.Euler(0,heading,0)*new Vector3(x,y,z),yaw=heading+yaw,scale=new Vector3(scale,1,scale)};
            p.rect=Footprint(TransformBounds(bounds[model],Matrix4x4.TRS(p.position,Quaternion.Euler(0,p.yaw,0),p.scale)),model<5?.35f:0);return p;
        }
        private static bool Fits(Placement p,Rect area,List<Rect> blocked)=>area.Contains(p.rect.min)&&area.Contains(p.rect.max)&&!blocked.Any(b=>b.Overlaps(p.rect));

        [MenuItem("Air Combat/Art/Apply Mission01 Field Sites v0")]
        private static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            try
            {
                var scene=SceneManager.GetActiveScene();
                if(!scene.isLoaded||scene.name!="Mission01_FirstSortie")throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var ground=Root(scene,"Mission01 / Faceted surface");var objectives=Root(scene,"Mission01 / Objectives");
                var rocks=Root(scene,"Mission01 / CL06 rock layout v3");var airfield=Root(scene,"Mission01 / Airfield");
                if(ground==null||objectives==null||rocks==null||airfield==null)throw new InvalidOperationException("Expected current Mission01 roots.");
                var world=ground.GetComponent<Renderer>().bounds;
                if(world.size.y>.05f)throw new InvalidOperationException("Expected flat ground.");
                var area=Footprint(world,-5);
                var sites=objectives.GetComponentsInChildren<SurfaceToAirLauncher>(true).Where(s=>s.gameObject.activeInHierarchy).OrderBy(s=>s.name).ToArray();
                if(sites.Length!=6)throw new InvalidOperationException("Expected six installed Picket launchers.");
                var blocked=new List<Rect>{Footprint(Combine(Renderers(airfield)),8)};
                foreach(var root in new[]{rocks,Root(scene,"Mission01 / ENV01 shallow relief"),Root(scene,"Mission01 / ENV02 desert vegetation"),Root(scene,"Mission01 / ENV03 desert stones")})
                    if(root!=null)foreach(var r in Renderers(root))if(r.bounds.min.y<15)blocked.Add(Footprint(r.bounds,.6f));
                foreach(var r in Renderers(objectives))
                    if(r.bounds.min.y<15&&!sites.Any(s=>r.transform.IsChildOf(s.transform)))blocked.Add(Footprint(r.bounds,2));
                var siteBounds=sites.Select(s=>Footprint(Combine(Renderers(s.gameObject)),.8f)).ToArray();
                var library=BuildLibrary();var local=library.Select(LocalBounds).ToArray();
                var layouts=new List<Placement[]>();int trackCount=0;
                // Six authored asymmetrical compositions, tested against current scene geometry.
                for(int i=0;i<sites.Length;i++)
                {
                    var site=sites[i];var origin=new Vector3(site.transform.position.x,world.center.y,site.transform.position.z);
                    var obstacles=new List<Rect>(blocked);for(int j=0;j<sites.Length;j++)if(i!=j)obstacles.Add(siteBounds[j]);
                    var propBlocked=new List<Rect>(obstacles){siteBounds[i]};
                    Placement[] chosen=null;
                    for(int attempt=0;attempt<32&&chosen==null;attempt++)
                    {
                        float heading=site.transform.eulerAngles.y+new[]{0f,35f,-25f,70f,-55f,105f}[i]+(attempt%16)*22.5f;
                        float size=attempt<16?1f:.84f;
                        float side=i%2==0?1:-1;
                        var pad=At(5+i%2,local,origin,heading,0,0,0,size,.035f);
                        if(!Fits(pad,area,obstacles))continue;
                        var props=new List<Placement>();
                        // Place the radar off to one side; the launcher remains the focal point.
                        var desired=new[]{
                            (model:0,x:side*9.2f,z:2.7f+(i%3-1)*2f,yaw:25f+i*19f),
                            (model:1+i%3,x:-side*7.8f,z:3.8f,yaw:12f+i*7f),
                            (model:i%2==0?2:4,x:-side*8.6f,z:-2.8f,yaw:75f+i*12f),
                            (model:i%2==0?4:1,x:side*5.7f,z:8.8f,yaw:-18f+i*9f)
                        };
                        var reserved=new List<Rect>(propBlocked);
                        foreach(var d in desired.Take(i%3==0?4:3))
                        {
                            var p=At(d.model,local,origin,heading,d.x,d.z,d.yaw,1,.035f);
                            if(!Fits(p,area,reserved))continue;
                            props.Add(p);reserved.Add(p.rect);
                        }
                        if(!props.Any(p=>p.model==0)||props.Count<3)continue;
                        var track=At(7,local,origin,heading,side*(i%3-1)*1.5f,-10.5f*size,0,.85f,.055f);
                        // Optional if the exit would cut across existing rocks/vegetation.
                        bool trackFits=Fits(track,area,obstacles)&&!props.Any(p=>p.rect.Overlaps(track.rect))&&!siteBounds[i].Overlaps(track.rect);
                        chosen=new[]{pad}.Concat(props).Concat(trackFits?new[]{track}:Array.Empty<Placement>()).ToArray();
                        if(trackFits)trackCount++;
                    }
                    if(chosen==null)throw new InvalidOperationException("No clear field-site composition near "+site.name+". Scene unchanged; adjust nearby decoration.");
                    layouts.Add(chosen);blocked.AddRange(chosen.Select(p=>p.rect));
                }
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply ENV04 field sites");
                var old=Root(scene,LayerName);if(old!=null)Undo.DestroyObjectImmediate(old);
                var layer=new GameObject(LayerName);SceneManager.MoveGameObjectToScene(layer,scene);Undo.RegisterCreatedObjectUndo(layer,"Create ENV04 layer");
                for(int i=0;i<sites.Length;i++)
                {
                    var siteLayer=new GameObject("Field site "+(char)('A'+i));SceneManager.MoveGameObjectToScene(siteLayer,scene);Undo.RegisterCreatedObjectUndo(siteLayer,"Create field site");Undo.SetTransformParent(siteLayer.transform,layer.transform,"Parent field site");
                    siteLayer.transform.position=sites[i].transform.position;
                    foreach(var p in layouts[i])
                    {
                        var go=(GameObject)PrefabUtility.InstantiatePrefab(library[p.model],siteLayer.transform);Undo.RegisterCreatedObjectUndo(go,"Place field prop");
                        go.transform.SetPositionAndRotation(p.position,Quaternion.Euler(0,p.yaw,0));go.transform.localScale=p.scale;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                    }
                }
                Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=layer;
                Debug.Log($"ENV04: six field sites, {layouts.Sum(l=>l.Length)} modules, {trackCount} clear track approaches. Save scene after review.");
            }
            catch(Exception ex){Debug.LogError("ENV04: "+ex);}
        }
        [MenuItem("Air Combat/Art/Remove Mission01 Field Sites")]
        private static void Remove()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var scene=SceneManager.GetActiveScene();if(scene.name!="Mission01_FirstSortie")return;
            var layer=Root(scene,LayerName);if(layer==null)return;Undo.DestroyObjectImmediate(layer);EditorSceneManager.MarkSceneDirty(scene);
        }
        private static Material GroundMaterial(string name,Color tint)
        {
            var path=Library+"ENV04_"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
            var sand=AssetDatabase.LoadAssetAtPath<Material>(Art+"Mission01_Sand.mat");if(sand==null)throw new InvalidOperationException("Original sand material missing.");
            var m=new Material(sand);m.SetColor("_BaseColor",tint);AssetDatabase.CreateAsset(m,path);return m;
        }
        private static GameObject[] BuildLibrary()
        {
            var materials=new Dictionary<string,Material>();
            foreach(var part in new[]{"Hull","Equipment","Metal","Caps"})
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(PicketArt+"SAM01_"+part+".mat");if(m==null)throw new InvalidOperationException("Missing Picket material "+part);materials.Add(part,m);
            }
            materials.Add("Pad",GroundMaterial("Pad",new Color(.90f,.90f,.88f,1)));
            materials.Add("Track",GroundMaterial("Track",new Color(.78f,.78f,.76f,1)));
            var result=new GameObject[Names.Length];var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                for(int i=0;i<Names.Length;i++)
                {
                    string path=Library+Names[i]+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null){result[i]=existing;continue;}
                    var go=new GameObject(Names[i]);SceneManager.MoveGameObjectToScene(go,preview);
                    foreach(var part in Parts[i])
                    {
                        var obj=AssetDatabase.LoadAssetAtPath<GameObject>(Library+Names[i]+"_"+part+".obj");
                        var filters=obj==null?Array.Empty<MeshFilter>():obj.GetComponentsInChildren<MeshFilter>(true);
                        if(filters.Length!=1||filters[0].sharedMesh==null)throw new InvalidOperationException("Missing ENV04 mesh "+Names[i]+"_"+part);
                        var child=new GameObject(part,typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(child,preview);child.transform.SetParent(go.transform,false);
                        child.GetComponent<MeshFilter>().sharedMesh=filters[0].sharedMesh;var r=child.GetComponent<MeshRenderer>();r.sharedMaterial=materials[part];r.shadowCastingMode=i<5?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;
                    }
                    result[i]=PrefabUtility.SaveAsPrefabAsset(go,path);if(result[i]==null)throw new InvalidOperationException("Cannot save "+path);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            AssetDatabase.SaveAssets();return result;
        }
    }
}
