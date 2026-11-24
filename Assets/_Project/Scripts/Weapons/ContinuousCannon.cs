using System;
using UnityEngine;

namespace AirCombat
{
    // Independent charge/cadence. The selected weapon receives trigger ticks;
    // a deselected weapon catches up its idle recovery from the same game clock.
    public sealed class ContinuousCannon
    {
        private readonly float interval;
        private readonly int capacity;
        private readonly float refillRate;
        private readonly float emptyLockout;
        private readonly Action fire;
        private float charge;
        private float lastUpdate;
        private float nextShot;
        private float recoverAt;
        private bool held;
        private bool overheated;

        public ContinuousCannon(float rate, int capacity, float refillSeconds,
            float emptyLockoutSeconds, Action emitRound, float now)
        {
            interval = 1f / Mathf.Max(1f, rate);
            this.capacity = Mathf.Max(1, capacity);
            refillRate = this.capacity / Mathf.Max(.1f, refillSeconds);
            emptyLockout = Mathf.Max(0f, emptyLockoutSeconds);
            fire = emitRound ?? throw new ArgumentNullException(nameof(emitRound));
            Reset(now);
        }

        public void Reset(float now)
        {
            charge = capacity;
            lastUpdate = nextShot = recoverAt = now;
            held = overheated = false;
        }

        private void Advance(float now)
        {
            if (now <= lastUpdate) return;
            if (overheated)
            {
                charge = Mathf.Min(capacity, charge + refillRate *
                    Mathf.Max(0f, now - Mathf.Max(lastUpdate, recoverAt)));
                if (charge >= capacity)
                {
                    overheated = false;
                    nextShot = now; // Never catch up shots during the lockout.
                }
            }
            else if (!held)
                charge = Mathf.Min(capacity, charge + refillRate * (now - lastUpdate));
            lastUpdate = now;
        }

        public float Readiness01(float now)
        {
            Advance(now);
            return Mathf.Clamp01(charge / capacity);
        }

        public bool IsOverheated(float now)
        {
            Advance(now);
            return overheated;
        }

        public void Tick(bool trigger, float now)
        {
            Advance(now);
            // Pressing again cannot bypass the ordinary shot interval.
            if (trigger && !held) nextShot = Mathf.Max(nextShot, now);
            held = trigger;
            if (!held || overheated) return;

            int emitted = 0;
            while (now >= nextShot && emitted < 3 && charge >= 1f)
            {
                nextShot += interval;
                charge -= 1f;
                emitted++;
                fire();
                if (charge < 1f)
                {
                    charge = 0f;
                    overheated = true;
                    recoverAt = now + emptyLockout;
                    return;
                }
            }
            if (emitted >= 3 && nextShot < now) nextShot = now + interval;
        }
    }
}
