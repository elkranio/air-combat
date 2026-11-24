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
    // Explicit scene authoring command, not a runtime road generator.
    internal static class Mission01UnifiedPavement
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string MeshPath = "Assets/_Project/Art/Environment/Mission01_FirstSortie/Mission01_AirfieldPavement.asset";
        private const string UndoName = "Unify Mission 01 pavement";
        private const float ShoulderWidth = 3f;
        private const int CornerSegments = 4;

        [MenuItem("Air Combat/Missions/Unify Mission 01 Airfield Pavement")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("MISSION-01C3: open Mission01_FirstSortie first.");
                return;
            }
            var anchor = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / Airfield");
            var layout = anchor != null ? anchor.transform.Cast<Transform>()
                .SingleOrDefault(t => t.name == "Airfield B - editable layout") : null;
            if (layout == null || layout.Cast<Transform>().Any(t => t.name == "Unified pavement"))
            {
                Debug.LogWarning("MISSION-01C3: expected an unmodified layout B; nothing changed.");
                return;
            }

            Transform asphalt, shoulder, apron, taxi, approach, edgeNorth, edgeSouth;
            Transform[] stands;
            Material dark, earth;
            try
            {
                var runway = Child(layout, "Runway and apron");
                var markings = Child(layout, "Apron connections and markings");
                asphalt = Child(runway, "02 / Asphalt runway 460x36");
                shoulder = Child(runway, "01 / Graded runway shoulders");
                apron = Child(runway, "03 / Apron - aircraft and hangar");
                taxi = Child(runway, "04 / Taxiway runway-to-apron");
                approach = Child(markings, "Hangar approach");
                edgeNorth = Child(runway, "Runway edge 1");
                edgeSouth = Child(markings, "Runway edge - south of apron access");
                stands = markings.Cast<Transform>().Where(t => t.name.StartsWith("Stand ", StringComparison.Ordinal)).ToArray();
                if (stands.Length != 9 || taxi.gameObject.activeSelf || !asphalt.gameObject.activeSelf ||
                    !apron.gameObject.activeSelf || !approach.gameObject.activeSelf || !shoulder.gameObject.activeSelf ||
                    !Close(runway.localPosition, 125f, 0f) || !Close(runway.localScale, 1f, 1f) ||
                    Quaternion.Angle(runway.localRotation, Quaternion.identity) > .01f ||
                    !Close(asphalt.localPosition, 0f, 0f) || !Close(asphalt.localScale, 24f, 240f) ||
                    !Close(apron.localPosition, 53f, -75f) || !Close(apron.localScale, 82f, 66f) ||
                    !Close(approach.localPosition, 231f, -75f) || !Close(approach.localScale, 24f, 26f) ||
                    !Close(markings.localPosition, 0f, 0f) || !Close(markings.localScale, 1f, 1f) ||
                    Quaternion.Angle(markings.localRotation, Quaternion.identity) > .01f)
                    throw new InvalidOperationException("Expected the saved 240 x 24 / 82 x 66 B layout.");
                dark = asphalt.GetComponent<MeshRenderer>().sharedMaterial;
                earth = shoulder.GetComponent<MeshRenderer>().sharedMaterial;
                if (dark == null || earth == null) throw new InvalidOperationException("Missing pavement materials.");
            }
            catch (Exception ex)
            {
                Debug.LogError("MISSION-01C3: " + ex.Message + " Nothing changed.");
                return;
            }

            Mesh paved = null, border = null;
            bool createdAsset = false;
            GameObject root = null;
            bool rootRegistered = false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                // Coordinates in the layout's X/Z plane. A single simple CCW outline:
                // runway, apron and the existing approach to the hangar's open portal.
                var outline = new[] {
                    new Vector2(113,-120), new Vector2(137,-120), new Vector2(137,-108),
                    new Vector2(219,-108), new Vector2(219,-88), new Vector2(243,-88),
                    new Vector2(243,-62), new Vector2(219,-62), new Vector2(219,-42),
                    new Vector2(137,-42), new Vector2(137,120), new Vector2(113,120)
                };
                // Small outside chamfers, wider inside fillets; preserve the narrow
                // strip between the southern threshold and apron and the parking paint.
                float[] radii = { 2, 2, 5, 2, 5, 1, 1, 5, 2, 6, 2, 2 };
                var existing = AssetDatabase.LoadMainAssetAtPath(MeshPath);
                if (existing == null)
                {
                    paved = MakeMesh("Airfield asphalt", Round(outline, radii));
                    border = MakeMesh("Airfield shoulder", Round(Offset(outline, ShoulderWidth), radii));
                    AssetDatabase.CreateAsset(paved, MeshPath);
                    createdAsset = true;
                    AssetDatabase.AddObjectToAsset(border, MeshPath);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    // Undo restores the scene but leaves its immutable generated mesh asset.
                    // Allow reapplying without rewriting that asset or breaking its GUID.
                    var meshes = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().ToArray();
                    paved = meshes.Single(m => m.name == "Airfield asphalt");
                    border = meshes.Single(m => m.name == "Airfield shoulder");
                }

                root = new GameObject("Unified pavement");
                root.transform.SetParent(layout, false);
                AddSurface(root.transform, "Continuous ground shoulder", border, earth, .028f);
                AddSurface(root.transform, "Continuous asphalt", paved, dark, .052f);
                Undo.RegisterCreatedObjectUndo(root, UndoName);
                rootRegistered = true;
                foreach (var old in new[] { asphalt, shoulder, apron, approach, edgeSouth })
                {
                    Undo.RecordObject(old.gameObject, UndoName);
                    old.gameObject.SetActive(false);
                    if (PrefabUtility.IsPartOfPrefabInstance(old))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
                }
                // Clear the seam paint across the entire apron mouth, including the
                // curved transitions. The remaining north edge ends at the old threshold.
                Undo.RecordObject(edgeNorth, UndoName);
                float farEnd = edgeNorth.localPosition.z + edgeNorth.localScale.z * .5f;
                const float openingEnd = -36f;
                Vector3 pos = edgeNorth.localPosition, scale = edgeNorth.localScale;
                pos.z = (farEnd + openingEnd) * .5f;
                scale.z = farEnd - openingEnd;
                edgeNorth.localPosition = pos;
                edgeNorth.localScale = scale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(edgeNorth);
                // Keep the U-shaped stands but reduce the stroke thickness only.
                foreach (var line in stands)
                {
                    Undo.RecordObject(line, UndoName);
                    Vector3 s = line.localScale;
                    if (line.name.EndsWith(" front", StringComparison.Ordinal)) s.z = .42f;
                    else s.x = .42f;
                    line.localScale = s;
                }
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = root;
                Debug.Log("MISSION-01C3: unified pavement ready. Review and save scene and mesh asset.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(group);
                if (!rootRegistered && root != null) UnityEngine.Object.DestroyImmediate(root);
                if (createdAsset) AssetDatabase.DeleteAsset(MeshPath);
                else if (paved != null && !EditorUtility.IsPersistent(paved)) UnityEngine.Object.DestroyImmediate(paved);
                if (border != null && !EditorUtility.IsPersistent(border)) UnityEngine.Object.DestroyImmediate(border);
                Debug.LogError("MISSION-01C3: change reverted. " + ex);
            }
        }

        private static Transform Child(Transform parent, string name) =>
            parent.Cast<Transform>().Single(t => t.name == name);

        private static bool Close(Vector3 value, float x, float z) =>
            Mathf.Abs(value.x - x) < .01f && Mathf.Abs(value.z - z) < .01f;

        private static void AddSurface(Transform parent, string name, Mesh mesh, Material material, float y)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2[] Offset(Vector2[] polygon, float distance)
        {
            // All source edges are axis-aligned and perpendicular. Their outward
            // offset lines intersect at p + (normalBefore + normalAfter) * distance.
            var result = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 p = polygon[i];
                Vector2 a = (p - polygon[(i + polygon.Length - 1) % polygon.Length]).normalized;
                Vector2 b = (polygon[(i + 1) % polygon.Length] - p).normalized;
                result[i] = p + distance * new Vector2(a.y + b.y, -a.x - b.x);
            }
            return result;
        }

        private static List<Vector2> Round(Vector2[] polygon, float[] radii)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 p = polygon[i];
                Vector2 a = p + (polygon[(i + polygon.Length - 1) % polygon.Length] - p).normalized * radii[i];
                Vector2 b = p + (polygon[(i + 1) % polygon.Length] - p).normalized * radii[i];
                for (int j = 0; j <= CornerSegments; j++)
                {
                    float t = (float)j / CornerSegments;
                    result.Add(Vector2.Lerp(Vector2.Lerp(a, p, t), Vector2.Lerp(p, b, t), t));
                }
            }
            return result;
        }

        private static Mesh MakeMesh(string name, List<Vector2> outline)
        {
            // Ear clipping of a simple concave CCW polygon. Reverse triangle
            // winding in X/Z so the visible side faces +Y in Unity.
            var remaining = Enumerable.Range(0, outline.Count).ToList();
            var triangles = new List<int>();
            while (remaining.Count > 3)
            {
                bool found = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % remaining.Count];
                    Vector2 p = outline[a], q = outline[b], r = outline[c];
                    if (Cross(q - p, r - p) <= .0001f) continue;
                    if (remaining.Any(k => k != a && k != b && k != c && Inside(outline[k], p, q, r))) continue;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }
                if (!found) throw new InvalidOperationException("Could not triangulate " + name);
            }
            if (Cross(outline[remaining[1]] - outline[remaining[0]], outline[remaining[2]] - outline[remaining[0]]) <= .0001f)
                throw new InvalidOperationException("Degenerate last face in " + name);
            triangles.Add(remaining[0]); triangles.Add(remaining[2]); triangles.Add(remaining[1]);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(outline.Select(p => new Vector3(p.x, 0f, p.y)).ToList());
            mesh.SetUVs(0, outline.Select(p => p * .05f).ToList());
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(b - a, p - a) >= -.0001f && Cross(c - b, p - b) >= -.0001f &&
            Cross(a - c, p - c) >= -.0001f;
    }
}
