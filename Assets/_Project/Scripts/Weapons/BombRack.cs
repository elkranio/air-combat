using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Fixed four-bomb burst. Owns cadence and release; physical flight and impact
    // are independent components. No predictive HUD: aiming is learned by flying.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(145)]
    public sealed class BombRack : MonoBehaviour
    {
        [Header("Bomb burst")]
        [SerializeField, Range(2, 8)] private int bombCount = 4;
        [SerializeField, Min(.05f)] private float releaseInterval = .25f;
        [SerializeField, Min(.1f)] private float cooldown = 3f;
        [SerializeField] private Vector3 releaseOffset = new Vector3(0f, -2f, 1.5f);
        [Header("Afterburner error (metres/second)")]
        [SerializeField, Min(0f)] private float boostErrorSpeed = 9f;

        private WeaponSelection selection;
        private FlightController flight;
        private Afterburner boost;
        private BreakTurn breakTurn;
        private Material shellMaterial;
        private Material flashMaterial;
        private Material scorchMaterial;
        private float nextShotAt;
        private float readyAt;
        private int pending;
        private bool configured;

        public int FiredCount { get; private set; }
        public int PendingCount => pending;
        public float CooldownLeft => Mathf.Max(0, readyAt - Time.time);
        public int BurstSize => bombCount;
        public bool IsBurstActive => pending > 0;
        public bool IsSelected => selection != null && selection.Selected == WeaponMode.Bomb;

        public void Configure(WeaponSelection weaponSelection, FlightController controller,
            Afterburner afterburner, BreakTurn maneuver)
        {
            if (configured) return;
            configured = true;
            selection = weaponSelection;
            flight = controller;
            boost = afterburner;
            breakTurn = maneuver;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            shellMaterial = MakeMaterial(shader, "G3 Bomb shell", new Color(.22f,.28f,.27f));
            flashMaterial = MakeMaterial(shader, "G3 Explosion", new Color(1f,.57f,.16f));
            scorchMaterial = MakeMaterial(shader, "G3 Scorch", new Color(.13f,.11f,.09f));
        }

        private static Material MakeMaterial(Shader shader, string name, Color color)
        {
            var result = new Material(shader) { name = name };
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Smoothness", .08f);
            return result;
        }

        private void LateUpdate()
        {
            if (!configured || selection == null) return;
            // A successful Q/E maneuver sacrifices remaining bombs, and the
            // full cooldown still applies. Failed (empty-tank) Q/E does not cancel.
            if (pending > 0 && breakTurn != null && breakTurn.IsBreaking)
                CancelBurst();
            if (!IsSelected) return;

            var mouse = Mouse.current;
            if (pending == 0 && Time.time >= readyAt && Application.isFocused &&
                mouse != null && mouse.leftButton.isPressed &&
                (breakTurn == null || !breakTurn.IsBreaking))
            {
                pending = bombCount;
                nextShotAt = Time.time;
                selection.SetSelectionLocked(true);
            }
            if (pending == 0 || Time.time < nextShotAt) return;

            ReleaseBomb();
            pending--;
            if (pending == 0)
            {
                readyAt = Time.time + cooldown;
                selection.SetSelectionLocked(false);
            }
            else nextShotAt = Time.time + releaseInterval;
        }

        private void ReleaseBomb()
        {
            Vector3 forward = transform.forward;
            Vector3 velocity = forward * flight.CurrentSpeed;
            if (boost != null && boost.IsActive)
            {
                Vector2 deviation = Random.insideUnitCircle * boostErrorSpeed;
                velocity += transform.right * deviation.x + forward * (deviation.y * .4f);
            }
            var go = new GameObject("Gravity bomb " + (FiredCount + 1));
            go.transform.position = transform.TransformPoint(releaseOffset);
            go.transform.rotation = transform.rotation;
            go.AddComponent<BombProjectile>().Launch(velocity, shellMaterial, flashMaterial,
                scorchMaterial);
            FiredCount++;
        }

        public void CancelBurst()
        {
            if (pending > 0) readyAt = Mathf.Max(readyAt, Time.time + cooldown);
            pending = 0;
            if (selection != null) selection.SetSelectionLocked(false);
        }

        private void OnDisable() { CancelBurst(); }
        private void OnDestroy()
        {
            if (shellMaterial != null) Destroy(shellMaterial);
            if (flashMaterial != null) Destroy(flashMaterial);
            if (scorchMaterial != null) Destroy(scorchMaterial);
        }
    }
}
