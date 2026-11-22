using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Weapon Lab only: a genuine lateral dodge with a sharp bank, not invulnerability frames.
    // Both pilots spend the entire shared Afterburner tank, with no extra cooldown.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)] // after both aircraft motors, before homing missiles
    public sealed class WeaponLabRoll : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float duration = .42f;
        [SerializeField, Min(.1f)] private float sidewaysDistance = 27f;
        [SerializeField, Range(0f, 85f)] private float dodgeBankAngle = 65f;

        private Afterburner boost;
        private Transform visual;
        private Quaternion visualRestRotation;
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
            visualRestRotation = visual != null ? visual.localRotation : Quaternion.identity;
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
            // AircraftMotor writes the regular steering bank every Update.
            // Blend it toward a steep, non-inverting bank and back, without spinning the model.
            if (rolling && visual != null)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
                const float snapFraction = .28f;
                float weight = t < snapFraction
                    ? Mathf.SmoothStep(0f, 1f, t / snapFraction)
                    : Mathf.SmoothStep(1f, 0f, (t - snapFraction) / (1f - snapFraction));
                // Ordinary steering uses negative bank for right turns; match that convention.
                var dodgeBank = Quaternion.AngleAxis(-direction * dodgeBankAngle, Vector3.forward)
                    * visualRestRotation;
                visual.localRotation = Quaternion.Slerp(visual.localRotation, dodgeBank, weight);
            }
            if (rolling && elapsed >= duration) CancelRoll();
        }

        private void OnDisable() { CancelRoll(); }
    }
}
