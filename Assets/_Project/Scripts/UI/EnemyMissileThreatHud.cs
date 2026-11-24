using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Screen-space cues, not modifications to the target's model or material.
    // Before launch track the shooter; afterwards track the individual projectile.
    [DisallowMultipleComponent]
    public sealed class EnemyMissileThreatHud : MonoBehaviour
    {
        private readonly List<EnemyMissileStressTest.WarningCue> warnings =
            new List<EnemyMissileStressTest.WarningCue>();
        private readonly List<Vector3> missiles = new List<Vector3>();
        private Camera view;
        private EnemyMissileStressTest threats;
        private GUIStyle label;
        private static readonly Color Threat = new Color(1f, .25f, .12f, 1f);

        public void Bind(Camera camera, EnemyMissileStressTest source)
        {
            view = camera;
            threats = source;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || view == null || threats == null) return;
            threats.CollectThreats(warnings, missiles);
            if (warnings.Count == 0 && missiles.Count == 0) return;
            Rect pixels = view.pixelRect;
            var rect = new Rect(pixels.xMin, Screen.height - pixels.yMax, pixels.width, pixels.height);
            float scale = Mathf.Clamp(rect.height / 720f, .75f, 3f);
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            label.fontSize = Mathf.RoundToInt(13f * scale);
            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -30;
            foreach (var cue in warnings)
            {
                float age = Mathf.Max(0f, Time.time - cue.Started);
                float phase = Mathf.Repeat(age, EnemyMissileStressTest.WarningPulsePeriod);
                // Exactly the three audio pulses, then a dim hold until the shot.
                float pulse = age < EnemyMissileStressTest.WarningClipSeconds
                    ? Mathf.Clamp01(1f - phase / EnemyMissileStressTest.WarningPulseLength) : 0f;
                float alpha = Mathf.Lerp(.42f, 1f, pulse);
                if (Project(cue.Position, rect, out Vector2 p))
                {
                    Vector3 edge = view.WorldToScreenPoint(cue.Position + view.transform.right * 9f);
                    Vector3 centre = view.WorldToScreenPoint(cue.Position);
                    float r = Mathf.Max(22f * scale, Mathf.Abs(edge.x - centre.x)) + 9f * scale * pulse;
                    Brackets(p, r, scale, alpha);
                    Text(new Vector2(p.x, p.y - r - 13f * scale), "LAUNCH", scale, alpha);
                }
                else EdgeCue(rect, p, scale, alpha, pulse, "LAUNCH");
            }
            foreach (var missile in missiles)
            {
                // No extra HUD over a missile already visible in the game world.
                if (Project(missile, rect, out Vector2 p)) continue;
                float pulse = .5f + .5f * Mathf.Cos(Time.time * 2f * Mathf.PI * 3.2f);
                EdgeCue(rect, p, scale, Mathf.Lerp(.38f, 1f, pulse), pulse, "AAM");
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
            return screen.z > view.nearClipPlane && screen.z < view.farClipPlane && rect.Contains(p);
        }

        private void EdgeCue(Rect rect, Vector2 projected, float s, float alpha, float pulse, string text)
        {
            Vector2 direction = projected - rect.center;
            if (direction.sqrMagnitude < .001f) direction = Vector2.up;
            direction.Normalize();
            // Intersect the screen-centre ray with a safe inset rectangle, rather
            // than clamping X/Y separately (which loses the real bearing).
            Vector2 extent = new Vector2(Mathf.Max(1f, rect.width * .5f - 46f * s),
                Mathf.Max(1f, rect.height * .5f - 43f * s));
            float tx = Mathf.Abs(direction.x) > .0001f ? extent.x / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float ty = Mathf.Abs(direction.y) > .0001f ? extent.y / Mathf.Abs(direction.y) : float.PositiveInfinity;
            Vector2 at = rect.center + direction * Mathf.Min(tx, ty);
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            float length = (13f + 5f * pulse) * s;
            Vector2 tip = at + direction * length;
            Vector2 left = at - direction * 4f * s + tangent * 10f * s;
            Vector2 right = at - direction * 4f * s - tangent * 10f * s;
            Stroke(left, tip, 3f * s, alpha);
            Stroke(tip, right, 3f * s, alpha);
            Stroke(at - direction * 13f * s, at - direction * 4f * s, 3f * s, alpha);
            Vector2 caption = at - direction * 28f * s;
            caption.x = Mathf.Clamp(caption.x, rect.xMin + 43f * s, rect.xMax - 43f * s);
            caption.y = Mathf.Clamp(caption.y, rect.yMin + 12f * s, rect.yMax - 12f * s);
            Text(caption, text, s, alpha);
        }

        private static void Brackets(Vector2 p, float r, float s, float alpha)
        {
            float arm = Mathf.Min(13f * s, r * .55f);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector2 corner = p + new Vector2(x * r, y * r);
                Stroke(corner, corner - Vector2.right * x * arm, 2.5f * s, alpha);
                Stroke(corner, corner - Vector2.up * y * arm, 2.5f * s, alpha);
            }
        }

        private static void Stroke(Vector2 a, Vector2 b, float width, float alpha)
        {
            Vector2 d = b - a;
            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.color = new Color(.04f, .02f, .015f, alpha * .95f);
            GUI.DrawTexture(new Rect(a.x - width * .5f, a.y - width, d.magnitude + width, width * 2f), Texture2D.whiteTexture);
            GUI.color = new Color(Threat.r, Threat.g, Threat.b, alpha);
            GUI.DrawTexture(new Rect(a.x, a.y - width * .5f, d.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = before;
        }

        private void Text(Vector2 at, string text, float s, float alpha)
        {
            var box = new Rect(at.x - 43f * s, at.y - 10f * s, 86f * s, 20f * s);
            GUI.color = new Color(.035f, .025f, .02f, alpha * .8f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            label.normal.textColor = new Color(1f, .68f, .48f, alpha);
            GUI.Label(box, text, label);
        }
    }
}
