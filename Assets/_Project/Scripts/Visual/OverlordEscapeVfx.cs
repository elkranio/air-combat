using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Presentation only: charge/boost inputs come from the target state machine.
    // The authored exhaust sockets point local +Z aft. No new prefab assets.
    internal sealed class OverlordEscapeVfx
    {
        private readonly Transform[] plumes = new Transform[2];
        private readonly Mesh mesh;
        private readonly Material material;

        public OverlordEscapeVfx(Transform visual)
        {
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null)
            {
                Debug.LogWarning("AE01C: escape plume shader missing; nozzle emission remains active.");
                return;
            }
            mesh = BuildPlume();
            material = new Material(shader) { name = "AE01 escape thrust (runtime)" };
            for (int i = 0; i < plumes.Length; i++)
            {
                var socket = visual.Find(i == 0 ? "EscapeExhaustL" : "EscapeExhaustR");
                if (socket == null) continue;
                var go = new GameObject("AE01 escape thrust", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(socket, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                plumes[i] = go.transform;
                go.SetActive(false);
            }
        }

        public void Charge(float charge, float pulse)
        {
            float amount = Mathf.SmoothStep(0f, 1f, charge);
            // From small pulsing nozzle cores to short, bright exhausts.
            Show(amount > .025f, Mathf.Lerp(.35f, .88f, amount) * pulse,
                Mathf.Lerp(.3f, 4f, amount) * pulse, 0f);
        }

        public void Boost(float thrust, float timer)
        {
            // Grow the visible jet with the actual S-curve thrust, not an
            // instantaneous maximum-length flame at the state transition.
            Show(true, Mathf.Lerp(.88f, 1.18f, thrust),
                Mathf.Lerp(4f, 26f, thrust), .06f * Mathf.Sin(timer * 33f));
        }

        private void Show(bool active, float width, float length, float flicker)
        {
            foreach (var plume in plumes)
            {
                if (plume == null) continue;
                if (plume.gameObject.activeSelf != active) plume.gameObject.SetActive(active);
                if (active) plume.localScale = new Vector3(width, width, length * (1f + flicker));
            }
        }

        public void Clear() => Show(false, 1f, 1f, 0f);

        public void Dispose()
        {
            foreach (var plume in plumes) if (plume != null) Object.Destroy(plume.gameObject);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }

        private static Mesh BuildPlume()
        {
            const int sides = 10;
            float[] length = { 0f, .14f, .45f, .78f, 1f };
            float[] radius = { .68f, 1f, .72f, .34f, .01f };
            Color[] ink = { new Color(.88f, 1f, 1f, 1f), new Color(.45f, .95f, 1f, 1f),
                new Color(.12f, .68f, 1f, .9f), new Color(.08f, .4f, 1f, .42f), new Color(.1f, .35f, 1f, 0f) };
            var vertices = new Vector3[sides * length.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[(length.Length - 1) * sides * 6];
            for (int ring = 0; ring < length.Length; ring++)
            for (int side = 0; side < sides; side++)
            {
                float a = 2f * Mathf.PI * side / sides;
                int i = ring * sides + side;
                vertices[i] = new Vector3(radius[ring] * Mathf.Cos(a), radius[ring] * Mathf.Sin(a), length[ring]);
                colors[i] = ink[ring];
                if (ring == length.Length - 1) continue;
                int next = ring * sides + (side + 1) % sides, t = i * 6;
                triangles[t] = i; triangles[t + 1] = next; triangles[t + 2] = i + sides;
                triangles[t + 3] = next; triangles[t + 4] = next + sides; triangles[t + 5] = i + sides;
            }
            var result = new Mesh { name = "AE01 escape thrust mesh" };
            result.vertices = vertices; result.colors = colors; result.triangles = triangles;
            result.RecalculateBounds();
            return result;
        }
    }
}
