using UnityEngine;

namespace AirCombat
{
    // Reusable pose/payload sequence. No scene queries, mission rules, radar or damage.
    internal sealed class CarryallPickupSequence
    {
        internal enum Phase { Approach, Align, Descend, Clamp, Lift, Depart, Complete, Failed, Abort }
        internal struct Settings
        {
            public float cruiseY, approachDistance, approachLateral, approachSeconds, alignSeconds;
            public float descendSeconds, clampSeconds, liftSeconds, departSeconds, departDistance;
        }

        private readonly Transform carrier, payload, mount, groundParent;
        private readonly Vector3 ground;
        private readonly Quaternion heading;
        private Settings tuning;
        private Vector3 contact, cruise, start, staging;
        private Quaternion approachHeading, abortHeading;
        private Vector3 abortStart;
        private float elapsed;
        internal Phase CurrentPhase { get; private set; }
        internal bool IsLoaded { get; private set; }

        internal CarryallPickupSequence(Transform carrier, Transform payload, Transform mount,
            Transform groundParent, Vector3 ground, Quaternion heading)
        {
            this.carrier = carrier; this.payload = payload; this.mount = mount;
            this.groundParent = groundParent; this.ground = ground; this.heading = heading;
        }

        internal void Reset(Settings settings)
        {
            tuning = settings;
            elapsed = 0f;
            IsLoaded = false;
            CurrentPhase = Phase.Approach;
            payload.SetParent(groundParent, false);
            payload.SetPositionAndRotation(ground, heading);
            payload.gameObject.SetActive(true);
            // CargoMount is a direct, identity-rotation child with unit scale (validated by preview).
            contact = ground - heading * mount.localPosition;
            cruise = new Vector3(contact.x, Mathf.Max(contact.y + 5f, tuning.cruiseY), contact.z);
            Vector3 forward = heading * Vector3.forward;
            Vector3 right = heading * Vector3.right;
            start = cruise - forward * Mathf.Max(15f, tuning.approachDistance) + right * tuning.approachLateral;
            staging = cruise - forward * 8f + right * 2f;
            approachHeading = Quaternion.LookRotation((staging - start).normalized, Vector3.up);
            carrier.gameObject.SetActive(true);
            carrier.SetPositionAndRotation(start, approachHeading);
        }

        // Range starts directly above the cargo; no approach or teleport during descent.
        internal void HoldOverCargo()
        {
            elapsed = 0f;
            CurrentPhase = Phase.Descend;
            carrier.SetPositionAndRotation(cruise, heading);
        }

        internal void AbortPickup()
        {
            if (IsLoaded || CurrentPhase == Phase.Abort || CurrentPhase == Phase.Depart ||
                CurrentPhase == Phase.Complete) return;
            abortStart = carrier.position;
            abortHeading = carrier.rotation;
            elapsed = 0f;
            CurrentPhase = Phase.Abort;
        }

        internal void Advance(float dt)
        {
            // Consume leftover time at phase boundaries; a slow frame cannot skip attachment.
            float remaining = Mathf.Max(0f, dt);
            while (remaining > 0f && CurrentPhase != Phase.Complete && CurrentPhase != Phase.Failed)
            {
                float duration = Duration();
                float step = Mathf.Min(remaining, Mathf.Max(0f, duration - elapsed));
                elapsed += step;
                remaining -= step;
                bool finished = elapsed >= duration - .00001f;
                ApplyPose(finished ? 1f : Mathf.Clamp01(elapsed / duration));
                if (!finished) break;
                elapsed = 0f;
                NextPhase();
            }
        }

        private float Duration()
        {
            float seconds;
            switch (CurrentPhase)
            {
                case Phase.Approach: seconds = tuning.approachSeconds; break;
                case Phase.Align: seconds = tuning.alignSeconds; break;
                case Phase.Descend: seconds = tuning.descendSeconds; break;
                case Phase.Clamp: seconds = tuning.clampSeconds; break;
                case Phase.Lift: seconds = tuning.liftSeconds; break;
                case Phase.Abort: seconds = Mathf.Max(.75f, Mathf.Abs(cruise.y - abortStart.y) / 26f); break;
                default: seconds = tuning.departSeconds; break;
            }
            return Mathf.Max(.05f, seconds);
        }

        private void ApplyPose(float t)
        {
            float eased = t * t * (3f - 2f * t);
            Vector3 position;
            Quaternion rotation = heading;
            switch (CurrentPhase)
            {
                case Phase.Approach:
                    position = Vector3.LerpUnclamped(start, staging, eased);
                    rotation = approachHeading;
                    break;
                case Phase.Align:
                    position = Vector3.LerpUnclamped(staging, cruise, eased);
                    rotation = Quaternion.Slerp(approachHeading, heading, eased);
                    break;
                case Phase.Descend:
                    position = Vector3.LerpUnclamped(cruise, contact, eased);
                    break;
                case Phase.Clamp:
                    position = contact;
                    break;
                case Phase.Lift:
                    position = Vector3.LerpUnclamped(contact, cruise, eased);
                    break;
                case Phase.Abort:
                    position = Vector3.LerpUnclamped(abortStart, cruise, eased);
                    rotation = Quaternion.Slerp(abortHeading, heading, eased);
                    break;
                default:
                    // Loaded departure starts at rest and accelerates away without a pose snap.
                    position = cruise + heading * Vector3.forward * (Mathf.Max(20f, tuning.departDistance) * t * t);
                    break;
            }
            carrier.SetPositionAndRotation(position, rotation);
        }

        private void NextPhase()
        {
            switch (CurrentPhase)
            {
                case Phase.Approach: CurrentPhase = Phase.Align; break;
                case Phase.Align: CurrentPhase = Phase.Descend; break;
                case Phase.Descend:
                    if (!AttachAtContact()) { CurrentPhase = Phase.Failed; break; }
                    CurrentPhase = Phase.Clamp;
                    break;
                case Phase.Clamp: CurrentPhase = Phase.Lift; break;
                case Phase.Lift:
                case Phase.Abort: CurrentPhase = Phase.Depart; break;
                case Phase.Depart: CurrentPhase = Phase.Complete; break;
            }
        }

        private bool AttachAtContact()
        {
            // Validate the real world-space contact before parenting. Never teleport a misaligned payload.
            for (int i = 1; i <= 4; i++)
            {
                var clamp = carrier.Find($"ClampSockets/Clamp_{i:00}");
                var grab = payload.Find($"CaptureSockets/Grab_{i:00}");
                if (clamp == null || grab == null || (clamp.position - grab.position).sqrMagnitude > .0004f)
                {
                    Debug.LogError("CA01 pickup: capture socket mismatch " + i, carrier);
                    return false;
                }
            }
            if ((mount.position - payload.position).sqrMagnitude > .0004f ||
                Quaternion.Angle(mount.rotation, payload.rotation) > .1f)
            {
                Debug.LogError("CA01 pickup: cargo mount mismatch.", carrier);
                return false;
            }
            payload.SetParent(mount, false);
            payload.localPosition = Vector3.zero;
            payload.localRotation = Quaternion.identity;
            IsLoaded = true;
            return true;
        }
    }
}
