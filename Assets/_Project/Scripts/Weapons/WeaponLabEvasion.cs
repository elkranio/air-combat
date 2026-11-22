using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Weapon Lab only. Reacts to being inside the PLAYER AIRCRAFT'S forward cone,
    // not the mouse, reticle or individual projectiles. Never steers the aircraft itself.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)] // B toggle is sampled before EnemyFlightController.Update (order 10).
    public sealed class WeaponLabEvasion : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Min(15f)] private float noticeRange = 135f;
        [SerializeField, Range(0f, 20f)] private float sectorPadding = 2f;
        [SerializeField, Range(0f, 1f)] private float evadeChance = .72f;
        [SerializeField, Min(.05f)] private float minReaction = .32f;
        [SerializeField, Min(.05f)] private float maxReaction = .52f;
        [SerializeField, Min(.1f)] private float minBreakDuration = .85f;
        [SerializeField, Min(.1f)] private float maxBreakDuration = 1.35f;
        [SerializeField, Min(.1f)] private float minCooldown = 1.6f;
        [SerializeField, Min(.1f)] private float maxCooldown = 2.7f;

        private enum State { Watching, Breaking, Cooldown }
        private State state;
        private WeaponLabFreeAim playerAim;
        private float exposedFor;
        private float reactAfter;
        private float breakRemaining;
        private float breakDuration;
        private float cooldownRemaining;
        private float turnSign;
        private bool useBoost;
        private WeaponLabRoll roll;
        private MissileProjectile incomingMissile;
        private bool missileRollPending;
        private bool missileRollOnDistance;
        private float missileRollAt;
        private float missileRollDistance;

        public bool EvasionEnabled { get; private set; } = true;
        public string ModeLabel => !EvasionEnabled ? "OFF" : state == State.Breaking
            ? (useBoost && breakRemaining > breakDuration * .3f ? "BOOST" : "BREAK")
            : state == State.Cooldown ? "RECOVER" : "WATCH";

        public void Configure(Transform target)
        {
            player = target;
        }

        public void ConfigureRoll(WeaponLabRoll maneuver) { roll = maneuver; }

        public void NotifyIncomingMissile(MissileProjectile missile)
        {
            incomingMissile = missile;
            // Randomize pilot judgment, never missile collision or invulnerability.
            missileRollPending = Random.value < .8f;
            missileRollOnDistance = Random.value < .55f;
            missileRollAt = Time.time + Random.Range(.16f, .65f);
            missileRollDistance = Random.Range(30f, 80f);
        }

        private void Start()
        {
            playerAim = player != null ? player.GetComponent<WeaponLabFreeAim>() : null;
            ResetForEncounter();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null && keyboard.bKey.wasPressedThisFrame)
            {
                EvasionEnabled = !EvasionEnabled;
                ResetForEncounter(); // Also cancels any ongoing roll when B disables evasion.
            }
        }

        // Called once per frame by the existing aircraft controller. Returns an
        // optional steering override and an optional regular Afterburner request.
        public bool Tick(float dt, out float steer, out bool boost)
        {
            steer = 0f;
            boost = false;
            if (!EvasionEnabled || player == null || dt <= 0f) return false;

            // React to an actual incoming missile. A late response or an empty
            // afterburner tank means no evasive roll; there are no free misses.
            if (missileRollPending)
            {
                if (incomingMissile == null) missileRollPending = false;
                else
                {
                    Vector3 separation = incomingMissile.transform.position - transform.position;
                    separation.y = 0f;
                    bool timeToRoll = missileRollOnDistance
                        ? separation.sqrMagnitude <= missileRollDistance * missileRollDistance
                        : Time.time >= missileRollAt;
                    if (timeToRoll)
                    {
                        missileRollPending = false;
                        int side = Random.value < .5f ? -1 : 1;
                        if (roll != null && roll.TryRoll(side))
                        {
                            BeginCooldown(); // Ignore the ordinary gun-threat break this frame.
                            return false;
                        }
                    }
                }
            }

            if (state == State.Breaking)
            {
                breakRemaining -= dt;
                if (breakRemaining <= 0f)
                {
                    BeginCooldown();
                    return false;
                }
                steer = turnSign * .95f; // Actual turning is still limited by EnemyFlightController.
                boost = useBoost && breakRemaining > breakDuration * .3f;
                return true;
            }
            if (state == State.Cooldown)
            {
                cooldownRemaining -= dt;
                if (cooldownRemaining <= 0f)
                {
                    state = State.Watching;
                    exposedFor = 0f;
                    reactAfter = Random.Range(minReaction, Mathf.Max(minReaction, maxReaction));
                }
                return false;
            }

            Vector3 relative = transform.position - player.position;
            relative.y = 0f;
            float distance = relative.magnitude;
            float halfAngle = playerAim != null ? playerAim.HalfAngle : 12f;
            bool threatened = distance > 12f && distance < noticeRange &&
                Mathf.Abs(Vector3.SignedAngle(player.forward, relative, Vector3.up)) <= halfAngle + sectorPadding;
            if (!threatened)
            {
                exposedFor = 0f;
                return false;
            }
            exposedFor += dt;
            if (exposedFor < reactAfter) return false;
            exposedFor = 0f;
            if (Random.value > evadeChance)
            {
                BeginCooldown(); // Sometimes the pilot simply fails to react.
                return false;
            }

            float signedAngle = Vector3.SignedAngle(player.forward, relative, Vector3.up);
            turnSign = Mathf.Abs(signedAngle) < 2f
                ? (Random.value < .5f ? -1f : 1f) : Mathf.Sign(signedAngle);
            breakDuration = Random.Range(minBreakDuration, Mathf.Max(minBreakDuration, maxBreakDuration));
            breakRemaining = breakDuration;
            // The full-tank roll takes priority; an ordinary sector break is the fallback.
            // Either gun can trigger this via the existing forward-cone threat reaction.
            bool rolled = roll != null && roll.TryRoll(turnSign > 0f ? 1 : -1);
            useBoost = !rolled;
            state = State.Breaking;
            steer = turnSign * .95f;
            boost = useBoost;
            return true;
        }

        public void ResetForEncounter()
        {
            if (roll != null) roll.CancelRoll();
            state = State.Watching;
            exposedFor = breakRemaining = breakDuration = cooldownRemaining = 0f;
            useBoost = false;
            incomingMissile = null;
            missileRollPending = false;
            reactAfter = Random.Range(minReaction, Mathf.Max(minReaction, maxReaction));
        }

        private void BeginCooldown()
        {
            state = State.Cooldown;
            useBoost = false;
            cooldownRemaining = Random.Range(minCooldown, Mathf.Max(minCooldown, maxCooldown));
        }
    }
}
