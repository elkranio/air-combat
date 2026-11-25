using System;
using UnityEngine;

namespace AirCombat
{
    // A mission-owned XZ operation rectangle, independent of visible terrain.
    // This is a plain helper: the launch sequence owns ordering, pause and retry.
    internal sealed class MissionBoundaryGuard
    {
        private readonly Rect bounds;
        private readonly float caution, critical;
        private readonly AudioSource speaker;
        private readonly AudioClip tone;
        private float distance = float.PositiveInfinity;
        private float nextChime;
        private int warningLevel;
        private GUIStyle label;

        public MissionBoundaryGuard(Transform owner, Rect bounds, float caution, float critical)
        {
            if (bounds.width <= .1f || bounds.height <= .1f)
                throw new ArgumentException("Operation bounds must have positive dimensions.");
            this.bounds = bounds;
            this.caution = Mathf.Max(1f, caution);
            this.critical = Mathf.Clamp(critical, 1f, this.caution);
            var go = new GameObject("Mission boundary caution", typeof(AudioSource));
            go.transform.SetParent(owner, false);
            speaker = go.GetComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0f;
            speaker.volume = .14f;
            speaker.dopplerLevel = 0f;
            tone = MakeTone();
        }

        public float Distance(Vector3 p) => Mathf.Min(
            Mathf.Min(p.x - bounds.xMin, bounds.xMax - p.x),
            Mathf.Min(p.z - bounds.yMin, bounds.yMax - p.z));

        // The rectangle is convex: an inside-to-inside segment cannot leave it.
        // For an outside endpoint, stop at the first edge, not at the overshoot.
        public bool TryClipExit(Vector3 from, Vector3 to, out Vector3 stop)
        {
            stop = to;
            if (Distance(to) > 0f && Distance(from) > 0f) return false;
            if (Distance(from) > 0f)
            {
                Vector3 delta = to - from;
                float t = 1f;
                if (to.x <= bounds.xMin) t = Mathf.Min(t, (bounds.xMin - from.x) / delta.x);
                if (to.x >= bounds.xMax) t = Mathf.Min(t, (bounds.xMax - from.x) / delta.x);
                if (to.z <= bounds.yMin) t = Mathf.Min(t, (bounds.yMin - from.z) / delta.z);
                if (to.z >= bounds.yMax) t = Mathf.Min(t, (bounds.yMax - from.z) / delta.z);
                stop = Vector3.Lerp(from, to, Mathf.Clamp01(t));
            }
            // Keep the final aircraft origin inside even with float rounding or
            // an invalid external teleport. The surrounding art covers the view.
            stop.x = Mathf.Clamp(stop.x, bounds.xMin + .01f, bounds.xMax - .01f);
            stop.z = Mathf.Clamp(stop.z, bounds.yMin + .01f, bounds.yMax - .01f);
            return true;
        }

        public void Tick(Vector3 position)
        {
            if (!Application.isFocused) { Clear(); return; }
            distance = Distance(position);
            int level = distance <= critical ? 2 : distance <= caution ? 1 : 0;
            if (level == 0) { Clear(); return; }
            if (level != warningLevel)
            {
                speaker.Stop();
                nextChime = Time.time;
                warningLevel = level;
            }
            if (Time.time >= nextChime)
            {
                speaker.pitch = level == 2 ? 1.22f : 1f;
                speaker.PlayOneShot(tone);
                nextChime = Time.time + (level == 2 ? .65f : 1.45f);
            }
        }

        public void Clear()
        {
            if (warningLevel != 0 && speaker != null) speaker.Stop();
            warningLevel = 0;
            distance = float.PositiveInfinity;
            nextChime = 0f;
        }

        public void DrawWarning()
        {
            if (warningLevel == 0) return;
            if (label == null) label = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
            };
            label.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .022f, 14f, 26f));
            float width = Mathf.Min(Screen.width * .62f, 660f);
            var rect = new Rect((Screen.width - width) * .5f, Screen.height * .085f, width,
                Mathf.Max(36f, label.fontSize * 1.8f));
            Color previous = GUI.color;
            GUI.color = new Color(.035f, .045f, .055f, .82f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            Color ink = warningLevel == 2 ? new Color(1f, .32f, .25f) : new Color(1f, .79f, .35f);
            if (warningLevel == 2) ink.a = Mathf.Lerp(.68f, 1f, .5f + .5f * Mathf.Sin(Time.unscaledTime * 9f));
            GUI.color = ink;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            label.normal.textColor = ink;
            string message = warningLevel == 2 ? "RETURN TO COMBAT AREA" : "APPROACHING OPERATION BOUNDARY";
            GUI.Label(rect, $"{message}  /  {Mathf.CeilToInt(distance)} m", label);
            GUI.color = previous;
        }

        public void Dispose()
        {
            Clear();
            if (speaker != null) UnityEngine.Object.Destroy(speaker.gameObject);
            if (tone != null) UnityEngine.Object.Destroy(tone);
        }

        private static AudioClip MakeTone()
        {
            const int rate = 22050;
            const float duration = .22f;
            var samples = new float[Mathf.RoundToInt(rate * duration)];
            for (int i = 0; i < samples.Length; ++i)
            {
                float t = i / (float)rate;
                float envelope = Mathf.SmoothStep(0f, 1f, t / .035f) *
                    Mathf.SmoothStep(0f, 1f, (duration - t) / .09f);
                // Soft lower register, distinct from terrain and SAM cues.
                samples[i] = Mathf.Sin(2f * Mathf.PI * 390f * t) * envelope * .65f;
            }
            var clip = AudioClip.Create("Operation boundary caution", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
