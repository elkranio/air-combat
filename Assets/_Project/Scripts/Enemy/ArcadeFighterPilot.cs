using System;
using UnityEngine;

namespace AirCombat
{
    // Arcade attack rhythm. The motor owns movement, the weapon owns burst/cooldown.
    // No input, scene references, projectile spawning or threat reactions here.
    public sealed class ArcadeFighterPilot
    {
        public enum Phase { Approach, Attack, Disengage, ReturnToMap }

        [Serializable]
        public sealed class Settings
        {
            [Min(1f)] public float attackDistance = 170f;
            [Min(1f)] public float fireDistance = 125f;
            [Range(1f, 30f)] public float fireHalfAngle = 6f;
            [Min(.1f)] public float attackSeconds = 3.2f;
            [Min(.1f)] public float closeApproachSeconds = 3f;
            [Min(1f)] public float passDistance = 23f;
            [Min(1f)] public float exitDistance = 150f;
            [Min(.1f)] public float exitMinSeconds = 1.3f;
            [Min(.1f)] public float exitMaxSeconds = 2.7f;
            [Min(1f)] public float catchUpDistance = 240f;
            [Min(0f)] public float exitBoostSeconds = .7f;
            [Range(0f, 1f)] public float aimLeadFraction = .75f;
        }

        private readonly CombatAircraftFlight flight;
        private readonly Afterburner boost;
        private readonly AutocannonWeapon gun;
        private readonly ITriggeredWeapon trigger;
        private readonly Transform target;
        private readonly FlightController targetFlight;
        private readonly Settings settings;
        private readonly float boundary;
        private float phaseStarted;
        private float closeSince = -1f;
        private bool catchUpBoost;
        private Vector3 exitHeading;

        public Phase State { get; private set; } = Phase.Approach;
        public int AttackPasses { get; private set; }
        public int FiredCount => gun.FiredCount;
        public int HitCount => gun.HitCount;

        public ArcadeFighterPilot(CombatAircraftFlight flight, Afterburner boost,
            AutocannonWeapon gun, Transform target, FlightController targetFlight,
            float boundary, Settings settings)
        {
            this.flight = flight;
            this.boost = boost;
            this.gun = gun;
            trigger = gun;
            this.target = target;
            this.targetFlight = targetFlight;
            this.boundary = boundary;
            this.settings = settings ?? new Settings();
            phaseStarted = Time.time;
        }

        // Called before CombatAircraftFlight.Update. No direct transform changes.
        public void TickFlight(float now)
        {
            Vector3 position = flight.transform.position;
            float extent = Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.z));
            if (State != Phase.ReturnToMap && extent > boundary - 150f)
            {
                Enter(Phase.ReturnToMap, now);
                trigger.CeaseFire(now);
            }
            if (State == Phase.ReturnToMap)
            {
                if (extent >= boundary - 300f)
                {
                    SteerTo(new Vector3(-position.x, 0f, -position.z), false);
                    return;
                }
                Enter(Phase.Approach, now);
            }

            Vector3 delta = Flat(target.position - position);
            float distance = delta.magnitude;
            float attackRange = Mathf.Max(settings.fireDistance, settings.attackDistance);
            float age = now - phaseStarted;

            if (State == Phase.Approach)
            {
                if (distance > attackRange) closeSince = -1f;
                else
                {
                    if (closeSince < 0f) closeSince = now;
                    if (distance > settings.passDistance &&
                        Mathf.Abs(HeadingError(AimVector(delta, distance))) <= 50f)
                    {
                        Enter(Phase.Attack, now);
                        AttackPasses++;
                        age = 0f;
                    }
                    else if (now - closeSince >= settings.closeApproachSeconds)
                    {
                        // Allow time to turn back after disengage, but never circle
                        // indefinitely when neither aircraft can line up a pass.
                        exitHeading = flight.transform.forward;
                        Enter(Phase.Disengage, now);
                        age = 0f;
                    }
                }
            }
            if (State == Phase.Attack)
            {
                bool passed = distance <= settings.passDistance ||
                    (distance < attackRange && Vector3.Dot(flight.transform.forward, delta) < 0f);
                if (passed || age >= settings.attackSeconds)
                {
                    exitHeading = flight.transform.forward;
                    Enter(Phase.Disengage, now);
                    age = 0f;
                }
            }
            if (State == Phase.Disengage)
            {
                bool minimumElapsed = age >= settings.exitMinSeconds;
                bool finished = minimumElapsed && (distance >= settings.exitDistance ||
                    age >= Mathf.Max(settings.exitMinSeconds, settings.exitMaxSeconds));
                if (!finished)
                {
                    SteerTo(exitHeading, age < settings.exitBoostSeconds && boost.Charge01 > .05f);
                    return;
                }
                Enter(Phase.Approach, now);
            }

            Vector3 aim = AimVector(delta, distance);
            bool wantsBoost = false;
            if (State == Phase.Approach)
            {
                // Spend the same boost resource as the player. Recharge instead of
                // holding an empty tank; don't spend it trying to turn around.
                if (boost.Charge01 >= .8f) catchUpBoost = true;
                if (boost.Charge01 <= .02f) catchUpBoost = false;
                wantsBoost = catchUpBoost && distance > settings.catchUpDistance &&
                    Mathf.Abs(HeadingError(aim)) < 25f;
            }
            SteerTo(aim, wantsBoost);
        }

        // Called after movement. Fire the real mounted AC-01 only in the attack
        // phase. Releasing the trigger lets a committed burst finish normally.
        public void TickFire(float now)
        {
            if (State == Phase.ReturnToMap) return;
            Vector3 delta = Flat(target.position - flight.transform.position);
            float distance = delta.magnitude;
            float range = Mathf.Min(settings.fireDistance, gun.Preset.MaxRange);
            bool held = State == Phase.Attack && distance > settings.passDistance &&
                distance <= range && Mathf.Abs(HeadingError(AimVector(delta, distance))) <= settings.fireHalfAngle;
            trigger.TickTrigger(held, now);
        }

        public void Stop(float now)
        {
            trigger.CeaseFire(now);
            flight.SetPilotCommand(0f, false);
        }

        private void Enter(Phase next, float now)
        {
            State = next;
            phaseStarted = now;
            closeSince = -1f;
        }

        private Vector3 AimVector(Vector3 delta, float distance)
        {
            // Deliberately imperfect arcade anticipation, not a guaranteed intercept.
            float speed = targetFlight != null ? targetFlight.CurrentSpeed : 0f;
            float lead = Mathf.Min(.35f, distance / Mathf.Max(1f, gun.Preset.MuzzleVelocity)) *
                settings.aimLeadFraction;
            return delta + Flat(target.forward) * (speed * lead);
        }

        private void SteerTo(Vector3 direction, bool wantsBoost)
        {
            flight.SetPilotCommand(Mathf.Clamp(HeadingError(direction) / 30f, -1f, 1f), wantsBoost);
        }

        private float HeadingError(Vector3 direction) => direction.sqrMagnitude > .0001f
            ? Vector3.SignedAngle(flight.transform.forward, direction, Vector3.up) : 0f;

        private static Vector3 Flat(Vector3 value) { value.y = 0f; return value; }
    }
}
