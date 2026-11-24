using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static partial class Mission01UnifiedPavement
    {
        private const string HangarMeshPath = "Assets/_Project/Art/Environment/Mission01_FirstSortie/Mission01_HangarPass.asset";
        private const string HangarUndo = "Apply Mission 01 hangar pass";

        [MenuItem("Air Combat/Missions/Apply Mission 01 Hangar Pass")]
        private static void ApplyHangarPass()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("MISSION-01C6: open Mission01_FirstSortie first.");
                return;
            }

            Transform layout, oldPavement, hangar;
            Material asphalt, shoulder, paint;
            Quaternion yaw;
            Vector3 destination;
            Mesh roofNumber = null;
            try
            {
                var anchor = scene.GetRootGameObjects().Single(g => g.name == "Mission01 / Airfield");
                layout = Child(anchor.transform, "Airfield B - editable layout");
                oldPavement = Child(layout, "Unified pavement");
                hangar = Child(Child(layout, "Buildings"), "Arch hangar");
                if (!oldPavement.gameObject.activeSelf || !Close(hangar.localPosition, 255f, -75f) ||
                    Quaternion.Angle(hangar.localRotation, Quaternion.identity) > .01f ||
                    layout.Cast<Transform>().Any(t => t.name == "Unified pavement C6"))
                    throw new InvalidOperationException("Expected saved C3-C5 pavement and original hangar transform.");

                asphalt = Child(oldPavement, "Continuous asphalt").GetComponent<MeshRenderer>().sharedMaterial;
                shoulder = Child(oldPavement, "Continuous ground shoulder").GetComponent<MeshRenderer>().sharedMaterial;
                paint = Child(Child(layout, "Apron connections and markings"), "Stand 2 left").GetComponent<MeshRenderer>().sharedMaterial;
                if (asphalt == null || shoulder == null || paint == null)
                    throw new InvalidOperationException("Missing materials.");

                // Use the actual imported visual portal, not the old collider orientation.
                var portal = Child(hangar, "Arch_FrontPortal").GetComponentInChildren<MeshRenderer>();
                if (portal == null) throw new InvalidOperationException("Missing hangar portal mesh.");
                float portalX = hangar.InverseTransformPoint(portal.bounds.center).x;
                if (Mathf.Abs(portalX) < 10f || Mathf.Abs(portalX) > 19f)
                    throw new InvalidOperationException("Unexpected portal center: " + portalX);
                yaw = Quaternion.Euler(0f, portalX > 0f ? 180f : 0f, 0f);
                // The portal faces the apron (-X); put it just inside the pavement edge.
                destination = new Vector3(218.6f + Mathf.Abs(portalX), hangar.localPosition.y, -75f);
                roofNumber = MakeRoofNumber(hangar, yaw);
            }
            catch (Exception ex)
            {
                if (roofNumber != null) UnityEngine.Object.DestroyImmediate(roofNumber);
                Debug.LogError("MISSION-01C6: " + ex.Message + " Nothing changed.");
                return;
            }

            Mesh paved = null, border = null, guide = null;
            bool createdAsset = false;
            var created = new List<GameObject>();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(HangarUndo);
            try
            {
                // Same 240 x 24 runway and 82 x 66 apron as C3. Remove only the
                // 24-unit neck; keep a broad, unobstructed lane in front of the portal.
                var outline = new[] {
                    new Vector2(113,-120), new Vector2(137,-120), new Vector2(137,-108),
                    new Vector2(219,-108), new Vector2(219,-42), new Vector2(137,-42),
                    new Vector2(137,120), new Vector2(113,120)
                };
                float[] radii = { 2, 2, 5, 2, 2, 6, 2, 2 };
                if (AssetDatabase.LoadMainAssetAtPath(HangarMeshPath) == null)
                {
                    paved = MakeMesh("C6 asphalt", Round(outline, radii));
                    border = MakeMesh("C6 shoulder", Round(Offset(outline, ShoulderWidth), radii));
                    guide = MakeHangarGuide();
                    AssetDatabase.CreateAsset(paved, HangarMeshPath);
                    createdAsset = true;
                    AssetDatabase.AddObjectToAsset(border, HangarMeshPath);
                    AssetDatabase.AddObjectToAsset(guide, HangarMeshPath);
                    AssetDatabase.AddObjectToAsset(roofNumber, HangarMeshPath);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    var meshes = AssetDatabase.LoadAllAssetsAtPath(HangarMeshPath).OfType<Mesh>().ToArray();
                    paved = meshes.Single(m => m.name == "C6 asphalt");
                    border = meshes.Single(m => m.name == "C6 shoulder");
                    guide = meshes.Single(m => m.name == "C6 hangar guide");
                    var savedNumber = meshes.Single(m => m.name == "C6 roof 05");
                    UnityEngine.Object.DestroyImmediate(roofNumber);
                    roofNumber = savedNumber;
                }

                var root = new GameObject("Unified pavement C6");
                created.Add(root);
                root.transform.SetParent(layout, false);
                AddSurface(root.transform, "Continuous ground shoulder", border, shoulder, .028f);
                AddSurface(root.transform, "Continuous asphalt", paved, asphalt, .052f);
                AddSurface(root.transform, "Hangar guide - three dashes", guide, paint, .078f);
                Undo.RegisterCreatedObjectUndo(root, HangarUndo);

                Undo.RecordObject(oldPavement.gameObject, HangarUndo);
                oldPavement.gameObject.SetActive(false);
                Undo.RecordObject(hangar, HangarUndo);
                hangar.localPosition = destination;
                hangar.localRotation = yaw;
                PrefabUtility.RecordPrefabInstancePropertyModifications(hangar);

                // Keep the marking attached to the hangar instance so moving it in
                // the editor also moves the number. No shared prefab is modified.
                var marking = new GameObject("Roof marking 05", typeof(MeshFilter), typeof(MeshRenderer));
                created.Add(marking);
                marking.transform.SetParent(hangar, false);
                marking.GetComponent<MeshFilter>().sharedMesh = roofNumber;
                var renderer = marking.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = paint;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                Undo.RegisterCreatedObjectUndo(marking, HangarUndo);

                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = hangar.gameObject;
                Debug.Log("MISSION-01C6: hangar faces the apron, roof number and compact pavement ready. Save scene and mesh asset. Portal local X was " + PortalOrientationForLog(yaw));
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(group);
                foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                if (createdAsset) AssetDatabase.DeleteAsset(HangarMeshPath);
                foreach (var mesh in new[] { paved, border, guide, roofNumber })
                    if (mesh != null && !EditorUtility.IsPersistent(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                Debug.LogError("MISSION-01C6: change reverted. " + ex);
            }
        }

        private static string PortalOrientationForLog(Quaternion yaw) => Quaternion.Angle(yaw, Quaternion.identity) > 90f ? "+X (turned 180 degrees)" : "-X (already facing apron)";

        private static Mesh MakeHangarGuide()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // A short guide in the clear center lane, not in the parking bays.
            for (int i = 0; i < 3; i++)
            {
                float x = 205f + i * 4f;
                int n = vertices.Count;
                vertices.AddRange(new[] { new Vector3(x,0,-75.24f), new Vector3(x,0,-74.76f),
                    new Vector3(x+2.1f,0,-74.76f), new Vector3(x+2.1f,0,-75.24f) });
                triangles.AddRange(new[] { n,n+1,n+2,n,n+2,n+3 });
            }
            var mesh = new Mesh { name = "C6 hangar guide" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh MakeRoofNumber(Transform hangar, Quaternion yaw)
        {
            var filter = Child(hangar, "Arch_Roof").GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException("Missing imported roof mesh.");
            var source = filter.sharedMesh;
            // Imported mesh data is accessible to editor tools outside play mode.
            var roof = source.vertices.Select(v => yaw * hangar.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
            var indices = source.triangles;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // Heavy squared, continuous strokes (no stencil gaps). 8.25 x 5.25
            // footprint on the camera-facing slope; between the structural ribs.
            // Coordinates are in layout orientation, relative to the hangar center.
            const float unit = .75f, x0 = -4.125f, z0 = -12.625f;
            var glyphs = new[] {
                new Rect(0,0,1.35f,7), new Rect(3.65f,0,1.35f,7),
                new Rect(1.35f,0,2.3f,1.35f), new Rect(1.35f,5.65f,2.3f,1.35f),
                new Rect(6,5.65f,5,1.35f), new Rect(6,4.175f,1.35f,1.475f),
                new Rect(6,2.825f,5,1.35f), new Rect(9.65f,1.35f,1.35f,1.475f), new Rect(6,0,5,1.35f)
            };
            // The lower right stroke connects the middle and bottom bars of 5.
            float expected = 0f, covered = 0f;
            Quaternion inverse = Quaternion.Inverse(yaw);
            foreach (var glyph in glyphs)
            {
                float minX = x0 + glyph.xMin * unit, maxX = x0 + glyph.xMax * unit;
                float minZ = z0 + glyph.yMin * unit, maxZ = z0 + glyph.yMax * unit;
                expected += (maxX-minX)*(maxZ-minZ);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = roof[indices[i]], b = roof[indices[i+1]], c = roof[indices[i+2]];
                    if (Vector3.Cross(b-a,c-a).y <= .0001f) continue;
                    var poly = new List<Vector3> { a,b,c };
                    poly = ClipRoof(poly, 0, minX, true);
                    poly = ClipRoof(poly, 0, maxX, false);
                    poly = ClipRoof(poly, 2, minZ, true);
                    poly = ClipRoof(poly, 2, maxZ, false);
                    for (int j = 1; j+1 < poly.Count; j++)
                    {
                        float area = Vector3.Cross(poly[j]-poly[0],poly[j+1]-poly[0]).y * .5f;
                        if (area <= .00001f) continue;
                        covered += area;
                        int n = vertices.Count;
                        foreach (Vector3 p in new[] { poly[0],poly[j],poly[j+1] })
                            vertices.Add(inverse * (p + Vector3.up * .045f));
                        triangles.AddRange(new[] { n,n+1,n+2 });
                    }
                }
            }
            if (covered < expected*.98f || covered > expected*1.02f)
                throw new InvalidOperationException("Roof marking does not fit the imported roof surface (coverage " + covered/expected + ").");
            var mesh = new Mesh { name = "C6 roof 05" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<Vector3> ClipRoof(List<Vector3> input, int axis, float boundary, bool greater)
        {
            var result = new List<Vector3>();
            if (input.Count == 0) return result;
            Vector3 a = input[input.Count-1];
            float da = (a[axis]-boundary)*(greater ? 1f : -1f);
            foreach (Vector3 b in input)
            {
                float db = (b[axis]-boundary)*(greater ? 1f : -1f);
                if ((da >= 0f) != (db >= 0f)) result.Add(Vector3.LerpUnclamped(a,b,da/(da-db)));
                if (db >= 0f) result.Add(b);
                a = b;
                da = db;
            }
            return result;
        }
    }
}
