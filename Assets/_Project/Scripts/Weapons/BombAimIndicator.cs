using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // One military-HUD four-corner footprint, derived from all four predicted landings.
    // The bounds are a level XZ rectangle aligned to the jet's horizontal heading;
    // impacts on roofs must not tilt or skew the outline.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(155)]
    public sealed class BombAimIndicator : MonoBehaviour
    {
        private const float SurfaceOffset = .4f;
        private static readonly Color GuideColor = new Color(.87f, .91f, .94f, .80f);

        private BombRack rack;
        private Material material;
        private LineRenderer[] corners;
        private Vector3[] predicted;

        public void Configure(BombRack owner)
        {
            rack = owner;
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "G3a bomb impact HUD (runtime)" };
            predicted = new Vector3[rack.BurstSize];
            corners = new LineRenderer[4];
            for (int i = 0; i < corners.Length; i++)
            {
                var go = new GameObject("Bomb impact / corner " + (i + 1));
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = 3;
                line.widthMultiplier = .32f; // Match the autocannon's actual-aim ring.
                line.startColor = line.endColor = GuideColor;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.numCapVertices = 0;
                corners[i] = line;
            }
            Show(false);
        }

        private void LateUpdate()
        {
            if (rack == null || corners == null) return;
            if (!rack.IsSelected) { Show(false); return; }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f) { Show(false); return; }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float minForward = float.PositiveInfinity, maxForward = float.NegativeInfinity;
            float minRight = float.PositiveInfinity, maxRight = float.NegativeInfinity;
            float highestSurface = float.NegativeInfinity;
            float margin = 2.5f;
            Vector3 anchor = default;
            for (int i = 0; i < predicted.Length; i++)
            {
                if (!rack.Predict(i, out Vector3 impact, out float flightTime))
                { Show(false); return; }
                predicted[i] = impact;
                if (i == 0) anchor = impact;
                Vector3 relative = impact - anchor;
                float along = Vector3.Dot(relative, forward);
                float across = Vector3.Dot(relative, right);
                minForward = Mathf.Min(minForward, along);
                maxForward = Mathf.Max(maxForward, along);
                minRight = Mathf.Min(minRight, across);
                maxRight = Mathf.Max(maxRight, across);
                highestSurface = Mathf.Max(highestSurface, impact.y);
                margin = Mathf.Max(margin, rack.ForecastErrorRadius(flightTime));
            }

            // Project every prediction into the same ground-aligned basis. Unlike
            // connecting individual impact points, roof hits cannot distort corners.
            float halfLength = (maxForward - minForward) * .5f + margin;
            float halfWidth = (maxRight - minRight) * .5f + margin;
            Vector3 center = anchor + forward * ((minForward + maxForward) * .5f)
                + right * ((minRight + maxRight) * .5f);
            center.y = highestSurface + SurfaceOffset;
            float longitudinalLeg = Mathf.Min(6.5f, halfLength * .35f);
            float lateralLeg = Mathf.Min(6f, halfWidth * .80f);

            for (int i = 0; i < corners.Length; i++)
            {
                float alongSign = i < 2 ? -1f : 1f;
                float sideSign = (i & 1) == 0 ? -1f : 1f;
                Vector3 outer = center + forward * (alongSign * halfLength)
                    + right * (sideSign * halfWidth);
                corners[i].SetPosition(0, outer - forward * (alongSign * longitudinalLeg));
                corners[i].SetPosition(1, outer);
                corners[i].SetPosition(2, outer - right * (sideSign * lateralLeg));
            }
            Show(true);
        }

        private void Show(bool visible)
        {
            if (corners == null) return;
            foreach (var line in corners) if (line != null) line.enabled = visible;
        }

        private void OnDisable() { Show(false); }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
