using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Player-local pre-launch bearing experiments, never a projectile tracking aid.
    // Its direction follows the shooter through the moving camera.
    [DisallowMultipleComponent]
    public sealed class EnemyMissileThreatHud : MonoBehaviour
    {
        private readonly List<EnemyMissileStressTest.WarningCue> warnings =
            new List<EnemyMissileStressTest.WarningCue>();
        private Camera view;
        private EnemyMissileStressTest threats;
        private Texture2D missileIcon;
        public enum WarningStyle { ShortVector, ThreatArc }

        [SerializeField] private WarningStyle warningStyle = WarningStyle.ThreatArc;
        [Header("Threat arc / reference pixels at 720p")]
        [SerializeField, Min(12f)] private float arcRadius = 56f;
        [SerializeField, Range(10f, 100f)] private float arcDegrees = 44f;
        [SerializeField, Min(1f)] private float arcStroke = 3f;
        [Header("Short vector / reference pixels at 720p")]
        [SerializeField, Min(0f)] private float hullGap = 38f;
        [SerializeField, Min(25f)] private float vectorLength = 64f;
        [SerializeField, Min(0f)] private float pulseTravel = 6f;
        private static readonly Color Threat = new Color(.56f, .91f, .47f, 1f);

        public void Bind(Camera camera, EnemyMissileStressTest source)
        {
            view = camera;
            threats = source;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || view == null || threats == null ||
                !threats.CollectWarnings(warnings, out Vector3 playerPosition)) return;
            Rect pixels = view.pixelRect;
            var rect = new Rect(pixels.xMin, Screen.height - pixels.yMax, pixels.width, pixels.height);
            float scale = Mathf.Clamp(rect.height / 720f, .75f, 3f);
            if (!Project(playerPosition, rect, out Vector2 player) || !rect.Contains(player)) return;
            if (warningStyle == WarningStyle.ShortVector && missileIcon == null) missileIcon = MakeMissileIcon();

            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -30;
            foreach (var cue in warnings)
            {
                float age = Mathf.Max(0f, Time.time - cue.Started);
                float phase = Mathf.Repeat(age, EnemyMissileStressTest.WarningPulsePeriod);
                // Follow the same three short pulses as the distress audio. Keep
                // a faint bearing until actual launch (the existing .8s lead).
                float pulse = age < EnemyMissileStressTest.WarningClipSeconds
                    ? Mathf.Clamp01(1f - phase / EnemyMissileStressTest.WarningPulseLength) : 0f;
                float alpha = Mathf.Lerp(.24f, .92f, pulse);
                Project(cue.Position, rect, out Vector2 source);
                if (warningStyle == WarningStyle.ThreatArc)
                    DrawArc(rect, source, player, scale, alpha, pulse);
                else
                    DrawVector(rect, source, player, scale, alpha, pulse);
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }

        private bool Project(Vector3 world, Rect rect, out Vector2 p)
        {
            Vector3 screen = view.WorldToScreenPoint(world);
            p = new Vector2(screen.x, Screen.height - screen.y);
            if (!view.orthographic && screen.z <= 0f) p = rect.center - (p - rect.center);
            return screen.z > view.nearClipPlane && screen.z < view.farClipPlane;
        }

        private void DrawArc(Rect rect, Vector2 source, Vector2 player, float s, float alpha, float pulse)
        {
            Vector2 delta = source - player;
            if (delta.sqrMagnitude < 1f) return;
            float bearing = Mathf.Atan2(delta.y, delta.x);
            float halfAngle = arcDegrees * .5f * Mathf.Deg2Rad;
            float radius = (arcRadius + pulseTravel * pulse) * s;
            float stroke = arcStroke * s;
            var ink = new Color(Threat.r, Threat.g, Threat.b, alpha);
            var backing = new Color(.025f, .065f, .035f, alpha * .8f);
            // A short sector on the incoming side, not a full ring or a line to
            // the shooter. It follows the player and contracts with each beep.
            const int segments = 20;
            Vector2 previous = ArcPoint(player, radius, bearing - halfAngle);
            for (int i = 1; i <= segments; i++)
            {
                float angle = bearing - halfAngle + 2f * halfAngle * i / segments;
                Vector2 next = ArcPoint(player, radius, angle);
                if (rect.Contains(previous) && rect.Contains(next))
                    DrawStroke(previous, next, stroke + 2f * s, backing);
                previous = next;
            }
            previous = ArcPoint(player, radius, bearing - halfAngle);
            for (int i = 1; i <= segments; i++)
            {
                float angle = bearing - halfAngle + 2f * halfAngle * i / segments;
                Vector2 next = ArcPoint(player, radius, angle);
                if (rect.Contains(previous) && rect.Contains(next))
                    DrawStroke(previous, next, stroke, ink);
                previous = next;
            }
            // A tiny inward notch singles out the exact bearing in the sector.
            Vector2 outer = ArcPoint(player, radius, bearing);
            Vector2 inner = ArcPoint(player, Mathf.Max(0f, radius - 7f * s), bearing);
            if (rect.Contains(outer) && rect.Contains(inner))
            {
                DrawStroke(outer, inner, stroke + 2f * s, backing);
                DrawStroke(outer, inner, stroke, ink);
            }
        }

        private static Vector2 ArcPoint(Vector2 centre, float radius, float angle) =>
            centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        private static void DrawStroke(Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 delta = to - from;
            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.color = tint;
            // Slight overlap keeps the short tessellated curve visually continuous.
            GUI.DrawTexture(new Rect(from.x - width * .25f, from.y - width * .5f,
                delta.magnitude + width * .5f, width), Texture2D.whiteTexture);
            GUI.matrix = before;
        }

        private void DrawVector(Rect rect, Vector2 source, Vector2 player, float s, float alpha, float pulse)
        {
            Vector2 delta = source - player;
            float distance = delta.magnitude;
            if (distance < 1f) return;
            Vector2 outward = delta / distance;
            // Player-relative bearing, independent of camera lead. Fit the whole
            // cue inside the viewport and do not extend beyond a nearby shooter.
            Rect inset = Rect.MinMaxRect(rect.xMin + 8f * s, rect.yMin + 8f * s,
                rect.xMax - 8f * s, rect.yMax - 8f * s);
            if (!inset.Contains(player)) return;
            float tx = outward.x > .0001f ? (inset.xMax - player.x) / outward.x :
                outward.x < -.0001f ? (inset.xMin - player.x) / outward.x : float.PositiveInfinity;
            float ty = outward.y > .0001f ? (inset.yMax - player.y) / outward.y :
                outward.y < -.0001f ? (inset.yMin - player.y) / outward.y : float.PositiveInfinity;
            float visibleDistance = Mathf.Min(distance, Mathf.Min(tx, ty));
            // Each audible pulse starts farther out, then slides toward the hull.
            float gap = (hullGap + pulseTravel * pulse) * s;
            float span = Mathf.Min(vectorLength * s, visibleDistance - gap);
            float iconLength = 20f * s;
            if (span < iconLength) return;
            Vector2 start = player + outward * (gap + span);
            Vector2 inward = -outward;
            float width = 1.5f * s;
            float height = iconLength * .5f;
            float lineLength = span - iconLength;
            Color ink = new Color(Threat.r, Threat.g, Threat.b, alpha);
            Color backing = new Color(.025f, .065f, .035f, alpha * .8f);

            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg, start);
            // Short dashes rather than a solid beam across the combat view.
            for (float x = 0f; x < lineLength; x += 10f * s)
            {
                float dash = Mathf.Min(6f * s, lineLength - x);
                GUI.color = backing;
                GUI.DrawTexture(new Rect(start.x + x, start.y - width, dash, width * 2f), Texture2D.whiteTexture);
                GUI.color = ink;
                GUI.DrawTexture(new Rect(start.x + x, start.y - width * .5f, dash, width), Texture2D.whiteTexture);
            }
            Rect icon = new Rect(start.x + lineLength, start.y - height * .5f, iconLength, height);
            GUI.color = backing;
            GUI.DrawTexture(new Rect(icon.x - s, icon.y - s, icon.width + 2f * s, icon.height + 2f * s), missileIcon);
            GUI.color = ink;
            GUI.DrawTexture(icon, missileIcon);
            GUI.matrix = before;
        }

        // Antialiased contour: pointed nose, long body, swept tail fins.
        // One generated texture for the HUD lifetime, no asset or shader changes.
        private static Texture2D MakeMissileIcon()
        {
            const int w = 64, h = 32, samples = 4;
            var outline = new[] {
                new Vector2(.03f, .10f), new Vector2(.29f, .34f),
                new Vector2(.77f, .34f), new Vector2(.98f, .50f),
                new Vector2(.77f, .66f), new Vector2(.29f, .66f),
                new Vector2(.03f, .90f), new Vector2(.12f, .50f)
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float px = (x + (sx + .5f) / samples) / w;
                    float py = (y + (sy + .5f) / samples) / h;
                    // Measure in texture pixels to keep the stroke isotropic.
                    Vector2 point = new Vector2(px * w, py * h);
                    for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
                    {
                        Vector2 a = new Vector2(outline[j].x * w, outline[j].y * h);
                        Vector2 b = new Vector2(outline[i].x * w, outline[i].y * h);
                        Vector2 ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
                        if ((point - (a + t * ab)).sqrMagnitude > 2.4f * 2.4f) continue;
                        covered++;
                        break;
                    }
                }
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(255 * covered / (samples * samples)));
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Prelaunch warning missile glyph", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            if (missileIcon != null) Destroy(missileIcon);
        }
    }
}
