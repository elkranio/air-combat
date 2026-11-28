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
        [SerializeField, Min(1)] private int hitDamage = 5;
        [SerializeField] private TrailRenderer trail;
        public int HitDamage => Mathf.Max(1, hitDamage);
        private Transform target;
        private Action<Vector3, Transform> onHit;
        private Action<Vector3> onTerrainImpact;
        private Vector3 lastTargetPosition;
        private float age;
        public float RemainingManeuverBudget { get; private set; }
        private bool launched;
        public bool IsInFlight => launched && isActiveAndEnabled;
        private Action<Vector3, Vector3> arcadeExhaust;
        private AirDamageableTarget arcadeTarget;
        private Vector3 previousExhaust;
        private Vector3 exhaustLocal = new Vector3(0f, 0f, -1.9f);

        // Bind only instance state; gameplay tuning belongs to the projectile prefab.
        public void ConfigureArcade(AirDamageableTarget aimTarget, Action<Vector3, Vector3> exhaust)
        {
            arcadeTarget = aimTarget;
            speed = Mathf.Max(.1f, speed);
            turnRate = Mathf.Max(1f, turnRate);
            maneuverBudget = Mathf.Max(0f, maneuverBudget);
            lifetime = Mathf.Max(.1f, lifetime);
            hitRadius = Mathf.Max(.1f, hitRadius);
            arcadeExhaust = exhaust;
            if (trail != null)
            {
                exhaustLocal = transform.InverseTransformPoint(trail.transform.position);
                trail.emitting = false;
                trail.Clear();
                trail.enabled = false;
                trail = null; // Keep the disabled legacy exhaust child on this instance.
            }
            // A displaced ground shadow reads as a second incoming missile.
            // Production AAM on both sides must not cast shadows.
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Cancel()
        {
            launched = false;
            onHit = null;
            onTerrainImpact = null;
            arcadeExhaust = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void EmitArcadeExhaust()
        {
            if (arcadeExhaust == null) return;
            Vector3 point = transform.TransformPoint(exhaustLocal);
            arcadeExhaust(previousExhaust, point);
            previousExhaust = point;
        }

        public void Launch(Transform aimTarget, Action<Vector3, Transform> hitCallback,
            Action<Vector3> terrainImpact = null)
        {
            age = 0f;
            RemainingManeuverBudget = Mathf.Max(0f, maneuverBudget);
            target = aimTarget;
            onHit = hitCallback;
            onTerrainImpact = terrainImpact;
            if (target != null) lastTargetPosition = target.position;
            if (trail != null) { trail.Clear(); trail.emitting = true; }
            previousExhaust = transform.TransformPoint(exhaustLocal);
            launched = true;
        }

        private void Update()
        {
            if (!launched) return;
            var dt = Mathf.Min(Time.deltaTime, Mathf.Max(0f, lifetime - age));
            var from = transform.position;
            bool hasTarget = target != null && target.gameObject.activeInHierarchy;
            if (arcadeExhaust != null && !WeaponLock.Alive(arcadeTarget)) hasTarget = false;
            // Opt-in presentation for the fictional Sparrow dive only. The
            // original horizontal AAM steering and budget remain unchanged.
            bool followDive = hasTarget && arcadeTarget is CruiseMissileTarget sparrow && sparrow.IsDiving;
            if (followDive)
            {
                Vector3 flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (flat.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            }
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
            if (followDive)
            {
                // Cosmetic altitude blend; no new physical guidance model.
                to.y = Mathf.Lerp(from.y, target.position.y, Mathf.Clamp01(dt * 12f));
                Vector3 visualTravel = to - from;
                if (visualTravel.sqrMagnitude > .000001f)
                    transform.rotation = Quaternion.LookRotation(visualTravel, Vector3.up);
            }
            bool terrainHit = RadarTerrainObstacle.TryProjectileHit(gameObject.scene, from, to,
                out float terrainAt, out Vector3 terrainPoint);
            if (hasTarget)
            {
                // Sweep in relative space so fast missiles cannot tunnel through a moving target.
                var relativeStart = from - lastTargetPosition;
                var relativeEnd = to - target.position;
                // A dive interception must also be visibly close in height.
                // All ordinary targets retain the accepted planar hit rule.
                if (!followDive) relativeStart.y = relativeEnd.y = 0f;
                var segment = relativeEnd - relativeStart;
                float t = segment.sqrMagnitude > 0.000001f ?
                    Mathf.Clamp01(-Vector3.Dot(relativeStart, segment) / segment.sqrMagnitude) : 0f;
                if ((relativeStart + segment * t).sqrMagnitude <= hitRadius * hitRadius &&
                    (!terrainHit || t < terrainAt) &&
                    !RadarTerrainObstacle.TryProjectileHit(gameObject.scene, Vector3.Lerp(from, to, t),
                        Vector3.Lerp(lastTargetPosition, target.position, t), out _, out _))
                {
                    transform.position = Vector3.Lerp(from, to, t);
                    EmitArcadeExhaust();
                    onHit?.Invoke(transform.position, target);
                    Finish();
                    return;
                }
                lastTargetPosition = target.position;
            }
            if (terrainHit)
            {
                transform.position = terrainPoint;
                EmitArcadeExhaust();
                onTerrainImpact?.Invoke(terrainPoint);
                Finish();
                return;
            }
            transform.position = to;
            EmitArcadeExhaust();
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
