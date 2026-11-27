using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // One real cargo location. Owns its runtime actors, not the mission clock or scoring.
    // CA03/CA04/CA05/CA06 retain the pose, FX and damage contracts accepted in the lab.
    internal sealed class CarryallEvacuationSite : IDisposable
    {
        internal enum CargoResult { Pending, Destroyed, Escaped }
        private enum State { Waiting, Inbound, Align, Pickup, Outbound, Crashing, Done }
        private readonly string label;
        private readonly GameObject root, hull, cargo;
        private readonly Transform carrier, payload, mount;
        private readonly Terrain terrain;
        private readonly AircraftRadar radar;
        private readonly Vector3[] route; // ground pad -> exterior pocket exit, at cruise altitude
        private readonly Vector3 ground;
        private readonly Quaternion heading;
        private readonly float speed, reserveDelay;
        private readonly int hullHp;
        private readonly CarryallRangeTarget hullTarget, cargoTarget;
        private readonly CarryallRotorVisual rotors;
        private readonly CarryallPickupVfx effects;
        private readonly CarryallCrashSequence crash;
        private readonly Contact hullContact, cargoContact;
        private CarryallPickupSequence sequence;
        private State state;
        private int nextNode, attempts;
        private float clock, launchAt, alignAge, wreckAge;
        private Quaternion alignFrom;
        private Vector3 velocity;
        internal CargoResult Result { get; private set; }

        internal CarryallEvacuationSite(string label, Transform owner, GameObject hullPrefab,
            GameObject cargoPrefab, Terrain terrain, AircraftRadar radar, Vector3[] path,
            float launchAt, float speed, float reserveDelay, int hullHp, int cargoHp)
        {
            this.label = label; this.terrain = terrain; this.radar = radar;
            this.launchAt = launchAt; this.speed = Mathf.Max(1f, speed);
            this.reserveDelay = Mathf.Max(5f, reserveDelay); this.hullHp = Mathf.Max(1, hullHp);
            route = (Vector3[])path.Clone();
            if (route.Length < 2) throw new ArgumentException("CA07 needs a pad and an exterior route.");
            ground = route[0];
            ground.y = terrain.SampleHeight(ground) + terrain.transform.position.y + .15f;
            float cruise = Mathf.Max(78f, ground.y + 12f);
            for (int i = 0; i < route.Length; i++) route[i].y = cruise;
            // Align cargo toward the corridor; the four capture sockets remain exact.
            heading = Quaternion.LookRotation(Vector3.ProjectOnPlane(route[1] - route[0], Vector3.up), Vector3.up);
            root = new GameObject("CA07 / " + label);
            root.transform.SetParent(owner, false);
            try
            {
                hull = Object.Instantiate(hullPrefab, root.transform);
                cargo = Object.Instantiate(cargoPrefab, root.transform);
                hull.name = label + " / Carryall"; cargo.name = label + " / Special Cargo";
                carrier = hull.transform; payload = cargo.transform;
                mount = carrier.Find("CargoMount");
                if (mount == null || mount.parent != carrier || (mount.localPosition - new Vector3(0f, -5f, 0f)).sqrMagnitude > .000001f ||
                    Quaternion.Angle(mount.localRotation, Quaternion.identity) > .01f ||
                    (carrier.lossyScale - Vector3.one).sqrMagnitude > .000001f ||
                    (payload.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                    throw new InvalidOperationException(label + ": invalid CA01/CG01 mount or scale.");
                payload.SetPositionAndRotation(ground, heading);
                rotors = hull.GetComponent<CarryallRotorVisual>();
                if (rotors == null) rotors = hull.AddComponent<CarryallRotorVisual>();
                if (!rotors.Initialize()) throw new InvalidOperationException(label + ": rotor initialization failed.");
                effects = CarryallPickupVfx.Create(carrier, root.transform, terrain);
                crash = new CarryallCrashSequence(carrier, payload, root.transform, terrain);
                hullTarget = hull.AddComponent<CarryallRangeTarget>();
                hullTarget.Configure(CombatTargetDomain.Air, new Vector3(0f, 2f, 1.11f),
                    new Vector3(27.3f, 8.2f, 23.22f), KillHull);
                cargoTarget = cargo.AddComponent<CarryallRangeTarget>();
                cargoTarget.Configure(CombatTargetDomain.Ground, new Vector3(0f, 2.5f, 0f),
                    new Vector3(8f, 5f, 16f), KillCargo);
                cargoTarget.ResetTarget(cargoHp);
                hull.SetActive(false);
                effects?.Clear();
                hullContact = new AirContact(carrier, hullTarget);
                cargoContact = new Contact(payload, cargoTarget, RadarContactType.GroundDefense);
                radar?.Register(hullContact); radar?.Register(cargoContact);
                state = State.Waiting;
            }
            catch { Dispose(); throw; }
        }

        private class Contact : IRadarContact
        {
            protected readonly Transform actor;
            private readonly CarryallRangeTarget target;
            public RadarContactType RadarKind { get; }
            public Vector3 RadarPosition => actor.position;
            public float RadarThreatRadius => 0f;
            public bool IsRadarVisible => actor != null && actor.gameObject.activeInHierarchy && target != null && target.Hp > 0;
            internal Contact(Transform actor, CarryallRangeTarget target, RadarContactType kind)
            { this.actor = actor; this.target = target; RadarKind = kind; }
        }

        private sealed class AirContact : Contact, IRadarAirContact
        {
            public Vector3 RadarForward => actor.forward;
            internal AirContact(Transform actor, CarryallRangeTarget target)
                : base(actor, target, RadarContactType.Aircraft) { }
        }

        private void Dispatch()
        {
            attempts++;
            crash.Reset(); // Only dispatch with intact grounded cargo; never restores HP.
            sequence = new CarryallPickupSequence(carrier, payload, mount, root.transform, ground, heading);
            sequence.Reset(new CarryallPickupSequence.Settings
            {
                cruiseY = route[0].y, approachDistance = 56f, approachLateral = 14f,
                approachSeconds = 3.5f, alignSeconds = 1.2f, descendSeconds = 5f,
                clampSeconds = 1.1f, liftSeconds = 6.5f, departSeconds = 9f,
                departDistance = 150f, stopBeforeDeparture = true
            });
            int end = route.Length - 1;
            carrier.SetPositionAndRotation(route[end], Quaternion.LookRotation(route[end - 1] - route[end], Vector3.up));
            hullTarget.ResetTarget(hullHp);
            rotors.ResetPhase(); effects?.Reset();
            velocity = Vector3.zero;
            nextNode = end - 1;
            state = State.Inbound;
        }

        internal void Tick(float dt, float now, bool mayDispatch)
        {
            clock = now;
            if (dt <= 0f) return;
            if (state == State.Waiting)
            {
                crash.AdvanceCargoDebris(dt);
                if (!mayDispatch || Result != CargoResult.Pending) { state = State.Done; return; }
                if (now >= launchAt) Dispatch();
                return;
            }
            if (state == State.Crashing)
            {
                crash.Advance(dt);
                rotors.Advance(720f * crash.RotorFactor * dt);
                if (!crash.HullLanded || (crash.HadPayload && !crash.CargoLanded)) return;
                wreckAge += dt;
                if (wreckAge < 4f) return;
                if (attempts == 1 && Result == CargoResult.Pending && mayDispatch)
                {
                    hull.SetActive(false);
                    state = State.Waiting;
                }
                else if (wreckAge >= 6f) state = State.Done;
                return;
            }
            if (state == State.Done) { crash.AdvanceCargoDebris(dt); return; }

            Vector3 before = carrier.position;
            var phase = CarryallPickupSequence.Phase.Approach;
            if (state == State.Inbound || state == State.Outbound)
            {
                bool inbound = state == State.Inbound;
                AdvanceRoute(dt, inbound);
                phase = inbound ? CarryallPickupSequence.Phase.Approach : CarryallPickupSequence.Phase.Depart;
            }
            else if (state == State.Align)
            {
                alignAge += dt;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(alignAge / 1.2f));
                carrier.rotation = Quaternion.Slerp(alignFrom, heading, t);
                phase = CarryallPickupSequence.Phase.Align;
                if (alignAge >= 1.2f) { sequence.HoldOverCargo(); state = State.Pickup; }
            }
            else if (state == State.Pickup)
            {
                sequence.Advance(dt);
                phase = sequence.CurrentPhase;
                if (phase == CarryallPickupSequence.Phase.Complete) BeginOutbound();
                else if (phase == CarryallPickupSequence.Phase.Failed)
                {
                    // Invalid capture is a visible configuration error, never a free extraction.
                    sequence.AbortPickup();
                }
            }
            velocity = (carrier.position - before) / Mathf.Max(dt, .00001f);
            rotors.Advance(720f * dt);
            if (state != State.Done)
                effects?.Tick(dt, phase, sequence.IsLoaded, 1f, 7f, true);
            crash.AdvanceCargoDebris(dt);
        }

        private void AdvanceRoute(float dt, bool inbound)
        {
            float budget = speed * dt;
            while (budget > .0001f && nextNode >= 0 && nextNode < route.Length)
            {
                Vector3 delta = route[nextNode] - carrier.position;
                float distance = delta.magnitude;
                if (distance > .001f)
                {
                    float step = Mathf.Min(distance, budget);
                    carrier.rotation = Quaternion.RotateTowards(carrier.rotation,
                        Quaternion.LookRotation(delta, Vector3.up), 100f * step / speed);
                    carrier.position += delta * (step / distance);
                    budget -= step;
                    if (step < distance) return;
                }
                carrier.position = route[nextNode];
                nextNode += inbound ? -1 : 1;
            }
            if (inbound && nextNode < 0)
            {
                alignFrom = carrier.rotation; alignAge = 0f; state = State.Align;
            }
            else if (!inbound && nextNode >= route.Length)
            {
                if (Result == CargoResult.Pending && sequence.IsLoaded)
                {
                    Result = CargoResult.Escaped;
                    cargoTarget.Deactivate();
                }
                hullTarget.Deactivate(); effects?.Clear(); hull.SetActive(false);
                state = State.Done;
            }
        }

        private void BeginOutbound()
        { nextNode = 1; state = State.Outbound; }

        private void KillHull()
        {
            if (state == State.Crashing || state == State.Done || state == State.Waiting) return;
            bool loaded = sequence != null && sequence.IsLoaded;
            hullTarget.Deactivate();
            if (loaded)
            {
                Result = CargoResult.Destroyed;
                cargoTarget.Deactivate();
            }
            effects?.Clear();
            crash.Begin(loaded, velocity, 24f, 16f);
            wreckAge = 0f;
            launchAt = clock + reserveDelay;
            state = State.Crashing;
        }

        private void KillCargo()
        {
            if (Result != CargoResult.Pending) return;
            Result = CargoResult.Destroyed;
            cargoTarget.Deactivate();
            if (sequence != null && sequence.IsLoaded && state != State.Crashing)
            { KillHull(); return; }
            crash.DestroyGroundCargo();
            if (state == State.Waiting) state = State.Done;
            else if (state == State.Inbound)
            {
                // Turn back along the current segment, never cut across a canyon wall.
                nextNode = Mathf.Min(route.Length - 1, nextNode + 1);
                state = State.Outbound;
            }
            else if (state == State.Align) BeginOutbound();
            else if (state == State.Pickup) sequence.AbortPickup();
        }

        internal string Status(float now)
        {
            string result = Result == CargoResult.Destroyed ? "DESTROYED" : Result == CargoResult.Escaped ? "ESCAPED" :
                $"CG HP {cargoTarget.Hp}";
            string activity = state == State.Waiting ? $"ETA {Mathf.Max(0f, launchAt - now):F0}s" :
                state == State.Pickup ? sequence.CurrentPhase.ToString() : state.ToString();
            if (float.IsPositiveInfinity(launchAt) && state == State.Waiting) activity = "TEST HOLD";
            return $"{label} | {result} | CA {attempts}/2 {activity} | HP {hullTarget.Hp}";
        }

        public void Dispose()
        {
            if (radar != null && hullContact != null) radar.Unregister(hullContact);
            if (radar != null && cargoContact != null) radar.Unregister(cargoContact);
            hullTarget?.Deactivate(); cargoTarget?.Deactivate();
            effects?.Dispose(); crash?.Dispose();
            if (root != null) { root.SetActive(false); Object.Destroy(root); }
        }
    }
}
