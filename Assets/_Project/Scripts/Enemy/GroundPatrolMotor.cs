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
        public int StartPoint => route != null && route.Count > 0
            ? Mathf.Clamp(startPoint, 0, route.Count - 1) : 0;
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
            nextPoint = StartPoint;
            direction = 1;
            finished = false;
            Vector3 start = route.GetPoint(nextPoint);
            Advance();
            heading = GetStartHeading();
            Place(start);
        }

        // One shared start-pose calculation for the editor preview and Play Mode.
        // The route anchor never moves when its child vehicle drives.
        public bool TryGetStartPose(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            if (route == null || route.Count == 0) return false;
            GetSurfacePose(route.GetPoint(StartPoint), GetStartHeading(), out position, out rotation);
            return true;
        }

        private float GetStartHeading()
        {
            int start = StartPoint;
            int next = start + 1;
            if (next >= route.Count)
            {
                if (route.Mode == GroundPatrolRoute.Traversal.Loop) next = 0;
                else if (route.Mode == GroundPatrolRoute.Traversal.PingPong) next = start - 1;
                else next = start;
            }
            if (next >= 0 && next < route.Count)
            {
                Vector3 delta = route.GetPoint(next) - route.GetPoint(start);
                if (new Vector2(delta.x, delta.z).sqrMagnitude > .001f)
                    return Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            }
            if (start > 0)
            {
                Vector3 delta = route.GetPoint(start) - route.GetPoint(start - 1);
                if (new Vector2(delta.x, delta.z).sqrMagnitude > .001f)
                    return Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            }
            Vector3 forward = route.transform.forward;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
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
            GetSurfacePose(point, heading, out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
        }

        private void GetSurfacePose(Vector3 point, float yaw, out Vector3 position, out Quaternion rotation)
        {
            Vector3 surface = route.Project(point, out Vector3 normal);
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, normal).normalized;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            position = surface + Vector3.up * groundClearance;
            rotation = Quaternion.LookRotation(forward, normal);
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
