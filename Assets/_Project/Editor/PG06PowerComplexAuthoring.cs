using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // One-time site composition. Never rebuild the mission or change the shared props.
    internal static class PG06PowerComplexAuthoring
    {
        private const string Root = "Assets/_Project/";
        private const string ScenePath = Root + "Scenes/Mission03_SparrowInterception.unity";
        private const string ArtParent = Root + "Art/Environment/Infrastructure";
        private const string Art = ArtParent + "/PG06_PowerComplex";
        private const string YardName = "M03 / PG06 Power Complex Yard";
        private const string LineName = "M03 / PG05 Power Line - VISUAL TRIAL";
        private static readonly Vector3 Origin = new Vector3(-182f, 0f, -780f);
        private const float Surface = .025f;
        private const float Ground = -.04f;
        private const float Road = .065f;
        private const float SpanLength = 58f;
        private static readonly string[] Names = {
            "M03 / PG01 Transformer - VISUAL TRIAL",
            "M03 / PG02 Control Building - VISUAL TRIAL",
            "M03 / PG03 Transformer Long - VISUAL TRIAL",
            "M03 / PG04 Portal A - VISUAL TRIAL",
            "M03 / PG04 Portal B - VISUAL TRIAL"
        };
        private static readonly string[] Assets = {
            "PG01_Transformer", "PG02_ControlBuilding", "PG03_TransformerLong",
            "PG04_Switchyard", "PG04_Switchyard"
        };
        private static readonly Vector3[] Poses = {
            new Vector3(-18f, Surface-.27f, 0f),
            new Vector3(-11f, Surface-.2912f, -35f),
            new Vector3(18f, Surface-.27f, 2f),
            new Vector3(-20f, Surface, 30f),
            new Vector3(16f, Surface, 33f)
        };
        private static readonly string[] PlateNames = {
            "PG01_Foundation_Concrete", "PG02_Foundation_Concrete", "PG03_Foundation_Concrete"
        };
        // Irregular clipped perimeter, not a separate concrete display base for each prop.
        private static readonly Vector2[] Outline = {
            new Vector2(-42,-44), new Vector2(-33,-54), new Vector2(31,-54),
            new Vector2(41,-42), new Vector2(41,36), new Vector2(31,44),
            new Vector2(-32,44), new Vector2(-42,34)
        };

        [MenuItem("Air Combat/Missions/M03 - Compose PG06 Power Complex")]
        private static void Compose()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError("PG06: open Mission03 first."); return; }
            var roots = scene.GetRootGameObjects();
            if (roots.Any(g => g.name == YardName))
            {
                Debug.Log("PG06 already composed. Existing site left unchanged; edit its layout manually.");
                return;
            }
            int undo = -1;
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("Missing URP Lit shader.");
                var prefabs = Assets.Select(LoadPrefab).ToArray();
                var pylon = LoadPrefab("PG05_Pylon"); var span = LoadPrefab("PG05_LineSpan");
                var props = new GameObject[Names.Length];
                for (int i=0; i<Names.Length; i++)
                {
                    props[i] = Existing(roots, Names[i]);
                    if (props[i] != null) RequireInstance(props[i], prefabs[i]);
                    if (i<3) FindPlate(props[i] != null ? props[i] : prefabs[i], PlateNames[i]);
                }
                // Do not silently duplicate a renamed or reparented preview.
                var known = props.Where(g=>g!=null).ToArray();
                foreach (var t in roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (prefabs.Contains(source) && !known.Contains(t.gameObject))
                        throw new InvalidOperationException("Unexpected existing PG01-PG04 instance: " + t.name);
                }
                var line = Existing(roots, LineName);
                if (line != null) ValidateLine(line, pylon, span);
                foreach (var t in roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if ((source == pylon || source == span) && (line == null || t.parent != line.transform))
                        throw new InvalidOperationException("Unexpected existing PG05 instance: " + t.name);
                }
                // Prepare deterministic mesh assets before touching the scene.
                if (!AssetDatabase.IsValidFolder(ArtParent)) throw new InvalidOperationException("Build PG01-PG05 assets first.");
                if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder(ArtParent,"PG06_PowerComplex");
                var gravel = new MeshBatch(); gravel.Polygon(Outline, Surface);
                var drive = new MeshBatch();
                drive.Rect(-34,-20,32,-14,Road); // east-west internal service lane
                drive.Rect(6,-54,14,-20,Road);   // access lane, edge meets first lane
                drive.Polygon(new[]{new Vector2(6,-54),new Vector2(14,-54),new Vector2(25,-85),new Vector2(17,-85)},Road);
                drive.Rect(2.8f,-37,6,-32,Road); // short path to control block
                var concrete = new MeshBatch();
                // PG02's broad plate and entrance step share one mesh batch. Recreate only the step.
                concrete.Box(Poses[1]+new Vector3(1.9f*1.22f,.38f*1.12f,6.42f*1.22f),
                    new Vector3(4.6f*1.22f,.24f*1.12f,2f*1.22f),Quaternion.identity);
                var fence = new MeshBatch();
                for (int i=0;i<Outline.Length;i++)
                {
                    var a=Outline[i]; var b=Outline[(i+1)%Outline.Length];
                    if (i==1) // leave an open access gate on the southern edge
                    {
                        FenceSegment(fence,a,new Vector2(5.65f,-54));
                        FenceSegment(fence,new Vector2(14.35f,-54),b);
                    }
                    else FenceSegment(fence,a,b);
                }
                // A short pair of decorative links visually meets the existing pylon line.
                var cables = new MeshBatch();
                cables.Cable(Poses[3]+new Vector3(5.7f,6.39f,2.5f),new Vector3(-5.65f,Ground+13.04f,60f),1.8f);
                cables.Cable(Poses[4]+new Vector3(-5.7f,6.39f,2.5f),new Vector3(5.65f,Ground+13.04f,60f),1.8f);
                var batches = new[]{gravel,drive,concrete,fence,cables};
                string[] labels = {"GravelYard","ServiceDrive","EntranceStep","PerimeterFence","DecorativeLinks"};
                var colors = new[]{new Color32(138,139,117,255),new Color32(101,109,102,255),
                    new Color32(137,140,130,255),new Color32(80,98,99,255),new Color32(74,85,86,255)};
                var meshes = new Mesh[batches.Length]; var mats = new Material[batches.Length];
                for (int i=0;i<batches.Length;i++)
                {
                    meshes[i]=SaveMesh(labels[i],batches[i]); mats[i]=SaveMaterial(labels[i],colors[i],shader);
                }
                AssetDatabase.SaveAssets();
                Undo.IncrementCurrentGroup(); undo=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Compose PG06 power complex");
                for(int i=0;i<Names.Length;i++)
                {
                    if (props[i]==null) props[i]=Place(prefabs[i],scene,Names[i]);
                    SetPose(props[i],Origin+Poses[i],Quaternion.identity);
                    if(i<3)
                    {
                        var plate=FindPlate(props[i],PlateNames[i]);
                        Undo.RecordObject(plate,"Hide individual display slab"); plate.enabled=false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(plate);
                    }
                }
                if (line==null)
                {
                    line=new GameObject(LineName); Undo.RegisterCreatedObjectUndo(line,"PG05 line");
                    for(int i=0;i<3;i++) PlaceLine(pylon,scene,line.transform,"PG05 / Pylon "+(i+1),i*SpanLength);
                    for(int i=0;i<2;i++) PlaceLine(span,scene,line.transform,"PG05 / Span "+(i+1),i*SpanLength);
                }
                SetPose(line,Origin+new Vector3(0,Ground,60),Quaternion.identity);
                // Preserve the accepted line geometry and its intended span length.
                for (int i=0;i<3;i++) SetLocalPose(line.transform.Find("PG05 / Pylon "+(i+1)),i*SpanLength);
                for (int i=0;i<2;i++) SetLocalPose(line.transform.Find("PG05 / Span "+(i+1)),i*SpanLength);
                var yard=new GameObject(YardName); Undo.RegisterCreatedObjectUndo(yard,"PG06 yard"); yard.transform.position=Origin;
                for(int i=0;i<labels.Length;i++)
                {
                    var go=new GameObject(labels[i],typeof(MeshFilter),typeof(MeshRenderer));
                    Undo.RegisterCreatedObjectUndo(go,"PG06 "+labels[i]);
                    go.transform.SetParent(yard.transform,false);
                    go.GetComponent<MeshFilter>().sharedMesh=meshes[i];
                    var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=mats[i];
                    if(i<2 || i==4) renderer.shadowCastingMode=ShadowCastingMode.Off;
                }
                Undo.CollapseUndoOperations(undo); undo=-1;
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject=yard; SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("PG06: shared yard composed; 3 display slabs hidden; no gameplay or runway changes. Save M03. Existing scene objects are retained.",yard);
            }
            catch(Exception ex)
            {
                if(undo>=0) Undo.RevertAllDownToGroup(undo);
                Debug.LogError("PG06 composition failed. Scene changes rolled back; generated assets may remain. "+ex);
            }
        }

        private static GameObject LoadPrefab(string name)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Environment/"+name+".prefab");
            if(prefab==null) throw new InvalidOperationException("Missing prefab: "+name+". Build its preview first.");
            return prefab;
        }
        private static GameObject Existing(GameObject[] roots,string name)
        {
            var matches=roots.Where(g=>g.name==name).ToArray();
            if(matches.Length>1) throw new InvalidOperationException("Duplicate root: "+name);
            return matches.SingleOrDefault();
        }
        private static void RequireInstance(GameObject instance,GameObject prefab)
        {
            if(PrefabUtility.GetCorrespondingObjectFromSource(instance)!=prefab || instance.transform.localScale!=Vector3.one)
                throw new InvalidOperationException("Unexpected preview or scale: "+instance.name);
        }
        private static MeshRenderer FindPlate(GameObject obj,string meshName)
        {
            var matches=obj.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null && f.sharedMesh.name==meshName).ToArray();
            if(matches.Length!=1 || matches[0].GetComponent<MeshRenderer>()==null)
                throw new InvalidOperationException("Expected one display slab mesh: "+meshName);
            return matches[0].GetComponent<MeshRenderer>();
        }
        private static void ValidateLine(GameObject line,GameObject pylon,GameObject span)
        {
            if(line.transform.childCount!=5 || line.transform.localScale!=Vector3.one)
                throw new InvalidOperationException("Unexpected PG05 group.");
            for(int i=0;i<3;i++) ValidateChild(line,"PG05 / Pylon "+(i+1),pylon);
            for(int i=0;i<2;i++) ValidateChild(line,"PG05 / Span "+(i+1),span);
        }
        private static void ValidateChild(GameObject parent,string name,GameObject prefab)
        {
            var child=parent.transform.Find(name);
            if(child==null) throw new InvalidOperationException("Missing PG05 child: "+name);
            RequireInstance(child.gameObject,prefab);
        }
        private static GameObject Place(GameObject prefab,Scene scene,string name)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            if(obj==null) throw new InvalidOperationException("Could not place "+name);
            Undo.RegisterCreatedObjectUndo(obj,"Place "+name); obj.name=name;
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj); return obj;
        }
        private static void PlaceLine(GameObject prefab,Scene scene,Transform parent,string name,float z)
        {
            var obj=Place(prefab,scene,name); Undo.SetTransformParent(obj.transform,parent,"Group PG05"); SetLocalPose(obj.transform,z);
        }
        private static void SetPose(GameObject obj,Vector3 position,Quaternion rotation)
        {
            Undo.RecordObject(obj.transform,"Position "+obj.name); obj.transform.SetPositionAndRotation(position,rotation);
            if(PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
        }
        private static void SetLocalPose(Transform t,float z)
        {
            Undo.RecordObject(t,"Position PG05"); t.localPosition=new Vector3(0,0,z); t.localRotation=Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        private static void FenceSegment(MeshBatch batch,Vector2 a,Vector2 b)
        {
            float length=Vector2.Distance(a,b); int count=Mathf.CeilToInt(length/5.5f);
            var dir=new Vector3(b.x-a.x,0,b.y-a.y).normalized;
            var rot=Quaternion.LookRotation(dir,Vector3.up);
            // Each segment owns its first post; corners do not get duplicate posts.
            for(int i=0;i<count;i++)
            {
                var p=Vector2.Lerp(a,b,i/(float)count);
                batch.Box(new Vector3(p.x,Surface+1.05f,p.y),new Vector3(.50f,2.1f,.50f),rot);
            }
            var mid=(a+b)*.5f;
            foreach(float y in new[]{.72f,1.66f})
                batch.Box(new Vector3(mid.x,Surface+y,mid.y),new Vector3(.30f,.26f,length-.12f),rot);
            // Last post at the left gate edge; other endpoints are owned by the next edge.
            if(Mathf.Abs(b.x-5.65f)<.001f && Mathf.Abs(b.y+54f)<.001f)
                batch.Box(new Vector3(b.x,Surface+1.05f,b.y),new Vector3(.50f,2.1f,.50f),rot);
        }
        private static Mesh SaveMesh(string label,MeshBatch batch)
        {
            string path=Art+"/PG06_"+label+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path); bool create=mesh==null;
            if(create) mesh=new Mesh{name="PG06_"+label}; else mesh.Clear();
            mesh.SetVertices(batch.V); mesh.SetTriangles(batch.T,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if(create) AssetDatabase.CreateAsset(mesh,path); else EditorUtility.SetDirty(mesh); return mesh;
        }
        private static Material SaveMaterial(string label,Color32 color,Shader shader)
        {
            string path=Art+"/PG06_"+label+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader){name="PG06_"+label};AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.03f);
            EditorUtility.SetDirty(mat);return mat;
        }
        private sealed class MeshBatch
        {
            internal readonly List<Vector3> V=new List<Vector3>();
            internal readonly List<int> T=new List<int>();
            private void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0){var q=b;b=c;c=q;}
                int i=V.Count;V.Add(a);V.Add(b);V.Add(c);T.Add(i);T.Add(i+1);T.Add(i+2);
            }
            internal void Polygon(Vector2[] p,float y)
            {
                // All authored surface polygons are convex. No overlapping faces within a layer.
                var a=new Vector3(p[0].x,y,p[0].y);
                for(int i=1;i<p.Length-1;i++) Tri(a,new Vector3(p[i].x,y,p[i].y),new Vector3(p[i+1].x,y,p[i+1].y),Vector3.up);
            }
            internal void Rect(float x0,float z0,float x1,float z1,float y) => Polygon(new[]{new Vector2(x0,z0),new Vector2(x1,z0),new Vector2(x1,z1),new Vector2(x0,z1)},y);
            internal void Box(Vector3 c,Vector3 size,Quaternion rotation)
            {
                var p=new Vector3[8];
                for(int i=0;i<8;i++)p[i]=c+rotation*Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                int[,] faces={{0,1,3,2},{4,5,7,6},{0,2,6,4},{1,3,7,5},{0,1,5,4},{2,3,7,6}};
                for(int i=0;i<6;i++)
                {
                    var a=p[faces[i,0]];var b=p[faces[i,1]];var d=p[faces[i,2]];var e=p[faces[i,3]];
                    var n=(a+b+d+e)*.25f-c;Tri(a,b,d,n);Tri(a,d,e,n);
                }
            }
            internal void Cable(Vector3 a,Vector3 b,float sag)
            {
                const int segments=12,sides=8;const float radius=.19f;
                var rings=new Vector3[segments+1,sides];var centers=new Vector3[segments+1];
                for(int i=0;i<=segments;i++)
                {
                    float t=i/(float)segments;centers[i]=Vector3.Lerp(a,b,t)-Vector3.up*(4*sag*t*(1-t));
                    var tangent=(b-a-Vector3.up*(4*sag*(1-2*t))).normalized;
                    var right=Vector3.Cross(Vector3.up,tangent).normalized;var up=Vector3.Cross(tangent,right);
                    for(int j=0;j<sides;j++){float angle=j*2*Mathf.PI/sides;rings[i,j]=centers[i]+radius*(right*Mathf.Cos(angle)+up*Mathf.Sin(angle));}
                }
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
                {
                    int k=(j+1)%sides;var p=rings[i,j];var q=rings[i,k];var r=rings[i+1,k];var s=rings[i+1,j];
                    var normal=(p+q+r+s)*.25f-(centers[i]+centers[i+1])*.5f;Tri(p,q,r,normal);Tri(p,r,s,normal);
                }
                for(int j=1;j<sides-1;j++)
                {Tri(rings[0,0],rings[0,j],rings[0,j+1],a-b);Tri(rings[segments,0],rings[segments,j],rings[segments,j+1],b-a);}
            }
        }
    }
}
