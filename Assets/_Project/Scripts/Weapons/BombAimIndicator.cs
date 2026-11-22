using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Four projected landings plus a forecast footprint. The first ring is white,
    // the remaining rings amber. The footprint widens under active afterburner.
    // All projected points assume the pilot holds heading/speed for the full burst.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(155)]
    public sealed class BombAimIndicator : MonoBehaviour
    {
        private const int RingSides = 16;
        private BombRack rack;
        private Material material;
        private LineRenderer outline;
        private LineRenderer[] rings;

        public void Configure(BombRack owner)
        {
            rack = owner;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "G3 predicted landing (runtime)" };
            outline = MakeLine("Predicted burst footprint", 5, .55f, new Color(1f,.71f,.26f,.70f));
            rings = new LineRenderer[rack.BurstSize];
            for (int i = 0; i < rings.Length; i++)
                rings[i] = MakeLine(i == 0 ? "First bomb / impact" : "Bomb " + (i+1) + " / impact",
                    RingSides + 1, i == 0 ? .9f : .55f,
                    i == 0 ? new Color(1f, 1f, .86f, .95f) : new Color(1f, .7f, .2f, .80f));
        }

        private LineRenderer MakeLine(string title, int count, float width, Color color)
        {
            var go = new GameObject(title);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = count;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 0;
            return line;
        }

        private void LateUpdate()
        {
            if (rack == null || rings == null) return;
            bool selected = rack.IsSelected;
            if (outline != null) outline.enabled = selected;
            foreach (var ring in rings) ring.enabled = selected;
            if (!selected) return;

            Vector3 first = default, last = default;
            float maxRadius = 2.5f;
            bool allFound = true;
            for (int i = 0; i < rings.Length; i++)
            {
                if (!rack.Predict(i, out Vector3 point, out float flightTime))
                { allFound = false; break; }
                point.y += .3f;
                if (i == 0) first = point;
                last = point;
                maxRadius = Mathf.Max(maxRadius, rack.ForecastErrorRadius(flightTime));
                float dotRadius = i == 0 ? 2.4f : 1.65f;
                for (int j = 0; j <= RingSides; j++)
                {
                    float a = 2f * Mathf.PI * j / RingSides;
                    rings[i].SetPosition(j, point + new Vector3(
                        Mathf.Cos(a) * dotRadius, 0, Mathf.Sin(a) * dotRadius));
                }
            }
            if (!allFound) { outline.enabled = false; foreach (var ring in rings) ring.enabled = false; return; }

            Vector3 axis = last - first;
            axis.y = 0f;
            axis = axis.sqrMagnitude > .0001f ? axis.normalized : transform.forward;
            Vector3 sideways = Vector3.Cross(Vector3.up, axis).normalized;
            Vector3 front = first - axis * maxRadius;
            Vector3 back = last + axis * maxRadius;
            outline.SetPosition(0, front + sideways * maxRadius);
            outline.SetPosition(1, back + sideways * maxRadius);
            outline.SetPosition(2, back - sideways * maxRadius);
            outline.SetPosition(3, front - sideways * maxRadius);
            outline.SetPosition(4, front + sideways * maxRadius);
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
