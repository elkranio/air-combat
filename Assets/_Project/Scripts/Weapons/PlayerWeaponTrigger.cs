using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Player-only input adapter. Modules stay input-agnostic so AI can use the
    // same ITriggeredWeapon. Production explicitly equips two independent slots;
    // legacy scenes retain their original wheel-selected loadout.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)]
    public sealed class PlayerWeaponTrigger : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour weaponComponent; // GUN-01 first weapon, for stable ordering.
        private readonly List<ITriggeredWeapon> weapons = new List<ITriggeredWeapon>();
        private int selectedIndex;
        private bool dualSlots;
        private MonoBehaviour secondaryComponent;

        public bool UsesDualSlots => dualSlots;
        public IWeaponHudStatus PrimaryHudStatus => PrimaryWeapon as IWeaponHudStatus;
        public IWeaponHudStatus SecondaryHudStatus => SecondaryWeapon as IWeaponHudStatus;
        private ITriggeredWeapon PrimaryWeapon => ActiveWeapon(weaponComponent);
        private ITriggeredWeapon SecondaryWeapon => ActiveWeapon(secondaryComponent);
        private static ITriggeredWeapon ActiveWeapon(MonoBehaviour component) =>
            component != null && component.isActiveAndEnabled ? component as ITriggeredWeapon : null;

        // Explicit loadout binding: never infer a second slot from child ordering.
        public void ConfigureDualSlots(MonoBehaviour secondary)
        {
            if (dualSlots)
            {
                PrimaryWeapon?.CeaseFire(Time.time);
                SecondaryWeapon?.CeaseFire(Time.time);
            }
            else SelectedWeapon?.CeaseFire(Time.time);
            dualSlots = true;
            secondaryComponent = secondary;
            RefreshWeapons();
        }

        public int WeaponCount => weapons.Count;
        public IWeaponHudStatus SelectedHudStatus => SelectedWeapon as IWeaponHudStatus;
        private ITriggeredWeapon SelectedWeapon => selectedIndex >= 0 && selectedIndex < weapons.Count
            ? weapons[selectedIndex] : null;

#if UNITY_EDITOR
        public void SetAuthoringWeapon(MonoBehaviour source) { weaponComponent = source; }
#endif

        private void Awake()
        {
            RefreshWeapons();
            if (weapons.Count == 0)
            {
                Debug.LogError("PlayerWeaponTrigger needs an equipped ITriggeredWeapon.", this);
                enabled = false;
            }
        }

        // Called when a loadout changes. Existing mounted modules are discovered
        // once, not searched every frame; the original GUN-01 mount remains first.
        public void RefreshWeapons()
        {
            var previous = SelectedWeapon;
            weapons.Clear();
            if (dualSlots)
            {
                if (PrimaryWeapon != null) weapons.Add(PrimaryWeapon);
                if (SecondaryWeapon != null && SecondaryWeapon != PrimaryWeapon) weapons.Add(SecondaryWeapon);
                selectedIndex = 0;
                return;
            }
            if (weaponComponent != null && weaponComponent is ITriggeredWeapon first &&
                weaponComponent.isActiveAndEnabled)
                weapons.Add(first);
            foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || !component.isActiveAndEnabled ||
                    !(component is ITriggeredWeapon trigger) || weapons.Contains(trigger)) continue;
                weapons.Add(trigger);
            }
            selectedIndex = previous != null ? weapons.IndexOf(previous) : 0;
            if (selectedIndex < 0) selectedIndex = 0;
        }

        private void LateUpdate()
        {
            if (MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            var mouse = Mouse.current;
            if (dualSlots)
            {
                bool focused = Application.isFocused && mouse != null;
                PrimaryWeapon?.TickTrigger(focused && mouse.leftButton.isPressed, Time.time);
                // Tick even on release: secondary owns acquisition and rearm.
                if (SecondaryWeapon != PrimaryWeapon)
                    SecondaryWeapon?.TickTrigger(focused && mouse.rightButton.isPressed, Time.time);
                return;
            }
            if (Application.isFocused && mouse != null && weapons.Count > 1)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f)
                {
                    SelectedWeapon?.CeaseFire(Time.time);
                    selectedIndex = (selectedIndex + (scroll > 0f ? -1 : 1) + weapons.Count) % weapons.Count;
                }
            }
            bool held = Application.isFocused && mouse != null && mouse.leftButton.isPressed;
            SelectedWeapon?.TickTrigger(held, Time.time);
        }

        private void OnDisable()
        {
            if (dualSlots)
            {
                PrimaryWeapon?.CeaseFire(Time.time);
                if (SecondaryWeapon != PrimaryWeapon) SecondaryWeapon?.CeaseFire(Time.time);
            }
            else SelectedWeapon?.CeaseFire(Time.time);
        }
    }
}
