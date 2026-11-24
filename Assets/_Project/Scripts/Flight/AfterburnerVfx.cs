using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public sealed class AfterburnerVfx : MonoBehaviour
    {
        [SerializeField] private Afterburner afterburner;
        [SerializeField] private Material effectMaterial;
        [SerializeField] private Vector3[] nozzlePositions;
        [SerializeField] private JetChassis chassis; // optional; old scenes keep nozzlePositions
        [SerializeField, Min(0.05f)] private float flameRadius = 0.42f;
        [SerializeField, Min(0.1f)] private float flameLength = 7f;
        [SerializeField] private bool playIgnitionSound;
        [SerializeField, Range(0f, 1f)] private float ignitionVolume = 0.18f;
        [Header("Tight turn feedback")]
        [SerializeField, Range(.1f, 1f)] private float maneuverLengthScale = .42f;
        [SerializeField, Range(.1f, 1f)] private float maneuverWidthScale = .65f;
        [SerializeField, Range(0f, 60f)] private float maneuverYaw = 25f;
        private Transform[] plumes;
        private Mesh flameMesh;
        private Mesh maneuverMesh;
        private AudioSource source;
        private AudioClip ignition;
        private bool wasActive;
        private bool wasManeuvering;
        private bool maneuverFlames;

        // Optional maneuver visual override; never changes boost fuel or thrust.
        public void SetManeuverFlames(bool active) { maneuverFlames = active; }
        public void BindChassis(JetChassis visualChassis) { chassis = visualChassis; }

        private void Awake()
        {
            bool useSockets = chassis != null && chassis.EngineLeft != null && chassis.EngineRight != null;
            if (afterburner == null || effectMaterial == null || (!useSockets && nozzlePositions == null)) return;
            flameMesh = BuildFlame();
            plumes = new Transform[useSockets ? 2 : nozzlePositions.Length];
            for (int i = 0; i < plumes.Length; i++)
            {
                var plume = new GameObject("Afterburner Flame " + i);
                var socket = useSockets ? (i == 0 ? chassis.EngineLeft : chassis.EngineRight) : null;
                plume.transform.SetParent(socket != null ? socket : transform, false);
                plume.transform.localPosition = socket != null ? Vector3.zero : nozzlePositions[i];
                plume.AddComponent<MeshFilter>().sharedMesh = flameMesh;
                var renderer = plume.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = effectMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                plumes[i] = plume.transform;
                plume.SetActive(false);
            }
            if (playIgnitionSound)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                ignition = MakeIgnition();
            }
        }

        private void LateUpdate()
        {
            if (plumes == null) return;
            // Only an actual powered S request, not the key itself, drives these
            // smaller jets. Legacy Q/E retains the original straight boost effect.
            bool tight = !maneuverFlames && afterburner != null && afterburner.IsManeuvering;
            bool active = tight || maneuverFlames || (afterburner != null && afterburner.IsActive);
            if (active && (!wasActive || tight != wasManeuvering) && source != null)
                source.PlayOneShot(ignition, ignitionVolume);
            if (tight && maneuverMesh == null)
            {
                maneuverMesh = Instantiate(flameMesh);
                maneuverMesh.name = "Tight turn flame";
                Color[] colors = maneuverMesh.colors;
                for (int i = 0; i < colors.Length; i++) colors[i].a *= .65f;
                maneuverMesh.colors = colors;
            }
            for (int i = 0; i < plumes.Length; i++)
            {
                var plume = plumes[i];
                if (plume.gameObject.activeSelf != active) plume.gameObject.SetActive(active);
                if (tight != wasManeuvering)
                {
                    plume.GetComponent<MeshFilter>().sharedMesh = tight ? maneuverMesh : flameMesh;
                    // Mesh extends along local -Z: positive yaw deflects left.
                    plume.localRotation = tight
                        ? Quaternion.Euler(0f, (i % 2 == 0 ? 1f : -1f) * maneuverYaw, 0f)
                        : Quaternion.identity;
                }
                if (!active) continue;
                float t = Time.time * 36f + i * 1.7f;
                float width = flameRadius * (1f + 0.08f * Mathf.Sin(t * 1.3f));
                if (tight) width *= maneuverWidthScale;
                float length = flameLength * (tight ? maneuverLengthScale : 1f);
                plume.localScale = new Vector3(width, width, length * (1f + 0.14f * Mathf.Sin(t)));
            }
            wasActive = active;
            wasManeuvering = tight;
        }

        private void OnDisable()
        {
            maneuverFlames = false;
            wasActive = false;
            if (source != null) source.Stop();
            if (plumes != null)
                foreach (var plume in plumes)
                {
                    if (plume == null) continue;
                    plume.gameObject.SetActive(false);
                    plume.localRotation = Quaternion.identity;
                    plume.GetComponent<MeshFilter>().sharedMesh = flameMesh;
                }
            wasManeuvering = false;
        }

        private void OnDestroy()
        {
            if (plumes != null)
                foreach (var plume in plumes) if (plume != null) Destroy(plume.gameObject);
            if (flameMesh != null) Destroy(flameMesh);
            if (maneuverMesh != null) Destroy(maneuverMesh);
            if (ignition != null) Destroy(ignition);
            if (source != null) Destroy(source);
        }

        private static Mesh BuildFlame()
        {
            const int sides = 8;
            float[] lengths = { 0f, 0.18f, 0.55f, 1f };
            float[] radii = { 0.67f, 1f, 0.58f, 0.01f };
            Color[] colors = { new Color(1f, 1f, 0.88f, 1f), new Color(1f, 0.83f, 0.3f, 1f),
                new Color(1f, 0.39f, 0.08f, 0.9f), new Color(1f, 0.18f, 0.04f, 0f) };
            var vertices = new Vector3[sides * lengths.Length];
            var vertexColors = new Color[vertices.Length];
            var triangles = new int[(lengths.Length - 1) * sides * 6];
            for (int ring = 0; ring < lengths.Length; ring++)
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                int index = ring * sides + i;
                vertices[index] = new Vector3(radii[ring] * Mathf.Cos(angle), radii[ring] * Mathf.Sin(angle), -lengths[ring]);
                vertexColors[index] = colors[ring];
                if (ring == lengths.Length - 1) continue;
                int next = ring * sides + (i + 1) % sides;
                int ti = index * 6;
                triangles[ti] = index; triangles[ti + 1] = next; triangles[ti + 2] = index + sides;
                triangles[ti + 3] = next; triangles[ti + 4] = next + sides; triangles[ti + 5] = index + sides;
            }
            var mesh = new Mesh { name = "Afterburner flame" };
            mesh.vertices = vertices;
            mesh.colors = vertexColors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static AudioClip MakeIgnition()
        {
            const int rate = 22050;
            var data = new float[rate / 2];
            var random = new System.Random(190);
            float filtered = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)(random.NextDouble() * 2 - 1), 0.19f);
                float envelope = Mathf.Clamp01(t / 0.055f) * Mathf.Clamp01((0.5f - t) / 0.29f);
                data[i] = (filtered * 0.8f + Mathf.Sin(2f * Mathf.PI * (87f * t + 85f * t * t)) * 0.24f) * envelope;
            }
            var clip = AudioClip.Create("Afterburner ignition", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
