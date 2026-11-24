using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Temporary high-frequency test policy. Owns independent launch schedules,
    // not flight commands. Projectiles outlive their launch aircraft.
    [DisallowMultipleComponent]
    public sealed class EnemyMissileStressTest : MonoBehaviour
    {
        [Serializable]
        public sealed class Settings
        {
            public bool missileOnly = true;
            [Min(.1f)] public float minInterval = 2f;
            [Min(.1f)] public float maxInterval = 3.2f;
            [Min(.1f)] public float warningSeconds = .8f;
            [Min(0f)] public float minLaunchRange = 90f;
            [Min(1f)] public float maxLaunchRange = 300f;
            [Range(1f, 180f)] public float acquisitionHalfAngle = 75f;
            [Min(.1f)] public float speed = 140f;
            [Min(1f)] public float turnRate = 95f;
            [Min(0f)] public float turnBudget = 120f;
            [Min(.1f)] public float lifetime = 4f;
            [Min(.1f)] public float hitRadius = 5f;
            [Min(1)] public int damage = 6;
        }

        private sealed class Shooter
        {
            public PlayerHitPoints Health;
            public JetAssembly Aircraft;
            public float NextAttempt;
            public float LaunchAt = -1f;
        }

        private readonly List<Shooter> shooters = new List<Shooter>();
        private readonly List<MissileProjectile> live = new List<MissileProjectile>();
        private readonly System.Random random = new System.Random();
        private PlayerHitPoints target;
        private MissileProjectile prefab;
        private CombatHitFeedback feedback;
        private Settings settings;
        private AudioSource warningSource;
        private AudioClip warningClip;
        private float nextWarning;
        public int Launched { get; private set; }
        public int Hits { get; private set; }
        public int InFlight => live.Count;

        public void Bind(PlayerHitPoints player, MissileProjectile projectile,
            CombatHitFeedback hitFeedback, Settings config)
        {
            target = player;
            prefab = projectile;
            feedback = hitFeedback;
            settings = config ?? new Settings();
            warningSource = gameObject.AddComponent<AudioSource>();
            warningSource.playOnAwake = false;
            warningSource.spatialBlend = 0f;
            warningClip = MakeWarning();
        }

        public void Register(PlayerHitPoints health)
        {
            if (health == null) return;
            shooters.Add(new Shooter { Health = health, Aircraft = health.GetComponent<JetAssembly>(),
                NextAttempt = Time.time + Range(.15f, .8f) });
        }

        public void Unregister(PlayerHitPoints health)
        {
            // A killed shooter loses pending attacks, not its airborne missiles.
            shooters.RemoveAll(s => s.Health == health);
        }

        public void Tick(float now)
        {
            live.RemoveAll(m => m == null);
            if (prefab == null || !WeaponLock.Alive(target)) return;
            foreach (var shooter in shooters)
            {
                if (!WeaponLock.Alive(shooter.Health)) continue;
                if (shooter.LaunchAt >= 0f)
                {
                    if (now < shooter.LaunchAt) continue;
                    Launch(shooter);
                    shooter.LaunchAt = -1f;
                    shooter.NextAttempt = now + Range(settings.minInterval,
                        Mathf.Max(settings.minInterval, settings.maxInterval));
                    continue;
                }
                if (now < shooter.NextAttempt || !CanAcquire(shooter.Health.transform)) continue;
                shooter.LaunchAt = now + Mathf.Max(.65f, settings.warningSeconds);
                // Nearly simultaneous warnings share one distress cue rather than
                // stacking identical audio clips at full volume.
                if (now >= nextWarning && warningSource != null)
                {
                    warningSource.PlayOneShot(warningClip, .65f);
                    nextWarning = now + .2f;
                }
            }
        }

        private bool CanAcquire(Transform owner)
        {
            Vector3 delta = target.transform.position - owner.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            return distance >= settings.minLaunchRange && distance <= settings.maxLaunchRange &&
                Vector3.Angle(owner.forward, delta) <= settings.acquisitionHalfAngle;
        }

        private void Launch(Shooter shooter)
        {
            Transform owner = shooter.Health.transform;
            var chassis = shooter.Aircraft != null ? shooter.Aircraft.Chassis : null;
            var mount = chassis != null ? chassis.MissileMount : null;
            Vector3 point = mount != null ? mount.position : owner.position + owner.forward * 6f;
            point.y = owner.position.y;
            Vector3 heading = Vector3.ProjectOnPlane(owner.forward, Vector3.up).normalized;
            var missile = Instantiate(prefab, point, Quaternion.LookRotation(heading, Vector3.up));
            missile.name = "Tiger AAM / stress test";
            float coreDebt = 1f, cloudDebt = 1f;
            missile.ConfigureArcade(target, settings.speed, settings.turnRate, settings.turnBudget,
                settings.lifetime, settings.hitRadius, (from, to) =>
                {
                    if (feedback != null) feedback.EmitMissileExhaust(from, to, ref coreDebt, ref cloudDebt);
                });
            missile.Launch(target.transform, (hitPoint, hit) =>
            {
                if (WeaponLock.Alive(target) && target.ApplyDamage(hitPoint, settings.damage)) Hits++;
            });
            live.Add(missile);
            Launched++;
        }

        public void StopTest()
        {
            foreach (var missile in live) if (missile != null) missile.Cancel();
            live.Clear();
            shooters.Clear();
            if (warningSource != null) warningSource.Stop();
            nextWarning = 0f;
        }

        public void ResetTest()
        {
            StopTest();
            Launched = Hits = 0;
        }

        private float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        private static AudioClip MakeWarning()
        {
            const int rate = 22050;
            const float pulsePeriod = .18f, pulseLength = .105f;
            var samples = new float[Mathf.RoundToInt(rate * .48f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float p = t % pulsePeriod;
                if (p >= pulseLength) continue;
                float env = Mathf.Clamp01(p / .006f) * Mathf.Clamp01((pulseLength - p) / .018f);
                // Urgent descending dual-tone chirp, distinct from the player's 740 Hz lock tone.
                float phase = 2f * Mathf.PI * (1500f * p - 1800f * p * p);
                samples[i] = env * (.43f * Mathf.Sin(phase) + .18f * Mathf.Sin(phase * .67f));
            }
            var clip = AudioClip.Create("Incoming AAM / three warning pulses", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            StopTest();
            if (warningClip != null) Destroy(warningClip);
            if (warningSource != null) Destroy(warningSource);
        }
    }
}
