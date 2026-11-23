using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // M1 surface-to-air missile: separate from the approved air-to-air missile.
    // Finite speed, yaw/pitch turn limit and TOTAL turning budget make Q/E useful.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class SurfaceToAirMissile : MonoBehaviour, IRadarContact
    {
        [SerializeField, Min(1f)] private float speed = 112f;
        [SerializeField, Min(1f)] private float turnRate = 72f;
        [SerializeField, Min(0f)] private float totalTurnBudget = 145f;
        [SerializeField, Min(.1f)] private float lifetime = 6.5f;
        [SerializeField, Min(.1f)] private float hitRadius = 4.2f;
        [SerializeField, Min(1)] private int damage = 5;
        [SerializeField] private TrailRenderer exhaust;
        [SerializeField] private Material impactMaterial;

        private Transform target;
        private PlayerHitPoints hitPoints;
        private AircraftRadar radar;
        private Transform launchSite;
        private Vector3 previousTargetPosition;
        private float age;
        private float remainingTurn;
        private bool flying;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[24];
        private static int lastPhysicsSyncFrame = -1;

        public bool IsFlying => flying;
        public RadarContactType RadarKind => RadarContactType.IncomingMissile;
        public Vector3 RadarPosition => transform.position;
        public float RadarThreatRadius => 0f;
        public bool IsRadarVisible => flying && isActiveAndEnabled;

        public void ConfigureVisual(TrailRenderer trail, Material impact)
        {
            exhaust = trail;
            impactMaterial = impact;
        }

        public void Launch(Transform aircraft, PlayerHitPoints health, AircraftRadar sensor, Transform owner)
        {
            target = aircraft;
            hitPoints = health;
            radar = sensor;
            launchSite = owner;
            previousTargetPosition = target != null ? target.position : transform.position;
            remainingTurn = totalTurnBudget;
            age = 0f;
            flying = true;
            if (exhaust != null) { exhaust.Clear(); exhaust.emitting = true; }
            radar?.Register(this); // Only hostile missiles, not player's ordnance.
        }

        private void LateUpdate()
        {
            if (!flying) return;
            if (lastPhysicsSyncFrame != Time.frameCount)
            {
                Physics.SyncTransforms(); // Player moves via Transform, without Rigidbody.
                lastPhysicsSyncFrame = Time.frameCount;
            }
            float dt = Time.deltaTime;
            Vector3 from = transform.position;
            bool hasTarget = target != null && hitPoints != null && hitPoints.IsAlive;
            if (hasTarget && remainingTurn > 0f)
            {
                Vector3 desired = target.position - from;
                if (desired.sqrMagnitude > .001f)
                {
                    float angle = Vector3.Angle(transform.forward, desired);
                    float applied = Mathf.Min(Mathf.Min(angle, turnRate * dt), remainingTurn);
                    Quaternion toward = Quaternion.LookRotation(desired, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, toward, applied);
                    remainingTurn -= applied;
                }
            }
            Vector3 to = from + transform.forward * (speed * dt);
            float travel = Vector3.Distance(from, to);
            float targetAt = float.PositiveInfinity;
            if (hasTarget)
            {
                // Relative sweep: detects crossings when the fast jet and rocket
                // move past one another between physics updates.
                Vector3 relativeFrom = from - previousTargetPosition;
                Vector3 relativeTo = to - target.position;
                Vector3 movement = relativeTo - relativeFrom;
                float denominator = movement.sqrMagnitude;
                float t = denominator > .000001f
                    ? Mathf.Clamp01(-Vector3.Dot(relativeFrom, movement) / denominator) : 0f;
                if ((relativeFrom + movement * t).sqrMagnitude <= hitRadius * hitRadius)
                    targetAt = t;
                previousTargetPosition = target.position;
            }

            float obstacleAt = float.PositiveInfinity;
            Vector3 obstaclePoint = default;
            if (travel > .0001f)
            {
                int count = Physics.RaycastNonAlloc(from, (to - from) / travel,
                    HitBuffer, travel, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = HitBuffer[i];
                    if (hit.collider == null ||
                        (launchSite != null && hit.collider.transform.IsChildOf(launchSite)) ||
                        (hitPoints != null && hit.collider.GetComponentInParent<PlayerHitPoints>() == hitPoints))
                        continue;
                    float t = hit.distance / travel;
                    if (t < obstacleAt) { obstacleAt = t; obstaclePoint = hit.point; }
                }
            }
            if (obstacleAt <= targetAt)
            {
                Finish(obstaclePoint, true);
                return;
            }
            if (!float.IsPositiveInfinity(targetAt))
            {
                Vector3 at = Vector3.Lerp(from, to, targetAt);
                if (hasTarget) hitPoints.ApplyDamage(at, damage);
                Finish(at, true);
                return;
            }
            transform.position = to;
            age += dt;
            if (age >= lifetime) Finish(transform.position, false);
        }

        private void Finish(Vector3 position, bool impact)
        {
            flying = false;
            radar?.Unregister(this);
            if (impact) MissileImpact.Spawn(position, impactMaterial);
            if (exhaust != null)
            {
                exhaust.emitting = false;
                exhaust.transform.SetParent(null, true);
                Destroy(exhaust.gameObject, exhaust.time + .1f);
            }
            Destroy(gameObject);
        }

        private void OnDestroy() { radar?.Unregister(this); }
    }
}
