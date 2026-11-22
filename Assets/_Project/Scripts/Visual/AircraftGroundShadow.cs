using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Decorative shadow for the flat Battlefield. No physics queries or shadow maps.
    [DisallowMultipleComponent]
    public sealed class AircraftGroundShadow : MonoBehaviour
    {
        [SerializeField] private Material shadowMaterial;
        [SerializeField, Min(0.1f)] private float baseWidth = 6.5f;
        [SerializeField, Min(0.1f)] private float baseLength = 10.5f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.30f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.09f;
        [SerializeField, Min(0.1f)] private float fadeHeight = 28f;
        [SerializeField] private float groundY = 0.12f;
        [SerializeField] private Vector2 lightOffsetPerHeight = new Vector2(-0.25f, 0.25f);
        [SerializeField, Min(0f)] private float scalePerHeight = 0.035f;

        private const int Sides = 24;
        private static readonly float[] Radii = { 0.34f, 0.68f, 1f };
        private static readonly float[] Opacities = { 0.95f, 0.43f, 0f };
        private Transform shadow;
        private MeshRenderer shadowRenderer;
        private Mesh mesh;
        private Color32[] colors;
        private byte lastAlpha;
        private bool alphaInitialized;

        // Runtime-only tuning for GroundAttackLab. Serialized shadows in Sandbox
        // still use their existing material and inspector values unchanged.
        public void Configure(Material material, float width, float length,
            float nearAlpha, float farAlpha, float alphaFadeHeight, float surfaceY,
            Vector2 offsetPerHeight, float growthPerHeight)
        {
            if (material == null) throw new System.ArgumentNullException(nameof(material));
            shadowMaterial = material;
            baseWidth = Mathf.Max(.1f, width);
            baseLength = Mathf.Max(.1f, length);
            maxAlpha = Mathf.Clamp01(nearAlpha);
            minAlpha = Mathf.Clamp01(farAlpha);
            fadeHeight = Mathf.Max(.1f, alphaFadeHeight);
            groundY = surfaceY;
            lightOffsetPerHeight = offsetPerHeight;
            scalePerHeight = Mathf.Max(0f, growthPerHeight);
            if (shadow == null) CreateShadow();
            shadowRenderer.sharedMaterial = material;
            alphaInitialized = false;
            UpdateShadow();
            shadowRenderer.enabled = isActiveAndEnabled;
        }

        private void Awake()
        {
            // Inspector-assigned legacy shadows initialize here; runtime-created
            // components receive their material via Configure after AddComponent.
            if (shadowMaterial != null) CreateShadow();
        }

        private void Start()
        {
            if (shadowRenderer != null) return;
            Debug.LogWarning("AircraftGroundShadow needs a shadow material.", this);
            enabled = false;
        }

        private void CreateShadow()
        {
            var owner = new GameObject("GroundShadow");
            shadow = owner.transform;
            shadow.SetParent(transform, false);
            mesh = CreateMesh();
            colors = new Color32[mesh.vertexCount];
            owner.AddComponent<MeshFilter>().sharedMesh = mesh;
            shadowRenderer = owner.AddComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = shadowMaterial;
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
            shadowRenderer.lightProbeUsage = LightProbeUsage.Off;
            shadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            UpdateShadow();
        }

        private void OnEnable()
        {
            if (shadowRenderer == null) return;
            UpdateShadow();
            shadowRenderer.enabled = true;
        }

        private void OnDisable()
        {
            if (shadowRenderer != null) shadowRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (shadowRenderer != null) UpdateShadow();
        }

        private void UpdateShadow()
        {
            var p = transform.position;
            float height = Mathf.Max(0f, p.y - groundY);
            shadow.SetPositionAndRotation(
                new Vector3(p.x + lightOffsetPerHeight.x * height, groundY,
                    p.z + lightOffsetPerHeight.y * height),
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            float scale = 1f + height * scalePerHeight;
            shadow.localScale = new Vector3(baseWidth * scale, 1f, baseLength * scale);

            float opacity = Mathf.Lerp(maxAlpha, minAlpha, Mathf.Clamp01(height / fadeHeight));
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);
            if (alphaInitialized && alpha == lastAlpha) return;
            alphaInitialized = true;
            lastAlpha = alpha;
            colors[0] = Shade(alpha);
            for (int r = 0; r < Radii.Length; r++)
            for (int i = 0; i < Sides; i++)
                colors[1 + r * Sides + i] = Shade((byte)Mathf.RoundToInt(alpha * Opacities[r]));
            mesh.colors32 = colors;
        }

        private static Color32 Shade(byte alpha) => new Color32(25, 37, 29, alpha);

        private static Mesh CreateMesh()
        {
            var vertices = new Vector3[1 + Radii.Length * Sides];
            var indices = new int[3 * Sides * (1 + (Radii.Length - 1) * 2)];
            int at = 0;
            for (int r = 0; r < Radii.Length; r++)
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                float radius = Radii[r] * 0.5f;
                vertices[1 + r * Sides + i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                indices[at++] = 0; indices[at++] = 1 + next; indices[at++] = 1 + i;
                for (int r = 0; r < Radii.Length - 1; r++)
                {
                    int a = 1 + r * Sides + i, b = 1 + r * Sides + next;
                    int c = a + Sides, d = b + Sides;
                    indices[at++] = a; indices[at++] = b; indices[at++] = c;
                    indices[at++] = b; indices[at++] = d; indices[at++] = c;
                }
            }
            var result = new Mesh { name = "Aircraft soft shadow" };
            result.vertices = vertices;
            result.triangles = indices;
            result.RecalculateBounds();
            return result;
        }

        private void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
            if (mesh != null) Destroy(mesh);
        }
    }
}
