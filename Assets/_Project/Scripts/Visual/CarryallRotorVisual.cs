using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // V8/V9A+ visual contract. Split only the existing blade triangles on the preview
    // instance. Never edit imported meshes or the shared accepted visual prefab.
    [DisallowMultipleComponent]
    public sealed class CarryallRotorVisual : MonoBehaviour
    {
        private sealed class Rotor
        {
            public MeshFilter filter;
            public Mesh source, shell, blades;
            public Transform pivot;
            public Vector3 center;
            public Material paint;
            public float sign;
        }
        private readonly List<Rotor> rotors = new List<Rotor>();
        private bool initialized;
        private float phase;

        public bool Initialize()
        {
            if (initialized) return true;
            try
            {
                foreach (string end in new[] { "Front", "Rear" })
                foreach (string side in new[] { "L", "R" })
                    Prepare(end, side);
                foreach (var rotor in rotors)
                {
                    var go = new GameObject("Animated blades", typeof(MeshFilter), typeof(MeshRenderer));
                    rotor.pivot = go.transform;
                    rotor.pivot.SetParent(rotor.filter.transform, false);
                    rotor.pivot.localPosition = rotor.center;
                    go.GetComponent<MeshFilter>().sharedMesh = rotor.blades;
                    var original = rotor.filter.GetComponent<MeshRenderer>();
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = rotor.paint;
                    renderer.shadowCastingMode = original.shadowCastingMode;
                    renderer.receiveShadows = original.receiveShadows;
                    go.layer = original.gameObject.layer;
                    rotor.filter.sharedMesh = rotor.shell;
                }
                initialized = true;
                ResetPhase();
                Debug.Log("CA01: 4 rotors initialized; 84 original blade triangles each. Shared mesh assets untouched.", this);
                return true;
            }
            catch (Exception ex)
            {
                Release();
                Debug.LogError("CA01 rotor visual: " + ex, this);
                return false;
            }
        }

        private void Prepare(string end, string side)
        {
            var tr = transform.Find("Visual/Engine" + end + side);
            var socket = transform.Find($"EngineSockets/Exhaust_{side}_{end}");
            var filter = tr != null ? tr.GetComponent<MeshFilter>() : null;
            var renderer = tr != null ? tr.GetComponent<MeshRenderer>() : null;
            if (socket == null || filter == null || renderer == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                throw new InvalidOperationException("Missing readable engine mesh/socket: " + end + side);
            var source = filter.sharedMesh;
            var materials = renderer.sharedMaterials;
            if (materials.Length != source.subMeshCount)
                throw new InvalidOperationException("Unexpected material slots: " + end + side);
            var center = transform.InverseTransformPoint(socket.position);
            center.y = 3.165f;
            var localCenter = tr.InverseTransformPoint(transform.TransformPoint(center));
            var vertices = source.vertices;
            var staticIndices = new List<int[]>();
            var bladeVertices = new List<Vector3>();
            Material paint = null;
            int total = 0;
            bool BladeVertex(Vector3 local)
            {
                Vector3 p = transform.InverseTransformPoint(tr.TransformPoint(local));
                float dx = p.x - center.x, dz = p.z - center.z;
                return p.y >= 3.079f && p.y <= 3.251f && dx * dx + dz * dz < 6.25f;
            }
            for (int slot = 0; slot < source.subMeshCount; slot++)
            {
                var indices = source.GetTriangles(slot);
                var keep = new List<int>(indices.Length);
                bool steel = materials[slot] != null && materials[slot].name == "CA01_Steel";
                total += indices.Length;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i+1], c = indices[i+2];
                    if (steel && BladeVertex(vertices[a]) && BladeVertex(vertices[b]) && BladeVertex(vertices[c]))
                    {
                        paint = materials[slot];
                        bladeVertices.Add(vertices[a] - localCenter);
                        bladeVertices.Add(vertices[b] - localCenter);
                        bladeVertices.Add(vertices[c] - localCenter);
                    }
                    else { keep.Add(a); keep.Add(b); keep.Add(c); }
                }
                staticIndices.Add(keep.ToArray());
            }
            if (bladeVertices.Count != 84 * 3 || paint == null)
                throw new InvalidOperationException($"Expected 84 blade triangles in {end}{side}, got {bladeVertices.Count / 3}.");
            int remaining = 0;
            foreach (var indices in staticIndices) remaining += indices.Length;
            if (remaining + bladeVertices.Count != total)
                throw new InvalidOperationException("Rotor triangle conservation failed.");
            var rotor = new Rotor { filter = filter, source = source, center = localCenter, paint = paint,
                sign = (end == "Front") == (side == "L") ? 1f : -1f };
            rotors.Add(rotor);
            rotor.shell = Instantiate(source);
            rotor.shell.name = source.name + " / preview shell";
            for (int slot = 0; slot < staticIndices.Count; slot++) rotor.shell.SetTriangles(staticIndices[slot], slot, false);
            rotor.blades = new Mesh { name = source.name + " / preview blades", vertices = bladeVertices.ToArray() };
            var triangles = new int[bladeVertices.Count];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            rotor.blades.triangles = triangles;
            rotor.blades.RecalculateNormals(); rotor.blades.RecalculateBounds();
        }

        public void Advance(float degrees)
        { phase = Mathf.Repeat(phase + Mathf.Max(0f, degrees), 360f); Apply(); }
        public void ResetPhase() { phase = 0f; Apply(); }
        private void Apply()
        {
            foreach (var rotor in rotors)
                if (rotor.pivot != null) rotor.pivot.localRotation = Quaternion.AngleAxis(phase * rotor.sign, Vector3.up);
        }
        private void OnDestroy() => Release();
        private void Release()
        {
            foreach (var rotor in rotors)
            {
                if (rotor.filter != null && rotor.filter.sharedMesh == rotor.shell) rotor.filter.sharedMesh = rotor.source;
                if (rotor.pivot != null) { rotor.pivot.gameObject.SetActive(false); Destroy(rotor.pivot.gameObject); }
                if (rotor.shell != null) Destroy(rotor.shell);
                if (rotor.blades != null) Destroy(rotor.blades);
            }
            rotors.Clear(); initialized = false;
        }
    }
}
