using UnityEngine;

namespace AirCombat
{
    // Arcade powered flight: constant launch velocity, including aircraft motion.
    // The footprint predictor and projectile share this straight-line step.
    // No steering, homing or correction after release.
    public static class NursBallistics
    {
        public const float IntegrationStep = .025f;

        public static Vector3 LaunchVelocity(Vector3 forward, Vector3 right, float aircraftSpeed,
            float muzzleSpeed, float pitchDegrees, float yawErrorDegrees, float pitchErrorDegrees)
        {
            Vector3 yawed = Quaternion.AngleAxis(yawErrorDegrees, Vector3.up) * forward;
            Vector3 direction = Quaternion.AngleAxis(-(pitchDegrees + pitchErrorDegrees), right) * yawed;
            return forward * aircraftSpeed + direction.normalized * muzzleSpeed;
        }

        public static Vector3 Integrate(ref Vector3 velocity, Vector3 position, float dt)
        {
            return position + velocity * dt;
        }
    }
}
