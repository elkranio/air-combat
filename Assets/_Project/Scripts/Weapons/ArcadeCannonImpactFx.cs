using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Visual-only spark on a *successful* arcade cannon air hit. Short-lived,
    // no lights, physics, pooled gameplay state or camera ownership.
    public sealed class ArcadeCannonImpactFx : MonoBehaviour
    {
        private const float Lifetime = .17f;
        private readonly LineRenderer[] streaks = new LineRenderer[4];
        private readonly Vector3[] directions = new Vector3[4];
        private float elapsed;
        private float scale;

        public static void Spawn(Vector3 point, Material glowMaterial, float size)
        {
            if (glowMaterial == null || size <= 0f) return;
            var go = new GameObject("AC-01 / impact spark");
            go.transform.position = point;
            var fx = go.AddComponent<ArcadeCannonImpactFx>();
            fx.Setup(glowMaterial, size);
        }

        private void Setup(Material material, float size)
        {
            scale = size;
            // Always face the game view, whether the projectile flies left/right.
            var camera = Camera.main;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;
            for (int i = 0; i < streaks.Length; i++)
            {
                float a = (i * 90f + 22.5f) * Mathf.Deg2Rad;
                directions[i] = Mathf.Cos(a) * right + Mathf.Sin(a) * up;
                var spark = new GameObject("Spark streak " + (i + 1));
                spark.transform.SetParent(transform, false);
                var line = spark.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthMultiplier = .35f * scale;
                line.numCapVertices = 3;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                streaks[i] = line;
            }
            Draw();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Lifetime) { Destroy(gameObject); return; }
            Draw();
        }

        private void Draw()
        {
            float t = Mathf.Clamp01(elapsed / Lifetime);
            for (int i = 0; i < streaks.Length; i++)
            {
                var line = streaks[i];
                if (line == null) continue;
                Vector3 from = transform.position + directions[i] * (t * .5f * scale);
                Vector3 to = transform.position + directions[i] * ((.7f + t * 2.1f) * scale);
                line.SetPosition(0, from);
                line.SetPosition(1, to);
                line.startColor = new Color(1f, 1f, .72f, .9f * (1f - t));
                line.endColor = new Color(1f, .48f, .09f, .15f * (1f - t));
            }
        }
    }
}
