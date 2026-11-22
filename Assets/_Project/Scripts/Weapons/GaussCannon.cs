using System;
using UnityEngine;

namespace AirCombat
{
    // Independent GAUSS single-click trigger and cooldown; renderer/projectile adapter
    // is supplied by a standalone GaussEmitter component.
    public sealed class GaussCannon
    {
        private readonly float cooldown;
        private readonly Action fire;
        private float nextReadyAt;

        public bool IsReady(float now) => now >= nextReadyAt;
        public float RemainingCooldown(float now) => nextReadyAt - now;

        public GaussCannon(float seconds, Action emitShot)
        {
            cooldown = Mathf.Max(.1f, seconds);
            fire = emitShot ?? throw new ArgumentNullException(nameof(emitShot));
        }

        // No held-fire repeats; reselecting a slot never resets this timer.
        public void Tick(bool pressedThisFrame, float now)
        {
            if (!pressedThisFrame || now < nextReadyAt) return;
            fire();
            nextReadyAt = now + cooldown;
        }
    }
}
