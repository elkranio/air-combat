using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // G3 lab adapter for the existing G2 material-grouped FBX. It builds four
    // independent hit zones and removes only the triangles belonging to a killed
    // structural part. Original imported meshes/materials remain untouched.
    [DisallowMultipleComponent]
    public sealed class GroundDepotTarget : MonoBehaviour
    {
        private readonly List<GroundDepotPart> parts = new List<GroundDepotPart>(4);
        // Only the latest runtime clone per renderer is needed.
        private Mesh[] runtimeMeshes;
        private MeshFilter[] filters;
        private Mesh[] sourceMeshes;
        private bool installed;

        public int DestroyedParts { get; private set; }
        public int TotalParts => parts.Count;

        public void Install()
        {
            if (installed) return;
            installed = true;
            filters = GetComponentsInChildren<MeshFilter>(true);
            sourceMeshes = new Mesh[filters.Length];
            runtimeMeshes = new Mesh[filters.Length];
            for (int i = 0; i < filters.Length; i++) sourceMeshes[i] = filters[i].sharedMesh;

            // The old G2 all-encompassing box would make bombs explode on an
            // invisible roof even after a section had been destroyed.
            var oldCollider = GetComponent<BoxCollider>();
            if (oldCollider != null) oldCollider.enabled = false;

            AddPart("Bunker", new Vector3(0, 5.7f, 0), new Vector3(35, 9.2f, 26), 3);
            AddPart("Fuel tank south", new Vector3(-24, 4.9f, -6.3f), new Vector3(9, 9, 8.5f), 1);
            AddPart("Fuel tank north", new Vector3(-24, 4.9f, 6.3f), new Vector3(9, 9, 8.5f), 1);
            AddPart("Supply crates", new Vector3(25.5f, 4.5f, 2), new Vector3(17, 8, 26), 2);
        }

        private void AddPart(string label, Vector3 center, Vector3 size, int hp)
        {
            var go = new GameObject(label + " / HP " + hp);
            go.transform.SetParent(transform, false);
            var part = go.AddComponent<GroundDepotPart>();
            part.Configure(this, center, size, hp);
            parts.Add(part);
        }

        public void OnPartDestroyed(GroundDepotPart part)
        {
            DestroyedParts++;
            RebuildSurvivingGeometry();
            Debug.Log("G3: " + name + " / " + part.name + " destroyed (" +
                DestroyedParts + "/" + TotalParts + ")", this);
        }

        private void RebuildSurvivingGeometry()
        {
            // G2 FBX combines geographically distant objects by material. On a
            // kill we clip just the triangles whose centroids belong to a dead
            // zone. Runtime mesh clones, not prefab or asset mutations.
            for (int i = 0; i < filters.Length; i++)
            {
                var source = sourceMeshes[i];
                if (source == null || !source.isReadable) continue;
                var vertices = source.vertices;
                var clone = Instantiate(source);
                bool changed = false;
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    int[] triangles = source.GetTriangles(sub);
                    var surviving = new List<int>(triangles.Length);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        Vector3 midpoint = (vertices[triangles[t]] + vertices[triangles[t + 1]] +
                            vertices[triangles[t + 2]]) / 3f;
                        Vector3 rootLocal = transform.InverseTransformPoint(
                            filters[i].transform.TransformPoint(midpoint));
                        bool removed = false;
                        foreach (var part in parts)
                        {
                            if (part.IsDestroyed && part.LocalBounds.Contains(rootLocal))
                            { removed = true; break; }
                        }
                        if (removed) { changed = true; continue; }
                        surviving.Add(triangles[t]);
                        surviving.Add(triangles[t + 1]);
                        surviving.Add(triangles[t + 2]);
                    }
                    clone.SetTriangles(surviving, sub, false);
                }
                if (changed)
                {
                    clone.RecalculateBounds();
                    filters[i].sharedMesh = clone;
                    if (runtimeMeshes[i] != null) Destroy(runtimeMeshes[i]);
                    runtimeMeshes[i] = clone;
                }
                else Destroy(clone);
            }
        }

        private void OnDestroy()
        {
            if (runtimeMeshes == null) return;
            foreach (var mesh in runtimeMeshes)
                if (mesh != null) Destroy(mesh);
        }
    }
}
