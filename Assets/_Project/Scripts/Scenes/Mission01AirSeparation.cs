using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Render-only separation for Mission01's fixed-altitude aircraft. Never move
    // the motor, damage receiver, camera target, or weapon mount transforms.
    internal sealed class Mission01AirSeparation
    {
        private sealed class Contact
        {
            public AirDamageableTarget Target;
            public Vector2 Previous;
            public bool HasPrevious;
            public bool Latched;
        }

        private readonly Transform aircraft, visualRoot;
        private readonly Quaternion visualRest;
        private readonly Transform[] parts;
        private readonly Vector3[] rest;
        private readonly List<Contact> contacts = new List<Contact>();
        private readonly Quaternion[] restRotation;
        private const float BreakSeconds = .45f;
        private const float PassSeconds = .20f;
        private const float ReturnSeconds = .55f;
        private const float Duration = BreakSeconds + PassSeconds + ReturnSeconds;
        private const float PredictionSeconds = BreakSeconds + PassSeconds * .5f;
        private const float BreakBank = 55f;
        private float elapsed, maneuverLift, maneuverSide;
        public bool IsEvading { get; private set; }

        public Mission01AirSeparation(Transform aircraft, Transform visual)
        {
            this.aircraft = aircraft;
            visualRoot = visual;
            visualRest = visual.localRotation;
            var assembly = aircraft.GetComponent<JetAssembly>();
            var chassis = assembly != null ? assembly.Chassis : null;
            // The accepted Lynx prefab has a separate mesh branch beside Sockets.
            // Never fall back to moving JetVisual: it contains the gun sockets.
            var model = chassis != null ? chassis.transform.Find("PF02_Lynx") : null;
            if (model == null || !model.IsChildOf(visual) || model.GetComponentInChildren<Collider>(true) != null)
            {
                Debug.LogWarning("COLL-01: Lynx visual branch unavailable; visual separation disabled.", aircraft);
                return;
            }
            var moving = new List<Transform> { model };
            foreach (var nozzle in new[] { chassis.EngineLeft, chassis.EngineRight })
                if (nozzle != null && nozzle.IsChildOf(visual) && !nozzle.IsChildOf(model) &&
                    nozzle.GetComponentInChildren<Collider>(true) == null && !moving.Contains(nozzle))
                    moving.Add(nozzle);
            parts = moving.ToArray();
            rest = new Vector3[parts.Length];
            restRotation = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                rest[i] = parts[i].localPosition;
                restRotation[i] = parts[i].localRotation;
            }

            // Mission01 targets are authored scene objects, reset in place on R.
            // Cache once; no global search or allocations during flight.
            foreach (var target in Object.FindObjectsByType<AirDamageableTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (target != null && target.transform != aircraft && target.gameObject.scene == aircraft.gameObject.scene)
                    contacts.Add(new Contact { Target = target });
        }

        // A predicted crossing starts one finite maneuver. Mere proximity,
        // parallel flight and a receding contact do not start it. Rearm only
        // once the same contact has cleared the outer envelope.
        public void Tick(float dt, float lift, float sideOffset, float startDistance, float fullDistance, float forwardSpeed)
        {
            if (parts == null || dt <= 0f) return;
            lift = Mathf.Max(0f, lift);
            sideOffset = Mathf.Max(0f, sideOffset);
            fullDistance = Mathf.Max(1f, fullDistance);
            startDistance = Mathf.Max(fullDistance + 1f, startDistance);
            bool start = false;
            float selectedSide = 1f, bestDistance = float.PositiveInfinity;
            Vector2 right = new Vector2(aircraft.right.x, aircraft.right.z).normalized;
            foreach (var contact in contacts)
            {
                var target = contact.Target;
                if (target == null || !target.isActiveAndEnabled || !target.IsAlive)
                { contact.HasPrevious = contact.Latched = false; continue; }
                Vector3 delta = target.transform.position - aircraft.position;
                if (Mathf.Abs(delta.y) > 8f)
                { contact.HasPrevious = contact.Latched = false; continue; }
                var relative = new Vector2(delta.x, delta.z);
                if (relative.sqrMagnitude > startDistance * startDistance) contact.Latched = false;
                if (!IsEvading && !contact.Latched && (lift > 0f || sideOffset > 0f) && contact.HasPrevious && dt > .0001f)
                {
                    Vector2 segment = relative - contact.Previous;
                    Vector2 velocity = segment / dt;
                    float v2 = velocity.sqrMagnitude;
                    if (v2 > 4f)
                    {
                        float closestTime = -Vector2.Dot(relative, velocity) / v2;
                        // Fast closure can require starting beyond the old proximity
                        // envelope. Use the actual time to closest approach instead.
                        bool approaching = closestTime > 0f && closestTime <= PredictionSeconds &&
                            (relative + velocity * closestTime).sqrMagnitude <= fullDistance * fullDistance;
                        // Recover a crossing that happened between two samples.
                        float sweptT = segment.sqrMagnitude > .0001f
                            ? -Vector2.Dot(contact.Previous, segment) / segment.sqrMagnitude : -1f;
                        bool crossed = sweptT > 0f && sweptT <= 1f &&
                            (contact.Previous + segment * sweptT).sqrMagnitude <= fullDistance * fullDistance;
                        if (approaching || crossed)
                        {
                            contact.Latched = true;
                            start = true;
                            Vector2 closest = crossed ? contact.Previous + segment * sweptT
                                : relative + velocity * closestTime;
                            // Keep the chosen side for the complete maneuver. If the
                            // predicted impact is centered, prefer the side opposite
                            // the current contact, then right for a symmetric pass.
                            if (closest.sqrMagnitude < bestDistance)
                            {
                                bestDistance = closest.sqrMagnitude;
                                float lateral = Vector2.Dot(closest, right);
                                if (Mathf.Abs(lateral) < 1f) lateral = Vector2.Dot(relative, right);
                                selectedSide = lateral > 1f ? -1f : 1f;
                            }
                        }
                    }
                }
                contact.Previous = relative;
                contact.HasPrevious = true;
            }

            if (!IsEvading)
            {
                if (!start) return;
                IsEvading = true;
                elapsed = 0f;
                maneuverLift = lift;
                maneuverSide = sideOffset * selectedSide;
            }
            else elapsed = Mathf.Min(Duration, elapsed + dt);

            if (elapsed >= Duration)
            {
                IsEvading = false;
                ApplyPose(Vector3.zero, Quaternion.identity);
                return;
            }

            // Lateral offset and tiny lift share a C1-continuous envelope.
            // Bank is the main visual gesture; the return uses a softer counterbank.
            float envelope, derivative, bank;
            float direction = maneuverSide < 0f ? -1f : 1f;
            if (elapsed < BreakSeconds)
            {
                float u = elapsed / BreakSeconds;
                envelope = Smooth(u);
                derivative = 6f * u * (1f - u) / BreakSeconds;
                bank = -direction * BreakBank * Smooth(Mathf.Clamp01(u / .55f));
            }
            else if (elapsed < BreakSeconds + PassSeconds)
            {
                float u = (elapsed - BreakSeconds) / PassSeconds;
                envelope = 1f;
                derivative = 0f;
                bank = -direction * Mathf.Lerp(BreakBank, 25f, Smooth(u));
            }
            else
            {
                float u = Mathf.Clamp01((elapsed - BreakSeconds - PassSeconds) / ReturnSeconds);
                envelope = 1f - Smooth(u);
                derivative = -6f * u * (1f - u) / ReturnSeconds;
                bank = direction * (u < .4f ? Mathf.Lerp(-25f, 25f, Smooth(u / .4f))
                    : Mathf.Lerp(25f, 0f, Smooth((u - .4f) / .6f)));
            }
            float yaw = Mathf.Clamp(Mathf.Atan2(maneuverSide * derivative, Mathf.Max(1f, forwardSpeed))
                * Mathf.Rad2Deg, -16f, 16f);
            float noseUp = Mathf.Clamp(Mathf.Atan2(maneuverLift * derivative, Mathf.Max(1f, forwardSpeed))
                * Mathf.Rad2Deg, -8f, 8f);
            Vector3 offset = aircraft.right * (maneuverSide * envelope) + Vector3.up * (maneuverLift * envelope);

            // Blend to an absolute visual bank instead of adding 55 degrees to
            // the flight motor's existing bank. Fade back to the live motor pose.
            Quaternion baseRotation = visualRoot.parent != null
                ? visualRoot.parent.rotation * visualRest : visualRest;
            Quaternion attitude = Quaternion.AngleAxis(yaw, Vector3.up)
                * Quaternion.AngleAxis(-noseUp, aircraft.right)
                * Quaternion.AngleAxis(bank, aircraft.forward);
            float weight = Smooth(Mathf.Clamp01(elapsed / .18f))
                * Smooth(Mathf.Clamp01((Duration - elapsed) / .22f));
            Quaternion correction = Quaternion.Slerp(Quaternion.identity,
                attitude * baseRotation * Quaternion.Inverse(visualRoot.rotation), weight);
            ApplyPose(offset, correction);
        }

        private static float Smooth(float u) => u * u * (3f - 2f * u);

        private void ApplyPose(Vector3 offset, Quaternion correction)
        {
            if (parts == null) return;
            Vector3 pivot = aircraft.position;
            for (int i = 0; i < parts.Length; i++)
            {
                Transform part = parts[i];
                if (part == null || part.parent == null) continue;
                if (offset.sqrMagnitude == 0f && correction == Quaternion.identity)
                {
                    part.localPosition = rest[i];
                    part.localRotation = restRotation[i];
                    continue;
                }
                Transform parent = part.parent;
                // Rotate both the mesh and separate exhaust sockets around the
                // same aircraft pivot, not around their individual local origins.
                Vector3 worldRest = parent.TransformPoint(rest[i]);
                part.localPosition = parent.InverseTransformPoint(pivot + correction * (worldRest - pivot) + offset);
                part.localRotation = Quaternion.Inverse(parent.rotation) * correction * parent.rotation * restRotation[i];
            }
        }

        public void Clear()
        {
            IsEvading = false;
            elapsed = maneuverLift = maneuverSide = 0f;
            foreach (var contact in contacts) contact.HasPrevious = contact.Latched = false;
            ApplyPose(Vector3.zero, Quaternion.identity);
        }
    }
}
