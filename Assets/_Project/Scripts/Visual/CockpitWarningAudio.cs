using UnityEngine;

namespace AirCombat
{
    // Boundary presentation owns its audio resources and chime clock.
    internal sealed class BoundaryWarningAudio
    {
        private readonly AudioSource speaker;
        private readonly AudioClip tone;
        private float nextChime;

        internal BoundaryWarningAudio(Transform owner)
        {
            var go = new GameObject("Mission boundary caution", typeof(AudioSource));
            go.transform.SetParent(owner, false);
            speaker = go.GetComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0f;
            speaker.volume = .14f;
            speaker.dopplerLevel = 0f;
            tone = MakeTone();
        }

        internal void Tick(int level, bool changed)
        {
            if (changed)
            {
                speaker.Stop();
                nextChime = Time.time;
            }
            if (Time.time >= nextChime)
            {
                speaker.pitch = level == 2 ? 1.22f : 1f;
                speaker.PlayOneShot(tone);
                nextChime = Time.time + (level == 2 ? .65f : 1.45f);
            }
        }

        internal void Clear(bool wasWarning)
        {
            if (wasWarning && speaker != null) speaker.Stop();
            nextChime = 0f;
        }

        internal void Dispose()
        {
            if (speaker != null) Object.Destroy(speaker.gameObject);
            if (tone != null) Object.Destroy(tone);
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

    // SAM owns launch/lock timing, including the required three-pulse gate.
    // This helper only creates and plays the existing cockpit warning tone.
    internal sealed class SamWarningAudio
    {
        private readonly AudioSource warningAudio;
        private readonly AudioClip warningClip;

        internal SamWarningAudio(GameObject owner)
        {
            warningAudio = owner.AddComponent<AudioSource>();
            warningAudio.playOnAwake = false;
            warningAudio.spatialBlend = 0f; // Cockpit receiver, not world audio.
            warningAudio.volume = .46f;
            warningAudio.dopplerLevel = 0f;
            warningClip = CreateWarningTone();
        }

        internal bool HasSource => warningAudio != null;
        internal bool CanPlay => warningAudio != null && warningClip != null;
        internal void Play() { if (CanPlay) warningAudio.PlayOneShot(warningClip); }
        internal void Stop() { if (warningAudio != null) warningAudio.Stop(); }

        internal void Dispose()
        {
            if (warningAudio != null) { warningAudio.Stop(); Object.Destroy(warningAudio); }
            if (warningClip != null) Object.Destroy(warningClip);
        }

        private static AudioClip CreateWarningTone()
        {
            const int sampleRate = 22050;
            const int samples = 2646; // 120ms: audible chirp with short release.
            float[] wave = new float[samples];
            for (int i = 0; i < wave.Length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1f, t * 90f) * Mathf.Min(1f, (samples - i) / 350f);
                wave[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * envelope * .46f;
            }
            var clip = AudioClip.Create("SAM threat receiver / beep", samples, 1, sampleRate, false);
            clip.SetData(wave, 0);
            return clip;
        }
    }
}
