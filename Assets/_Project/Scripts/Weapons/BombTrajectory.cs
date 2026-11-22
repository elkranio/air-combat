using UnityEngine;

namespace AirCombat
{
    // Shared integration for the live bomb and its predicted impact. Gameplay units
    // deliberately fall in ~1.1 seconds from the GroundAttackLab's 48m flight level.
    public static class BombTrajectory
    {
        public const float Gravity = 80f;
        public const float HorizontalDrag = .045f;
        public const float IntegrationStep = .04f;

        public static Vector3 Integrate(ref Vector3 velocity, Vector3 position, float dt)
        {
            Vector3 before = velocity;
            velocity += Vector3.down * (Gravity * dt);
            float damping = Mathf.Exp(-HorizontalDrag * dt);
            velocity.x *= damping;
            velocity.z *= damping;
            return position + (before + velocity) * (.5f * dt);
        }

        // Raycast against the real world, including depot damage-zone colliders.
        // The same integrator is used by BombProjectile so the cue is not fake.
        public static bool Predict(Vector3 origin, Vector3 initialVelocity,
            out Vector3 impact, out float flightTime)
        {
            Vector3 pos = origin;
            Vector3 velocity = initialVelocity;
            float elapsed = 0f;
            for (int i = 0; i < 125; i++)
            {
                Vector3 next = Integrate(ref velocity, pos, IntegrationStep);
                Vector3 delta = next - pos;
                float distance = delta.magnitude;
                if (distance > 0.0001f && Physics.Raycast(pos, delta / distance,
                    out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    impact = hit.point;
                    flightTime = elapsed + IntegrationStep * (hit.distance / distance);
                    return true;
                }
                if (next.y <= 0f)
                {
                    float fraction = Mathf.Clamp01(pos.y / Mathf.Max(.0001f, pos.y - next.y));
                    impact = Vector3.Lerp(pos, next, fraction);
                    flightTime = elapsed + IntegrationStep * fraction;
                    return true;
                }
                pos = next;
                elapsed += IntegrationStep;
            }
            impact = pos;
            flightTime = elapsed;
            return false;
        }
    }
}
