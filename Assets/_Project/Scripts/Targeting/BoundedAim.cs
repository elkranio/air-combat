using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Mouse-to-flight-plane aim within a bounded forward sector.
    // Does not alter the cursor, aircraft controls or camera.
    [DisallowMultipleComponent]
    public sealed class BoundedAim : MonoBehaviour
    {
        [SerializeField, Range(5f, 65f)] private float halfAngle = 12f;
        [SerializeField, Min(15f)] private float guideReach = 165f;
        [SerializeField, Min(1f)] private float minimumReticleDistance = 12f;
        [SerializeField, Range(.01f, .25f)] private float aimSmoothing = .065f;

        private Camera viewCamera;
        private Transform aircraft;
        private Material guideMaterial;
        private LineRenderer leftEdge;
        private LineRenderer rightEdge;
        private LineRenderer ring;
        private float angle;
        private float angularVelocity;
        private bool wasAiming;

        public float HalfAngle => halfAngle; // Enemy can react to aircraft sector, never mouse position.
        public Vector3 FireDirection { get; private set; }
        public string ModeLabel => "FREE +/-" + halfAngle.ToString("F0");

        public void Configure(Camera camera, Transform player)
        {
            viewCamera = camera;
            aircraft = player;
            FireDirection = player.forward;
            angle = angularVelocity = 0f;
            wasAiming = false;
            Hide();
        }

        private void Awake()
        {
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null)
            {
                Debug.LogError("Bounded aim guide shader missing.", this);
                enabled = false;
                return;
            }
            guideMaterial = new Material(shader) { name = "Bounded aim guide (runtime)" };
            leftEdge = MakeLine("Free aim / left limit", 2, .18f,
                new Color(.87f, .91f, .94f, .44f));
            rightEdge = MakeLine("Free aim / right limit", 2, .18f,
                new Color(.87f, .91f, .94f, .44f));
            ring = MakeLine("Free aim / actual firing point", 33, .32f,
                new Color(.91f, .94f, .96f, .80f));
            ring.loop = true;
            Hide();
        }

        // Sample after the camera has settled; the aim direction drives reticle and projectile.
        public void Sample(bool sectorSelected, bool triggerHeld)
        {
            bool visible = isActiveAndEnabled && sectorSelected && aircraft != null;
            FireDirection = aircraft != null ? aircraft.forward : Vector3.forward;
            if (visible) DrawSector();
            else HideSector();

            bool active = visible && triggerHeld && viewCamera != null && Mouse.current != null;
            if (!active)
            {
                wasAiming = false;
                angularVelocity = 0f;
                if (ring != null) ring.enabled = false;
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(aircraft.right, Vector3.up).normalized;
            var pointer = Mouse.current.position.ReadValue();
            var ray = viewCamera.ScreenPointToRay(pointer);
            var plane = new Plane(Vector3.up, aircraft.position);
            float along = guideReach * .55f;
            float sideways = 0f;
            if (plane.Raycast(ray, out float distance))
            {
                Vector3 fromAircraft = ray.GetPoint(distance) - aircraft.position;
                along = Vector3.Dot(fromAircraft, forward);
                sideways = Vector3.Dot(fromAircraft, right);
            }

            // Behind the wing line: reset angular aim to the nose, but keep radial
            // pointer distance. No cursor teleport or accidental 180-degree turret.
            float radius = Mathf.Clamp(Mathf.Sqrt(along * along + sideways * sideways),
                minimumReticleDistance, guideReach - 3f);
            // Keep the WHOLE ring inside the wedge, not just its centre.
            float ringClearance = Mathf.Asin(Mathf.Clamp(2.25f / radius, 0f, .95f)) * Mathf.Rad2Deg;
            float allowedAngle = Mathf.Max(0f, halfAngle - ringClearance);
            float desired = along <= 0f ? 0f :
                Mathf.Clamp(Mathf.Atan2(sideways, along) * Mathf.Rad2Deg, -allowedAngle, allowedAngle);

            // Initialize instantly at trigger press; smooth crossings while held so
            // a cursor emerging from the rear half-plane cannot jerk the crosshair.
            angle = !wasAiming ? desired : Mathf.SmoothDampAngle(angle, desired,
                ref angularVelocity, aimSmoothing, Mathf.Infinity, Time.deltaTime);
            wasAiming = true;
            FireDirection = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            DrawRing(radius);
        }

        private void DrawSector()
        {
            if (leftEdge == null || rightEdge == null) return;
            Vector3 forward = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up).normalized;
            Vector3 origin = aircraft.position;
            Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
            Vector3 right = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward;
            // The limits really are rays from the aircraft; omit the innermost 7m
            // so the guide does not cross over the visible fuselage.
            leftEdge.SetPosition(0, origin + left * 7f);
            leftEdge.SetPosition(1, origin + left * guideReach);
            rightEdge.SetPosition(0, origin + right * 7f);
            rightEdge.SetPosition(1, origin + right * guideReach);
            leftEdge.enabled = rightEdge.enabled = true;
        }

        private void DrawRing(float radius)
        {
            if (ring == null) return;
            Vector3 center = aircraft.position + FireDirection * radius;
            const float ringRadius = 2.25f;
            for (int i = 0; i < 33; i++)
            {
                float theta = i * (Mathf.PI * 2f / 32f);
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)) * ringRadius);
            }
            ring.enabled = true;
        }

        private LineRenderer MakeLine(string name, int vertices, float width, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = guideMaterial;
            line.useWorldSpace = true;
            line.positionCount = vertices;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.startColor = line.endColor = tint;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Hide()
        {
            HideSector();
            if (ring != null) ring.enabled = false;
        }

        private void HideSector()
        {
            if (leftEdge != null) leftEdge.enabled = false;
            if (rightEdge != null) rightEdge.enabled = false;
        }

        private void OnDisable() { Hide(); }
        private void OnDestroy()
        {
            if (guideMaterial != null) Destroy(guideMaterial);
        }
    }
}
