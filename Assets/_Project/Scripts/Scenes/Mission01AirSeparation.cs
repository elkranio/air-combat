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

        private readonly Transform aircraft;
        private readonly Transform[] parts;
        private readonly Vector3[] rest;
        private readonly List<Contact> contacts = new List<Contact>();
        private readonly Quaternion[] restRotation;
        private const float ClimbSeconds = .35f;
        private const float CrestSeconds = .20f;
        private const float DescentSeconds = .45f;
        private const float Duration = ClimbSeconds + CrestSeconds + DescentSeconds;
        private const float PredictionSeconds = ClimbSeconds + CrestSeconds * .5f;
        private float elapsed, maneuverLift;
        public bool IsEvading { get; private set; }

        public Mission01AirSeparation(Transform aircraft, Transform visual)
        {
            this.aircraft = aircraft;
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
        public void Tick(float dt, float lift, float startDistance, float fullDistance, float forwardSpeed)
        {
            if (parts == null || dt <= 0f) return;
            lift = Mathf.Max(0f, lift);
            fullDistance = Mathf.Max(1f, fullDistance);
            startDistance = Mathf.Max(fullDistance + 1f, startDistance);
            bool start = false;
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
                if (!IsEvading && !contact.Latched && lift > 0f && contact.HasPrevious && dt > .0001f)
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
            }
            else elapsed = Mathf.Min(Duration, elapsed + dt);

            if (elapsed >= Duration)
            {
                IsEvading = false;
                ApplyPose(0f, 0f);
                return;
            }

            float height, verticalSpeed, pitchLimit;
            if (elapsed < ClimbSeconds)
            {
                float u = elapsed / ClimbSeconds;
                height = maneuverLift * Smooth(u);
                verticalSpeed = maneuverLift * 6f * u * (1f - u) / ClimbSeconds;
                pitchLimit = 25f;
            }
            else if (elapsed < ClimbSeconds + CrestSeconds)
            {
                height = maneuverLift;
                verticalSpeed = 0f;
                pitchLimit = 25f;
            }
            else
            {
                float u = Mathf.Clamp01((elapsed - ClimbSeconds - CrestSeconds) / DescentSeconds);
                height = maneuverLift * (1f - Smooth(u));
                verticalSpeed = -maneuverLift * 6f * u * (1f - u) / DescentSeconds;
                pitchLimit = 20f;
            }
            // Nose follows the visual trajectory, with an arcade pitch cap.
            // Smoothstep has zero vertical speed at every phase boundary.
            float pitch = Mathf.Clamp(Mathf.Atan2(verticalSpeed, Mathf.Max(1f, forwardSpeed)) * Mathf.Rad2Deg,
                -pitchLimit, pitchLimit);
            ApplyPose(height, pitch);
        }

        private static float Smooth(float u) => u * u * (3f - 2f * u);

        private void ApplyPose(float height, float noseUp)
        {
            if (parts == null) return;
            Quaternion pitch = Quaternion.AngleAxis(-noseUp, aircraft.right);
            Vector3 pivot = aircraft.position;
            for (int i = 0; i < parts.Length; i++)
            {
                Transform part = parts[i];
                if (part == null || part.parent == null) continue;
                if (height == 0f && noseUp == 0f)
                {
                    part.localPosition = rest[i];
                    part.localRotation = restRotation[i];
                    continue;
                }
                Transform parent = part.parent;
                // Rotate both the mesh and separate exhaust sockets around the
                // same aircraft pivot, not around their individual local origins.
                Vector3 worldRest = parent.TransformPoint(rest[i]);
                part.localPosition = parent.InverseTransformPoint(pivot + pitch * (worldRest - pivot) + Vector3.up * height);
                part.localRotation = Quaternion.Inverse(parent.rotation) * pitch * parent.rotation * restRotation[i];
            }
        }

        public void Clear()
        {
            IsEvading = false;
            elapsed = maneuverLift = 0f;
            foreach (var contact in contacts) contact.HasPrevious = contact.Latched = false;
            ApplyPose(0f, 0f);
        }
    }
}
