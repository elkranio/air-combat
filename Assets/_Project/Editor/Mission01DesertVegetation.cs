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
    // ENV02 is scene dressing only: no colliders, radar, or flight logic.
    internal static class Mission01DesertVegetation
    {
        private const string Library = "Assets/_Project/Art/Environment/Mission01_FirstSortie/ENV02_DesertVegetation/";
        private const string LayerName = "Mission01 / ENV02 desert vegetation";
        private static readonly string[] Names = {
            "ENV02_A_Umbrella", "ENV02_B_WindBent", "ENV02_C_Forked",
            "ENV02_D_SaltBush", "ENV02_E_Scrub"
        };
        private static readonly string[] Parts = { "Bark", "Leaf", "Light" };
        private static readonly Color32[] Palette = {
            new Color32(112,91,66,255), new Color32(115,120,70,255), new Color32(141,139,83,255)
        };
        private static GameObject Root(Scene scene, string name) => scene.GetRootGameObjects().SingleOrDefault(g => g.name == name);
        private static Rect Footprint(Bounds b, float pad) => Rect.MinMaxRect(b.min.x-pad,b.min.z-pad,b.max.x+pad,b.max.z+pad);
        private static Bounds TransformedBounds(Bounds b, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int x=0;x<2;x++) for(int y=0;y<2;y++) for(int z=0;z<2;z++)
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));
            return result;
        }
        private static Bounds Combine(Renderer[] renderers)
        {
            if (renderers.Length == 0) throw new InvalidOperationException("Expected renderers.");
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
        private static float Sample(System.Random rng, float a, float b) => a+(b-a)*(float)rng.NextDouble();

        [MenuItem("Air Combat/Art/Apply Mission01 Desert Vegetation v0")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.name != "Mission01_FirstSortie") throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var ground = Root(scene,"Mission01 / Faceted surface");
                var airfield = Root(scene,"Mission01 / Airfield");
                var rocks = Root(scene,"Mission01 / CL06 rock layout v3");
                var objectives = Root(scene,"Mission01 / Objectives");
                var relief = Root(scene,"Mission01 / ENV01 shallow relief");
                if (ground == null || airfield == null || rocks == null || objectives == null || relief == null)
                    throw new InvalidOperationException("Expected current mission roots and ENV01 relief layer.");
                var world = ground.GetComponent<Renderer>().bounds;
                if (world.size.y > .05f || world.size.x < 1000 || world.size.z < 1000) throw new InvalidOperationException("Expected flat mission ground.");
                var area = Footprint(world,-12);
                var blocked = new List<Rect> { Footprint(Combine(airfield.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray()),45) };
                foreach (var root in new[]{rocks,objectives,relief})
                    foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                        if(r.enabled && r.gameObject.activeInHierarchy && r.bounds.min.y < 20f)
                            blocked.Add(Footprint(r.bounds,root==objectives?30:root==rocks?6:2));
                var library = BuildLibrary();
                // Prefab local bounds, calculated from mesh geometry, not asset-world renderer bounds.
                var localBounds = library.Select(PrefabBounds).ToArray();
                var rng = new System.Random(23019);
                var placements = new List<(int model,Vector3 pos,float yaw,float scale)>();
                var hillBounds = relief.transform.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy)
                    .Select(t=>t.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled).ToArray())
                    .Where(rs=>rs.Length>0).Select(Combine).ToList();
                // Deterministic shuffle to avoid dressing every hill in sequence.
                for(int i=hillBounds.Count-1;i>0;i--){int j=rng.Next(i+1);var tmp=hillBounds[i];hillBounds[i]=hillBounds[j];hillBounds[j]=tmp;}
                var centers = new List<Vector2>();
                foreach(var hill in hillBounds.Take(28))
                {
                    // Find one loose composition near the edge, not on the raised mesh.
                    for(int attempt=0;attempt<18;attempt++)
                    {
                        float a=Sample(rng,0,2*Mathf.PI);
                        var direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                        float edge=Mathf.Min(hill.extents.x/Mathf.Max(.001f,Mathf.Abs(direction.x)),hill.extents.z/Mathf.Max(.001f,Mathf.Abs(direction.y)));
                        var p=new Vector2(hill.center.x,hill.center.z)+direction*(edge+Sample(rng,22,48));
                        if(!area.Contains(p)||blocked.Any(b=>b.Contains(p))||centers.Any(c=>(c-p).sqrMagnitude<55*55))continue;
                        centers.Add(p);break;
                    }
                }
                // A handful of independent compositions; large areas remain bare.
                foreach(var p in new[]{new Vector2(-680,-150),new Vector2(-390,660),new Vector2(-610,1410),new Vector2(380,1760),new Vector2(1210,1650),new Vector2(1890,1280),new Vector2(1580,520),new Vector2(1890,-380)})
                    if(area.Contains(p)&&!blocked.Any(b=>b.Contains(p))&&!centers.Any(c=>(c-p).sqrMagnitude<70*70))centers.Add(p);
                for(int i=0;i<centers.Count;i++)
                {
                    var c=centers[i];
                    // One or two trees with 1-3 shrubs; some shrub-only clusters.
                    int trees=i%5==0?0:i%4==0?2:1;
                    int shrubs=1+rng.Next(3);
                    for(int k=0;k<trees+shrubs;k++)
                    {
                        int model=k<trees?rng.Next(3):3+rng.Next(2);
                        for(int attempt=0;attempt<32;attempt++)
                        {
                            float a=Sample(rng,0,Mathf.PI*2), d=Sample(rng,0,k<trees?22:35);
                            var p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*d;
                            float yaw=Sample(rng,0,360),scale=Sample(rng,.8f,1.2f);
                            var pos=new Vector3(p.x,world.center.y,p.y);
                            var b=TransformedBounds(localBounds[model],Matrix4x4.TRS(pos,Quaternion.Euler(0,yaw,0),Vector3.one*scale));
                            var rect=Footprint(b,model<3?3:1.5f);
                            if(!area.Contains(rect.min)||!area.Contains(rect.max)||blocked.Any(o=>o.Overlaps(rect)))continue;
                            placements.Add((model,pos,yaw,scale));blocked.Add(rect);break;
                        }
                    }
                }
                if(placements.Count<12) throw new InvalidOperationException("Too few clear vegetation placements. Scene unchanged.");
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply ENV02 vegetation");
                var old=Root(scene,LayerName);if(old!=null)Undo.DestroyObjectImmediate(old);
                var layer=new GameObject(LayerName);SceneManager.MoveGameObjectToScene(layer,scene);Undo.RegisterCreatedObjectUndo(layer,"Create ENV02 layer");
                for(int i=0;i<placements.Count;i++)
                {
                    var p=placements[i];var go=(GameObject)PrefabUtility.InstantiatePrefab(library[p.model],layer.transform);
                    Undo.RegisterCreatedObjectUndo(go,"Place ENV02 plant");go.name=Names[p.model]+" / "+(i+1).ToString("00");
                    go.transform.SetPositionAndRotation(p.pos,Quaternion.Euler(0,p.yaw,0));go.transform.localScale=Vector3.one*p.scale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                }
                Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=layer;
                Debug.Log($"ENV02: {placements.Count(p=>p.model<3)} trees, {placements.Count(p=>p.model>=3)} shrubs, {centers.Count} candidate compositions. Save scene after review.");
            }
            catch(Exception ex){Debug.LogError("ENV02: "+ex);}
        }

        [MenuItem("Air Combat/Art/Remove Mission01 Desert Vegetation")]
        private static void Remove()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var scene=SceneManager.GetActiveScene();if(scene.name!="Mission01_FirstSortie")return;
            var layer=Root(scene,LayerName);if(layer==null)return;
            Undo.DestroyObjectImmediate(layer);EditorSceneManager.MarkSceneDirty(scene);
        }
        private static Bounds PrefabBounds(GameObject prefab)
        {
            var filters=prefab.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length!=Parts.Length||filters.Any(f=>f.sharedMesh==null))throw new InvalidOperationException("Invalid plant prefab "+prefab.name);
            var rootInv=prefab.transform.worldToLocalMatrix;
            var b=TransformedBounds(filters[0].sharedMesh.bounds,rootInv*filters[0].transform.localToWorldMatrix);
            foreach(var f in filters)b.Encapsulate(TransformedBounds(f.sharedMesh.bounds,rootInv*f.transform.localToWorldMatrix));
            return b;
        }
        private static GameObject[] BuildLibrary()
        {
            var meshes=new Mesh[Names.Length,Parts.Length];
            for(int i=0;i<Names.Length;i++)for(int k=0;k<Parts.Length;k++)
            {
                string path=Library+Names[i]+"_"+Parts[k]+".obj";
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filters=model==null?Array.Empty<MeshFilter>():model.GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length!=1||filters[0].sharedMesh==null)throw new InvalidOperationException("Missing plant mesh "+path);
                meshes[i,k]=filters[0].sharedMesh;
            }
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit shader missing.");
            var materials=new Material[Parts.Length];
            for(int k=0;k<Parts.Length;k++)
            {
                string path=Library+"ENV02_"+Parts[k]+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(shader);material.SetColor("_BaseColor",Palette[k]);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(material,path);}
                materials[k]=material;
            }
            var result=new GameObject[Names.Length];var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                for(int i=0;i<Names.Length;i++)
                {
                    string path=Library+Names[i]+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(existing!=null){result[i]=existing;continue;}
                    var go=new GameObject(Names[i]);SceneManager.MoveGameObjectToScene(go,preview);
                    for(int k=0;k<Parts.Length;k++)
                    {
                        var child=new GameObject(Parts[k],typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(child,preview);child.transform.SetParent(go.transform,false);
                        child.GetComponent<MeshFilter>().sharedMesh=meshes[i,k];var r=child.GetComponent<MeshRenderer>();r.sharedMaterial=materials[k];r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                    }
                    result[i]=PrefabUtility.SaveAsPrefabAsset(go,path);if(result[i]==null)throw new InvalidOperationException("Cannot save "+path);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            AssetDatabase.SaveAssets();return result;
        }
    }
}
