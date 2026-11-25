using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Mission-local landing path and cue. The launch sequence owns flight state,
    // weapons, retry and mission completion; no changes to shared aircraft motors.
    internal sealed class Mission01Landing
    {
        private readonly Vector3 origin, runwayForward, right;
        private readonly Quaternion landingRotation, visualRest;
        private readonly Rect bounds;
        private readonly float altitude, near, far, halfWidth, reveal, headingDot, holdSeconds, touchdownOffset, touchdownSpeed;
        private readonly GameObject cue;
        private readonly Material material;
        private readonly LineRenderer[] lines;
        private float held, elapsed, duration, rollout;
        private bool visible, aligned;
        private Vector3 p0, p1, p2, p3;
        private Quaternion captureVisual;
        private GUIStyle label;

        public Mission01Landing(Transform owner, Vector3 runwayPosition, Quaternion runwayRotation,
            Quaternion visualRest, float altitude, Rect bounds, float near, float far, float width,
            float reveal, float headingTolerance, float holdSeconds, float touchdownOffset, float touchdownSpeed)
        {
            origin = runwayPosition;
            runwayForward = Vector3.ProjectOnPlane(runwayRotation * Vector3.forward, Vector3.up).normalized;
            right = Vector3.Cross(Vector3.up, runwayForward);
            landingRotation = Quaternion.LookRotation(-runwayForward, Vector3.up);
            this.visualRest = visualRest;
            this.altitude = altitude;
            this.bounds = bounds;
            this.touchdownOffset = Mathf.Max(10f, touchdownOffset);
            this.near = Mathf.Max(this.touchdownOffset + 30f, near);
            this.far = Mathf.Max(this.near + 30f, far);
            halfWidth = Mathf.Max(10f, width * .5f);
            this.reveal = Mathf.Max(this.far + 30f, reveal);
            headingDot = Mathf.Cos(Mathf.Clamp(headingTolerance, 5f, 60f) * Mathf.Deg2Rad);
            this.holdSeconds = Mathf.Max(.15f, holdSeconds);
            this.touchdownSpeed = Mathf.Max(5f, touchdownSpeed);

            cue = new GameObject("Mission01 / landing corridor (runtime)");
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
                    Line("Left", At(near, -halfWidth), At(far, -halfWidth)),
                    Line("Right", At(near, halfWidth), At(far, halfWidth)),
                    Line("Entry", At(far, -halfWidth), At(far, halfWidth)),
                    Arrow(Mathf.Lerp(near, far, .33f)), Arrow(Mathf.Lerp(near, far, .72f))
                };
            }
            cue.SetActive(false);
        }

        private Vector3 At(float along, float side)
        {
            Vector3 p = origin + runwayForward * along + right * side;
            p.y = altitude;
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

        private LineRenderer Arrow(float along) => Line("Inbound chevron",
            At(along + 8f, -8f), At(along, 0f), At(along + 8f, 8f));

        // Called after the normal flight motor and all boundary/rock checks.
        // Holding S is an explicit consent cue; it does not depend on fuel.
        public bool TickApproach(Transform aircraft, Transform visual, float speed, bool available, bool heldS, float dt)
        {
            Vector3 delta = aircraft.position - origin;
            delta.y = 0f;
            visible = available && delta.sqrMagnitude <= reveal * reveal;
            if (!visible) { Clear(); return false; }
            float along = Vector3.Dot(delta, runwayForward);
            float side = Vector3.Dot(delta, right);
            Vector3 heading = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up).normalized;
            aligned = along >= near && along <= far && Mathf.Abs(side) <= halfWidth &&
                Vector3.Dot(heading, -runwayForward) >= headingDot;
            held = aligned && heldS ? Mathf.Min(holdSeconds, held + Mathf.Max(0f, dt)) : 0f;
            cue.SetActive(true);
            var ink = aligned ? new Color(.43f, 1f, .78f, .84f) : new Color(.45f, .9f, 1f, .55f);
            foreach (var line in lines) { line.startColor = ink; line.endColor = ink; }
            if (held < holdSeconds) return false;

            // Cubic horizontal path with exact entry and touchdown tangents.
            // Choose duration from axial distance and endpoint velocities, so
            // neither control point overshoots the runway along the approach.
            p0 = aircraft.position;
            p3 = origin + runwayForward * touchdownOffset;
            float axial = along - touchdownOffset;
            speed = Mathf.Max(1f, speed);
            float inbound = speed * Vector3.Dot(heading, -runwayForward);
            duration = 2f * axial / (inbound + touchdownSpeed);
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
                // Delay descent slightly until the captured aircraft is aligning.
                float descent = Mathf.InverseLerp(.1f, 1f, u);
                p.y = Mathf.Lerp(p0.y, origin.y, Mathf.SmoothStep(0f, 1f, descent));
                aircraft.SetPositionAndRotation(p, Quaternion.LookRotation(tangent, Vector3.up));
                float pitch = 15f * Mathf.Sin(Mathf.PI * descent);
                visual.localRotation = Quaternion.Slerp(captureVisual,
                    visualRest * Quaternion.Euler(pitch, 0f, 0f), Mathf.SmoothStep(0f, 1f, elapsed / .55f));
                return false;
            }
            float r = elapsed >= duration + rollout ? 1f : Mathf.Clamp01((elapsed - duration) / rollout);
            // Constant deceleration; starts at touchdownSpeed and ends at zero.
            float distance = touchdownOffset * (2f * r - r * r);
            aircraft.SetPositionAndRotation(origin + runwayForward * (touchdownOffset - distance), landingRotation);
            visual.localRotation = visualRest;
            return r >= 1f;
        }

        public void DrawWarning()
        {
            if (!visible) return;
            if (label == null) label = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            label.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .022f, 14f, 25f));
            float width = Mathf.Min(Screen.width * .62f, 620f);
            var rect = new Rect((Screen.width - width) * .5f, Screen.height * .76f, width, 38f);
            Color previous = GUI.color;
            GUI.color = new Color(.035f, .06f, .07f, .82f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            label.normal.textColor = aligned ? new Color(.5f, 1f, .82f) : new Color(.65f, .94f, 1f);
            GUI.Label(rect, aligned ? "HOLD S TO LAND" : "LANDING / ENTER APPROACH CORRIDOR", label);
            if (held > 0f)
            {
                GUI.color = label.normal.textColor;
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 3f, rect.width * held / holdSeconds, 3f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

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
