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
        }

        private readonly Transform aircraft;
        private readonly Transform[] parts;
        private readonly Vector3[] rest;
        private readonly List<Contact> contacts = new List<Contact>();
        private float height, heightVelocity;

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
            for (int i = 0; i < parts.Length; i++) rest[i] = parts[i].localPosition;

            // Mission01 targets are authored scene objects, reset in place on R.
            // Cache once; no global search or allocations during flight.
            foreach (var target in Object.FindObjectsByType<AirDamageableTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (target != null && target.transform != aircraft && target.gameObject.scene == aircraft.gameObject.scene)
                    contacts.Add(new Contact { Target = target });
        }

        public void Tick(float dt, float lift, float startDistance, float fullDistance)
        {
            if (parts == null || dt <= 0f) return;
            lift = Mathf.Max(0f, lift);
            fullDistance = Mathf.Max(1f, fullDistance);
            startDistance = Mathf.Max(fullDistance + 1f, startDistance);
            float desired = 0f;
            foreach (var contact in contacts)
            {
                var target = contact.Target;
                if (target == null || !target.isActiveAndEnabled || !target.IsAlive)
                { contact.HasPrevious = false; continue; }
                Vector3 delta = target.transform.position - aircraft.position;
                if (Mathf.Abs(delta.y) > 8f)
                { contact.HasPrevious = false; continue; }
                var relative = new Vector2(delta.x, delta.z);
                float distance = relative.magnitude;
                if (contact.HasPrevious && dt > .0001f)
                {
                    // Relative velocity handles head-on, crossing, and overtaking
                    // contacts alike. Predict up to .35 seconds of motion, without
                    // reacting to an already receding contact as if it were closing.
                    Vector2 velocity = (relative - contact.Previous) / dt;
                    if (velocity.sqrMagnitude > .01f)
                    {
                        float t = Mathf.Clamp(-Vector2.Dot(relative, velocity) / velocity.sqrMagnitude, 0f, .35f);
                        distance = Mathf.Min(distance, (relative + velocity * t).magnitude + 12f);
                    }
                    // Catch a crossing between samples (e.g. Editor fast travel).
                    Vector2 segment = relative - contact.Previous;
                    if (segment.sqrMagnitude > .01f)
                    {
                        float t = Mathf.Clamp01(-Vector2.Dot(contact.Previous, segment) / segment.sqrMagnitude);
                        distance = Mathf.Min(distance, (contact.Previous + segment * t).magnitude);
                    }
                }
                contact.Previous = relative;
                contact.HasPrevious = true;
                float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(startDistance, fullDistance, distance));
                // Multiple close contacts never stack visual altitude.
                desired = Mathf.Max(desired, lift * weight);
            }
            height = Mathf.SmoothDamp(height, desired, ref heightVelocity,
                desired > height ? .10f : .25f, Mathf.Infinity, dt);
            if (desired == 0f && height < .01f) { height = heightVelocity = 0f; }
            ApplyHeight();
        }

        private void ApplyHeight()
        {
            if (parts == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                Transform part = parts[i];
                if (part == null || part.parent == null) continue;
                // World-up offset even while the aircraft banks; keep local
                // rotations/scale and original authored rest positions intact.
                part.localPosition = rest[i] + part.parent.InverseTransformVector(Vector3.up * height);
            }
        }

        public void Clear()
        {
            height = heightVelocity = 0f;
            foreach (var contact in contacts) contact.HasPrevious = false;
            ApplyHeight();
        }
    }
}
