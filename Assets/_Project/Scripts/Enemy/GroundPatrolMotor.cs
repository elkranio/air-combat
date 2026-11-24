using UnityEngine;

namespace AirCombat
{
    // Ground locomotion only. No targeting, radar, damage, or weapon decisions.
    // Tracked vehicles turn toward the next point before moving into a new leg.
    [DisallowMultipleComponent]
    public sealed class GroundPatrolMotor : MonoBehaviour
    {
        [SerializeField] private GroundPatrolRoute route;
        [SerializeField, Min(.1f)] private float speed = 12f;
        [SerializeField, Min(1f)] private float turnRate = 95f;
        [SerializeField, Min(0)] private int startPoint;
        [SerializeField, Min(0f)] private float groundClearance;
        private int nextPoint;
        private int direction = 1;
        private float heading;
        private bool finished;

        public GroundPatrolRoute Route => route;
        public int NextPoint => nextPoint;
        public bool Finished => finished;

        private void OnEnable() => RestartPatrol();

        public void SetRoute(GroundPatrolRoute newRoute, int startIndex = 0)
        {
            route = newRoute;
            startPoint = startIndex;
            if (isActiveAndEnabled && Application.isPlaying) RestartPatrol();
        }

        public void RestartPatrol()
        {
            if (route == null || route.Count == 0) { finished = true; return; }
            nextPoint = Mathf.Clamp(startPoint, 0, route.Count - 1);
            direction = 1;
            finished = false;
            heading = transform.eulerAngles.y;
            Vector3 start = route.GetPoint(nextPoint);
            Advance();
            if (!finished)
            {
                Vector3 delta = route.GetPoint(nextPoint) - start;
                if (new Vector2(delta.x, delta.z).sqrMagnitude > .001f)
                    heading = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            }
            Place(start);
        }

        private void Update()
        {
            if (finished || route == null || route.Count == 0) return;
            if (nextPoint < 0 || nextPoint >= route.Count) { RestartPatrol(); return; }
            Vector3 current = transform.position;
            Vector3 goal = route.GetPoint(nextPoint);
            Vector3 delta = goal - current;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= .01f) { Place(goal); Advance(); return; }
            float desired = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            heading = Mathf.MoveTowardsAngle(heading, desired, turnRate * Time.deltaTime);
            // Turn on the spot instead of sliding sideways around sharp corners.
            if (Mathf.Abs(Mathf.DeltaAngle(heading, desired)) <= 4f)
            {
                heading = desired;
                float step = Mathf.Min(distance, speed * Time.deltaTime);
                current += delta * (step / distance);
                // Interpolate fallback height for non-terrain routes.
                current.y = Mathf.Lerp(current.y - groundClearance, goal.y, step / distance);
                Place(current);
                if (step >= distance) Advance();
            }
            else Place(current - Vector3.up * groundClearance);
        }

        private void Place(Vector3 point)
        {
            Vector3 surface = route.Project(point, out Vector3 normal);
            Vector3 forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, normal).normalized;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            transform.SetPositionAndRotation(surface + Vector3.up * groundClearance,
                Quaternion.LookRotation(forward, normal));
        }

        private void Advance()
        {
            if (route.Count < 2) { finished = true; return; }
            int candidate = nextPoint + direction;
            if (candidate >= 0 && candidate < route.Count) { nextPoint = candidate; return; }
            switch (route.Mode)
            {
                case GroundPatrolRoute.Traversal.Loop:
                    nextPoint = candidate >= route.Count ? 0 : route.Count - 1;
                    break;
                case GroundPatrolRoute.Traversal.PingPong:
                    direction = -direction;
                    nextPoint += direction;
                    break;
                default:
                    finished = true;
                    break;
            }
        }
    }
}
