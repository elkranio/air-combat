using System;
using UnityEngine;

namespace AirCombat
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(30)] // after the target moves
    public sealed class MissileProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 120f;
        [SerializeField, Min(1f)] private float turnRate = 110f;
        [SerializeField, Min(0f)] private float maneuverBudget = 150f;
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [SerializeField, Min(0.1f)] private float hitRadius = 4f;
        [SerializeField] private TrailRenderer trail;
        private Transform target;
        private Action<Vector3, Transform> onHit;
        private Vector3 lastTargetPosition;
        private float age;
        public float RemainingManeuverBudget { get; private set; }
        private bool launched;

        public void Launch(Transform aimTarget, Action<Vector3, Transform> hitCallback)
        {
            age = 0f;
            RemainingManeuverBudget = Mathf.Max(0f, maneuverBudget);
            target = aimTarget;
            onHit = hitCallback;
            if (target != null) lastTargetPosition = target.position;
            if (trail != null) { trail.Clear(); trail.emitting = true; }
            launched = true;
        }

        private void Update()
        {
            if (!launched) return;
            var dt = Mathf.Min(Time.deltaTime, Mathf.Max(0f, lifetime - age));
            var from = transform.position;
            bool hasTarget = target != null && target.gameObject.activeInHierarchy;
            if (hasTarget && RemainingManeuverBudget > 0f)
            {
                var direction = target.position - from;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    // Spend only the actual yaw applied, not the requested angle.
                    // Reversing turn direction does not refund maneuver budget.
                    float error = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
                    float limit = Mathf.Min(turnRate * dt, RemainingManeuverBudget);
                    float yaw = Mathf.Clamp(error, -limit, limit);
                    transform.Rotate(0f, yaw, 0f, Space.World);
                    RemainingManeuverBudget = Mathf.Max(0f, RemainingManeuverBudget - Mathf.Abs(yaw));
                }
            }
            var to = from + transform.forward * speed * dt;
            if (hasTarget)
            {
                // Sweep in relative space so fast missiles cannot tunnel through a moving target.
                var relativeStart = from - lastTargetPosition;
                var relativeEnd = to - target.position;
                relativeStart.y = relativeEnd.y = 0f;
                var segment = relativeEnd - relativeStart;
                float t = segment.sqrMagnitude > 0.000001f ?
                    Mathf.Clamp01(-Vector3.Dot(relativeStart, segment) / segment.sqrMagnitude) : 0f;
                if ((relativeStart + segment * t).sqrMagnitude <= hitRadius * hitRadius)
                {
                    transform.position = Vector3.Lerp(from, to, t);
                    onHit?.Invoke(transform.position, target);
                    Finish();
                    return;
                }
                lastTargetPosition = target.position;
            }
            transform.position = to;
            age += dt;
            if (age >= lifetime) Finish();
        }

        private void Finish()
        {
            launched = false;
            if (trail != null)
            {
                trail.emitting = false;
                trail.transform.SetParent(null, true);
                Destroy(trail.gameObject, trail.time + 0.1f);
            }
            Destroy(gameObject);
        }
    }
}
