using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Presentation only. Nine purpose-built damaged fragments, not intact
    // vehicle components; no colliders, damage receivers or radar contacts.
    public sealed class PicketDestructionFx : MonoBehaviour
    {
        private const float Lifetime = 4f;
        private const float FadeStart = 3.25f;
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private Terrain terrain;
        private Material particleMaterial, charMaterial;
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

        public static GameObject Spawn(Transform vehicle, Terrain terrain, Vector3 marchVelocity)
        {
            if (vehicle == null) return null;
            var root = new GameObject("Picket destruction / debris and blast");
            SceneManager.MoveGameObjectToScene(root, vehicle.gameObject.scene);
            root.transform.position = vehicle.position;
            var fx = root.AddComponent<PicketDestructionFx>();
            fx.terrain = terrain;
            fx.rng = new System.Random(System.Guid.NewGuid().GetHashCode());
            fx.BuildKit(vehicle, marchVelocity);

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

        // Hand-shaped low-poly kit. Vertices are in fragment-local metres;
        // the folds and jagged outlines are baked into each mesh. Intact Picket
        // art is used only as a palette, never as a source of whole components.
        private void BuildKit(Transform vehicle, Vector3 marchVelocity)
        {
            Material hull = FindMaterial(vehicle, "Hull");
            Material rail = FindMaterial(vehicle, "RailLauncher") ?? hull;
            Material metal = FindMaterial(vehicle, "BodyMetal") ?? hull;
            Material rubber = FindMaterial(vehicle, "Rubber") ?? metal;
            if (hull == null) return;
            charMaterial = new Material(hull) { name = "Picket torn / charred metal" };
            charMaterial.SetColor("_BaseColor", new Color(.13f, .115f, .09f));
            // The ordered contours are intentionally irregular, with raised
            // corners, a kinked centre and dark torn cross-sections.
            Plate(vehicle, marchVelocity, "Torn rear deck", V(-1.2f, 2.5f, -3.6f), hull, .38f, .55f,
                V(-1.8f,.1f,-2.1f), V(.15f,-.2f,-2.6f), V(1.6f,.5f,-1.5f),
                V(1.05f,-.25f,.2f), V(1.8f,.75f,1.55f), V(-.5f,.05f,2.05f), V(-1.6f,-.5f,.7f));
            Plate(vehicle, marchVelocity, "Ruptured chassis", V(1.15f, 2.2f, -.8f), hull, .48f, -.38f,
                V(-1.65f,.3f,-1.4f), V(.4f,-.3f,-2.1f), V(1.8f,.4f,-.55f),
                V(.85f,.1f,.45f), V(1.3f,.85f,1.85f), V(-.75f,-.3f,1.4f), V(-1.45f,.45f,.4f));
            Plate(vehicle, marchVelocity, "Crushed cab", V(-.55f, 3.25f, 3.9f), hull, .85f, .9f,
                V(-1.65f,-.35f,-1.25f), V(.3f,.25f,-1.55f), V(1.45f,-.4f,-.55f),
                V(1.1f,.65f,.8f), V(.1f,.5f,1.6f), V(-1.55f,-.1f,.95f));
            Plate(vehicle, marchVelocity, "Broken launcher rail", V(.85f, 4.6f, -3.8f), rail, .45f, .3f,
                V(-.65f,.1f,-2.15f), V(.55f,-.25f,-1.9f), V(.75f,.4f,-.35f),
                V(.35f,.9f,1.7f), V(-.55f,.3f,1.1f), V(-.9f,-.2f,-.25f));
            Plate(vehicle, marchVelocity, "Ripped side armor", V(-2.1f, 2.8f, .9f), hull, .19f, .42f,
                V(-.9f,0f,-.85f), V(.65f,.35f,-1.05f), V(.4f,-.4f,.15f), V(.85f,.2f,1f), V(-.55f,.1f,.65f));
            Plate(vehicle, marchVelocity, "Bent equipment panel", V(2f, 3f, 2.1f), metal, .16f, -.25f,
                V(-.65f,.2f,-.85f), V(.7f,-.25f,-.6f), V(.45f,.4f,.35f), V(-.1f,.1f,.9f), V(-.75f,-.1f,.35f));
            Plate(vehicle, marchVelocity, "Torn launcher skin", V(.25f, 4.8f, 1.2f), rail, .16f, .35f,
                V(-.55f,-.1f,-.9f), V(.4f,.25f,-.6f), V(.72f,-.25f,.1f), V(.1f,.5f,.95f), V(-.5f,.1f,.35f));
            AddPiece(vehicle, marchVelocity, "Detached wheel L", V(-2.2f, 1.1f, -1.6f),
                MakeWheel(), rubber, metal, true);
            AddPiece(vehicle, marchVelocity, "Detached wheel R", V(2.2f, 1.1f, 4.35f),
                MakeWheel(), rubber, metal, true);
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static Material FindMaterial(Transform vehicle, string label)
        {
            foreach (var renderer in vehicle.GetComponentsInChildren<MeshRenderer>(false))
                if (renderer.name == label) return renderer.sharedMaterial;
            return null;
        }

        private void Plate(Transform vehicle, Vector3 marchVelocity, string label, Vector3 anchor,
            Material paint, float thickness, float fold, params Vector3[] rim)
        {
            // All contours wind counter-clockwise in the X/Z plane. A central
            // fold and a capped underside give actual volume, not paper cards.
            var mesh = new FragmentMesh();
            Vector3 centre = Vector3.up * fold;
            Vector3 depth = Vector3.down * thickness;
            for (int i = 0; i < rim.Length; i++)
            {
                Vector3 a = rim[i], b = rim[(i + 1) % rim.Length];
                mesh.Triangle(centre, b, a, false);
                mesh.Triangle(centre + depth, a + depth, b + depth, true);
                mesh.Triangle(a, b, b + depth, true);
                mesh.Triangle(a, b + depth, a + depth, true);
            }
            AddPiece(vehicle, marchVelocity, label, anchor, mesh.Build(label), paint, charMaterial, false);
        }

        private static Mesh MakeWheel()
        {
            var mesh = new FragmentMesh();
            float[] xs = { -.53f, -.52f, -.35f, .35f, .52f, .53f };
            float[] rs = { .48f, .85f, 1.1f, 1.1f, .85f, .48f };
            const int sides = 12;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                for (int ring = 0; ring < xs.Length - 1; ring++)
                {
                    Vector3 p = Ring(xs[ring], rs[ring], a), q = Ring(xs[ring], rs[ring], b);
                    Vector3 r = Ring(xs[ring + 1], rs[ring + 1], b), s = Ring(xs[ring + 1], rs[ring + 1], a);
                    mesh.Triangle(p, q, r, false); mesh.Triangle(p, r, s, false);
                }
                mesh.Triangle(V(xs[0], 0f, 0f), Ring(xs[0], rs[0], b), Ring(xs[0], rs[0], a), true);
                int last = xs.Length - 1;
                mesh.Triangle(V(xs[last], 0f, 0f), Ring(xs[last], rs[last], a), Ring(xs[last], rs[last], b), true);
            }
            return mesh.Build("Detached wheel");
        }

        private static Vector3 Ring(float x, float r, float angle) => V(x, r * Mathf.Cos(angle), r * Mathf.Sin(angle));

        private sealed class FragmentMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> painted = new List<int>(), dark = new List<int>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, bool inner)
            {
                var indices = inner ? dark : painted;
                indices.Add(vertices.Count); vertices.Add(a);
                indices.Add(vertices.Count); vertices.Add(b);
                indices.Add(vertices.Count); vertices.Add(c);
            }

            public Mesh Build(string label)
            {
                var mesh = new Mesh { name = label };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(painted, 0); mesh.SetTriangles(dark, 1);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }

        private void AddPiece(Transform vehicle, Vector3 marchVelocity, string label, Vector3 anchor,
            Mesh mesh, Material paint, Material inner, bool wheel)
        {
            ownedMeshes.Add(mesh);
            var go = new GameObject(label);
            Transform t = go.transform;
            t.SetParent(transform, false);
            t.SetPositionAndRotation(vehicle.TransformPoint(anchor), vehicle.rotation);
            var renderer = go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer.sharedMaterials = new[] { paint, inner };

            Vector3 radial = Vector3.ProjectOnPlane(t.position - vehicle.position, Vector3.up).normalized;
            Vector3 noise = new Vector3(Next(-1f, 1f), 0f, Next(-1f, 1f));
            Vector3 direction = (radial + noise * .35f).normalized;
            if (direction.sqrMagnitude < .01f) direction = Vector3.right;
            float speed = wheel ? Next(27f, 42f) : Next(19f, 36f);
            Vector3 velocity = direction * speed + Vector3.up * Next(26f, 36f) + marchVelocity * .35f;
            Vector3 spin;
            if (wheel)
            {
                t.rotation = Quaternion.LookRotation(direction, Vector3.up);
                spin = t.right * (speed / 1.1f * Mathf.Rad2Deg);
            }
            else spin = new Vector3(Next(-330f, 330f), Next(-240f, 240f), Next(-330f, 330f));
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
                // Shrink in place rather than leaving an opaque upright wreck.
                if (age > FadeStart)
                    piece.Pivot.localScale = Vector3.one * Mathf.Max(0f, (Lifetime - age) / (Lifetime - FadeStart));
            }
        }

        private void Step(Piece piece, float dt)
        {
            Transform t = piece.Pivot;
            piece.Velocity += Vector3.down * (58f * dt);
            t.position += piece.Velocity * dt;
            float spin = piece.Spin.magnitude;
            if (spin > .1f) t.rotation = Quaternion.AngleAxis(spin * dt, piece.Spin / spin) * t.rotation;
            float ground = GroundHeight(t.position);
            float bottom = piece.Renderer.bounds.min.y;
            if (bottom >= ground) return;
            t.position += Vector3.up * (ground - bottom);
            if (piece.Velocity.y < 0f)
                piece.Velocity.y = Mathf.Abs(piece.Velocity.y) > 3f ? -piece.Velocity.y * (piece.Wheel ? .23f : .13f) : 0f;
            float friction = Mathf.Exp(-(piece.Wheel ? 2.6f : 9f) * dt);
            piece.Velocity.x *= friction;
            piece.Velocity.z *= friction;
            piece.Spin *= friction;
            // Plates settle on a broad face instead of balancing on one edge.
            if (!piece.Wheel && piece.Velocity.y < 1f)
            {
                Vector3 up = Vector3.Dot(t.up, Vector3.up) >= 0f ? Vector3.up : Vector3.down;
                Quaternion flat = Quaternion.FromToRotation(t.up, up) * t.rotation;
                t.rotation = Quaternion.Slerp(t.rotation, flat, 1f - Mathf.Exp(-12f * dt));
                t.position += Vector3.up * (ground - piece.Renderer.bounds.min.y);
            }
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
            if (charMaterial != null) Destroy(charMaterial);
        }
    }
}
