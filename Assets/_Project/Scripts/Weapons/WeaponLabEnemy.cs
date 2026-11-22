using UnityEngine;

namespace AirCombat
{
    // Weapon Lab only: a five-hit, unarmed sparring aircraft. No incoming damage or combat AI.
    [DisallowMultipleComponent]
    public sealed class WeaponLabEnemy : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHp = 5;
        [SerializeField, Min(0.1f)] private float respawnDelay = 2f;
        [SerializeField] private Transform player;
        [SerializeField] private EnemyFlightController flight;
        [SerializeField] private Collider hitbox;
        [SerializeField] private Renderer[] visibleParts;
        [SerializeField] private WeaponLabEvasion evasion;
        [SerializeField] private Afterburner evadeBoost;

        private MaterialPropertyBlock hitTint;
        private float flashUntil;
        private float hideAt;
        private float respawnAt;
        private bool down;
        private bool hidden;

        public int Hp { get; private set; } = 5;
        public int MaxHp => maxHp;
        public int Kills { get; private set; }
        public bool IsDown => down;
        public string EvasionLabel => evasion != null ? evasion.ModeLabel : "OFF";

        public void Configure(Transform chaseTarget, EnemyFlightController controller,
            Collider collider, Renderer[] renderers)
        {
            player = chaseTarget;
            flight = controller;
            hitbox = collider;
            visibleParts = renderers;
        }

        public void ConfigureEvasion(WeaponLabEvasion brain, Afterburner boost)
        {
            evasion = brain;
            evadeBoost = boost;
        }

        private void Start() { ResetEncounter(); }

        public void ResetEncounter()
        {
            if (player == null || flight == null || hitbox == null) return;
            Hp = maxHp;
            if (evasion != null) evasion.ResetForEncounter();
            down = hidden = false;
            flashUntil = hideAt = respawnAt = 0f;
            SetVisible(true);
            ClearHitTint();
            hitbox.enabled = true;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 at = player.position + forward * 92f + right * 16f;
            at.y = player.position.y;
            flight.enabled = true;
            flight.Respawn(at, Quaternion.LookRotation(forward, Vector3.up));
        }

        // Returns false when a round reaches the aircraft after it has been downed.
        public bool Hit(Vector3 point)
        {
            if (down || Hp <= 0) return false;
            Hp--;
            flashUntil = Time.time + 0.12f;
            if (hitTint == null) hitTint = new MaterialPropertyBlock();
            hitTint.Clear();
            hitTint.SetColor("_BaseColor", new Color(1f, .95f, .45f));
            if (visibleParts != null)
                foreach (var renderer in visibleParts)
                    if (renderer != null) renderer.SetPropertyBlock(hitTint);

            if (Hp == 0)
            {
                Kills++;
                down = true;
                flight.enabled = false;
                if (evasion != null) evasion.ResetForEncounter();
                if (evadeBoost != null) evadeBoost.Tick(false, 0f); // extinguish enemy flame on kill
                hitbox.enabled = false;
                hideAt = Time.time + 0.12f; // let the final hit flash register
                respawnAt = Time.time + respawnDelay;
            }
            return true;
        }

        private void Update()
        {
            if (down)
            {
                if (!hidden && Time.time >= hideAt)
                {
                    hidden = true;
                    SetVisible(false);
                }
                if (Time.time >= respawnAt) ResetEncounter();
            }
            else if (flashUntil > 0f && Time.time >= flashUntil)
            {
                flashUntil = 0f;
                ClearHitTint();
            }
        }

        private void SetVisible(bool show)
        {
            if (visibleParts == null) return;
            foreach (var renderer in visibleParts)
                if (renderer != null) renderer.enabled = show;
        }

        private void ClearHitTint()
        {
            if (visibleParts == null) return;
            foreach (var renderer in visibleParts)
                if (renderer != null) renderer.SetPropertyBlock(null);
        }
    }
}
