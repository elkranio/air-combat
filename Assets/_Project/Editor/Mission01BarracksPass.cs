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
    internal static partial class Mission01UnifiedPavement
    {
        private const string BarracksPath = "Assets/_Project/Art/Environment/Mission01_FirstSortie/Mission01_BarracksPass.asset";
        private const string BarracksUndo = "Apply Mission 01 barracks pass";

        [MenuItem("Air Combat/Missions/Apply Mission 01 Barracks Pass")]
        private static void ApplyBarracksPass()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            var generated = new List<Mesh>();
            bool newAsset = false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(BarracksUndo);
            try
            {
                var anchor = scene.GetRootGameObjects().Single(g => g.name == "Mission01 / Airfield");
                var layout = Child(anchor.transform, "Airfield B - editable layout");
                var buildings = Child(layout, "Buildings");
                var hangar = Child(buildings, "Arch hangar");
                var pavement = Child(layout, "Unified pavement C6");
                var guide = Child(pavement, "Hangar guide - three dashes");
                var marking = Child(hangar, "Roof marking 05");
                var annex = Child(hangar, "Arch_SideAnnex");
                var annexCollider = Child(hangar, "Collision - walls only (front is open)")
                    .GetComponents<BoxCollider>().Single(c => Mathf.Abs(c.center.z) > 18f);
                if (!pavement.gameObject.activeSelf || !annex.gameObject.activeSelf ||
                    Mathf.Abs(hangar.localPosition.z + 75f) > .01f ||
                    buildings.Cast<Transform>().Any(t => t.name == "Pilot barracks"))
                    throw new InvalidOperationException("Expected saved C6 layout with annex; nothing changed.");

                Material walls = Child(hangar, "Arch_Walls").GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Material roof = Child(hangar, "Arch_Roof").GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Material dark = Child(hangar, "Arch_DarkDetails").GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Material path = Child(pavement, "Continuous ground shoulder").GetComponent<MeshRenderer>().sharedMaterial;
                if (walls == null || roof == null || dark == null || path == null)
                    throw new InvalidOperationException("Missing building materials.");

                var detail = Child(hangar, "Arch_DarkDetails").GetComponentInChildren<MeshFilter>();
                float annexSide = Mathf.Sign(hangar.InverseTransformPoint(annex.GetComponentInChildren<MeshRenderer>().bounds.center).z);
                // Remove any door/window triangles belonging to the annex from the
                // separate detail mesh as well. Preserve the shared source asset.
                Mesh trimmed = TrimAnnexDetails(hangar, detail, annexSide);
                if (trimmed != null) generated.Add(trimmed);
                Mesh number = ClearRoofNumber(hangar);
                generated.Add(number);
                Mesh wallMesh = BarracksWalls(); generated.Add(wallMesh);
                Mesh roofMesh = BarracksRoof(); generated.Add(roofMesh);
                Mesh detailMesh = BarracksDetails(); generated.Add(detailMesh);
                Mesh pathMesh = MakeMesh("C7 footpath", new List<Vector2> {
                    new Vector2(218.8f,-97.5f), new Vector2(244,-97.5f),
                    new Vector2(244,-94.5f), new Vector2(218.8f,-94.5f) });
                generated.Add(pathMesh);

                if (AssetDatabase.LoadMainAssetAtPath(BarracksPath) == null)
                {
                    AssetDatabase.CreateAsset(number, BarracksPath);
                    newAsset = true;
                    foreach (Mesh mesh in generated.Where(m => m != number)) AssetDatabase.AddObjectToAsset(mesh, BarracksPath);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    var saved = AssetDatabase.LoadAllAssetsAtPath(BarracksPath).OfType<Mesh>().ToDictionary(m => m.name);
                    // Reapply after Undo using the same persistent asset references.
                    number = saved[number.name]; wallMesh = saved[wallMesh.name]; roofMesh = saved[roofMesh.name];
                    detailMesh = saved[detailMesh.name]; pathMesh = saved[pathMesh.name];
                    if (trimmed != null) trimmed = saved[trimmed.name];
                    foreach (Mesh mesh in generated) UnityEngine.Object.DestroyImmediate(mesh);
                    generated.Clear();
                }

                Undo.RecordObjects(new UnityEngine.Object[] { hangar, guide, annex.gameObject,
                    annexCollider, marking.GetComponent<MeshFilter>(), detail }, BarracksUndo);
                hangar.localPosition += Vector3.forward * 12f;
                guide.localPosition += Vector3.forward * 12f;
                annex.gameObject.SetActive(false);
                annexCollider.enabled = false;
                marking.GetComponent<MeshFilter>().sharedMesh = number;
                if (trimmed != null) detail.sharedMesh = trimmed;
                foreach (UnityEngine.Object obj in new UnityEngine.Object[] { hangar, annex.gameObject, annexCollider, detail })
                    if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);

                var root = new GameObject("Pilot barracks");
                root.transform.SetParent(buildings, false);
                Undo.RegisterCreatedObjectUndo(root, BarracksUndo);
                for (int i = 0; i < 2; i++)
                {
                    var building = new GameObject("Barrack 0" + (i + 1));
                    building.transform.SetParent(root.transform, false);
                    building.transform.localPosition = new Vector3(232f, 0f, -102f + i * 12f);
                    building.transform.localRotation = Quaternion.Euler(0, i * 180f, 0);
                    BarracksPart(building.transform, "Walls", wallMesh, walls);
                    BarracksPart(building.transform, "Gabled roof", roofMesh, roof);
                    BarracksPart(building.transform, "Doors and windows", detailMesh, dark);
                }
                AddSurface(root.transform, "Shared footpath", pathMesh, path, .033f);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = root;
                Debug.Log("MISSION-01C7: offset hangar, clear roof number and two pilot barracks ready. Save scene and asset.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(group);
                if (newAsset) AssetDatabase.DeleteAsset(BarracksPath);
                foreach (Mesh mesh in generated)
                    if (mesh != null && !EditorUtility.IsPersistent(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                Debug.LogError("MISSION-01C7: change reverted. " + ex);
            }
        }

        private static Mesh ClearRoofNumber(Transform hangar)
        {
            Quaternion yaw = hangar.localRotation;
            var obstacles = new List<Rect>();
            foreach (string name in new[] { "Arch_RoofVents", "Arch_Ribs" })
            foreach (var f in Child(hangar, name).GetComponentsInChildren<MeshFilter>())
            {
                var v = f.sharedMesh.vertices.Select(p => yaw * hangar.InverseTransformPoint(f.transform.TransformPoint(p))).ToArray();
                var t = f.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i+1]], c = v[t[i+2]];
                    obstacles.Add(Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x)-.25f, Mathf.Min(a.z,b.z,c.z)-.25f,
                        Mathf.Max(a.x,b.x,c.x)+.25f, Mathf.Max(a.z,b.z,c.z)+.25f));
                }
            }
            // Search the camera-facing slope, preferring the original marking size
            // and its previous position. Try a slightly smaller print only if needed.
            var centers = (from x in Enumerable.Range(-22,45)
                           from z in Enumerable.Range(-27,16)
                           let p = new Vector2(x*.5f,z*.5f)
                           orderby (p-new Vector2(0,-10)).sqrMagnitude
                           select p).ToArray();
            foreach (float unit in new[] { .75f, .70f, .65f, .60f })
            foreach (Vector2 center in centers)
            {
                var rect = new Rect(center.x-5.5f*unit, center.y-3.5f*unit, 11f*unit, 7f*unit);
                if (rect.xMin < -13.5f || rect.xMax > 13.5f || rect.yMin < -15.3f || rect.yMax > -4f ||
                    obstacles.Any(r => r.Overlaps(rect))) continue;
                try
                {
                    Mesh mesh = MakeRoofNumber(hangar, yaw, unit, rect.xMin, rect.yMin);
                    mesh.name = "C7 roof 05";
                    return mesh;
                }
                catch (InvalidOperationException) { /* Not entirely on the actual roof. */ }
            }
            throw new InvalidOperationException("No unobstructed marking region found on roof.");
        }

        private static Mesh TrimAnnexDetails(Transform hangar, MeshFilter filter, float side)
        {
            var source = filter.sharedMesh;
            var v = source.vertices.Select(p => hangar.InverseTransformPoint(filter.transform.TransformPoint(p))).ToArray();
            float cut = side * 16.8f;
            if (!v.Any(p => side*p.z > 16.8f)) return null;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var indices = source.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var polygon = ClipRoof(new List<Vector3> { v[indices[i]],v[indices[i+1]],v[indices[i+2]] }, 2, cut, side < 0);
                for (int j=1; j+1<polygon.Count; j++)
                {
                    if (Vector3.Cross(polygon[j]-polygon[0],polygon[j+1]-polygon[0]).sqrMagnitude < 1e-8f) continue;
                    int n=vertices.Count;
                    foreach (Vector3 p in new[] {polygon[0],polygon[j],polygon[j+1]})
                        vertices.Add(filter.transform.InverseTransformPoint(hangar.TransformPoint(p)));
                    triangles.AddRange(new[] {n,n+1,n+2});
                }
            }
            return BarracksMesh("C7 hangar details without annex",vertices,triangles);
        }

        private static void BarracksPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static Mesh BarracksMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh {name=name};
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private sealed class BarracksGeometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            internal void Face(Vector3 inside, params Vector3[] pts)
            {
                if (Vector3.Dot(Vector3.Cross(pts[1]-pts[0],pts[2]-pts[0]),inside-pts[0]) > 0) Array.Reverse(pts);
                int n=vertices.Count; vertices.AddRange(pts);
                for (int i=1;i+1<pts.Length;i++) triangles.AddRange(new[] {n,n+i,n+i+1});
            }
            internal void Box(float x,float y,float z,float w,float h,float d)
            {
                Vector3[] p = new Vector3[8];
                for(int i=0;i<8;i++) p[i]=new Vector3(x+((i&1)==0?-w:w)*.5f,y+((i&2)==0?-h:h)*.5f,z+((i&4)==0?-d:d)*.5f);
                var c=new Vector3(x,y,z);
                foreach(int[] f in new[]{new[]{0,1,3,2},new[]{4,5,7,6},new[]{0,2,6,4},new[]{1,3,7,5},new[]{0,1,5,4},new[]{2,3,7,6}})
                    Face(c,f.Select(i=>p[i]).ToArray());
            }
            internal Mesh Finish(string name) => BarracksMesh(name,vertices,triangles);
        }
        private static Mesh BarracksWalls()
        {
            var g=new BarracksGeometry();
            g.Box(0,1.85f,0,20f,3.7f,7.6f);
            g.Box(0,.13f,0,20.3f,.26f,7.9f);
            return g.Finish("C7 barracks walls");
        }
        private static Mesh BarracksRoof()
        {
            var g=new BarracksGeometry(); var c=new Vector3(0,4f,0);
            var a=new[]{new Vector3(-10.5f,3.65f,-4.15f),new Vector3(-10.5f,4f,-4.15f),new Vector3(-10.5f,5.25f,0),new Vector3(-10.5f,4f,4.15f),new Vector3(-10.5f,3.65f,4.15f)};
            var b=a.Select(p=>new Vector3(10.5f,p.y,p.z)).ToArray();
            g.Face(c,a); g.Face(c,b);
            for(int i=0;i<a.Length;i++){int j=(i+1)%a.Length;g.Face(c,a[i],a[j],b[j],b[i]);}
            return g.Finish("C7 barracks roof");
        }
        private static Mesh BarracksDetails()
        {
            var g=new BarracksGeometry();
            foreach(float side in new[]{-1f,1f})
            foreach(float x in new[]{-6.5f,-2.2f,2.2f,6.5f})
            {
                if(side>0 && x==-6.5f) continue;
                g.Box(x,2.3f,side*3.83f,2.2f,.95f,.10f);
            }
            g.Box(-6.5f,1.2f,3.83f,1.45f,2.4f,.10f);
            return g.Finish("C7 barracks doors and windows");
        }
    }
}
