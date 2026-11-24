using UnityEngine;

namespace AirCombat
{
    // Passive arcade target movement, independent of combat and mission rules.
    [DisallowMultipleComponent]
    public sealed class AirRouteMotor : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float speed = 55f;
        [SerializeField, Min(1f)] private float visualTurnRate = 120f;
        [Header("Flight Personality")]
        [SerializeField, Min(0f)] private float swayAmplitude;
        [SerializeField, Min(0f)] private float swayFrequency = .65f;
        [SerializeField, Min(0f)] private float rollAmplitude;
        [SerializeField] private Transform visualRoot;
        private Vector3[] points;
        private int next;
        private Vector3 routePosition;
        private float phase, frequencyScale, amplitudeScale, rollScale;
        public bool Finished => points != null && next >= points.Length;

        public void Begin(Vector3[] route)
        {
            points = (Vector3[])route.Clone();
            next = 1;
            routePosition = points[0];
            if (points.Length > 1 && (points[1] - points[0]).sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(points[1] - points[0], Vector3.up);

            // One sample per spawn, never per frame: independent smooth motion.
            phase = Random.Range(0f, 2f * Mathf.PI);
            frequencyScale = Random.Range(.75f, 1.25f);
            amplitudeScale = Random.Range(.6f, 1f);
            rollScale = Random.Range(.6f, 1f);
            ApplyPersonality();
        }

        public void Tick(float dt)
        {
            if (points == null || Finished || dt <= 0f) return;
            float budget = Mathf.Max(0f, speed * dt);
            Quaternion heading = transform.rotation;
            while (!Finished && budget > 0f)
            {
                // Advance along the nominal route, not the oscillating hitbox position.
                Vector3 delta = points[next] - routePosition;
                float distance = delta.magnitude;
                if (distance <= .001f) { next++; continue; }
                heading = Quaternion.LookRotation(delta, Vector3.up);
                float travel = Mathf.Min(distance, budget);
                routePosition += delta / distance * travel;
                budget -= travel;
                if (travel >= distance) next++;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, heading, visualTurnRate * dt);
            phase = Mathf.Repeat(phase + 2f * Mathf.PI * swayFrequency * frequencyScale * dt, 2f * Mathf.PI);
            ApplyPersonality();
        }

        private void ApplyPersonality()
        {
            float wave = swayFrequency > 0f ? Mathf.Sin(phase) : 0f;
            // Root translation moves the radar position and collider together.
            Vector3 lateral = Vector3.Cross(Vector3.up, transform.forward).normalized;
            transform.position = routePosition + lateral * (wave * swayAmplitude * amplitudeScale);
            // The authored child is an identity pivot; the collider stays on the root.
            if (visualRoot != null)
                visualRoot.localRotation = Quaternion.AngleAxis(wave * rollAmplitude * rollScale, Vector3.forward);
        }

        public void SetAltitude(float y)
        {
            if (points != null)
                for (int i = 0; i < points.Length; i++) points[i].y = y;
            routePosition.y = y;
            Vector3 position = transform.position;
            position.y = y;
            transform.position = position;
        }
    }
}
