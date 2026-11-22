using UnityEngine;

namespace AirCombat
{
    // Common deterministic semi-implicit step for unpowered ordnance.
    // Bomb and NURS tuning remain in their separate wrappers.
    internal static class UnguidedBallistics
    {
        public static Vector3 Integrate(ref Vector3 velocity, Vector3 position,
            float dt, float gravity, float horizontalDrag)
        {
            Vector3 before = velocity;
            velocity += Vector3.down * (gravity * dt);
            float damping = Mathf.Exp(-horizontalDrag * dt);
            velocity.x *= damping;
            velocity.z *= damping;
            return position + (before + velocity) * (.5f * dt);
        }
    }
}
