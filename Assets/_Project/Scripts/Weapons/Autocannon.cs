using System;
using UnityEngine;

namespace AirCombat
{
    // Independent BURST firing cadence. No scene, input, projectile or VFX ownership.
    // The caller supplies an emission action for each physical round.
    public sealed class Autocannon
    {
        private readonly float roundsPerSecond;
        private readonly int burstSize;
        private readonly float burstCooldown;
        private readonly Action fire;
        private float nextShot;
        private int remaining;

        public int BurstSize => Mathf.Max(2, burstSize);
        public bool HasPendingShots => remaining > 0;

        // In-flight burst is unavailable; after the last round, refill over the
        // *actual* inter-burst cooldown. This is HUD data, not a second timer.
        public float Readiness01(float now) => remaining > 0 ? 0f :
            Mathf.Clamp01(1f - (nextShot - now) / burstCooldown);

        public Autocannon(float rate, int count, float cooldown, Action emitRound)
        {
            roundsPerSecond = Mathf.Max(1f, rate);
            burstSize = Mathf.Max(2, count);
            burstCooldown = Mathf.Max(.05f, cooldown);
            fire = emitRound ?? throw new ArgumentNullException(nameof(emitRound));
        }

        // Even reselecting the same slot cancels a partial burst and preserves its cooldown.
        public void CancelBurst(float now)
        {
            if (remaining > 0) nextShot = Mathf.Max(nextShot, now + burstCooldown);
            remaining = 0;
        }

        // Used only when the scene re-equips a selector; original behavior did not
        // impose an extra cooldown for the initial configuration.
        public void ClearPending() { remaining = 0; }

        public void ResetMissingMaterial(float now)
        {
            remaining = 0;
            nextShot = now;
        }

        public void Tick(bool held, float now)
        {
            if (remaining == 0 && held && now >= nextShot)
            {
                remaining = BurstSize;
                nextShot = now; // No accumulated backlog after a pause.
            }
            if (remaining == 0) return;

            float interval = 1f / roundsPerSecond;
            int emitted = 0;
            // Keep the existing max-three-shots-per-frame hitch protection.
            while (now >= nextShot && emitted < 3)
            {
                nextShot += interval;
                emitted++;
                fire();
                if (--remaining == 0)
                {
                    nextShot = now + burstCooldown;
                    break;
                }
            }
            if (emitted >= 3 && nextShot < now) nextShot = now + interval;
        }
    }
}
