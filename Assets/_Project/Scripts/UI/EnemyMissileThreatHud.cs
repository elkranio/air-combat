using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // A temporary pre-launch bearing vector, never a projectile tracking aid.
    // Both ends follow their actual world positions through the moving camera.
    [DisallowMultipleComponent]
    public sealed class EnemyMissileThreatHud : MonoBehaviour
    {
        private readonly List<EnemyMissileStressTest.WarningCue> warnings =
            new List<EnemyMissileStressTest.WarningCue>();
        private Camera view;
        private EnemyMissileStressTest threats;
        private Texture2D missileIcon;
        private static readonly Color Threat = new Color(.38f, .96f, .58f, 1f);

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
            if (missileIcon == null) missileIcon = MakeMissileIcon();

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
                DrawVector(rect, source, player, scale, alpha);
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

        private void DrawVector(Rect rect, Vector2 source, Vector2 player, float s, float alpha)
        {
            Vector2 delta = source - player;
            float distance = delta.magnitude;
            if (distance < 1f) return;
            Vector2 outward = delta / distance;
            // Intersect the ray from the PLAYER, not the screen centre. Camera
            // lead must not shift the displayed bearing to a different edge.
            float tx = outward.x > .0001f ? (rect.xMax - player.x) / outward.x :
                outward.x < -.0001f ? (rect.xMin - player.x) / outward.x : float.PositiveInfinity;
            float ty = outward.y > .0001f ? (rect.yMax - player.y) / outward.y :
                outward.y < -.0001f ? (rect.yMin - player.y) / outward.y : float.PositiveInfinity;
            float visibleDistance = Mathf.Min(distance, Mathf.Min(tx, ty));
            const float gap = 38f;
            const float iconLength = 25f;
            float span = visibleDistance - gap * s;
            if (span < (iconLength + 3f) * s) return; // Never overlap the player's hull.
            Vector2 start = player + outward * visibleDistance;
            Vector2 inward = -outward;
            float length = iconLength * s;
            float width = 1.8f * s;
            float height = length * .5f;

            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg, start);
            // A narrow dark backing preserves contrast over sunlit sand.
            GUI.color = new Color(.025f, .065f, .035f, alpha * .65f);
            GUI.DrawTexture(new Rect(start.x, start.y - width, span - length + 2f * s, width * 2f), Texture2D.whiteTexture);
            GUI.color = new Color(Threat.r, Threat.g, Threat.b, alpha);
            GUI.DrawTexture(new Rect(start.x, start.y - width * .5f, span - length + 2f * s, width), Texture2D.whiteTexture);
            // The icon points inward and its nose stops short of the aircraft.
            GUI.DrawTexture(new Rect(start.x + span - length, start.y - height * .5f, length, height), missileIcon);
            GUI.matrix = before;
        }

        // Small antialiased silhouette: pointed nose, long body, swept tail fins.
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
                    bool inside = false;
                    for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
                    {
                        Vector2 a = outline[i], b = outline[j];
                        if ((a.y > py) != (b.y > py) && px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)
                            inside = !inside;
                    }
                    if (inside) covered++;
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
