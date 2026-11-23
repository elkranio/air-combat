using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Player-only input adapter. An enemy does NOT attach this component:
    // its AI calls the same ITriggeredWeapon interface with its own intent.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)]
    public sealed class PlayerWeaponTrigger : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour weaponComponent;
        private ITriggeredWeapon weapon;

#if UNITY_EDITOR
        public void SetAuthoringWeapon(MonoBehaviour source) { weaponComponent = source; }
#endif

        private void Awake()
        {
            weapon = weaponComponent as ITriggeredWeapon;
            if (weapon == null)
            {
                Debug.LogError("PlayerWeaponTrigger needs a component implementing ITriggeredWeapon.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            var mouse = Mouse.current;
            bool held = Application.isFocused && mouse != null && mouse.leftButton.isPressed;
            weapon?.TickTrigger(held, Time.time);
        }

        private void OnDisable() { weapon?.CeaseFire(Time.time); }
    }
}
