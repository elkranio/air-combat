using UnityEngine;

namespace AirCombat
{
    // G4b: six sequential unguided launches across the G4a climb. Cabrage owns
    // attack start, Q/E cancellation, movement locking and cooldowns; this rack
    // only observes its phase and spawns physical projectiles.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(155)] // After CabrageManeuver's LateUpdate height/pitch.
    public sealed class NursRocketRack : MonoBehaviour
    {
        [Header("NURS salvo (during climb only)")]
        [SerializeField, Range(1, 12)] private int rocketCount = 6;
        [SerializeField, Min(0f)] private float firstReleaseSeconds = .55f;
        [SerializeField, Min(.02f)] private float releaseInterval = .15f;
        [SerializeField, Min(1f)] private float muzzleSpeed = 170f;
        [SerializeField, Range(0f, 15f)] private float yawSpreadDegrees = 5f;
        [SerializeField, Range(0f, 10f)] private float pitchSpreadDegrees = 3f;
        [SerializeField] private Vector3 leftPylon = new Vector3(-3.2f, -1.2f, 1.9f);
        [SerializeField] private Vector3 rightPylon = new Vector3(3.2f, -1.2f, 1.9f);

        private CabrageManeuver cabrage;
        private FlightController flight;
        private Material shellMaterial;
        private Material streakMaterial;
        private Material flashMaterial;
        private Material scorchMaterial;
        private int firedThisClimb;
        private bool wasClimbing;
        private bool configured;

        public int FiredCount { get; private set; }
        public int ImpactCount { get; private set; }
        public int DamagedPartHits { get; private set; }
        public int FiredThisClimb => firedThisClimb;

        public void Configure(CabrageManeuver maneuver, FlightController controller)
        {
            if (configured) return;
            cabrage = maneuver;
            flight = controller;
            if (cabrage == null || flight == null)
            {
                Debug.LogError("G4b: NURS rack needs CabrageManeuver and FlightController.", this);
                enabled = false;
                return;
            }
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var trailShader = Shader.Find("AirCombat/SandboxEffect");
            if (lit == null || trailShader == null)
            {
                Debug.LogError("G4b: required URP Lit or AirCombat/SandboxEffect shader missing.", this);
                enabled = false;
                return;
            }
            shellMaterial = MakeMaterial(lit, "G4b NURS body", new Color(.26f, .32f, .29f));
            streakMaterial = new Material(trailShader) { name = "G4b NURS streak" };
            flashMaterial = MakeMaterial(lit, "G4b NURS flash", new Color(1f, .57f, .17f));
            scorchMaterial = MakeMaterial(lit, "G4b NURS scorch", new Color(.13f, .11f, .09f));
            configured = true;
        }

        private static Material MakeMaterial(Shader shader, string label, Color tint)
        {
            var material = new Material(shader) { name = label };
            material.SetColor("_BaseColor", tint);
            return material;
        }

        private void LateUpdate()
        {
            if (!configured) return;
            if (cabrage.Phase != CabrageManeuver.ManeuverPhase.Climb)
            {
                wasClimbing = false;
                firedThisClimb = 0;
                return;
            }
            if (!wasClimbing)
            {
                firedThisClimb = 0;
                wasClimbing = true;
            }

            float elapsed = cabrage.ClimbElapsedSeconds;
            // Catch up if a frame skips a scheduled release; never fire rockets
            // after Q/E changed the phase to EmergencyReturn or Recovery.
            while (firedThisClimb < rocketCount &&
                elapsed >= firstReleaseSeconds + firedThisClimb * releaseInterval)
            {
                Fire(firedThisClimb);
                firedThisClimb++;
            }
        }

        private void Fire(int index)
        {
            float yawError = Random.Range(-yawSpreadDegrees, yawSpreadDegrees);
            float pitchError = Random.Range(-pitchSpreadDegrees, pitchSpreadDegrees);
            float pitch = cabrage.PitchAtClimbTime(cabrage.ClimbElapsedSeconds);
            Vector3 velocity = NursBallistics.LaunchVelocity(transform.forward, transform.right,
                flight.CurrentSpeed, muzzleSpeed, pitch, yawError, pitchError);
            // Alternate between the two pylons; each launch uses the *current*
            // aircraft position and heading, not a cached pre-maneuver frame.
            Vector3 pylon = (index & 1) == 0 ? leftPylon : rightPylon;
            var rocket = new GameObject("NURS " + (FiredCount + 1));
            rocket.transform.position = transform.TransformPoint(pylon);
            rocket.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
            rocket.AddComponent<NursProjectile>().Launch(velocity, transform, shellMaterial,
                streakMaterial, flashMaterial, scorchMaterial, OnImpact);
            FiredCount++;
        }

        private void OnImpact(int damagedParts)
        {
            ImpactCount++;
            DamagedPartHits += damagedParts;
        }

        private void OnDisable()
        {
            wasClimbing = false;
            firedThisClimb = 0;
        }

        private void OnDestroy()
        {
            if (shellMaterial != null) Destroy(shellMaterial);
            if (streakMaterial != null) Destroy(streakMaterial);
            if (flashMaterial != null) Destroy(flashMaterial);
            if (scorchMaterial != null) Destroy(scorchMaterial);
        }
    }
}
