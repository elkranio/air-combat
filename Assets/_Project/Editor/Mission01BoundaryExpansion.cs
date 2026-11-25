using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // BOUND-03A: extend the operation without moving authored objectives, SAMs
    // or the airfield. The southern massif must still reach the new edge.
    internal static class Mission01BoundaryExpansion
    {
        private const string RootName = "Mission01 / Southern massif extension";
        private const string RockPath = "Assets/_Project/Prefabs/Environment/Cliffs/CL06_A_Bastion.prefab";
        private static readonly Rect Expanded = Rect.MinMaxRect(-1100f, -1400f, 2200f, 2550f);
        private static readonly float[] NorthRow = { 250f, 400f, 550f, 700f, 850f, 1000f };
        private static readonly float[] SouthRow = { 400f, 550f, 700f, 850f };

        [MenuItem("Air Combat/Missions/Apply Mission01 Boundary Expansion v0")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int undo = -1;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.name != "Mission01_FirstSortie")
                    throw new InvalidOperationException("Open Mission01_FirstSortie in Edit Mode.");
                var roots = scene.GetRootGameObjects();
                var launch = roots.SelectMany(g => g.GetComponentsInChildren<Mission01LaunchSequence>(true)).Single();
                var ground = roots.Single(g => g.name == "Mission01 / Faceted surface").GetComponent<Renderer>();
                var surround = roots.Single(g => g.name == "Mission01 / Boundary surround (visual only)").GetComponent<Renderer>();
                if (ground == null || surround == null || !surround.enabled || !surround.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Apply Boundary Surround v0 first.");
                Bounds b = surround.bounds;
                const float reserve = 590f;
                if (b.min.x > Expanded.xMin - reserve || b.max.x < Expanded.xMax + reserve ||
                    b.min.z > Expanded.yMin - reserve || b.max.z < Expanded.yMax + reserve)
                    throw new InvalidOperationException("Re-apply Boundary Surround v0 with the enlarged padding first.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockPath);
                if (prefab == null || prefab.GetComponent<RadarTerrainObstacle>() == null)
                    throw new InvalidOperationException("Accepted CL06 Bastion prefab missing.");
                var serialized = new SerializedObject(launch);
                var bounds = serialized.FindProperty("operationBounds");
                var altitude = serialized.FindProperty("cruiseAltitude");
                if (bounds == null || altitude == null)
                    throw new InvalidOperationException("Mission boundary or altitude property missing.");
                var existing = roots.SingleOrDefault(g => g.name == RootName);
                if (existing != null && (existing.GetComponents<Component>().Length != 1 ||
                    existing.transform.Cast<Transform>().Any(t => t.GetComponent<RadarTerrainObstacle>() == null)))
                    throw new InvalidOperationException("Unexpected contents in extension root; refusing to replace it.");

                Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Mission01 boundary expansion");
                if (existing != null) Undo.DestroyObjectImmediate(existing);
                var extension = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(extension, scene);
                Undo.RegisterCreatedObjectUndo(extension, "Create southern extension");
                AddRow(prefab, extension.transform, NorthRow, -1220f, ground.bounds.center.y, 0);
                AddRow(prefab, extension.transform, SouthRow, -1370f, ground.bounds.center.y, 1);

                // Validate the actual solid section, not renderer AABBs. A
                // continuous line of solid terrain at cruise altitude joins the
                // original massif to the new southern boundary, so there is no
                // through-route between them even with zero aircraft radius.
                if (!TerrainSectionUnion.TryBakeForAuthoring(scene, altitude.floatValue,
                    out _, out var fill, out _) || !ContinuousSection(fill, 550f, -1405f, -980f))
                    throw new InvalidOperationException("Southern massif is not continuously closed at cruise altitude; changes reverted.");

                Undo.RecordObject(launch, "Expand operation bounds");
                bounds.rectValue = Expanded;
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(launch);
                EditorUtility.SetDirty(launch);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo);
                Selection.activeGameObject = extension;
                Debug.Log("BOUND-03A: operation X -1100..2200, Z -1400..2550. Southern solid section validated. Save scene, then bake Mission Map.");
            }
            catch (Exception ex)
            {
                if (undo >= 0) Undo.RevertAllDownToGroup(undo);
                Debug.LogError("BOUND-03A: " + ex);
            }
        }

        private static void AddRow(GameObject prefab, Transform parent, float[] xs, float z, float y, int row)
        {
            for (int i = 0; i < xs.Length; i++)
            {
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                Undo.RegisterCreatedObjectUndo(rock, "Add massif rock");
                rock.name = $"South extension {row + 1}-{i + 1}";
                rock.transform.SetPositionAndRotation(new Vector3(xs[i], y, z), Quaternion.identity);
                rock.transform.localScale = new Vector3(1.45f, 1.1f, 1.8f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rock.transform);
            }
        }

        private static bool ContinuousSection(Vector2[] fill, float x, float start, float end)
        {
            var spans = new List<Vector2>();
            for (int i = 0; i + 2 < fill.Length; i += 3)
            {
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                for (int j = 0; j < 3; j++)
                {
                    Vector2 a = fill[i + j], b = fill[i + (j + 1) % 3];
                    if (x < Mathf.Min(a.x, b.x) || x > Mathf.Max(a.x, b.x)) continue;
                    if (Mathf.Abs(a.x - b.x) < 1e-6f)
                    {
                        lo = Mathf.Min(lo, Mathf.Min(a.y, b.y));
                        hi = Mathf.Max(hi, Mathf.Max(a.y, b.y));
                    }
                    else
                    {
                        float z = Mathf.LerpUnclamped(a.y, b.y, (x - a.x) / (b.x - a.x));
                        lo = Mathf.Min(lo, z); hi = Mathf.Max(hi, z);
                    }
                }
                if (hi >= lo) spans.Add(new Vector2(lo, hi));
            }
            spans.Sort((a,b) => a.x.CompareTo(b.x));
            float reached = start;
            foreach (var span in spans)
            {
                if (span.y < reached) continue;
                if (span.x > reached + .01f) return false;
                reached = Mathf.Max(reached, span.y);
                if (reached >= end) return true;
            }
            return false;
        }
    }
}
