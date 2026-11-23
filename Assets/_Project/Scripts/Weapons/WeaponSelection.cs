using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    public enum WeaponMode { Burst, Gauss, Missile, Bomb, Nurs, DirectNurs }

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
        public bool SelectionLocked { get; private set; }
        public bool BombsEquipped { get; private set; }
        public bool NursEquipped { get; private set; }
        public bool DirectNursEquipped { get; private set; }

        // GroundAttackLab enables slots 4/5/6; WeaponLab/Sandbox keep their old slots.
        public void EquipBombs() { BombsEquipped = true; }
        public void EquipNurs() { NursEquipped = true; }
        public void EquipDirectNurs() { DirectNursEquipped = true; }
        public void SetSelectionLocked(bool locked) { SelectionLocked = locked; }

        private void Awake() { Selected = initialMode; }

        public void Select(WeaponMode next)
        {
            if (next < WeaponMode.Burst || next > WeaponMode.DirectNurs)
                throw new System.ArgumentOutOfRangeException(nameof(next));
            if (SelectionLocked || (next == WeaponMode.Bomb && !BombsEquipped) ||
                (next == WeaponMode.Nurs && !NursEquipped) ||
                (next == WeaponMode.DirectNurs && !DirectNursEquipped)) return;
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
            if (keyboard.digit4Key.wasPressedThisFrame && BombsEquipped) Select(WeaponMode.Bomb);
            if (keyboard.digit5Key.wasPressedThisFrame && NursEquipped) Select(WeaponMode.Nurs);
            if (keyboard.digit6Key.wasPressedThisFrame && DirectNursEquipped) Select(WeaponMode.DirectNurs);
        }
    }
}
