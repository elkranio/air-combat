using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // G5: a forward/downward unguided rocket salvo. Unlike G4 cabrage, it never
    // changes the aircraft's altitude, steering, throttle or selection lock.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(155)]
    public sealed class DirectNursRack : MonoBehaviour
    {
        [Header("Direct NURS salvo")]
        [SerializeField, Range(1, 12)] private int rocketCount = 6;
        [SerializeField, Min(.02f)] private float releaseInterval = .15f;
        [SerializeField, Min(0f)] private float cooldown = 4f;
        [SerializeField, Min(1f)] private float muzzleSpeed = 170f;
        [SerializeField, Range(0f, 45f)] private float downwardAngle = 10f;
        [SerializeField, Range(0f, 15f)] private float yawSpread = .7f;
        [SerializeField, Range(0f, 10f)] private float pitchSpread = .5f;
        [SerializeField, Min(1f)] private float boostSpreadMultiplier = 3f;
        [SerializeField, Min(.1f)] private float blastRadius = 8f;
        [SerializeField] private Vector3 leftPylon = new Vector3(-3.2f, -1.2f, 1.9f);
        [SerializeField] private Vector3 rightPylon = new Vector3(3.2f, -1.2f, 1.9f);

        private WeaponSelection selection;
        private FlightController flight;
        private Afterburner boost;
        private BreakTurn breakTurn;
        private Material shellMaterial;
        private Material trailMaterial;
        private Material flashMaterial;
        private Material scorchMaterial;
        private uint burstRevision;
        private float nextShotAt;
        private float readyAt;
        private int pending;
        private int firedInSalvo;
        private bool configured;

        public int FiredCount { get; private set; }
        public int ImpactCount { get; private set; }
        public int DamagedPartHits { get; private set; }
        public int PendingCount => pending;
        public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

        public void Configure(WeaponSelection weaponSelection, FlightController controller,
            Afterburner afterburner, BreakTurn maneuver)
        {
            if (configured) return;
            selection = weaponSelection;
            flight = controller;
            boost = afterburner;
            breakTurn = maneuver;
            if (selection == null || flight == null)
            {
                Debug.LogError("G5: Direct NURS rack needs weapon selection and flight controller.", this);
                enabled = false;
                return;
            }

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var streak = Shader.Find("AirCombat/SandboxEffect");
            if (lit == null || streak == null)
            {
                Debug.LogError("G5: required URP Lit or AirCombat/SandboxEffect shader missing.", this);
                enabled = false;
                return;
            }
            shellMaterial = MakeMaterial(lit, "G5 NURS shell", new Color(.26f, .32f, .29f));
            trailMaterial = new Material(streak) { name = "G5 NURS streak" };
            flashMaterial = MakeMaterial(lit, "G5 NURS flash", new Color(1f, .57f, .17f));
            scorchMaterial = MakeMaterial(lit, "G5 NURS scorch", new Color(.13f, .11f, .09f));
            configured = true;
        }

        private static Material MakeMaterial(Shader shader, string name, Color tint)
        {
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", tint);
            return mat;
        }

        private void LateUpdate()
        {
            if (!configured) return;
            bool selected = selection.Selected == WeaponMode.DirectNurs;
            bool breaking = breakTurn != null && breakTurn.IsBreaking;
            // Selection and Q/E can interrupt the salvo without undoing missiles
            // already launched. Reselecting slot 6 also cancels the remainder.
            if (pending > 0 && (!selected || breaking || selection.SelectionLocked ||
                burstRevision != selection.SelectionRevision)) CancelSalvo();
            if (!selected || breaking || selection.SelectionLocked || !Application.isFocused) return;

            var mouse = Mouse.current;
            if (pending == 0 && Time.time >= readyAt && mouse != null &&
                mouse.leftButton.wasPressedThisFrame)
            {
                pending = rocketCount;
                firedInSalvo = 0;
                burstRevision = selection.SelectionRevision;
                nextShotAt = Time.time;
            }

            // A fixed schedule, with no steering or afterburner suppression.
            // Every shot samples the CURRENT heading/position/boost state.
            while (pending > 0 && Time.time >= nextShotAt)
            {
                Fire(firedInSalvo);
                firedInSalvo++;
                pending--;
                nextShotAt += releaseInterval;
                if (pending == 0) readyAt = Time.time + cooldown;
            }
        }

        private void Fire(int index)
        {
            float spread = boost != null && boost.IsActive ? boostSpreadMultiplier : 1f;
            float yaw = Random.Range(-yawSpread, yawSpread) * spread;
            float pitch = Random.Range(-pitchSpread, pitchSpread) * spread;
            // Existing G4 ballistics: negative pitch means forward and DOWN.
            Vector3 velocity = NursBallistics.LaunchVelocity(transform.forward, transform.right,
                flight.CurrentSpeed, muzzleSpeed, -downwardAngle, yaw, pitch);
            Vector3 pylon = (index & 1) == 0 ? leftPylon : rightPylon;
            var rocket = new GameObject("Direct NURS " + (FiredCount + 1));
            rocket.transform.position = transform.TransformPoint(pylon);
            rocket.transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            rocket.AddComponent<NursProjectile>().Launch(velocity, transform, shellMaterial,
                trailMaterial, flashMaterial, scorchMaterial, OnImpact, blastRadius);
            FiredCount++;
        }

        private void OnImpact(int damagedParts)
        {
            ImpactCount++;
            DamagedPartHits += damagedParts;
        }

        public void CancelSalvo()
        {
            if (pending > 0) readyAt = Mathf.Max(readyAt, Time.time + cooldown);
            pending = 0;
            firedInSalvo = 0;
        }

        private void OnDisable() { CancelSalvo(); }
        private void OnDestroy()
        {
            if (shellMaterial != null) Destroy(shellMaterial);
            if (trailMaterial != null) Destroy(trailMaterial);
            if (flashMaterial != null) Destroy(flashMaterial);
            if (scorchMaterial != null) Destroy(scorchMaterial);
        }
    }
}
