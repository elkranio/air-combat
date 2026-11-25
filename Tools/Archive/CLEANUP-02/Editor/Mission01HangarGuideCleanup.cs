using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class Mission01HangarGuideCleanup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string MeshPath = "Assets/_Project/Art/Environment/Mission01_FirstSortie/Mission01_HangarGuideTwoDashes.asset";
        private const string UndoName = "Simplify Mission 01 hangar guide";

        [MenuItem("Air Combat/Missions/Simplify Mission 01 Hangar Guide")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            var anchor = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / Airfield");
            var layout = anchor != null ? anchor.transform.Find("Airfield B - editable layout") : null;
            var pavement = layout != null ? layout.Find("Unified pavement C6") : null;
            var guide = pavement != null ? pavement.Find("Hangar guide - three dashes") : null;
            if (guide == null && pavement != null) guide = pavement.Find("Hangar guide - two dashes");
            var filter = guide != null ? guide.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null || !pavement.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("C8b: expected the active C6 hangar guide; nothing changed.");
                return;
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null)
                {
                    Debug.LogError("C8b: unexpected asset at guide mesh path.");
                    return;
                }
                // Same width, length and Z center as C6; two marks farther from
                // the hangar so the overhanging roof does not hide most of them.
                // Existing guide transform retains the C7 +12 Z offset.
                var vertices = new Vector3[8];
                var triangles = new int[12];
                for (int i = 0; i < 2; i++)
                {
                    float x = 195f + i * 6f;
                    int n = i * 4, t = i * 6;
                    vertices[n] = new Vector3(x, 0f, -75.24f);
                    vertices[n+1] = new Vector3(x, 0f, -74.76f);
                    vertices[n+2] = new Vector3(x+2.1f, 0f, -74.76f);
                    vertices[n+3] = new Vector3(x+2.1f, 0f, -75.24f);
                    triangles[t] = n; triangles[t+1] = n+1; triangles[t+2] = n+2;
                    triangles[t+3] = n; triangles[t+4] = n+2; triangles[t+5] = n+3;
                }
                mesh = new Mesh { name = "C8b hangar guide - two dashes" };
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, MeshPath);
                AssetDatabase.SaveAssets();
            }
            Undo.RecordObjects(new Object[] { filter, guide.gameObject }, UndoName);
            filter.sharedMesh = mesh;
            guide.gameObject.name = "Hangar guide - two dashes";
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("C8b: two hangar guide marks ready. Save scene and mesh asset.");
        }
    }
}
