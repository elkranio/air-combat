using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Explicit art-only authoring. No runtime components or collision volumes.
    internal static class CL06BaseDebrisAuthoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Prefabs = "Assets/_Project/Prefabs/Environment/Cliffs/";
        private const string GroupName = "CL06E - Decorative base debris";
        private const string UndoName = "CL06F compact base debris";
        private static readonly string[] Names = { "CL06_G_Boulder", "CL06_H_Slab" };
        private readonly struct Piece
        {
            public readonly int Kind;
            public readonly float X, Z, Yaw, Sx, Sy, Sz;
            public Piece(int kind, float x, float z, float yaw, float sx, float sy, float sz)
            { Kind = kind; X = x; Z = z; Yaw = yaw; Sx = sx; Sy = sy; Sz = sz; }
        }
        // CL06F: compact debris seated against the actual low wall section.
        // Main is world XZ, Trial is local to each test wall.
        private static readonly Piece[] Main = {
            new Piece(0, -911.980f, -102.900f, 22.260f, 0.256f, 0.294f, 0.187f),
            new Piece(1, -846.370f, -22.480f, -84.190f, 0.310f, 0.241f, 0.211f),
            new Piece(1, -745.780f, 59.440f, -83.590f, 0.307f, 0.270f, 0.176f),
            new Piece(1, -673.270f, 152.730f, -35.570f, 0.311f, 0.313f, 0.284f),
            new Piece(0, -594.490f, 214.720f, -29.820f, 0.289f, 0.307f, 0.242f),
            new Piece(0, -504.510f, 309.250f, -50.590f, 0.228f, 0.302f, 0.215f),
            new Piece(0, -403.930f, 387.410f, -36.640f, 0.223f, 0.249f, 0.210f),
            new Piece(0, -392.010f, 406.340f, -20.800f, 0.128f, 0.151f, 0.178f),
            new Piece(1, -341.070f, 461.720f, -38.730f, 0.252f, 0.209f, 0.282f),
            new Piece(0, -339.570f, 486.580f, -58.070f, 0.213f, 0.103f, 0.150f),
            new Piece(1, -269.140f, 542.600f, -50.040f, 0.240f, 0.265f, 0.249f),
            new Piece(1, -257.980f, 556.350f, -76.780f, 0.197f, 0.175f, 0.173f),
            new Piece(0, -185.340f, 624.050f, -16.340f, 0.270f, 0.300f, 0.282f),
            new Piece(0, -102.960f, 727.640f, -37.190f, 0.283f, 0.234f, 0.185f),
            new Piece(0, -86.160f, 736.080f, -34.930f, 0.190f, 0.106f, 0.137f),
            new Piece(1, -36.670f, 798.880f, -31.820f, 0.217f, 0.245f, 0.173f),
            new Piece(1, -25.000f, 808.540f, -51.480f, 0.216f, 0.142f, 0.106f),
            new Piece(0, 44.530f, 902.160f, 6.080f, 0.345f, 0.296f, 0.277f),
            new Piece(1, 142.910f, 993.060f, -52.130f, 0.211f, 0.301f, 0.233f),
            new Piece(0, 168.220f, 1010.220f, -50.400f, 0.195f, 0.154f, 0.161f),
            new Piece(0, 204.290f, 1050.550f, -44.260f, 0.309f, 0.274f, 0.184f),
            new Piece(0, 300.150f, 1132.220f, -62.000f, 0.261f, 0.255f, 0.174f),
            new Piece(0, 327.940f, 1150.740f, -42.390f, 0.125f, 0.124f, 0.102f),
            new Piece(1, 415.610f, 1217.720f, -43.110f, 0.215f, 0.209f, 0.164f),
            new Piece(0, 507.050f, 1254.580f, 19.300f, 0.273f, 0.297f, 0.264f),
            new Piece(0, 603.680f, 1306.770f, 11.440f, 0.249f, 0.307f, 0.261f),
            new Piece(0, 693.000f, 1249.040f, 31.930f, 0.287f, 0.232f, 0.258f),
            new Piece(0, 730.750f, 1137.290f, 154.040f, 0.212f, 0.308f, 0.282f),
            new Piece(0, 659.590f, 1032.290f, 108.540f, 0.331f, 0.281f, 0.241f),
            new Piece(1, 643.990f, 1005.840f, 124.640f, 0.220f, 0.128f, 0.157f),
            new Piece(0, 622.470f, 960.010f, 114.030f, 0.246f, 0.269f, 0.255f),
            new Piece(0, 611.290f, 944.950f, 131.560f, 0.185f, 0.171f, 0.150f),
            new Piece(1, 523.890f, 884.100f, 109.570f, 0.327f, 0.243f, 0.253f),
            new Piece(0, 505.750f, 861.610f, 121.600f, 0.172f, 0.140f, 0.147f),
            new Piece(1, 457.350f, 820.670f, 167.090f, 0.349f, 0.255f, 0.222f),
            new Piece(0, 369.470f, 739.040f, 130.620f, 0.325f, 0.211f, 0.238f),
            new Piece(0, 354.580f, 732.090f, 173.850f, 0.201f, 0.115f, 0.153f),
            new Piece(0, 305.860f, 666.990f, 128.450f, 0.219f, 0.228f, 0.261f),
            new Piece(0, 297.040f, 639.190f, 118.480f, 0.189f, 0.124f, 0.184f),
            new Piece(0, 248.650f, 615.770f, 85.720f, 0.325f, 0.207f, 0.181f),
            new Piece(0, 218.940f, 592.320f, 110.040f, 0.144f, 0.119f, 0.143f),
            new Piece(0, 140.180f, 541.940f, 104.930f, 0.278f, 0.207f, 0.214f),
            new Piece(1, 53.120f, 446.320f, 108.830f, 0.349f, 0.241f, 0.206f),
            new Piece(0, 45.920f, 435.000f, 120.580f, 0.149f, 0.103f, 0.160f),
            new Piece(0, -38.520f, 344.380f, 95.180f, 0.332f, 0.277f, 0.257f),
            new Piece(1, -94.030f, 249.800f, 146.650f, 0.214f, 0.300f, 0.213f),
            new Piece(0, -171.830f, 187.370f, 158.540f, 0.298f, 0.279f, 0.282f),
            new Piece(0, -272.760f, 87.210f, 160.880f, 0.283f, 0.256f, 0.240f),
            new Piece(0, -374.170f, -13.790f, 137.850f, 0.286f, 0.269f, 0.256f),
            new Piece(1, -391.190f, -34.360f, 116.020f, 0.198f, 0.154f, 0.161f),
            new Piece(0, -423.090f, -119.940f, 174.140f, 0.339f, 0.251f, 0.244f),
            new Piece(1, -442.550f, -133.240f, 163.850f, 0.181f, 0.163f, 0.162f),
            new Piece(1, -502.810f, -174.670f, 124.810f, 0.233f, 0.244f, 0.169f),
            new Piece(0, -525.920f, -188.960f, 145.420f, 0.150f, 0.126f, 0.169f),
            new Piece(1, -576.440f, -244.790f, 93.850f, 0.232f, 0.243f, 0.214f),
            new Piece(1, -657.040f, -324.810f, 130.630f, 0.306f, 0.296f, 0.174f),
            new Piece(0, -748.070f, -400.780f, 137.300f, 0.264f, 0.316f, 0.184f),
            new Piece(1, -757.250f, -412.370f, 115.640f, 0.213f, 0.141f, 0.152f),
            new Piece(0, -793.750f, -482.950f, 115.010f, 0.239f, 0.259f, 0.283f),
            new Piece(0, -875.150f, -536.510f, 161.620f, 0.324f, 0.311f, 0.183f),
            new Piece(0, -967.810f, -594.070f, 140.300f, 0.332f, 0.262f, 0.258f),
            new Piece(1, 1048.210f, -927.540f, -58.990f, 0.308f, 0.286f, 0.276f),
            new Piece(1, 1124.870f, -836.230f, -46.200f, 0.321f, 0.260f, 0.198f),
            new Piece(1, 1139.130f, -822.270f, -65.480f, 0.172f, 0.116f, 0.128f),
            new Piece(1, 1193.550f, -739.790f, -69.610f, 0.260f, 0.226f, 0.258f),
            new Piece(1, 1276.340f, -676.750f, -99.700f, 0.277f, 0.295f, 0.245f),
            new Piece(1, 1295.540f, -665.600f, -69.400f, 0.159f, 0.122f, 0.136f),
            new Piece(0, 1389.030f, -649.540f, 23.110f, 0.309f, 0.226f, 0.184f),
            new Piece(0, 1409.070f, -670.490f, 44.220f, 0.149f, 0.163f, 0.135f),
            new Piece(0, 1511.330f, -812.860f, 67.140f, 0.357f, 0.283f, 0.216f),
            new Piece(1, 1457.450f, -873.930f, 109.730f, 0.247f, 0.243f, 0.279f),
        };
        private static readonly Piece[] Trial = {
            new Piece(0, -260.890f, -132.580f, -164.530f, 0.211f, 0.238f, 0.207f),
            new Piece(1, -319.240f, -86.060f, -91.240f, 0.242f, 0.312f, 0.193f),
            new Piece(0, -316.600f, -50.550f, -94.080f, 0.143f, 0.130f, 0.119f),
            new Piece(1, -322.180f, 46.400f, -96.910f, 0.291f, 0.277f, 0.174f),
            new Piece(1, -266.390f, 112.510f, 20.360f, 0.306f, 0.294f, 0.291f),
            new Piece(0, -249.680f, 113.710f, -37.280f, 0.131f, 0.181f, 0.117f),
            new Piece(1, -187.450f, 137.220f, 17.940f, 0.307f, 0.290f, 0.282f),
            new Piece(1, -169.640f, 134.650f, -31.630f, 0.134f, 0.171f, 0.155f),
            new Piece(0, -84.270f, 147.720f, -51.180f, 0.217f, 0.316f, 0.208f),
            new Piece(0, 31.530f, 168.170f, 37.120f, 0.278f, 0.230f, 0.287f),
            new Piece(0, 136.870f, 176.730f, 10.740f, 0.227f, 0.272f, 0.189f),
            new Piece(0, 169.150f, 179.060f, 30.980f, 0.185f, 0.147f, 0.183f),
            new Piece(1, 222.860f, 177.730f, 47.220f, 0.226f, 0.231f, 0.187f),
            new Piece(1, 241.030f, 157.530f, 40.450f, 0.176f, 0.124f, 0.119f),
            new Piece(1, 291.260f, 127.760f, 78.130f, 0.257f, 0.319f, 0.237f),
            new Piece(0, 317.310f, 96.930f, 71.660f, 0.175f, 0.109f, 0.133f),
            new Piece(1, 319.290f, 32.500f, 83.250f, 0.333f, 0.318f, 0.202f),
            new Piece(1, 317.380f, -86.530f, 174.630f, 0.274f, 0.259f, 0.279f),
            new Piece(0, 299.790f, -100.200f, 124.530f, 0.161f, 0.138f, 0.184f),
            new Piece(0, 216.360f, -145.180f, 187.240f, 0.313f, 0.312f, 0.206f),
            new Piece(1, 203.090f, -144.550f, 197.790f, 0.169f, 0.111f, 0.104f),
            new Piece(0, 83.150f, -140.910f, 177.490f, 0.302f, 0.244f, 0.227f),
            new Piece(0, -52.590f, -144.690f, -154.970f, 0.252f, 0.212f, 0.279f),
            new Piece(0, -187.560f, -157.460f, -185.590f, 0.246f, 0.221f, 0.288f),
        };

        [MenuItem("Air Combat/Missions/Refresh CL06F Base Debris")]
        private static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = GetScene();
                var roots = scene.GetRootGameObjects();
                var barrier = roots.SingleOrDefault(g => g.name == "Mission01 / CL06 rock barrier");
                if (barrier == null || barrier.GetComponentsInChildren<RadarTerrainObstacle>(true).Length != 138 ||
                    barrier.transform.position.sqrMagnitude > .001f ||
                    Quaternion.Angle(barrier.transform.rotation, Quaternion.identity) > .01f ||
                    (barrier.transform.lossyScale - Vector3.one).sqrMagnitude > .001f)
                    throw new InvalidOperationException("Expected saved CL06B/D/E main barrier at its authored transform.");
                var ground = roots.Single(g => g.name == "Mission01 / Faceted surface").GetComponent<MeshFilter>();
                if (ground == null || ground.sharedMesh == null || Mathf.Abs(ground.sharedMesh.bounds.min.y) > .01f || Mathf.Abs(ground.sharedMesh.bounds.max.y) > .01f)
                    throw new InvalidOperationException("Expected flat mission ground.");
                var trials = roots.SingleOrDefault(g => g.name == "Mission01 / CL06 orientation trials");
                if (trials != null && (trials.transform.childCount != 2 || trials.GetComponentsInChildren<RadarTerrainObstacle>(true).Length != 50))
                    throw new InvalidOperationException("Expected two unmodified CL06 orientation walls.");
                var sources = Names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + n + ".prefab")).ToArray();
                foreach (var source in sources)
                {
                    if (source == null) throw new InvalidOperationException("Missing CL06 low rock prefab.");
                    var filters = source.GetComponentsInChildren<MeshFilter>(true);
                    if (filters.Length != 1 || filters[0].sharedMesh == null || filters[0].GetComponent<MeshRenderer>() == null ||
                        filters[0].sharedMesh.subMeshCount != 1 || filters[0].GetComponent<MeshRenderer>().sharedMaterial == null)
                        throw new InvalidOperationException("Expected one visual mesh and material: " + source.name);
                }
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    int count = Add(barrier.transform, Main, sources);
                    if (trials != null)
                        for (int i = 0; i < trials.transform.childCount; i++)
                            count += Add(trials.transform.GetChild(i), Trial, sources);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = barrier;
                    Debug.Log($"CL06F: {count} compact decorative rocks. Previous debris replaced. No colliders or radar components.");
                }
                catch { Undo.RevertAllDownToGroup(group); throw; }
            }
            catch (Exception ex) { Debug.LogError("CL06F debris: " + ex); }
        }

        private static int Add(Transform parent, Piece[] recipe, GameObject[] sources)
        {
            // Keep the group name stable for existing CL06E scenes and cleanup.
            // Replace only generated visual debris; never touch wall children.
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name != GroupName) continue;
                if (child.GetComponentsInChildren<Component>(true).Any(c =>
                    !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)))
                    throw new InvalidOperationException("Debris group contains non-visual components; inspect before refresh.");
                Undo.DestroyObjectImmediate(child.gameObject);
            }
            var root = new GameObject(GroupName);
            SceneManager.MoveGameObjectToScene(root, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            Undo.SetTransformParent(root.transform, parent, UndoName);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            int count = 0;
            foreach (var p in recipe)
            {
                var source = sources[p.Kind];
                var filter = source.GetComponentInChildren<MeshFilter>(true);
                var sourceRenderer = filter.GetComponent<MeshRenderer>();
                // Only mesh/material data is copied, never gameplay scripts or colliders.
                var piece = new GameObject($"{count+1:00} / {Names[p.Kind]}", typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(piece, parent.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(piece, UndoName);
                Undo.SetTransformParent(piece.transform, root.transform, UndoName);
                // Account for the imported model's relative transform. The accepted
                // CL06 visual roots have unit scale and no rotation/translation.
                var relative = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var placement = Matrix4x4.TRS(new Vector3(p.X, -.3f, p.Z), Quaternion.Euler(0,p.Yaw,0), new Vector3(p.Sx,p.Sy,p.Sz));
                var pose = placement * relative;
                piece.transform.localPosition = pose.GetColumn(3);
                piece.transform.localRotation = pose.rotation;
                piece.transform.localScale = pose.lossyScale;
                piece.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = piece.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                renderer.receiveShadows = sourceRenderer.receiveShadows;
                var b = renderer.bounds;
                if (b.max.y > 18f || b.min.y < -2f || b.min.y > 1f || b.min.x < -1000 || b.min.z < -1000 || b.max.x > 2200 || b.max.z > 2200)
                {
                    // Trim decorative pieces that cross the ground edge; never alter
                    // the actual wall or extend the flight obstacle to accommodate art.
                    Undo.DestroyObjectImmediate(piece);
                    continue;
                }
                GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.BatchingStatic);
                count++;
            }
            return count;
        }

        [MenuItem("Air Combat/Missions/Remove CL06 Base Debris")]
        private static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = GetScene();
                var parents = scene.GetRootGameObjects().Where(g => g.name == "Mission01 / CL06 rock barrier").Select(g => g.transform).ToList();
                var trials = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / CL06 orientation trials");
                if (trials != null) for (int i = 0; i < trials.transform.childCount; i++) parents.Add(trials.transform.GetChild(i));
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                foreach (var parent in parents)
                    for (int i = parent.childCount - 1; i >= 0; i--)
                        if (parent.GetChild(i).name == GroupName) Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            catch (Exception ex) { Debug.LogError("CL06F debris: " + ex); }
        }

        private static Scene GetScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Open Mission01_FirstSortie first.");
            return scene;
        }
    }
}
