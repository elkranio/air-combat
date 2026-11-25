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
            "ENV04_E_Pallet"
        };
        private static readonly string[][] Parts = {
            new[]{"Hull","Equipment","Metal","Caps"}, new[]{"Hull","Equipment","Metal","Caps"},
            new[]{"Hull","Equipment","Metal","Caps"}, new[]{"Hull","Equipment","Metal","Caps"},
            new[]{"Hull","Equipment","Metal","Caps"}
        };
        // Existing accepted nature prefabs. Indices 5+ in the combined placement library.
        private static readonly string[] NaturePaths = {
            "ENV02_DesertVegetation/ENV02_A_Umbrella.prefab",
            "ENV02_DesertVegetation/ENV02_B_WindBent.prefab",
            "ENV02_DesertVegetation/ENV02_D_SaltBush.prefab",
            "ENV02_DesertVegetation/ENV02_E_Scrub.prefab",
            "ENV03_DesertStones/ENV03_B_RubbleScatter.prefab",
            "ENV03_DesertStones/ENV03_C_WeatheredBoulder.prefab"
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
            var p=new Placement{model=model,position=origin+Quaternion.Euler(0,heading,0)*new Vector3(x,y,z),yaw=heading+yaw,scale=Vector3.one*scale};
            p.rect=Footprint(TransformBounds(bounds[model],Matrix4x4.TRS(p.position,Quaternion.Euler(0,p.yaw,0),p.scale)),model<5?1.5f:2f);return p;
        }
        private static bool Fits(Placement p,Rect area,List<Rect> blocked)=>area.Contains(p.rect.min)&&area.Contains(p.rect.max)&&!blocked.Any(b=>b.Overlaps(p.rect));

        // Try the authored offset first, then nearby alternatives. Only a genuinely
        // crowded site needs the wider search; do not move existing scene objects.
        private static Placement FindProp(int model,Bounds[] bounds,Vector3 origin,float heading,
            float x,float z,float yaw,Rect area,List<Rect> blocked,bool wide,float scale=1f)
        {
            var preferred=At(model,bounds,origin,heading,x,z,yaw,scale,.015f);
            if(Fits(preferred,area,blocked))return preferred;
            float radius=Mathf.Sqrt(x*x+z*z),angle=Mathf.Atan2(z,x);
            // Alternate sides of the preferred bearing, rather than uniformly scattering.
            foreach(float extra in (wide?new[]{0f,3f,6f,10f,16f,24f}:new[]{0f,3f,6f,10f}))
                for(int j=0;j<16;j++)
                {
                    int step=j==0?0:(j%2==1?1:-1)*((j+1)/2);
                    float a=angle+step*Mathf.PI/8f,r=radius+extra;
                    var p=At(model,bounds,origin,heading,Mathf.Cos(a)*r,Mathf.Sin(a)*r,yaw,scale,.015f);
                    if(Fits(p,area,blocked))return p;
                }
            return null;
        }

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
                var nature=NaturePaths.Select(path=>{
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+path);
                    if(prefab==null)throw new InvalidOperationException("Missing accepted nature prefab: "+path+". Apply ENV02 and ENV03 first.");
                    return prefab;
                }).ToArray();
                var library=BuildLibrary().Concat(nature).ToArray();var local=library.Select(LocalBounds).ToArray();
                var layouts=new List<Placement[]>();int radarCount=0,natureCount=0,propCount=0;
                var existingNature=new List<Vector2>();
                foreach(var root in new[]{Root(scene,"Mission01 / ENV02 desert vegetation"),Root(scene,"Mission01 / ENV03 desert stones")})
                    if(root!=null)foreach(Transform child in root.transform)
                        if(child.gameObject.activeInHierarchy)existingNature.Add(new Vector2(child.position.x,child.position.z));
                // Leave the original sand uninterrupted. Equipment is a loose, off-center
                // group rather than a ring of props on a display base.
                for(int i=0;i<sites.Length;i++)
                {
                    var site=sites[i];var origin=new Vector3(site.transform.position.x,world.center.y,site.transform.position.z);
                    var obstacles=new List<Rect>(blocked);obstacles.AddRange(siteBounds);
                    Placement[] chosen=Array.Empty<Placement>();int bestScore=-1;float chosenHeading=0;
                    float side=i%2==0?1:-1;
                    for(int attempt=0;attempt<32;attempt++)
                    {
                        float heading=site.transform.eulerAngles.y+new[]{0f,35f,-25f,70f,-55f,105f}[i]+(attempt%16)*22.5f;
                        var desired=new[]{
                            (model:0,x:side*14.5f,z:4f+(i%3-1)*3f,yaw:25f+i*19f),
                            (model:1+i%3,x:-side*13.5f,z:5.5f,yaw:12f+i*7f),
                            (model:i%2==0?2:4,x:-side*17f,z:-3.5f,yaw:75f+i*12f),
                            (model:i%2==0?4:1,x:side*8f,z:16f,yaw:-18f+i*9f)
                        };
                        var props=new List<Placement>();var reserved=new List<Rect>(obstacles);
                        int wanted=i%3==0?4:3;
                        foreach(var d in desired.Take(wanted))
                        {
                            var p=FindProp(d.model,local,origin,heading,d.x,d.z,d.yaw,area,reserved,d.model==0&&attempt>=16);
                            if(p==null)continue;
                            props.Add(p);reserved.Add(p.rect);
                        }
                        bool hasRadar=props.Any(p=>p.model==0);
                        int score=(hasRadar?100:0)+props.Count*10-(attempt>=16?2:0);
                        if(score<=bestScore)continue;
                        bestScore=score;chosen=props.ToArray();chosenHeading=heading;
                        if(hasRadar&&props.Count==wanted)break;
                    }
                    var composition=chosen.ToList();var occupied=new List<Rect>(obstacles);occupied.AddRange(chosen.Select(p=>p.rect));
                    // Dress the outside of the site, not the truck. Preserve any nearby
                    // accepted scenery; supplement it only where the site is still bare.
                    var center=new Vector2(origin.x,origin.z);
                    int nearby=existingNature.Count(p=>(p-center).sqrMagnitude<38f*38f);
                    int wantedNature=Mathf.Clamp((i%3==0?1:2)-nearby,0,2);
                    var naturalModels=new[]{new[]{9,7},new[]{5,10},new[]{8,9},new[]{6,7},new[]{10,8},new[]{7,5}};
                    for(int k=0;k<wantedNature;k++)
                    {
                        int model=naturalModels[i][k];float scale=.85f+(i+k)%4*.07f;
                        var p=FindProp(model,local,origin,chosenHeading,side*(k==0?25f:33f),k==0?-16f:-10f,40f+i*53f+k*29f,area,occupied,false,scale);
                        if(p==null)continue;
                        composition.Add(p);occupied.Add(p.rect);natureCount++;
                        existingNature.Add(new Vector2(p.position.x,p.position.z));
                    }
                    bool radarPlaced=chosen.Any(p=>p.model==0);int extras=chosen.Count(p=>p.model>0);
                    if(radarPlaced)radarCount++;propCount+=extras;
                    if(!radarPlaced||extras<2)Debug.LogWarning($"ENV04: {site.name}: tight clearance; radar={radarPlaced}, props={extras}. Existing scenery kept.",site);
                    layouts.Add(composition.ToArray());blocked.AddRange(composition.Select(p=>p.rect));
                }
                if(radarCount+propCount==0)throw new InvalidOperationException("No free field-site placements in this scene.");
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
                Debug.Log($"ENV04: six sites checked, {radarCount} radars, {propCount} equipment props, {natureCount} additional nature modules. No pads or tracks. Save scene after review.");
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
        private static GameObject[] BuildLibrary()
        {
            var materials=new Dictionary<string,Material>();
            foreach(var part in new[]{"Hull","Equipment","Metal","Caps"})
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(PicketArt+"SAM01_"+part+".mat");if(m==null)throw new InvalidOperationException("Missing Picket material "+part);materials.Add(part,m);
            }
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
                        child.GetComponent<MeshFilter>().sharedMesh=filters[0].sharedMesh;var r=child.GetComponent<MeshRenderer>();r.sharedMaterial=materials[part];r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                    }
                    result[i]=PrefabUtility.SaveAsPrefabAsset(go,path);if(result[i]==null)throw new InvalidOperationException("Cannot save "+path);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            AssetDatabase.SaveAssets();return result;
        }
    }
}
