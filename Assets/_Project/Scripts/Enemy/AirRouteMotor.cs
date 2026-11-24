using UnityEngine;

namespace AirCombat
{
    // Passive arcade target movement, independent of combat and mission rules.
    [DisallowMultipleComponent]
    public sealed class AirRouteMotor : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float speed = 55f;
        [SerializeField, Min(1f)] private float visualTurnRate = 120f;
        private Vector3[] points;
        private int next;
        public bool Finished => points != null && next >= points.Length;

        public void Begin(Vector3[] route)
        {
            points = (Vector3[])route.Clone();
            next = 1;
            transform.position = points[0];
            if (points.Length > 1 && (points[1] - points[0]).sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(points[1] - points[0], Vector3.up);
        }

        public void Tick(float dt)
        {
            if (points == null || Finished) return;
            float budget = Mathf.Max(0f, speed * dt);
            while (!Finished && budget > 0f)
            {
                Vector3 delta = points[next] - transform.position;
                float distance = delta.magnitude;
                if (distance <= .001f) { next++; continue; }
                Quaternion heading = Quaternion.LookRotation(delta, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, heading, visualTurnRate * dt);
                float travel = Mathf.Min(distance, budget);
                transform.position += delta / distance * travel;
                budget -= travel;
                if (travel >= distance) next++;
            }
        }

        public void SetAltitude(float y)
        {
            if (points != null)
                for (int i = 0; i < points.Length; i++) points[i].y = y;
            Vector3 position = transform.position;
            position.y = y;
            transform.position = position;
        }
    }
}
