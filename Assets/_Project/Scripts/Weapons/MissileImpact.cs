using UnityEngine;

namespace AirCombat
{
    // A lightweight sandbox hit cue; no physics, damage or gameplay dependency.
    public sealed class MissileImpact : MonoBehaviour
    {
        private LineRenderer ring;
        private float elapsed;
        private const float Duration = 0.45f;
        private const int Segments = 32;

        public static void Spawn(Vector3 position, Material material)
        {
            if (material == null) return;
            var root = new GameObject("MissileImpact");
            root.transform.position = position + Vector3.up;
            var effect = root.AddComponent<MissileImpact>();
            effect.ring = root.AddComponent<LineRenderer>();
            effect.ring.sharedMaterial = material;
            effect.ring.useWorldSpace = false;
            effect.ring.loop = true;
            effect.ring.positionCount = Segments;
            effect.ring.alignment = LineAlignment.View;
            effect.Draw(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Duration) { Destroy(gameObject); return; }
            Draw(elapsed / Duration);
        }

        private void Draw(float t)
        {
            float radius = Mathf.Lerp(1.2f, 13f, 1f - (1f - t) * (1f - t));
            ring.widthMultiplier = Mathf.Lerp(3f, 0.3f, t);
            var color = Color.Lerp(new Color(1f, 0.96f, 0.69f), new Color(1f, 0.36f, 0.12f), t);
            color.a = 1f - t;
            ring.startColor = ring.endColor = color;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * 2f * Mathf.PI / Segments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }
    }
}
