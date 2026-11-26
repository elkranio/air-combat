using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // World-space runway geometry. Origin is the final stop point; Forward
    // points from that origin toward the inbound approach threshold.
    internal readonly struct LandingRunway
    {
        internal readonly Vector3 Origin, Forward;
        internal readonly float Width, Threshold, CueY;

        internal LandingRunway(Vector3 origin, Vector3 forward, float width, float threshold, float cueY)
        {
            Origin = origin;
            Forward = forward;
            Width = width;
            Threshold = threshold;
            CueY = cueY;
        }
    }

    // Reusable assisted landing path and cue. The caller owns mission state,
    // weapons, retry and completion, and provides resolved runway geometry.
    internal sealed class ScriptedLanding
    {
        private readonly Vector3 origin, runwayForward, right;
        private readonly Quaternion landingRotation, visualRest;
        private readonly Rect bounds;
        private readonly float cueY, near, far, halfWidth, reveal, headingDot, holdSeconds, touchdownOffset, touchdownSpeed;
        // Arcade landing: keep the handover velocity, then cover the remaining
        // distance briskly. Pitch is visual and independent of descent velocity.
        private const float MaxApproachSeconds = 3f;
        private const float FlarePitch = 10f;
        private const float NoseLowerSeconds = .65f;
        private const float RearContactArm = 4f; // Fake main-gear pivot; no gear mesh.
        private readonly GameObject cue;
        private readonly Material material;
        private readonly LineRenderer[] lines;
        private readonly LineRenderer groundMarker;
        private float held, elapsed, duration, rollout;
        private bool visible, aligned;
        private Vector3 p0, p1, p2, p3;
        private Quaternion captureVisual;
        internal bool ApproachVisible => visible;
        internal bool ApproachAligned => aligned;
        internal float ApproachHeld => held;
        internal float ApproachHoldSeconds => holdSeconds;

        // Ground point on the resolved runway, not a separate navigation anchor.
        internal Vector3 NavigationPoint => At(touchdownOffset, 0f);

        internal ScriptedLanding(Transform owner, LandingRunway runway,
            Quaternion visualRest, Rect bounds, float near, float far, float widthScale,
            float reveal, float headingTolerance, float holdSeconds, float touchdownOffset, float touchdownSpeed)
        {
            origin = runway.Origin;
            runwayForward = runway.Forward;
            float physicalWidth = runway.Width;
            cueY = runway.CueY;
            float threshold = runway.Threshold;
            right = Vector3.Cross(Vector3.up, runwayForward);
            landingRotation = Quaternion.LookRotation(-runwayForward, Vector3.up);
            this.visualRest = visualRest;
            this.bounds = bounds;
            this.touchdownOffset = Mathf.Max(10f, touchdownOffset);
            this.near = Mathf.Max(this.touchdownOffset + 30f, threshold);
            this.far = this.near + Mathf.Max(30f, far - near);
            halfWidth = Mathf.Max(2f, physicalWidth * Mathf.Clamp(widthScale, .25f, 4f) * .5f);
            this.reveal = Mathf.Max(this.far + 30f, reveal);
            headingDot = Mathf.Cos(Mathf.Clamp(headingTolerance, 5f, 60f) * Mathf.Deg2Rad);
            this.holdSeconds = Mathf.Max(.15f, holdSeconds);
            this.touchdownSpeed = Mathf.Max(5f, touchdownSpeed);

            cue = new GameObject("Landing corridor (runtime)");
            cue.transform.SetParent(owner, false);
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("LAND-01: corridor shader unavailable; text cue remains active.");
                lines = new LineRenderer[0];
            }
            else
            {
                material = new Material(shader) { name = "Landing corridor (runtime)" };
                lines = new[] {
                    Line("Left", At(this.near, -halfWidth), At(this.far, -halfWidth)),
                    Line("Right", At(this.near, halfWidth), At(this.far, halfWidth)),
                    Line("Entry", At(this.far, -halfWidth), At(this.far, halfWidth)),
                    Arrow(Mathf.Lerp(this.near, this.far, .33f)), Arrow(Mathf.Lerp(this.near, this.far, .72f))
                };
                // The corridor is on the ground. This diamond shows the exact
                // aircraft XZ used by the capture check, not the offset shadow.
                groundMarker = Line("Aircraft ground projection", new Vector3[5]);
                groundMarker.widthMultiplier = .65f;
            }
            cue.SetActive(false);
        }

        private Vector3 At(float along, float side)
        {
            Vector3 p = origin + runwayForward * along + right * side;
            p.y = cueY;
            return p;
        }

        private LineRenderer Line(string name, params Vector3[] points)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(cue.transform, false);
            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = .7f;
            line.positionCount = points.Length;
            line.SetPositions(points);
            return line;
        }

        private LineRenderer Arrow(float along)
        {
            float size = Mathf.Min(8f, halfWidth * .55f);
            return Line("Inbound chevron", At(along + size, -size), At(along, 0f), At(along + size, size));
        }

        private void DrawGroundMarker(Vector3 position, Color ink)
        {
            if (groundMarker == null) return;
            position.y = cueY + .08f;
            const float size = 2.2f;
            Vector3 front = position + runwayForward * size;
            groundMarker.SetPosition(0, front);
            groundMarker.SetPosition(1, position + right * size);
            groundMarker.SetPosition(2, position - runwayForward * size);
            groundMarker.SetPosition(3, position - right * size);
            groundMarker.SetPosition(4, front);
            ink.a = 1f;
            groundMarker.startColor = groundMarker.endColor = ink;
        }

        // Called after the normal flight motor and all boundary/rock checks.
        // Continuous aligned flight inside the corridor is the consent cue.
        public bool TickApproach(Transform aircraft, Transform visual, float speed, bool available, float dt)
        {
            Vector3 delta = aircraft.position - origin;
            delta.y = 0f;
            visible = available && delta.sqrMagnitude <= reveal * reveal;
            if (!visible) { Clear(); return false; }
            float along = Vector3.Dot(delta, runwayForward);
            float side = Vector3.Dot(delta, right);
            Vector3 heading = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up).normalized;
            bool wasAligned = aligned;
            aligned = along >= near && along <= far && Mathf.Abs(side) <= halfWidth &&
                Vector3.Dot(heading, -runwayForward) >= headingDot;
            // The first in-corridor sample starts the timer at zero. A frame
            // spent outside the corridor must not count toward capture.
            held = aligned && wasAligned ? Mathf.Min(holdSeconds, held + Mathf.Max(0f, dt)) : 0f;
            cue.SetActive(true);
            var ink = aligned ? new Color(.43f, 1f, .78f, .84f) : new Color(.45f, .9f, 1f, .55f);
            foreach (var line in lines) { line.startColor = ink; line.endColor = ink; }
            DrawGroundMarker(aircraft.position, ink);
            if (held < holdSeconds) return false;

            // Cubic horizontal path with exact entry and touchdown tangents.
            // Cap duration instead of inheriting a long slow approach. Keeping
            // both endpoint derivatives preserves entry and touchdown speed.
            // The cap can only shorten control handles, never overshoot axially.
            p0 = aircraft.position;
            p3 = origin + runwayForward * touchdownOffset;
            float axial = along - touchdownOffset;
            speed = Mathf.Max(1f, speed);
            float inbound = speed * Vector3.Dot(heading, -runwayForward);
            duration = Mathf.Min(MaxApproachSeconds, 2f * axial / (inbound + touchdownSpeed));
            p1 = p0 + heading * (speed * duration / 3f);
            p2 = p3 + runwayForward * (touchdownSpeed * duration / 3f);
            // The XZ curve remains in the convex hull of its control points.
            if (!Inside(p0) || !Inside(p1) || !Inside(p2) || !Inside(p3) || !Inside(origin))
            { held = 0f; return false; }
            captureVisual = visual.localRotation;
            elapsed = 0f;
            rollout = 2f * touchdownOffset / touchdownSpeed;
            Clear();
            return true;
        }

        private bool Inside(Vector3 p) => p.x > bounds.xMin && p.x < bounds.xMax &&
            p.z > bounds.yMin && p.z < bounds.yMax;

        public bool TickLanding(Transform aircraft, Transform visual, float dt)
        {
            elapsed = Mathf.Min(duration + rollout, elapsed + Mathf.Max(0f, dt));
            if (elapsed < duration)
            {
                float u = Mathf.Clamp01(elapsed / duration), v = 1f - u;
                Vector3 p = v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
                Vector3 tangent = 3f * v * v * (p1 - p0) + 6f * v * u * (p2 - p1) + 3f * u * u * (p3 - p2);
                tangent.y = 0f;
                // Begin descent and nose-up alignment together at capture.
                // Hold the landing attitude throughout the glide, not just at flare.
                float descentBlend = Mathf.SmoothStep(0f, 1f, u);
                p.y = Mathf.Lerp(p0.y, origin.y + ContactLift(FlarePitch), descentBlend);
                aircraft.SetPositionAndRotation(p, Quaternion.LookRotation(tangent, Vector3.up));
                // Blend the captured bank into nose-up (negative local X).
                // Reach full pitch within .3 s or the first 20% of the approach.
                float attitudeBlend = Mathf.SmoothStep(0f, 1f,
                    elapsed / Mathf.Min(.3f, duration * .2f));
                visual.localRotation = Quaternion.Slerp(captureVisual,
                    visualRest * Quaternion.Euler(-FlarePitch, 0f, 0f), attitudeBlend);
                return false;
            }
            float r = elapsed >= duration + rollout ? 1f : Mathf.Clamp01((elapsed - duration) / rollout);
            // Constant deceleration; starts at touchdownSpeed and ends at zero.
            float distance = touchdownOffset * (2f * r - r * r);
            // Keep the nose up at touchdown, then rotate around an imagined
            // rear contact point. Both pitch and vertical offset reach zero
            // continuously before the aircraft stops, without a gear model.
            float lower = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((elapsed - duration) / Mathf.Min(NoseLowerSeconds, rollout)));
            float noseUp = Mathf.Lerp(FlarePitch, 0f, lower);
            Vector3 position = origin + runwayForward * (touchdownOffset - distance);
            position.y += ContactLift(noseUp);
            aircraft.SetPositionAndRotation(position, landingRotation);
            visual.localRotation = visualRest * Quaternion.Euler(-noseUp, 0f, 0f);
            return r >= 1f;
        }

        private static float ContactLift(float noseUp) => RearContactArm * Mathf.Sin(noseUp * Mathf.Deg2Rad);

        public void Clear()
        {
            visible = aligned = false;
            held = 0f;
            if (cue != null) cue.SetActive(false);
        }

        public void Dispose()
        {
            if (cue != null) Object.Destroy(cue);
            if (material != null) Object.Destroy(material);
        }
    }
}
