using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // BOUND-01: purely visual apron around the authored operation rectangle.
    // Keep the original renderer's bounds intact: MissionMapBaker uses them.
    internal static class Mission01BoundarySurround
    {
        private const string SurfaceName = "Mission01 / Faceted surface";
        private const string SurroundName = "Mission01 / Boundary surround (visual only)";
        private const string MeshPath = "Assets/_Project/Art/Environment/Mission01_FirstSortie/Mission01_BoundarySurround.asset";
        private const float Padding = 600f;

        [MenuItem("Air Combat/Art/Apply Mission01 Boundary Surround v0")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int undo = -1;
            Mesh draft = null;
            try
            {
                Scene scene = RequireMission();
                var roots = scene.GetRootGameObjects();
                var ground = roots.Single(g => g.name == SurfaceName);
                var filter = ground.GetComponent<MeshFilter>();
                var renderer = ground.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null ||
                    renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null)
                    throw new InvalidOperationException("Expected the original single-material ground surface.");
                Bounds b = renderer.bounds;
                if (b.size.x <= 0f || b.size.z <= 0f || b.size.y > .01f)
                    throw new InvalidOperationException("Expected a flat, nonempty ground rectangle.");

                var existing = roots.SingleOrDefault(g => g.name == SurroundName);
                if (existing != null && (existing.transform.childCount != 0 ||
                    existing.GetComponents<Component>().Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer))))
                    throw new InvalidOperationException("Surround has unexpected components or children; refusing to overwrite it.");

                draft = BuildMesh(filter, renderer, b);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (mesh == null && AssetDatabase.LoadMainAssetAtPath(MeshPath) != null)
                    throw new InvalidOperationException("Unexpected asset at " + MeshPath);

                Undo.IncrementCurrentGroup();
                undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Mission01 boundary surround");
                if (mesh == null)
                {
                    AssetDatabase.CreateAsset(draft, MeshPath);
                    mesh = draft;
                    draft = null;
                }
                else
                {
                    Undo.RegisterCompleteObjectUndo(mesh, "Refresh boundary mesh");
                    EditorUtility.CopySerialized(draft, mesh);
                    EditorUtility.SetDirty(mesh);
                }

                var surround = existing;
                if (surround == null)
                {
                    surround = new GameObject(SurroundName);
                    SceneManager.MoveGameObjectToScene(surround, scene);
                    Undo.RegisterCreatedObjectUndo(surround, "Create boundary surround");
                }
                Undo.RecordObjects(new UnityEngine.Object[] { surround, surround.transform }, "Align boundary surround");
                surround.SetActive(true);
                surround.layer = ground.layer;
                surround.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                surround.transform.localScale = Vector3.one;
                // Unity objects may be native-null while their managed wrapper is non-null.
                // Use Unity's equality operator, not C# null-coalescing, for component lookup.
                var mf = surround.GetComponent<MeshFilter>();
                if (mf == null) mf = Undo.AddComponent<MeshFilter>(surround);
                var mr = surround.GetComponent<MeshRenderer>();
                if (mr == null) mr = Undo.AddComponent<MeshRenderer>(surround);
                if (mf == null || mr == null)
                    throw new InvalidOperationException("Could not create boundary mesh components.");
                Undo.RecordObjects(new UnityEngine.Object[] { mf, mr }, "Set boundary visual");
                mf.sharedMesh = mesh;
                mr.sharedMaterial = renderer.sharedMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = renderer.receiveShadows;
                mr.renderingLayerMask = renderer.renderingLayerMask;
                mr.enabled = true;
                EditorUtility.SetDirty(mf);
                EditorUtility.SetDirty(mr);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undo);
                Selection.activeGameObject = surround;
                Debug.Log($"BOUND-01: original X/Z {b.size.x:0} x {b.size.z:0}; visual extent " +
                    $"{b.size.x + 2 * Padding:0} x {b.size.z + 2 * Padding:0}. Save scene after review.");
            }
            catch (Exception ex)
            {
                if (undo >= 0) Undo.RevertAllDownToGroup(undo);
                Debug.LogError("BOUND-01: " + ex);
            }
            finally
            {
                if (draft != null && !AssetDatabase.Contains(draft)) UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [MenuItem("Air Combat/Art/Remove Mission01 Boundary Surround")]
        private static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = RequireMission();
                var surround = scene.GetRootGameObjects().SingleOrDefault(g => g.name == SurroundName);
                if (surround == null) return;
                if (surround.transform.childCount != 0 || surround.GetComponents<Component>().Any(c =>
                    !(c is Transform || c is MeshFilter || c is MeshRenderer)))
                    throw new InvalidOperationException("Surround has unexpected components or children; refusing to remove it.");
                Undo.DestroyObjectImmediate(surround);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("BOUND-01: removed visual surround; generated mesh retained.");
            }
            catch (Exception ex) { Debug.LogError("BOUND-01: " + ex); }
        }

        private static Scene RequireMission()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.name != "Mission01_FirstSortie")
                throw new InvalidOperationException("Open Mission01_FirstSortie in Edit Mode.");
            return scene;
        }

        private static Mesh BuildMesh(MeshFilter ground, MeshRenderer renderer, Bounds b)
        {
            var source = ground.sharedMesh;
            if (!source.isReadable) throw new InvalidOperationException("Ground mesh must be readable.");
            var sourceUV = source.uv;
            var sourceVertices = source.vertices;
            if (sourceVertices.Length == 0 || sourceUV.Length != sourceVertices.Length)
                throw new InvalidOperationException("Ground UVs missing.");

            // Supported ground variants: the original palette swatch and the
            // SAND-V0 world-XZ texture. Never silently guess a new UV layout.
            bool constant = sourceUV.All(uv => (uv - sourceUV[0]).sqrMagnitude < 1e-10f);
            Vector2 Project(Vector3 p) => new Vector2((p.x - b.min.x) / b.size.x, (p.z - b.min.z) / b.size.z);
            if (!constant)
            {
                for (int i = 0; i < sourceVertices.Length; i++)
                    if ((sourceUV[i] - Project(ground.transform.TransformPoint(sourceVertices[i]))).sqrMagnitude > 1e-8f)
                        throw new InvalidOperationException("Unknown ground UV layout; surround not changed.");
                var mat = renderer.sharedMaterial;
                var texture = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                if (texture == null || texture.wrapModeU != TextureWrapMode.Clamp || texture.wrapModeV != TextureWrapMode.Clamp ||
                    (mat.GetTextureScale("_BaseMap") - Vector2.one).sqrMagnitude > 1e-8f ||
                    mat.GetTextureOffset("_BaseMap").sqrMagnitude > 1e-8f)
                    throw new InvalidOperationException("World-XZ sand requires the original clamped base texture.");
            }

            // A 4x4 grid with the center cell omitted: no coplanar overlap with
            // the original surface. Outer UVs clamp at the original edge.
            float[] xs = { b.min.x - Padding, b.min.x, b.max.x, b.max.x + Padding };
            float[] zs = { b.min.z - Padding, b.min.z, b.max.z, b.max.z + Padding };
            var vertices = new Vector3[16];
            var uv0 = new Vector2[16];
            var normals = new Vector3[16];
            var tangents = new Vector4[16];
            for (int z = 0; z < 4; z++)
            for (int x = 0; x < 4; x++)
            {
                int i = z * 4 + x;
                vertices[i] = new Vector3(xs[x], b.center.y, zs[z]);
                uv0[i] = constant ? sourceUV[0] : Project(vertices[i]);
                normals[i] = Vector3.up;
                tangents[i] = new Vector4(1f, 0f, 0f, -1f);
            }
            var triangles = new int[48];
            int t = 0;
            for (int z = 0; z < 3; z++)
            for (int x = 0; x < 3; x++)
            {
                if (x == 1 && z == 1) continue;
                int a = z * 4 + x;
                triangles[t++] = a; triangles[t++] = a + 4; triangles[t++] = a + 5;
                triangles[t++] = a; triangles[t++] = a + 5; triangles[t++] = a + 1;
            }
            var mesh = new Mesh { name = "Mission01 Boundary Surround" };
            mesh.vertices = vertices;
            mesh.uv = uv0;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
