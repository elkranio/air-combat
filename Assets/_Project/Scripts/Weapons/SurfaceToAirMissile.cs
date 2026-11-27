using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // M1b surface-to-air missile: separate from the approved air-to-air missile.
    // Vertical ejection and initial pitch-over are FREE of the pursuit turn budget.
    // Once homing, finite turn speed and TOTAL turning budget make Q/E useful.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class SurfaceToAirMissile : MonoBehaviour, IRadarMissileHeading
    {
        [SerializeField, Min(1f)] private float speed = 150f;
        [Header("Vertical launch (no pursuit budget spent)")]
        [SerializeField, Min(.05f)] private float verticalSeconds = .34f;
        [SerializeField, Min(1f)] private float pitchOverRate = 210f;
        [SerializeField, Min(.05f)] private float pitchOverMaxSeconds = .75f;
        [Header("Pursuit")]
        [SerializeField, Min(1f)] private float turnRate = 140f;
        [SerializeField, Min(0f)] private float totalTurnBudget = 180f;
        [SerializeField, Min(.1f)] private float lifetime = 6.5f;
        [SerializeField, Min(.1f)] private float hitRadius = 4.2f;
        [SerializeField, Min(1)] private int damage = 5;
        [SerializeField] private TrailRenderer exhaust;
        [SerializeField] private Material impactMaterial;

        private enum FlightPhase { Vertical, PitchOver, Homing }
        private FlightPhase phase;
        private float phaseElapsed;
        private Transform target;
        private PlayerHitPoints hitPoints;
        private AircraftRadar radar;
        private Transform launchSite;
        private Vector3 previousTargetPosition;
        private float age;
        private float remainingTurn;
        private bool flying;
        private System.Action<string> onFinished;
        // Picket uses the authored rail pose instead of a vertical ejection.
        private bool railLaunch;
        private bool arcadeLead;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[24];
        private static int lastPhysicsSyncFrame = -1;

        public bool IsFlying => flying;
        public string PhaseLabel => !flying ? "OFF" : phase == FlightPhase.Vertical ? "VERTICAL"
            : phase == FlightPhase.PitchOver ? "PITCH-OVER" : "HOMING";
        public float RemainingTurnBudget => remainingTurn;
        public RadarContactType RadarKind => RadarContactType.IncomingMissile;
        public Vector3 RadarPosition => transform.position;
        public Vector3 RadarForward => transform.forward;
        public float RadarThreatRadius => 0f;
        public bool IsRadarVisible => flying && isActiveAndEnabled;

        public void ConfigureVisual(TrailRenderer trail, Material impact)
        {
            exhaust = trail;
            impactMaterial = impact;
        }

        public void Launch(Transform aircraft, PlayerHitPoints health, AircraftRadar sensor,
            Transform owner, System.Action<string> finished = null)
        {
            target = aircraft;
            hitPoints = health;
            radar = sensor;
            launchSite = owner;
            onFinished = finished;
            previousTargetPosition = target != null ? target.position : transform.position;
            remainingTurn = totalTurnBudget;
            age = phaseElapsed = 0f;
            railLaunch = false;
            arcadeLead = false;
            phase = FlightPhase.Vertical;
            transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            flying = true;
            if (exhaust != null) { exhaust.Clear(); exhaust.emitting = true; }
            radar?.Register(this); // Only hostile missiles, not player's ordnance.
        }

        // Closed-pod game art uses the projectile's existing mesh at the authored pose.
        internal void BeginAngledLaunch(Quaternion rotation, bool useArcadeLead = false)
        {
            arcadeLead = useArcadeLead;
            railLaunch = true;
            phase = FlightPhase.PitchOver;
            phaseElapsed = 0f;
            transform.rotation = rotation;
            if (exhaust != null) exhaust.Clear();
        }

        // Game-only forward aim offset for the second Bastion shot.
        // Fade it out close to the player; actual hit detection is unchanged.
        private Vector3 AimDirection(Vector3 from)
        {
            Vector3 delta = target.position - from;
            if (!arcadeLead) return delta;
            Vector3 heading = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            float offset = Mathf.Min(18f, delta.magnitude * .18f);
            return delta + heading * offset;
        }

        // The display mesh is shared with the loaded rail prop. Start the game
        // projectile at that same pose and enter the existing pitch-over phase.
        internal bool AttachRailVisual(Transform source, Vector3 center, Quaternion rotation, float tailLength)
        {
            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null) return false;
            railLaunch = true;
            phase = FlightPhase.PitchOver;
            phaseElapsed = 0f;
            transform.SetPositionAndRotation(center, rotation);
            foreach (var old in GetComponentsInChildren<MeshRenderer>(true)) old.enabled = false;
            var railVisual = new GameObject("Picket flight visual").transform;
            railVisual.SetParent(transform, false);
            railVisual.localPosition = Vector3.zero;
            railVisual.localRotation = Quaternion.identity;
            var mesh = new GameObject("Rail rocket", typeof(MeshFilter), typeof(MeshRenderer));
            mesh.transform.SetParent(railVisual, false);
            mesh.transform.SetPositionAndRotation(source.position, source.rotation);
            mesh.transform.localScale = source.lossyScale;
            mesh.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var paint = mesh.GetComponent<MeshRenderer>();
            paint.sharedMaterials = renderer.sharedMaterials;
            paint.shadowCastingMode = renderer.shadowCastingMode;
            paint.receiveShadows = renderer.receiveShadows;
            if (exhaust != null)
            {
                exhaust.transform.SetParent(railVisual, false);
                exhaust.transform.localPosition = Vector3.back * tailLength;
                exhaust.transform.localRotation = Quaternion.identity;
                exhaust.Clear();
            }
            return true;
        }

        private void LateUpdate()
        {
            if (!flying || Time.deltaTime <= 0f) return;
            if (lastPhysicsSyncFrame != Time.frameCount)
            {
                Physics.SyncTransforms(); // Player moves via Transform, without Rigidbody.
                lastPhysicsSyncFrame = Time.frameCount;
            }
            float dt = Time.deltaTime;
            Vector3 from = transform.position;
            bool hasTarget = target != null && hitPoints != null && hitPoints.IsAlive;
            if (phase == FlightPhase.Vertical)
            {
                // Keep the ejection truly vertical before steering starts.
                phaseElapsed += dt;
                if (phaseElapsed >= verticalSeconds)
                {
                    phase = FlightPhase.PitchOver;
                    phaseElapsed = 0f;
                }
            }
            else if (phase == FlightPhase.PitchOver)
            {
                phaseElapsed += dt;
                if (hasTarget)
                {
                    Vector3 desired = AimDirection(from);
                    if (desired.sqrMagnitude > .001f)
                    {
                        Quaternion toward = Quaternion.LookRotation(desired, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, toward,
                            pitchOverRate * dt);
                        if (Vector3.Angle(transform.forward, desired) < 6f)
                            phase = FlightPhase.Homing;
                    }
                }
                // Never grant endless free turning against a maneuvering jet.
                if (phaseElapsed >= pitchOverMaxSeconds) phase = FlightPhase.Homing;
            }
            else if (hasTarget && remainingTurn > 0f)
            {
                Vector3 desired = AimDirection(from);
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
                if ((relativeFrom + movement * t).sqrMagnitude <= hitRadius * hitRadius &&
                    !RadarTerrainObstacle.TryProjectileHit(gameObject.scene, Vector3.Lerp(from, to, t),
                        Vector3.Lerp(previousTargetPosition, target.position, t), out _, out _))
                    targetAt = t;
                previousTargetPosition = target.position;
            }

            float obstacleAt = float.PositiveInfinity;
            Vector3 obstaclePoint = default;
            string obstacleName = null;
            // Legacy cold launch keeps its grace period. Picket starts at the
            // rail and ignores only its own launcher through the filter below.
            if (travel > .0001f && (railLaunch || age >= verticalSeconds))
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
                    if (t < obstacleAt)
                    {
                        obstacleAt = t;
                        obstaclePoint = hit.point;
                        obstacleName = hit.collider.name;
                    }
                }
            }
            // Only the silo's collider immunity is preserved during cold launch;
            // a marked terrain face still blocks the missile throughout flight.
            if (RadarTerrainObstacle.TryProjectileHit(gameObject.scene, from, to,
                    out float rockAt, out Vector3 rockPoint) && rockAt <= obstacleAt)
            {
                obstacleAt = rockAt; obstaclePoint = rockPoint; obstacleName = "TERRAIN";
            }
            // Both values are +Infinity when nothing was hit. Without this guard,
            // Infinity <= Infinity despawns every missile on its first frame.
            if (!float.IsPositiveInfinity(obstacleAt) && obstacleAt <= targetAt)
            {
                Finish(obstaclePoint, true, "BLOCKED " + obstacleName);
                return;
            }
            if (!float.IsPositiveInfinity(targetAt))
            {
                Vector3 at = Vector3.Lerp(from, to, targetAt);
                if (hasTarget) hitPoints.ApplyDamage(at, damage);
                Finish(at, true, "HIT PLAYER");
                return;
            }
            transform.position = to;
            age += dt;
            if (age >= lifetime) Finish(transform.position, false, "EXPIRED");
        }

        private void Finish(Vector3 position, bool impact, string reason)
        {
            flying = false;
            onFinished?.Invoke($"{reason} at {age:F2}s / {position}");
            onFinished = null;
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
