using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Four world-space corner brackets around an approximate six-rocket footprint.
    // Never a lock-on or six guaranteed impact points. GroundAttackLab only.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(235)] // After aircraft, rocket rack and camera updates.
    public sealed class NursImpactAreaMarker : MonoBehaviour
    {
        private const float RefreshSeconds = .18f;
        private const float Margin = 9f; // blast radius + buffer around spread extrema
        private const float CornerLeg = 11f;
        private const float LineWidth = .48f;
        private const float SurfaceOffset = .22f;
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        private WeaponSelection selection;
        private CabrageManeuver cabrage;
        private FlightController flight;
        private NursRocketRack rack;
        private GameObject visual;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Material material;
        private readonly Vector3[] vertices = new Vector3[8 * 4];
        private float nextRefreshAt;
        private bool wasCommitted;
        private bool hasArea;
        private Vector3 areaCenter;

        public bool HasArea => hasArea;
        public Vector3 AreaCenter => areaCenter;

        public void Configure(WeaponSelection weapons, CabrageManeuver maneuver,
            FlightController controller, NursRocketRack launcher)
        {
            if (visual != null) return;
            selection = weapons;
            cabrage = maneuver;
            flight = controller;
            rack = launcher;
            if (selection == null || cabrage == null || flight == null || rack == null)
            {
                Debug.LogError("G4c: NURS area needs selector, cabrage, flight and rocket rack.", this);
                enabled = false;
                return;
            }
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null)
            {
                Debug.LogError("G4c: AirCombat/SandboxEffect shader is missing.", this);
                enabled = false;
                return;
            }
            material = new Material(shader) { name = "G4c NURS area brackets (runtime)" };
            visual = new GameObject("NURS approximate impact area / four corners");
            mesh = new Mesh { name = "NURS four corners" };
            mesh.MarkDynamic();
            var indices = new int[8 * 6];
            var colors = new Color32[vertices.Length];
            for (int i = 0; i < 8; i++)
            {
                int v = i * 4, t = i * 6;
                indices[t] = v;
                indices[t + 1] = v + 1;
                indices[t + 2] = v + 2;
                indices[t + 3] = v + 2;
                indices[t + 4] = v + 1;
                indices[t + 5] = v + 3;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] =
                    new Color32(201, 219, 219, 160);
            }
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.triangles = indices;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (visual == null || selection.Selected != WeaponMode.Nurs)
            {
                Hide();
                wasCommitted = false;
                return;
            }
            bool committed = cabrage.IsCommitted;
            // Snapshot when the player commits. The landing area must not chase
            // the moving aircraft after the first rockets have left the pylons.
            if ((committed && !wasCommitted) || (!committed && Time.time >= nextRefreshAt))
            {
                RefreshArea();
                nextRefreshAt = Time.time + RefreshSeconds;
            }
            wasCommitted = committed;
            if (meshRenderer != null) meshRenderer.enabled = hasArea;
        }

        private void RefreshArea()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .001f) { Hide(); return; }
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            int samples = 0;
            int count = rack.RocketCount;
            for (int i = 0; i < count; i++)
            {
                IncludePrediction(i, 0f, 0f, forward, right,
                    ref minX, ref maxX, ref minZ, ref maxZ, ref samples);
                // Bounding extremes at the beginning, middle and end of the
                // salvo. Random per-rocket errors still vary within this area.
                if (i != 0 && i != count / 2 && i != count - 1) continue;
                for (int yaw = -1; yaw <= 1; yaw += 2)
                for (int pitch = -1; pitch <= 1; pitch += 2)
                    IncludePrediction(i, yaw * rack.YawSpreadDegrees,
                        pitch * rack.PitchSpreadDegrees, forward, right,
                        ref minX, ref maxX, ref minZ, ref maxZ, ref samples);
            }
            if (samples == 0) { Hide(); return; }
            minX -= Margin; maxX += Margin;
            minZ -= Margin; maxZ += Margin;
            areaCenter = transform.position + right * ((minX + maxX) * .5f)
                + forward * ((minZ + maxZ) * .5f);
            areaCenter.y = 0f;

            Vector3 centre = areaCenter;
            Vector3[] corners =
            {
                centre + right * minX - right * ((minX + maxX) * .5f)
                    + forward * (minZ - (minZ + maxZ) * .5f),
                centre + right * maxX - right * ((minX + maxX) * .5f)
                    + forward * (minZ - (minZ + maxZ) * .5f),
                centre + right * maxX - right * ((minX + maxX) * .5f)
                    + forward * (maxZ - (minZ + maxZ) * .5f),
                centre + right * minX - right * ((minX + maxX) * .5f)
                    + forward * (maxZ - (minZ + maxZ) * .5f)
            };
            // Draw exactly eight short segments: two inward-facing legs per corner.
            float width = maxX - minX, length = maxZ - minZ;
            float legX = Mathf.Min(CornerLeg, width * .35f);
            float legZ = Mathf.Min(CornerLeg, length * .35f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = corners[i];
                Vector3 inwardX = (i == 0 || i == 3 ? right : -right) * legX;
                Vector3 inwardZ = (i <= 1 ? forward : -forward) * legZ;
                WriteSegment(i * 2, corner, corner + inwardX);
                WriteSegment(i * 2 + 1, corner, corner + inwardZ);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            hasArea = true;
        }

        private void IncludePrediction(int index, float yawError, float pitchError,
            Vector3 forward, Vector3 right, ref float minX, ref float maxX,
            ref float minZ, ref float maxZ, ref int samples)
        {
            float at = rack.ReleaseAt(index);
            float climb01 = Mathf.Clamp01(at / Mathf.Max(.1f, cabrage.ClimbDuration));
            Vector3 origin = transform.position + forward * (flight.CurrentSpeed * at)
                + Vector3.up * (cabrage.ClimbHeight * Mathf.SmoothStep(0f, 1f, climb01))
                + transform.TransformVector(rack.PylonAt(index));
            Vector3 velocity = NursBallistics.LaunchVelocity(forward, right,
                flight.CurrentSpeed, rack.MuzzleSpeed, cabrage.PitchAtClimbTime(at),
                yawError, pitchError);
            if (!TryPredictHit(origin, velocity, out Vector3 point)) return;
            Vector3 relative = point - transform.position;
            float x = Vector3.Dot(relative, right);
            float z = Vector3.Dot(relative, forward);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
            samples++;
        }

        private bool TryPredictHit(Vector3 start, Vector3 velocity, out Vector3 point)
        {
            Vector3 position = start;
            for (float elapsed = 0f; elapsed < 7f; elapsed += NursBallistics.IntegrationStep)
            {
                Vector3 next = NursBallistics.Integrate(ref velocity, position,
                    NursBallistics.IntegrationStep);
                Vector3 delta = next - position;
                float distance = delta.magnitude;
                if (distance > .0001f)
                {
                    int count = Physics.RaycastNonAlloc(position, delta / distance, Hits,
                        distance, ~0, QueryTriggerInteraction.Ignore);
                    float closest = float.PositiveInfinity;
                    point = default;
                    for (int i = 0; i < count; i++)
                    {
                        var hit = Hits[i];
                        if (hit.collider == null || hit.distance >= closest ||
                            hit.collider.transform.IsChildOf(transform)) continue;
                        var target = hit.collider.GetComponentInParent<DamageableTarget>();
                        if (target != null && target.Domain == CombatTargetDomain.Air) continue;
                        closest = hit.distance;
                        point = hit.point;
                    }
                    if (!float.IsPositiveInfinity(closest)) return true;
                }
                if (next.y <= 0f)
                {
                    float fraction = Mathf.Clamp01(position.y /
                        Mathf.Max(.0001f, position.y - next.y));
                    point = Vector3.Lerp(position, next, fraction);
                    return true;
                }
                position = next;
            }
            point = default;
            return false;
        }

        private void WriteSegment(int index, Vector3 a, Vector3 b)
        {
            a = SnapToSurface(a);
            b = SnapToSurface(b);
            Vector3 direction = b - a;
            Vector3 across = Vector3.Cross(Vector3.up, direction.normalized) * (LineWidth * .5f);
            int v = index * 4;
            vertices[v] = a - across;
            vertices[v + 1] = a + across;
            vertices[v + 2] = b - across;
            vertices[v + 3] = b + across;
        }

        private Vector3 SnapToSurface(Vector3 point)
        {
            Vector3 start = new Vector3(point.x, 300f, point.z);
            int count = Physics.RaycastNonAlloc(start, Vector3.down, Hits, 340f,
                ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            float surface = 0f;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.collider == null || hit.distance >= closest ||
                    hit.collider.transform.IsChildOf(transform)) continue;
                var target = hit.collider.GetComponentInParent<DamageableTarget>();
                if (target != null && target.Domain == CombatTargetDomain.Air) continue;
                closest = hit.distance;
                surface = hit.point.y;
            }
            point.y = surface + SurfaceOffset;
            return point;
        }

        private void Hide()
        {
            hasArea = false;
            if (meshRenderer != null) meshRenderer.enabled = false;
        }

        private void OnDisable() { Hide(); }
        private void OnDestroy()
        {
            if (visual != null) Destroy(visual);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
