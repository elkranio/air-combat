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

    }
}
