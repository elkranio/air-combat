using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Presentation only. Real authored Picket mesh islands become independent
    // debris; no rigidbodies, colliders, damage receivers or radar contacts.
    public sealed class PicketDestructionFx : MonoBehaviour
    {
        private const float Lifetime = 6f;
        private const float FadeStart = 4.8f;
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private Terrain terrain;
        private Material particleMaterial;
        private Texture2D particleMask;
        private float age;
        private System.Random rng;

        private sealed class Piece
        {
            public Transform Pivot;
            public MeshRenderer Renderer;
            public Vector3 Velocity, Spin;
            public bool Wheel, Settled;
        }

        private sealed class Island
        {
            public readonly List<int> Triangles = new List<int>();
            public Bounds Bounds;
        }

        public static GameObject Spawn(Transform vehicle, Terrain terrain, Vector3 marchVelocity)
        {
            if (vehicle == null) return null;
            var root = new GameObject("Picket destruction / debris and blast");
            SceneManager.MoveGameObjectToScene(root, vehicle.gameObject.scene);
            root.transform.position = vehicle.position;
            var fx = root.AddComponent<PicketDestructionFx>();
            fx.terrain = terrain;
            fx.rng = new System.Random(System.Guid.NewGuid().GetHashCode());
            Vector3 centre = vehicle.position + vehicle.up * 2.5f;

            // These are the actual named render meshes in the mobile prefab.
            // The hull and all wheel tyres are readable and contain disconnected
            // modeled solids. Split those; keep rail/missile meshes intact.
            foreach (var filter in vehicle.GetComponentsInChildren<MeshFilter>(false))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                switch (filter.name)
                {
                    case "Hull": fx.Split(filter, renderer, 4, false, centre, marchVelocity); break;
                    case "Rubber": fx.Split(filter, renderer, 8, true, centre, marchVelocity); break;
                    case "BodyMetal": fx.Split(filter, renderer, 3, false, centre, marchVelocity); break;
                    case "BodyEquipment": fx.Split(filter, renderer, 2, false, centre, marchVelocity); break;
                    case "BaseLauncher":
                    case "RailLauncher":
                    case "RailLauncherDark":
                    case "MissileL":
                    case "MissileR":
                        fx.CopyWhole(filter, renderer, centre, marchVelocity); break;
                }
            }

            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader != null)
            {
                fx.particleMaterial = RocketImpactFx.CreateMaterial(shader, out fx.particleMask);
                // Deliberately larger than the ordinary radius-8 NURS hit.
                var blast = RocketImpactFx.Spawn(vehicle.position + vehicle.up * 1.3f,
                    fx.particleMaterial, 18f);
                blast.transform.SetParent(root.transform, true);
            }
            return root;
        }

        private static int Find(int[] parents, int i)
        {
            while (parents[i] != i) { parents[i] = parents[parents[i]]; i = parents[i]; }
            return i;
        }

        private static void Union(int[] parents, int a, int b)
        {
            parents[Find(parents, a)] = Find(parents, b);
        }

        private void Split(MeshFilter filter, MeshRenderer source, int limit, bool wheel,
            Vector3 centre, Vector3 marchVelocity)
        {
            Mesh mesh = filter.sharedMesh;
            if (!mesh.isReadable || mesh.subMeshCount != 1)
            {
                // Never try to read a non-readable mesh. A whole rigid part
                // is a safe fallback if the importer changes in the future.
                CopyWhole(filter, source, centre, marchVelocity);
                return;
            }
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            var parents = new int[vertices.Length];
            var positions = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                parents[i] = i;
                // Imported flat normals may split indices at the same point.
                Vector3Int key = Vector3Int.RoundToInt(vertices[i] * 10000f);
                if (positions.TryGetValue(key, out int other)) Union(parents, i, other);
                else positions.Add(key, i);
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Union(parents, triangles[i], triangles[i + 1]);
                Union(parents, triangles[i], triangles[i + 2]);
            }
            var groups = new Dictionary<int, Island>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int key = Find(parents, triangles[i]);
                if (!groups.TryGetValue(key, out Island island))
                {
                    island = new Island { Bounds = new Bounds(vertices[triangles[i]], Vector3.zero) };
                    groups.Add(key, island);
                }
                for (int j = 0; j < 3; j++)
                {
                    int index = triangles[i + j];
                    island.Triangles.Add(index);
                    island.Bounds.Encapsulate(vertices[index]);
                }
            }
            var ordered = new List<Island>(groups.Values);
            ordered.Sort((a, b) => Score(b.Bounds).CompareTo(Score(a.Bounds)));
            for (int i = 0; i < Mathf.Min(limit, ordered.Count); i++)
            {
                Island island = ordered[i];
                if (island.Bounds.size.sqrMagnitude < .15f) continue;
                Vector3 pivot = island.Bounds.center;
                var verts = new Vector3[island.Triangles.Count];
                var indices = new int[verts.Length];
                for (int j = 0; j < verts.Length; j++)
                {
                    verts[j] = vertices[island.Triangles[j]] - pivot;
                    indices[j] = j;
                }
                var part = new Mesh { name = filter.name + " / debris island" };
                part.vertices = verts;
                part.triangles = indices;
                part.RecalculateNormals();
                part.RecalculateBounds();
                ownedMeshes.Add(part);
                AddPiece(filter, source, part, pivot, Vector3.zero, wheel, centre, marchVelocity);
            }
        }

        private static float Score(Bounds b)
        {
            Vector3 s = b.size;
            return s.x * s.y + s.y * s.z + s.z * s.x;
        }

        private void CopyWhole(MeshFilter filter, MeshRenderer source, Vector3 centre, Vector3 marchVelocity)
        {
            Vector3 pivot = filter.sharedMesh.bounds.center;
            AddPiece(filter, source, filter.sharedMesh, pivot, -pivot, false, centre, marchVelocity);
        }

        private void AddPiece(MeshFilter filter, MeshRenderer source, Mesh mesh, Vector3 pivot,
            Vector3 meshOffset, bool wheel, Vector3 centre, Vector3 marchVelocity)
        {
            var go = new GameObject(filter.name + " / flying fragment");
            Transform t = go.transform;
            t.SetParent(transform, false);
            t.SetPositionAndRotation(filter.transform.TransformPoint(pivot), filter.transform.rotation);
            t.localScale = filter.transform.lossyScale;
            var child = new GameObject("Geometry");
            child.transform.SetParent(t, false);
            child.transform.localPosition = meshOffset;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            Vector3 radial = Vector3.ProjectOnPlane(t.position - centre, Vector3.up);
            Vector3 noise = new Vector3(Next(-1f, 1f), 0f, Next(-1f, 1f));
            Vector3 direction = (radial.normalized + noise * .65f).normalized;
            if (direction.sqrMagnitude < .01f) direction = Vector3.right;
            Vector3 velocity = direction * Next(wheel ? 10f : 7f, wheel ? 18f : 15f)
                + Vector3.up * Next(wheel ? 7f : 9f, wheel ? 12f : 18f) + marchVelocity * .35f;
            Vector3 spin = wheel ? t.right * Next(260f, 440f) * (Next(0f, 1f) < .5f ? -1f : 1f)
                : new Vector3(Next(-220f, 220f), Next(-160f, 160f), Next(-220f, 220f));
            pieces.Add(new Piece { Pivot = t, Renderer = renderer, Velocity = velocity, Spin = spin, Wheel = wheel });
        }

        private float Next(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            foreach (Piece piece in pieces)
            {
                if (!piece.Settled && age <= FadeStart)
                {
                    float remaining = Mathf.Min(dt, .15f);
                    while (remaining > .00001f && !piece.Settled)
                    {
                        float step = Mathf.Min(remaining, 1f / 45f);
                        remaining -= step;
                        Step(piece, step);
                    }
                }
                // Opaque low-poly debris: sink out of the terrain instead of
                // swapping shared materials to a transparent rendering mode.
                if (age > FadeStart)
                    piece.Pivot.position -= Vector3.up * (dt * 5f);
            }
        }

        private void Step(Piece piece, float dt)
        {
            Transform t = piece.Pivot;
            piece.Velocity += Vector3.down * (24f * dt);
            t.position += piece.Velocity * dt;
            float spin = piece.Spin.magnitude;
            if (spin > .1f) t.rotation = Quaternion.AngleAxis(spin * dt, piece.Spin / spin) * t.rotation;
            float ground = GroundHeight(t.position);
            float bottom = piece.Renderer.bounds.min.y;
            if (bottom >= ground) return;
            t.position += Vector3.up * (ground - bottom);
            if (piece.Velocity.y < 0f)
                piece.Velocity.y = Mathf.Abs(piece.Velocity.y) > 3f ? -piece.Velocity.y * (piece.Wheel ? .32f : .22f) : 0f;
            float friction = Mathf.Exp(-(piece.Wheel ? 3.2f : 7f) * dt);
            piece.Velocity.x *= friction;
            piece.Velocity.z *= friction;
            piece.Spin *= friction;
            if (piece.Velocity.sqrMagnitude < 1f && piece.Spin.sqrMagnitude < 225f) piece.Settled = true;
        }

        private float GroundHeight(Vector3 point)
        {
            if (terrain == null || terrain.terrainData == null) return transform.position.y;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return origin.y + terrain.terrainData.GetInterpolatedHeight(
                Mathf.Clamp01((point.x - origin.x) / size.x), Mathf.Clamp01((point.z - origin.z) / size.z));
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (particleMask != null) Destroy(particleMask);
        }
    }
}
