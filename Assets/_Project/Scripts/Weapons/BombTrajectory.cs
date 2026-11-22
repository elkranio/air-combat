using UnityEngine;

namespace AirCombat
{
    // Gravity-bomb tuning. The integration kernel is shared with NURS.
    public static class BombTrajectory
    {
        public const float Gravity = 80f;
        public const float HorizontalDrag = .045f;
        public const float IntegrationStep = .04f;

        public static Vector3 Integrate(ref Vector3 velocity, Vector3 position, float dt)
        {
            return UnguidedBallistics.Integrate(ref velocity, position, dt, Gravity, HorizontalDrag);
        }
    }
}
