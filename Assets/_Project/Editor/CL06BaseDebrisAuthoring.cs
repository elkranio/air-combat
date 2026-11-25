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
        private const string UndoName = "CL06E base debris";
        private static readonly string[] Names = { "CL06_G_Boulder", "CL06_H_Slab" };
        private readonly struct Piece
        {
            public readonly int Kind;
            public readonly float X, Z, Yaw, Sx, Sy, Sz;
            public Piece(int kind, float x, float z, float yaw, float sx, float sy, float sz)
            { Kind = kind; X = x; Z = z; Yaw = yaw; Sx = sx; Sy = sy; Sz = sz; }
        }
        // Hand-off recipe sampled along the outside of the accepted CL06B
        // ground footprint. Main is world XZ, Trial is local to each test wall.
        private static readonly Piece[] Main = {
            new Piece(0, -919.410f, -94.120f, 23.450f, 0.441f, 0.599f, 0.334f),
            new Piece(1, -862.390f, -12.790f, -77.080f, 0.535f, 0.491f, 0.376f),
            new Piece(1, -748.270f, 61.280f, -74.820f, 0.530f, 0.551f, 0.315f),
            new Piece(1, -678.430f, 160.530f, -24.030f, 0.537f, 0.639f, 0.508f),
            new Piece(0, -600.470f, 221.950f, -29.340f, 0.498f, 0.627f, 0.433f),
            new Piece(0, -522.640f, 314.150f, -63.390f, 0.393f, 0.616f, 0.384f),
            new Piece(0, -421.430f, 389.140f, -35.370f, 0.385f, 0.508f, 0.375f),
            new Piece(0, -424.290f, 421.720f, -12.690f, 0.197f, 0.308f, 0.317f),
            new Piece(1, -356.690f, 456.570f, -50.180f, 0.434f, 0.426f, 0.504f),
            new Piece(0, -355.280f, 489.110f, -63.100f, 0.327f, 0.211f, 0.267f),
            new Piece(1, -280.120f, 548.810f, -56.270f, 0.414f, 0.541f, 0.444f),
            new Piece(1, -288.090f, 584.810f, -74.860f, 0.303f, 0.357f, 0.309f),
            new Piece(0, -191.170f, 628.920f, -13.280f, 0.466f, 0.613f, 0.503f),
            new Piece(0, -114.660f, 737.570f, -43.900f, 0.488f, 0.477f, 0.331f),
            new Piece(0, -109.230f, 768.650f, -43.810f, 0.293f, 0.216f, 0.244f),
            new Piece(1, -44.810f, 810.610f, -25.470f, 0.374f, 0.500f, 0.309f),
            new Piece(1, -51.730f, 834.360f, -55.870f, 0.332f, 0.290f, 0.189f),
            new Piece(0, 46.770f, 919.090f, -1.630f, 0.594f, 0.604f, 0.495f),
            new Piece(1, 120.720f, 997.330f, -54.290f, 0.363f, 0.615f, 0.416f),
            new Piece(0, 116.540f, 1027.800f, -38.740f, 0.300f, 0.315f, 0.288f),
            new Piece(0, 192.380f, 1058.610f, -32.240f, 0.533f, 0.559f, 0.329f),
            new Piece(0, 294.410f, 1148.330f, -59.710f, 0.450f, 0.521f, 0.311f),
            new Piece(0, 303.510f, 1185.970f, -35.210f, 0.193f, 0.254f, 0.182f),
            new Piece(1, 407.920f, 1225.320f, -41.350f, 0.371f, 0.427f, 0.292f),
            new Piece(0, 492.570f, 1266.800f, 13.580f, 0.470f, 0.606f, 0.472f),
            new Piece(0, 590.660f, 1322.710f, 15.850f, 0.430f, 0.626f, 0.466f),
            new Piece(0, 699.610f, 1257.410f, 41.260f, 0.494f, 0.473f, 0.460f),
            new Piece(0, 741.970f, 1144.590f, 147.350f, 0.365f, 0.629f, 0.503f),
            new Piece(0, 675.110f, 1035.900f, 115.790f, 0.571f, 0.573f, 0.431f),
            new Piece(1, 681.130f, 1001.560f, 122.200f, 0.338f, 0.261f, 0.281f),
            new Piece(0, 629.530f, 953.350f, 118.290f, 0.424f, 0.549f, 0.455f),
            new Piece(0, 631.370f, 926.480f, 132.920f, 0.285f, 0.348f, 0.267f),
            new Piece(1, 527.350f, 882.650f, 104.660f, 0.563f, 0.496f, 0.452f),
            new Piece(0, 532.630f, 845.110f, 127.980f, 0.265f, 0.286f, 0.262f),
            new Piece(1, 458.460f, 810.040f, 161.120f, 0.602f, 0.521f, 0.397f),
            new Piece(0, 371.750f, 733.790f, 136.220f, 0.561f, 0.431f, 0.425f),
            new Piece(0, 358.110f, 705.260f, 172.980f, 0.309f, 0.234f, 0.274f),
            new Piece(0, 316.150f, 656.950f, 133.140f, 0.378f, 0.466f, 0.466f),
            new Piece(0, 325.330f, 631.790f, 129.250f, 0.291f, 0.253f, 0.329f),
            new Piece(0, 244.990f, 607.710f, 80.710f, 0.560f, 0.423f, 0.323f),
            new Piece(0, 259.200f, 573.740f, 108.450f, 0.221f, 0.243f, 0.256f),
            new Piece(0, 143.540f, 538.980f, 110.580f, 0.480f, 0.422f, 0.382f),
            new Piece(1, 64.550f, 445.310f, 97.250f, 0.602f, 0.491f, 0.367f),
            new Piece(0, 76.120f, 412.560f, 132.840f, 0.229f, 0.210f, 0.286f),
            new Piece(0, -34.620f, 343.270f, 84.870f, 0.572f, 0.565f, 0.459f),
            new Piece(0, -25.370f, 310.700f, 139.350f, 0.218f, 0.274f, 0.201f),
            new Piece(1, -90.410f, 245.900f, 148.830f, 0.369f, 0.613f, 0.380f),
            new Piece(0, -182.680f, 179.730f, 150.570f, 0.514f, 0.569f, 0.503f),
            new Piece(0, -272.070f, 78.400f, 164.500f, 0.488f, 0.522f, 0.428f),
            new Piece(0, -369.940f, -18.770f, 135.820f, 0.493f, 0.549f, 0.457f),
            new Piece(1, -357.400f, -51.720f, 120.550f, 0.304f, 0.314f, 0.287f),
            new Piece(0, -415.420f, -129.490f, 167.260f, 0.584f, 0.512f, 0.436f),
            new Piece(1, -417.310f, -160.430f, 159.680f, 0.279f, 0.332f, 0.290f),
            new Piece(1, -507.840f, -187.500f, 126.560f, 0.401f, 0.497f, 0.302f),
            new Piece(0, -503.910f, -226.950f, 144.030f, 0.230f, 0.258f, 0.301f),
            new Piece(1, -564.510f, -251.290f, 105.230f, 0.400f, 0.495f, 0.382f),
            new Piece(1, -646.950f, -325.710f, 142.840f, 0.528f, 0.605f, 0.310f),
            new Piece(0, -739.520f, -400.560f, 135.840f, 0.456f, 0.645f, 0.328f),
            new Piece(1, -742.130f, -434.540f, 115.860f, 0.328f, 0.287f, 0.272f),
            new Piece(0, -788.690f, -489.450f, 104.620f, 0.412f, 0.529f, 0.506f),
            new Piece(0, -877.940f, -550.770f, 158.540f, 0.558f, 0.634f, 0.327f),
            new Piece(0, -964.340f, -607.880f, 146.560f, 0.573f, 0.534f, 0.461f),
            new Piece(1, 1043.320f, -918.970f, -58.060f, 0.531f, 0.583f, 0.492f),
            new Piece(1, 1100.250f, -831.480f, -51.090f, 0.553f, 0.531f, 0.353f),
            new Piece(1, 1104.480f, -803.640f, -69.660f, 0.265f, 0.237f, 0.229f),
            new Piece(1, 1176.610f, -741.990f, -73.950f, 0.449f, 0.461f, 0.460f),
            new Piece(1, 1271.090f, -669.220f, -94.410f, 0.477f, 0.602f, 0.438f),
            new Piece(1, 1260.980f, -634.830f, -75.030f, 0.244f, 0.249f, 0.243f),
            new Piece(0, 1401.370f, -638.200f, 34.550f, 0.533f, 0.461f, 0.329f),
            new Piece(0, 1426.720f, -643.840f, 35.360f, 0.229f, 0.333f, 0.241f),
            new Piece(1, 1481.250f, -706.180f, 56.870f, 0.465f, 0.608f, 0.362f),
            new Piece(0, 1519.040f, -811.670f, 58.100f, 0.616f, 0.577f, 0.386f),
            new Piece(1, 1461.010f, -869.090f, 115.650f, 0.426f, 0.495f, 0.498f),
        };
        private static readonly Piece[] Trial = {
            new Piece(0, -267.710f, -141.050f, -175.180f, 0.363f, 0.485f, 0.369f),
            new Piece(1, -338.380f, -77.620f, -97.230f, 0.418f, 0.636f, 0.345f),
            new Piece(0, -359.400f, -54.330f, -82.340f, 0.220f, 0.265f, 0.213f),
            new Piece(1, -338.370f, 33.600f, -97.220f, 0.502f, 0.566f, 0.310f),
            new Piece(1, -366.090f, 51.880f, -120.130f, 0.279f, 0.336f, 0.189f),
            new Piece(1, -271.210f, 129.320f, 12.190f, 0.527f, 0.600f, 0.519f),
            new Piece(0, -253.480f, 153.420f, -24.970f, 0.202f, 0.370f, 0.209f),
            new Piece(1, -183.710f, 146.760f, 13.820f, 0.530f, 0.591f, 0.504f),
            new Piece(1, -157.700f, 162.870f, -22.670f, 0.206f, 0.348f, 0.276f),
            new Piece(0, -91.420f, 159.570f, -42.310f, 0.374f, 0.644f, 0.371f),
            new Piece(0, 33.120f, 176.120f, 40.310f, 0.480f, 0.469f, 0.512f),
            new Piece(0, 134.320f, 185.560f, 2.640f, 0.392f, 0.555f, 0.337f),
            new Piece(0, 165.530f, 199.020f, 21.030f, 0.284f, 0.301f, 0.326f),
            new Piece(1, 240.160f, 185.630f, 43.180f, 0.390f, 0.472f, 0.334f),
            new Piece(1, 271.230f, 184.310f, 31.160f, 0.270f, 0.253f, 0.212f),
            new Piece(1, 320.200f, 130.030f, 71.720f, 0.443f, 0.652f, 0.424f),
            new Piece(0, 354.330f, 120.210f, 60.300f, 0.269f, 0.222f, 0.237f),
            new Piece(1, 326.090f, 20.330f, 95.970f, 0.575f, 0.649f, 0.361f),
            new Piece(1, 319.440f, -88.840f, 163.490f, 0.473f, 0.528f, 0.499f),
            new Piece(0, 320.250f, -132.470f, 125.320f, 0.247f, 0.282f, 0.329f),
            new Piece(0, 204.030f, -149.840f, 197.690f, 0.540f, 0.636f, 0.367f),
            new Piece(1, 190.600f, -174.120f, 191.320f, 0.260f, 0.227f, 0.185f),
            new Piece(0, 83.540f, -147.760f, 176.610f, 0.521f, 0.497f, 0.405f),
            new Piece(0, -53.400f, -149.890f, -155.410f, 0.434f, 0.432f, 0.498f),
            new Piece(0, -188.900f, -161.450f, -183.380f, 0.424f, 0.452f, 0.514f),
        };

        [MenuItem("Air Combat/Missions/Place CL06E Base Debris")]
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
                    Debug.Log($"CL06E: {count} new decorative rocks. Existing debris groups are not duplicated. No colliders or radar components.");
                }
                catch { Undo.RevertAllDownToGroup(group); throw; }
            }
            catch (Exception ex) { Debug.LogError("CL06E debris: " + ex); }
        }

        private static int Add(Transform parent, Piece[] recipe, GameObject[] sources)
        {
            if (parent.Find(GroupName) != null) return 0;
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
                if (b.max.y > 40f || b.min.y < -2f || b.min.y > 1f || b.min.x < -1000 || b.min.z < -1000 || b.max.x > 2200 || b.max.z > 2200)
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

        [MenuItem("Air Combat/Missions/Remove CL06E Base Debris")]
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
            catch (Exception ex) { Debug.LogError("CL06E debris: " + ex); }
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
