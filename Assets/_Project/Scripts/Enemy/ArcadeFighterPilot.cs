using System;
using System.Collections.Generic;
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
            [Range(1f, 30f)] public float fireHalfAngle = 10f;
            // Timeout without actual gunfire, not a fixed duration of a successful attack.
            [Min(.1f)] public float attackSeconds = 5.5f;
            [Min(.1f)] public float closeApproachSeconds = 3f;
            [Min(1f)] public float passDistance = 23f;
            [Min(1f)] public float exitDistance = 150f;
            [Min(.1f)] public float exitMinSeconds = 1.3f;
            [Min(.1f)] public float exitMaxSeconds = 2.7f;
            [Min(1f)] public float catchUpDistance = 100f;
            [Min(0f)] public float exitBoostSeconds = .7f;
            [Range(0f, 1f)] public float aimLeadFraction = .75f;
            [Header("Imperfect gun aim")]
            [Range(0f, 35f)] public float burstAimError = 15f;
            [Min(0f)] public float aimDelayMin = .15f;
            [Min(0f)] public float aimDelayMax = .25f;
            [Min(1)] public int minBurstsPerPass = 2;
            [Min(1)] public int maxBurstsPerPass = 3;
            [Min(.1f)] public float maxGunPressureSeconds = 4f;
            [Header("Powered pursuit")]
            [Min(1f)] public float tightTurnDistance = 200f;
            [Range(10f, 90f)] public float tightEnterAngle = 40f;
            [Range(1f, 60f)] public float tightExitAngle = 20f;
            [Range(0f, 1f)] public float fuelResume = .4f;
            [Range(0f, 1f)] public float fuelReserve = .08f;
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
                variant.attackSeconds *= UnityEngine.Random.Range(.9f, 1.1f);
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
        private bool fuelAvailable, tightTurning, pursuitBoost;
        private float lastGunActivity;
        private Vector3 exitHeading;
        private struct AimSample
        {
            public float Time;
            public Vector3 Position, Velocity;
        }
        private readonly List<AimSample> aimHistory = new List<AimSample>(64);
        private readonly float aimDelay;
        private AimSample delayedAim;
        private float burstAimOffset;
        private int burstLimit, burstsStarted;
        private float firstBurstAt = -1f;
        public int BurstsThisPass => burstsStarted;
        public int BurstLimit => burstLimit;

        public Phase State { get; private set; } = Phase.Approach;
        public int AttackPasses { get; private set; }
        public int FiredCount => gun.FiredCount;
        public int HitCount => gun.HitCount;
        public string FlightMode => boost.IsManeuvering ? "TIGHT" : boost.IsActive ? "BOOST" : "CRUISE";

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
            lastGunActivity = phaseStarted;
            float delayMin = Mathf.Max(0f, this.settings.aimDelayMin);
            aimDelay = UnityEngine.Random.Range(delayMin, Mathf.Max(delayMin, this.settings.aimDelayMax));
            SampleAim(phaseStarted);
        }

        // Called before CombatAircraftFlight.Update. No direct transform changes.
        public void TickFlight(float now, Transform liveWingman = null)
        {
            SampleAim(now);
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
                    if ((linedUp && now - linedUpSince >= settings.attackReactionSeconds) ||
                        now - closeSince >= settings.closeApproachSeconds)
                    {
                        // A difficult approach becomes an active pursuit, not an
                        // automatic fly-away just because the nose is not lined up.
                        Enter(Phase.Attack, now);
                        AttackPasses++;
                        age = 0f;
                    }
                }
            }
            if (State == Phase.Attack)
            {
                // Crossing the nose is not itself a reason to give up. Turn
                // back into the fight. Only point-blank spacing or a prolonged
                // failed pursuit forces a break; a working gun solution stays.
                bool stalled = now - lastGunActivity >= settings.attackSeconds &&
                    !HasGunSolution(delta, distance) && !gun.HasPendingShots;
                // Allow a committed burst to finish before leaving a productive
                // gun pass. A time cap also prevents endless intermittent fire.
                bool spent = !gun.HasPendingShots && (burstsStarted >= burstLimit ||
                    (firstBurstAt >= 0f && now - firstBurstAt >= settings.maxGunPressureSeconds));
                if (distance <= settings.passDistance || stalled || spent)
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

            Pursue(delta, distance);
        }

        private void Pursue(Vector3 delta, float distance)
        {
            float gunRange = Mathf.Min(settings.fireDistance, gun.Preset.MaxRange);
            Vector3 desired = State == Phase.Attack && distance <= gunRange + 15f
                ? GunAimVector(distance) : AimVector(delta, distance);
            Vector3 aim = WithSeparation(desired);
            float error = HeadingError(aim);
            float angle = Mathf.Abs(error);
            // Hysteresis gives the shared tank time to recharge and avoids
            // one-frame on/off oscillation of the speed and turn mode.
            float reserve = Mathf.Clamp(settings.fuelReserve, 0f, .99f);
            float resume = Mathf.Clamp(settings.fuelResume, reserve + .01f, 1f);
            if (boost.Charge01 >= resume) fuelAvailable = true;
            if (boost.Charge01 <= reserve) fuelAvailable = false;
            float tightThreshold = tightTurning
                ? Mathf.Min(settings.tightExitAngle, settings.tightEnterAngle) : settings.tightEnterAngle;
            tightTurning = fuelAvailable && distance <= settings.tightTurnDistance && angle >= tightThreshold;

            // Don't just boost from far away: the slower Tiger needs to close
            // on an escaping player even inside the old attack range. Avoid
            // burning fuel on a rapidly closing head-on pass or a large turn.
            Vector3 along = distance > .001f ? delta / distance : Vector3.zero;
            float targetSpeed = targetFlight != null ? targetFlight.CurrentSpeed : 0f;
            float closingSpeed = Vector3.Dot(Flat(flight.transform.forward) * flight.CurrentSpeed -
                Flat(target.forward) * targetSpeed, along);
            bool startBoost = angle < 25f && distance > settings.catchUpDistance &&
                (closingSpeed < 15f || distance > settings.attackDistance);
            bool keepBoost = pursuitBoost && angle < 32f && distance > settings.catchUpDistance * .8f;
            pursuitBoost = fuelAvailable && !tightTurning && (startBoost || keepBoost);
            flight.SetPilotCommand(Mathf.Clamp(error / 30f, -1f, 1f), pursuitBoost, tightTurning);
        }

        // Called after movement. Fire the real mounted AC-01 only in the attack
        // phase. Releasing the trigger lets a committed burst finish normally.
        public void TickFire(float now)
        {
            if (State == Phase.ReturnToMap) return;
            Vector3 delta = Flat(target.position - flight.transform.position);
            float distance = delta.magnitude;
            bool held = State == Phase.Attack && burstsStarted < burstLimit &&
                (firstBurstAt < 0f || now - firstBurstAt < settings.maxGunPressureSeconds) &&
                HasGunSolution(delta, distance);
            bool pendingBefore = gun.HasPendingShots;
            int before = gun.FiredCount;
            trigger.TickTrigger(held, now);
            bool fired = gun.FiredCount > before;
            if (State != Phase.Attack) return;
            if (fired)
            {
                lastGunActivity = now;
                if (!pendingBefore)
                {
                    burstsStarted++;
                    if (firstBurstAt < 0f) firstBurstAt = now;
                }
            }
            // Hold the same aim error through every round of a committed burst;
            // roll a new error only when it actually finishes, including hitches.
            if ((pendingBefore || fired) && !gun.HasPendingShots) RollAimError();
        }

        private bool HasGunSolution(Vector3 delta, float distance)
        {
            float range = Mathf.Min(settings.fireDistance, gun.Preset.MaxRange);
            return distance > settings.passDistance && distance <= range &&
                Mathf.Abs(HeadingError(GunAimVector(distance))) <= Mathf.Min(settings.fireHalfAngle, 5f);
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
            tightTurning = pursuitBoost = false;
            if (next == Phase.Attack)
            {
                lastGunActivity = now;
                burstsStarted = 0;
                firstBurstAt = -1f;
                int min = Mathf.Max(1, settings.minBurstsPerPass);
                int max = Mathf.Max(min, settings.maxBurstsPerPass);
                burstLimit = UnityEngine.Random.Range(min, max + 1);
                RollAimError();
            }
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

        private void RollAimError()
        {
            float error = Mathf.Max(0f, settings.burstAimError);
            burstAimOffset = UnityEngine.Random.Range(-error, error);
        }

        private void SampleAim(float now)
        {
            var sample = new AimSample
            {
                Time = now,
                Position = target.position,
                Velocity = Flat(target.forward) * (targetFlight != null ? targetFlight.CurrentSpeed : 0f)
            };
            if (aimHistory.Count > 0 && aimHistory[aimHistory.Count - 1].Time >= now)
                aimHistory[aimHistory.Count - 1] = sample;
            else aimHistory.Add(sample);
            float wanted = now - aimDelay;
            int trim = 0;
            while (trim + 1 < aimHistory.Count && aimHistory[trim + 1].Time <= wanted) trim++;
            if (trim > 0) aimHistory.RemoveRange(0, trim);
            delayedAim = aimHistory[0];
            if (aimHistory.Count > 1)
            {
                AimSample a = aimHistory[0], b = aimHistory[1];
                float t = Mathf.InverseLerp(a.Time, b.Time, wanted);
                delayedAim.Position = Vector3.Lerp(a.Position, b.Position, t);
                delayedAim.Velocity = Vector3.Lerp(a.Velocity, b.Velocity, t);
            }
        }

        private Vector3 GunAimVector(float distance)
        {
            // Aim at the recently observed target, not its instantaneous heading.
            // Extrapolate that old velocity through the reaction delay so straight
            // flight remains dangerous but a new maneuver is not predicted perfectly.
            float lead = Mathf.Min(.35f, distance / Mathf.Max(1f, gun.Preset.MuzzleVelocity)) *
                settings.aimLeadFraction;
            Vector3 aim = Flat(delayedAim.Position - flight.transform.position) +
                delayedAim.Velocity * (aimDelay + lead);
            return Quaternion.AngleAxis(burstAimOffset, Vector3.up) * aim;
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
