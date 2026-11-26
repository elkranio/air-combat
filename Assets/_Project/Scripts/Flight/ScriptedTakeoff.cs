using UnityEngine;

namespace AirCombat
{
    // A scripted runway roll and climb. The caller owns input, phase transitions,
    // flame presentation, pause/retry and the handover to the flight motor.
    internal sealed class ScriptedTakeoff
    {
        private readonly Transform aircraft, visual;
        private readonly Vector3 runwayPosition;
        private readonly Quaternion runwayRotation, visualRest;
        private double elapsed;

        internal ScriptedTakeoff(Transform aircraft, Transform visual, Vector3 runwayPosition,
            Quaternion runwayRotation, Quaternion visualRest)
        {
            this.aircraft = aircraft;
            this.visual = visual;
            this.runwayPosition = runwayPosition;
            this.runwayRotation = runwayRotation;
            this.visualRest = visualRest;
        }

        internal void ResetTimer() => elapsed = 0d;

        internal bool Tick(float dt, float speed, float rollSeconds, float climbSeconds,
            float cruiseAltitude, float maxPitch)
        {
            float roll = Mathf.Max(.1f, rollSeconds);
            float climb = Mathf.Max(.1f, climbSeconds);
            // Do not saturate a float timer and then compare it to a separately
            // evaluated sum: 2.2f + 2.5f can round differently across runtimes.
            // Advance in double precision; clamp only the trajectory sample.
            elapsed += Mathf.Max(0f, dt);
            double duration = (double)roll + climb;
            bool finished = elapsed >= duration;
            float t = finished ? roll : Mathf.Min((float)elapsed, roll);
            float airTime = finished ? climb : Mathf.Clamp((float)(elapsed - roll), 0f, climb);
            // Analytic integral of linear acceleration; deterministic at any FPS.
            float distance = .5f * speed * t * t / roll + speed * airTime;
            float u = Mathf.Clamp01(airTime / climb);
            float rise = Mathf.Max(0f, cruiseAltitude - runwayPosition.y) * Mathf.SmoothStep(0f, 1f, u);
            Vector3 forward = runwayRotation * Vector3.forward;
            aircraft.SetPositionAndRotation(runwayPosition + forward * distance + Vector3.up * rise, runwayRotation);
            // Pitch the visual only. Limit pitch near the runway so the tail does not dip through it.
            float pitch = Mathf.Min(maxPitch * Mathf.Sin(Mathf.PI * u), Mathf.Atan2(rise, 7.5f) * Mathf.Rad2Deg);
            visual.localRotation = visualRest * Quaternion.Euler(-Mathf.Max(0f, pitch), 0f, 0f);
            return finished;
        }

#if UNITY_EDITOR
        internal void PlaceAtEnd(float speed, float rollSeconds, float climbSeconds, float cruiseAltitude)
        {
            // Exact endpoint of the scripted takeoff, without waiting for it.
            float distance = speed * (.5f * Mathf.Max(.1f, rollSeconds) + Mathf.Max(.1f, climbSeconds));
            Vector3 position = runwayPosition + runwayRotation * Vector3.forward * distance;
            position.y = Mathf.Max(runwayPosition.y, cruiseAltitude);
            aircraft.SetPositionAndRotation(position, runwayRotation);
        }
#endif
    }
}
