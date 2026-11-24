using UnityEngine;

namespace AirCombat
{
    // Ground locomotion only. Drive along the same rounded path drawn in the editor.
    [DisallowMultipleComponent]
    public sealed class GroundPatrolMotor : MonoBehaviour
    {
        [SerializeField] private GroundPatrolRoute route;
        [SerializeField, Min(.1f)] private float speed = 12f;
        [SerializeField, Min(1f)] private float turnRate = 95f;
        [SerializeField, Min(0)] private int startPoint;
        [SerializeField, Min(0f)] private float groundClearance;
        private GroundPatrolPath path;
        private int segmentIndex;
        private int direction = 1;
        private float progress;
        private float heading;
        private bool finished;

        public GroundPatrolRoute Route => route;
        public int StartPoint => route != null && route.Count > 0
            ? Mathf.Clamp(startPoint, 0, route.Count - 1) : 0;
        public int NextPoint => path != null && segmentIndex >= 0 && segmentIndex < path.Count
            ? (direction > 0 ? path.Segments[segmentIndex].ToWaypoint : path.Segments[segmentIndex].FromWaypoint)
            : StartPoint;
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
            if (route == null || route.Count == 0) { finished = true; path = null; return; }
            path = route.BuildPath();
            GetStart(path, out segmentIndex, out direction, out progress, out heading, out finished);
            Place(path.StartPosition(StartPoint));
        }

        // Shared calculation for the edit-time unit preview and Play Mode.
        public bool TryGetStartPose(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            if (route == null || route.Count == 0) return false;
            var preview = route.BuildPath();
            GetStart(preview, out _, out _, out _, out float yaw, out _);
            GetSurfacePose(preview.StartPosition(StartPoint), yaw, out position, out rotation);
            return true;
        }

        private void GetStart(GroundPatrolPath built, out int segment, out int travel,
            out float distance, out float yaw, out bool done)
        {
            segment = built.StartSegment(StartPoint);
            travel = 1;
            distance = 0f;
            done = built.Count == 0;
            if (!done && segment >= built.Count)
            {
                if (route.Mode == GroundPatrolRoute.Traversal.Loop) segment = 0;
                else
                {
                    segment = built.Count - 1;
                    distance = built.Segments[segment].Length;
                    if (route.Mode == GroundPatrolRoute.Traversal.PingPong) travel = -1;
                    else done = true;
                }
            }
            yaw = built.Count > 0
                ? built.Segments[segment].Heading(distance / built.Segments[segment].Length, travel)
                : route.transform.eulerAngles.y;
        }

        private void Update()
        {
            if (finished || route == null || path == null || path.Count == 0) return;
            float timeLeft = Time.deltaTime;
            // Carry unused frame time across tangent joins, so a normal 90-degree
            // corner never incurs a one-frame stop. Bound loops for tiny routes.
            for (int i = 0; i < 128 && timeLeft > .00001f && !finished; i++)
            {
                var segment = path.Segments[segmentIndex];
                float desired = segment.Heading(progress / segment.Length, direction);
                float turnTime = Mathf.Abs(Mathf.DeltaAngle(heading, desired)) / Mathf.Max(1f, turnRate);
                if (turnTime > .0001f)
                {
                    // Only hard corners (radius zero, duplicates, U-turns and
                    // PingPong reversals) need an in-place rotation.
                    float used = Mathf.Min(timeLeft, turnTime);
                    heading = Mathf.MoveTowardsAngle(heading, desired, turnRate * used);
                    Place(segment.Position(progress / segment.Length));
                    timeLeft -= used;
                    if (used < turnTime) return;
                }
                // Angular rate = speed / radius. Tight curves automatically slow
                // down instead of skating sideways or exceeding the motor yaw limit.
                float velocity = Mathf.Max(.1f, speed);
                if (segment.Radius > 0f)
                    velocity = Mathf.Min(velocity, Mathf.Max(1f, turnRate) * Mathf.Deg2Rad * segment.Radius);
                float left = direction > 0 ? segment.Length - progress : progress;
                float step = Mathf.Min(left, velocity * timeLeft);
                progress = Mathf.Clamp(progress + step * direction, 0f, segment.Length);
                float t = progress / segment.Length;
                heading = segment.Heading(t, direction);
                Place(segment.Position(t));
                timeLeft -= step / velocity;
                if (left - step <= .00001f) Advance();
                else return;
            }
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
            int candidate = segmentIndex + direction;
            if (candidate >= 0 && candidate < path.Count) segmentIndex = candidate;
            else if (route.Mode == GroundPatrolRoute.Traversal.Loop) segmentIndex = direction > 0 ? 0 : path.Count - 1;
            else if (route.Mode == GroundPatrolRoute.Traversal.PingPong) direction = -direction;
            else { finished = true; return; }
            progress = direction > 0 ? 0f : path.Segments[segmentIndex].Length;
        }
    }
}
