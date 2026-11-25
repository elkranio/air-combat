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
    // ENV-01: visual-only modules. Never add a collider or RadarTerrainObstacle.
    internal static class Mission01ShallowRelief
    {
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/";
        private const string Library = Art + "ENV01_ShallowRelief/";
        private const string LayerName = "Mission01 / ENV01 shallow relief";
        private static readonly string[] Names = {
            "ENV01_A_SandHump", "ENV01_B_LongDrift", "ENV01_C_BroadRise",
            "ENV01_D_DoubleCrest", "ENV01_E_WindRidge"
        };
        // Compositions near the main/optional flight corridors, not uniform scatter.
        // Some expanses remain deliberately empty. Placement checks use the saved scene.
        private static readonly Vector2[] Clusters = {
            new Vector2(-420,-550), new Vector2(-160,-360), new Vector2(-470,-130),
            new Vector2(-310,110), new Vector2(-640,340), new Vector2(-245,490),
            new Vector2(-630,790), new Vector2(-260,1020), new Vector2(-560,1240),
            new Vector2(-200,1590), new Vector2(160,1840), new Vector2(780,1900),
            new Vector2(1100,1700), new Vector2(1510,1540), new Vector2(1810,1380),
            new Vector2(1460,1110), new Vector2(1510,790), new Vector2(1240,540),
            new Vector2(1460,290), new Vector2(1890,40), new Vector2(1650,-240),
            new Vector2(1880,-620), new Vector2(120,-830), new Vector2(2060,1830)
        };

        private static GameObject Root(Scene scene, string name) =>
            scene.GetRootGameObjects().SingleOrDefault(g => g.name == name);

        [MenuItem("Air Combat/Art/Apply Mission01 Shallow Relief v0")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.name != "Mission01_FirstSortie" || !scene.isLoaded)
                    throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var ground = Root(scene, "Mission01 / Faceted surface");
                var airfield = Root(scene, "Mission01 / Airfield");
                var rocks = Root(scene, "Mission01 / CL06 rock layout v3");
                var objectives = Root(scene, "Mission01 / Objectives");
                if (ground == null || airfield == null || rocks == null || objectives == null)
                    throw new InvalidOperationException("Expected current Mission01 authored roots.");
                var mf = ground.GetComponent<MeshFilter>(); var mr = ground.GetComponent<MeshRenderer>();
                var sandMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "Mission01_Facets.asset");
                var sand = AssetDatabase.LoadAssetAtPath<Material>(Art + "Mission01_Sand.mat");
                if (mf == null || mr == null || sandMesh == null || sand == null)
                    throw new InvalidOperationException("Missing original sand references.");
                var world = mr.bounds;
                if (world.size.y > .05f || world.size.x < 1000 || world.size.z < 1000)
                    throw new InvalidOperationException("ENV01 expects the flat Mission01 ground.");
                var library = BuildLibrary(sand);
                var blocked = new List<Rect>();
                // Keep the entire airfield clear, not just individual props.
                var air = airfield.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();
                if (air.Length == 0) throw new InvalidOperationException("Airfield has no renderers.");
                Bounds airBounds = air[0].bounds;
                foreach (var r in air) airBounds.Encapsulate(r.bounds);
                blocked.Add(Footprint(airBounds, 55));
                foreach (var root in new[]{rocks, objectives})
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                        if (r.enabled && r.gameObject.activeInHierarchy && r.bounds.min.y < 20f)
                            blocked.Add(Footprint(r.bounds, root == objectives ? 28 : 10));
                var area = Footprint(world, -12);
                var rng = new System.Random(71035);
                var placements = new List<(int model,Vector3 pos,float yaw,Vector3 scale)>();
                for (int c=0;c<Clusters.Length;c++)
                {
                    int wanted = c%3==0 ? 3 : 2;
                    for (int k=0;k<wanted;k++)
                    {
                        int model=(c*3+k)%Names.Length;
                        var mesh = library[model].GetComponent<MeshFilter>().sharedMesh;
                        for (int attempt=0;attempt<24;attempt++)
                        {
                            float angle = (float)rng.NextDouble()*Mathf.PI*2;
                            float dist = 25 + (float)rng.NextDouble()*150;
                            var p = Clusters[c]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*dist;
                            float yaw = (float)rng.NextDouble()*360;
                            float size=.78f+(float)rng.NextDouble()*.43f;
                            var scale=new Vector3(size,.85f+(float)rng.NextDouble()*.27f,size*(.90f+(float)rng.NextDouble()*.20f));
                            var pos=new Vector3(p.x,world.center.y,p.y);
                            // Conservative rotated footprint, including a small visual breathing gap.
                            var bounds=TransformedBounds(mesh.bounds,Matrix4x4.TRS(pos,Quaternion.Euler(0,yaw,0),scale));
                            var rect=Footprint(bounds,12);
                            if (!area.Contains(rect.min) || !area.Contains(rect.max) || blocked.Any(b=>b.Overlaps(rect))) continue;
                            placements.Add((model,pos,yaw,scale));blocked.Add(rect);break;
                        }
                    }
                }
                if (placements.Count < 10) throw new InvalidOperationException("Too few unobstructed relief placements; scene left unchanged.");
                Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Apply ENV01 shallow relief");
                var old = Root(scene,LayerName);
                if (old!=null) Undo.DestroyObjectImmediate(old);
                Undo.RecordObjects(new UnityEngine.Object[]{mf,mr},"Restore original sand");
                mf.sharedMesh=sandMesh; mr.sharedMaterial=sand;
                mr.shadowCastingMode=ShadowCastingMode.Off; mr.receiveShadows=true;
                var layer=new GameObject(LayerName); SceneManager.MoveGameObjectToScene(layer,scene);
                Undo.RegisterCreatedObjectUndo(layer,"Create ENV01 layer");
                for (int i=0;i<placements.Count;i++)
                {
                    var p=placements[i]; var go=(GameObject)PrefabUtility.InstantiatePrefab(library[p.model],layer.transform);
                    Undo.RegisterCreatedObjectUndo(go,"Place ENV01 relief");
                    go.name=Names[p.model]+" / "+(i+1).ToString("00");
                    go.transform.SetPositionAndRotation(p.pos,Quaternion.Euler(0,p.yaw,0)); go.transform.localScale=p.scale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                }
                Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject=layer;
                Debug.Log($"ENV01: {placements.Count} relief instances from five modules. Original sand restored; save scene after review.");
            }
            catch(Exception ex){Debug.LogError("ENV01: "+ex);}
        }

        [MenuItem("Air Combat/Art/Remove Mission01 Shallow Relief")]
        private static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene=SceneManager.GetActiveScene(); if(scene.name!="Mission01_FirstSortie") return;
            var layer=Root(scene,LayerName); if(layer==null)return;
            Undo.DestroyObjectImmediate(layer); EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject[] BuildLibrary(Material sand)
        {
            var meshes=Names.Select(name=>{
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(Library+name+".obj");
                var filters=model==null?Array.Empty<MeshFilter>():model.GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length!=1 || filters[0].sharedMesh==null) throw new InvalidOperationException("Missing relief mesh "+name);
                return filters[0].sharedMesh;
            }).ToArray();
            var result=new GameObject[Names.Length];
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                for(int i=0;i<Names.Length;i++)
                {
                    string path=Library+Names[i]+".prefab";
                    var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(existing!=null){result[i]=existing;continue;}
                    var go=new GameObject(Names[i],typeof(MeshFilter),typeof(MeshRenderer));
                    SceneManager.MoveGameObjectToScene(go,preview);
                    go.GetComponent<MeshFilter>().sharedMesh=meshes[i];
                    var r=go.GetComponent<MeshRenderer>(); r.sharedMaterial=sand;
                    r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=true;
                    result[i]=PrefabUtility.SaveAsPrefabAsset(go,path);
                    if(result[i]==null)throw new InvalidOperationException("Cannot save "+path);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            AssetDatabase.SaveAssets();return result;
        }

        private static Rect Footprint(Bounds b,float pad) => Rect.MinMaxRect(b.min.x-pad,b.min.z-pad,b.max.x+pad,b.max.z+pad);
        private static Bounds TransformedBounds(Bounds b,Matrix4x4 matrix)
        {
            var result=new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));
            return result;
        }
    }
}
