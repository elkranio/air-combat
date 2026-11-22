using UnityEngine;

namespace AirCombat
{
    // Physical trajectory for unguided NURS rockets. No impact predictor.
    // The launcher supplies aircraft velocity plus an initial upward muzzle kick;
    // no thrust, steering, homing or random corrections happen after release.
    public static class NursBallistics
    {
        public const float Gravity = 80f;
        public const float HorizontalDrag = .015f;
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
            return UnguidedBallistics.Integrate(ref velocity, position, dt, Gravity, HorizontalDrag);
        }
    }
}
