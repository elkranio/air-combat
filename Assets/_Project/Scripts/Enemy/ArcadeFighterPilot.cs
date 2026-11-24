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
            [Header("Wingman separation")]
            [Min(0f)] public float separationRadius = 50f;
            [Range(0f, 90f)] public float separationTurnDegrees = 55f;
            [NonSerialized] public float attackReactionSeconds;

            // Keep the authored baseline untouched. Two non-overlapping tempo
            // bands guarantee some variety, with the assignments swapped each sortie.
            public Settings CreateVariant(bool later)
            {
                var variant = (Settings)MemberwiseClone();
                variant.attackReactionSeconds = later
                    ? UnityEngine.Random.Range(.35f, .6f) : UnityEngine.Random.Range(.05f, .2f);
                variant.attackDistance *= later
                    ? UnityEngine.Random.Range(.88f, .98f) : UnityEngine.Random.Range(1.04f, 1.12f);
                float exitTempo = later
                    ? UnityEngine.Random.Range(1.1f, 1.25f) : UnityEngine.Random.Range(.8f, .95f);
                variant.exitMinSeconds *= exitTempo;
                variant.exitMaxSeconds *= exitTempo;
                variant.closeApproachSeconds *= UnityEngine.Random.Range(.9f, 1.1f);
                variant.exitDistance *= UnityEngine.Random.Range(.9f, 1.1f);
                variant.catchUpDistance *= UnityEngine.Random.Range(.9f, 1.1f);
                variant.exitBoostSeconds *= UnityEngine.Random.Range(.85f, 1.15f);
                return variant;
            }
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
        private float linedUpSince = -1f;
        private readonly float fallbackSeparationSide;
        private float separationSide;
        private Transform wingman;
        private bool catchUpBoost;
        private Vector3 exitHeading;

        public Phase State { get; private set; } = Phase.Approach;
        public int AttackPasses { get; private set; }
        public int FiredCount => gun.FiredCount;
        public int HitCount => gun.HitCount;

        public ArcadeFighterPilot(CombatAircraftFlight flight, Afterburner boost,
            AutocannonWeapon gun, Transform target, FlightController targetFlight,
            float boundary, Settings settings, float fallbackSeparationSide = 1f)
        {
            this.flight = flight;
            this.boost = boost;
            this.gun = gun;
            trigger = gun;
            this.target = target;
            this.targetFlight = targetFlight;
            this.boundary = boundary;
            this.settings = settings ?? new Settings();
            this.fallbackSeparationSide = fallbackSeparationSide < 0f ? -1f : 1f;
            phaseStarted = Time.time;
        }

        // Called before CombatAircraftFlight.Update. No direct transform changes.
        public void TickFlight(float now, Transform liveWingman = null)
        {
            wingman = liveWingman;
            if (wingman == null) separationSide = 0f;
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
                if (distance > attackRange)
                {
                    closeSince = -1f;
                    linedUpSince = -1f;
                }
                else
                {
                    if (closeSince < 0f) closeSince = now;
                    bool linedUp = distance > settings.passDistance &&
                        Mathf.Abs(HeadingError(AimVector(delta, distance))) <= 50f;
                    if (!linedUp) linedUpSince = -1f;
                    else if (linedUpSince < 0f) linedUpSince = now;
                    // Delay the phase decision, never the actual steering input.
                    if (linedUp && now - linedUpSince >= settings.attackReactionSeconds)
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
            linedUpSince = -1f;
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
            if (State != Phase.ReturnToMap)
                direction = WithSeparation(direction);
            else separationSide = 0f;
            flight.SetPilotCommand(Mathf.Clamp(HeadingError(direction) / 30f, -1f, 1f), wantsBoost);
        }

        private Vector3 WithSeparation(Vector3 direction)
        {
            float radius = settings.separationRadius;
            if (wingman == null || radius <= 0f || settings.separationTurnDegrees <= 0f)
            {
                separationSide = 0f;
                return direction;
            }
            Vector3 away = Flat(flight.transform.position - wingman.position);
            float distance = away.magnitude;
            if (distance >= radius)
            {
                separationSide = 0f;
                return direction;
            }
            Vector3 heading = Flat(direction).normalized;
            if (heading.sqrMagnitude < .0001f) heading = Flat(flight.transform.forward).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, heading);
            float lateral = distance > .001f ? Vector3.Dot(right, away / distance) : 0f;
            // Commit to a side for this close encounter, rather than oscillating
            // left/right at the moment the two aircraft cross or overlap.
            if (separationSide == 0f)
            {
                if (Mathf.Abs(lateral) > .15f) separationSide = Mathf.Sign(lateral);
                else
                {
                    // On a head-on encounter both turn to their own right;
                    // for a same-heading stack the two indices choose opposite sides.
                    bool headOn = Vector3.Dot(heading, Flat(wingman.forward).normalized) < -.5f;
                    separationSide = headOn ? 1f : fallbackSeparationSide;
                }
            }
            float strength = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius * .35f, radius, distance));
            return Quaternion.AngleAxis(separationSide * settings.separationTurnDegrees * strength,
                Vector3.up) * heading;
        }

        private float HeadingError(Vector3 direction) => direction.sqrMagnitude > .0001f
            ? Vector3.SignedAngle(flight.transform.forward, direction, Vector3.up) : 0f;

        private static Vector3 Flat(Vector3 value) { value.y = 0f; return value; }
    }
}
