using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    public enum WeaponMode { Burst, Gauss, Missile }

    // Owns weapon selection. Firing and aiming stay in their respective components.
    // SelectionRevision advances even when reselecting the current slot: a reselect
    // must still cancel a pending burst without resetting the cooldown.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)] // before the current guns (130) and lock (200)
    public sealed class WeaponSelection : MonoBehaviour
    {
        [SerializeField] private WeaponMode initialMode = WeaponMode.Burst;

        public WeaponMode Selected { get; private set; } = WeaponMode.Burst;
        public uint SelectionRevision { get; private set; }

        private void Awake() { Selected = initialMode; }

        public void Select(WeaponMode next)
        {
            if (next < WeaponMode.Burst || next > WeaponMode.Missile)
                throw new System.ArgumentOutOfRangeException(nameof(next));
            Selected = next;
            unchecked { SelectionRevision++; }
        }

        private void LateUpdate()
        {
            if (!Application.isFocused) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            // Preserve original priority if two slot keys happen to register together.
            if (keyboard.digit1Key.wasPressedThisFrame) Select(WeaponMode.Burst);
            if (keyboard.digit2Key.wasPressedThisFrame) Select(WeaponMode.Gauss);
            if (keyboard.digit3Key.wasPressedThisFrame) Select(WeaponMode.Missile);
        }
    }
}
