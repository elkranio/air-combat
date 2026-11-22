using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Weapon Lab only: a genuine lateral displacement, not invulnerability frames.
    // Both pilots spend the entire shared Afterburner tank, with no extra cooldown.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)] // after both aircraft motors, before homing missiles
    public sealed class WeaponLabRoll : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float duration = .32f;
        [SerializeField, Min(.1f)] private float sidewaysDistance = 18f;

        private Afterburner boost;
        private Transform visual;
        private WeaponLabEnemy owner;
        private bool playerControlled;
        private bool rolling;
        private int direction;
        private Vector3 sidewaysWorld;
        private float elapsed;
        private float previousEase;

        public bool IsRolling => rolling;

        public void Configure(Afterburner tank, Transform aircraftVisual, bool playerInput,
            WeaponLabEnemy enemy = null)
        {
            boost = tank;
            visual = aircraftVisual;
            playerControlled = playerInput;
            owner = enemy;
            CancelRoll();
        }

        public bool TryRoll(int side)
        {
            if (!isActiveAndEnabled || rolling || side == 0 || boost == null ||
                (owner != null && owner.IsDown) || !boost.TryConsumeFullCharge()) return false;
            direction = side < 0 ? -1 : 1;
            sidewaysWorld = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            if (sidewaysWorld.sqrMagnitude < .1f) sidewaysWorld = Vector3.right;
            rolling = true;
            elapsed = previousEase = 0f;
            return true;
        }

        public void CancelRoll()
        {
            rolling = false;
            elapsed = previousEase = 0f;
        }

        private void Update()
        {
            if (playerControlled && Application.isFocused)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.qKey.wasPressedThisFrame) TryRoll(-1);
                    else if (keyboard.eKey.wasPressedThisFrame) TryRoll(1);
                }
            }
            if (!rolling) return;
            if (owner != null && owner.IsDown) { CancelRoll(); return; }

            float t = Mathf.Clamp01((elapsed + Time.deltaTime) / Mathf.Max(.05f, duration));
            float ease = t * t * (3f - 2f * t);
            transform.position += sidewaysWorld * (direction * sidewaysDistance * (ease - previousEase));
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            previousEase = ease;
        }

        private void LateUpdate()
        {
            // AircraftMotor rewrites the baseline visual bank each Update.
            // Add the roll here so the existing steering and banking remain intact.
            if (rolling && visual != null)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
                visual.localRotation = visual.localRotation *
                    Quaternion.AngleAxis(direction * 360f * t, Vector3.forward);
            }
            if (rolling && elapsed >= duration) CancelRoll();
        }

        private void OnDisable() { CancelRoll(); }
    }
}
