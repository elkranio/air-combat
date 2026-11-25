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
    // Low decorative sandstone, independent of the collision/radar rock library.
    internal static class Mission01DesertStones
    {
        private const string Library = "Assets/_Project/Art/Environment/Mission01_FirstSortie/ENV03_DesertStones/";
        private const string LayerName = "Mission01 / ENV03 desert stones";
        private static readonly string[] Names = { "ENV03_A_ExposedSlab", "ENV03_B_RubbleScatter", "ENV03_C_WeatheredBoulder", "ENV03_D_BrokenOutcrop" };
        private static readonly int[] Tones = { 0, 1, 1, 0 };
        private static readonly Color32[] Palette = { new Color32(177,149,119,255), new Color32(164,136,110,255) };
        private static GameObject Root(Scene scene, string name) => scene.GetRootGameObjects().SingleOrDefault(g=>g.name==name);
        private static Rect Footprint(Bounds b,float pad) => Rect.MinMaxRect(b.min.x-pad,b.min.z-pad,b.max.x+pad,b.max.z+pad);
        private static float Sample(System.Random rng,float a,float b)=>a+(b-a)*(float)rng.NextDouble();
        private static Bounds TransformedBounds(Bounds b, Matrix4x4 matrix)
        {
            var result=new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));
            return result;
        }
        private static Bounds Combine(Renderer[] renderers)
        {
            if(renderers.Length==0)throw new InvalidOperationException("Expected renderers.");
            var b=renderers[0].bounds;
            foreach(var r in renderers)b.Encapsulate(r.bounds);
            return b;
        }
        private static Renderer[] Renderers(GameObject root) => root.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled).ToArray();

        [MenuItem("Air Combat/Art/Apply Mission01 Desert Stones v0")]
        private static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            try
            {
                var scene=SceneManager.GetActiveScene();
                if(!scene.isLoaded || scene.name!="Mission01_FirstSortie")throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var ground=Root(scene,"Mission01 / Faceted surface");
                var airfield=Root(scene,"Mission01 / Airfield");
                var rocks=Root(scene,"Mission01 / CL06 rock layout v3");
                var objectives=Root(scene,"Mission01 / Objectives");
                var relief=Root(scene,"Mission01 / ENV01 shallow relief");
                var plants=Root(scene,"Mission01 / ENV02 desert vegetation");
                if(ground==null || airfield==null || rocks==null || objectives==null || relief==null)
                    throw new InvalidOperationException("Expected Mission01 roots and ENV01 relief.");
                if(plants==null)Debug.LogWarning("ENV03: ENV02 layer absent. Apply vegetation before stones for combined composition.");
                var world=ground.GetComponent<Renderer>().bounds;
                if(world.size.y>.05f || world.size.x<1000 || world.size.z<1000)throw new InvalidOperationException("Expected flat Mission01 ground.");
                var area=Footprint(world,-12);
                var blocked=new List<Rect>{Footprint(Combine(Renderers(airfield)),45)};
                foreach(var root in new[]{rocks,objectives,relief,plants})
                {
                    if(root==null)continue;
                    foreach(var r in Renderers(root))if(r.bounds.min.y<20f)
                        blocked.Add(Footprint(r.bounds,root==objectives?32:root==rocks?5:2));
                }
                var library=BuildLibrary();
                var local=library.Select(p=>p.GetComponent<MeshFilter>().sharedMesh.bounds).ToArray();
                var rng=new System.Random(33073);
                var anchors=new List<Bounds>();
                foreach(var root in new[]{relief,plants})
                {
                    if(root==null)continue;
                    var candidates=root.transform.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy)
                        .Select(t=>Renderers(t.gameObject)).Where(rs=>rs.Length>0).Select(Combine).ToList();
                    for(int i=candidates.Count-1;i>0;i--){int j=rng.Next(i+1);var tmp=candidates[i];candidates[i]=candidates[j];candidates[j]=tmp;}
                    anchors.AddRange(candidates.Take(root==relief?22:12));
                }
                var centers=new List<Vector2>();
                foreach(var hill in anchors)
                {
                    for(int attempt=0;attempt<22;attempt++)
                    {
                        float a=Sample(rng,0,Mathf.PI*2);var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                        float edge=Mathf.Min(hill.extents.x/Mathf.Max(.001f,Mathf.Abs(d.x)),hill.extents.z/Mathf.Max(.001f,Mathf.Abs(d.y)));
                        var p=new Vector2(hill.center.x,hill.center.z)+d*(edge+Sample(rng,16,42));
                        if(!area.Contains(p)||blocked.Any(b=>b.Contains(p))||centers.Any(c=>(c-p).sqrMagnitude<55*55))continue;
                        centers.Add(p);break;
                    }
                }
                foreach(var p in new[]{new Vector2(-690,-260),new Vector2(-420,380),new Vector2(-640,1120),new Vector2(410,1640),new Vector2(1070,1830),new Vector2(1940,1470),new Vector2(1520,590),new Vector2(1810,-370)})
                    if(area.Contains(p)&&!blocked.Any(b=>b.Contains(p))&&!centers.Any(c=>(c-p).sqrMagnitude<65*65))centers.Add(p);
                var placements=new List<(int model,Vector3 pos,float yaw,float size)>();
                for(int i=0;i<centers.Count;i++)
                {
                    // Mostly one module, occasionally a second smaller companion.
                    int count=i%3==0?2:1;
                    for(int k=0;k<count;k++)
                    {
                        int model=k==0?i%Names.Length:(i%2==0?1:2);
                        for(int attempt=0;attempt<28;attempt++)
                        {
                            float a=Sample(rng,0,Mathf.PI*2),dist=Sample(rng,k==0?0:18,k==0?18:32);
                            var p=centers[i]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*dist;
                            float yaw=Sample(rng,0,360),size=Sample(rng,.75f,1.18f)*(k==0?1:.8f);
                            var pos=new Vector3(p.x,world.center.y,p.y);
                            var rect=Footprint(TransformedBounds(local[model],Matrix4x4.TRS(pos,Quaternion.Euler(0,yaw,0),Vector3.one*size)),3);
                            if(!area.Contains(rect.min)||!area.Contains(rect.max)||blocked.Any(b=>b.Overlaps(rect)))continue;
                            placements.Add((model,pos,yaw,size));blocked.Add(rect);break;
                        }
                    }
                }
                if(placements.Count<10)throw new InvalidOperationException("Too few free stone placements; scene unchanged.");
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply ENV03 desert stones");
                var old=Root(scene,LayerName);if(old!=null)Undo.DestroyObjectImmediate(old);
                var layer=new GameObject(LayerName);SceneManager.MoveGameObjectToScene(layer,scene);Undo.RegisterCreatedObjectUndo(layer,"Create ENV03 layer");
                for(int i=0;i<placements.Count;i++)
                {
                    var p=placements[i];var go=(GameObject)PrefabUtility.InstantiatePrefab(library[p.model],layer.transform);
                    Undo.RegisterCreatedObjectUndo(go,"Place ENV03 stones");go.name=Names[p.model]+" / "+(i+1).ToString("00");
                    go.transform.SetPositionAndRotation(p.pos,Quaternion.Euler(0,p.yaw,0));go.transform.localScale=Vector3.one*p.size;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                }
                Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=layer;
                Debug.Log($"ENV03: {placements.Count} decorative stone modules. Save scene after review.");
            }
            catch(Exception ex){Debug.LogError("ENV03: "+ex);}
        }
        [MenuItem("Air Combat/Art/Remove Mission01 Desert Stones")]
        private static void Remove()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var scene=SceneManager.GetActiveScene();if(scene.name!="Mission01_FirstSortie")return;
            var layer=Root(scene,LayerName);if(layer==null)return;
            Undo.DestroyObjectImmediate(layer);EditorSceneManager.MarkSceneDirty(scene);
        }
        private static GameObject[] BuildLibrary()
        {
            var meshes=Names.Select(name=>{
                var obj=AssetDatabase.LoadAssetAtPath<GameObject>(Library+name+".obj");
                var filters=obj==null?Array.Empty<MeshFilter>():obj.GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length!=1||filters[0].sharedMesh==null)throw new InvalidOperationException("Missing stone mesh "+name);
                return filters[0].sharedMesh;
            }).ToArray();
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit missing.");
            var materials=new Material[Palette.Length];
            for(int i=0;i<Palette.Length;i++)
            {
                string path=Library+"ENV03_Stone_"+i+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(shader);mat.SetColor("_BaseColor",Palette[i]);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(mat,path);}
                materials[i]=mat;
            }
            var result=new GameObject[Names.Length];var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                for(int i=0;i<Names.Length;i++)
                {
                    string path=Library+Names[i]+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(existing!=null){result[i]=existing;continue;}
                    var go=new GameObject(Names[i],typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(go,preview);
                    go.GetComponent<MeshFilter>().sharedMesh=meshes[i];
                    var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=materials[Tones[i]];r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                    result[i]=PrefabUtility.SaveAsPrefabAsset(go,path);if(result[i]==null)throw new InvalidOperationException("Cannot save "+path);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            AssetDatabase.SaveAssets();return result;
        }
    }
}
