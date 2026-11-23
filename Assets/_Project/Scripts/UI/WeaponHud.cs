using UnityEngine;
using UnityEngine.UI;

namespace AirCombat
{
    // Lightweight readout bound to the player loadout, not to a specific gun.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(250)] // Sample after player trigger (130) schedules shots.
    public sealed class WeaponHud : MonoBehaviour
    {
        [SerializeField] private Text weaponLabel;
        [SerializeField] private Image cooldownFill;
        private PlayerWeaponTrigger loadout;
        private string lastName;

        public void Bind(PlayerWeaponTrigger source)
        {
            loadout = source;
            lastName = null;
            gameObject.SetActive(source != null);
        }

        private void LateUpdate()
        {
            var current = loadout != null ? loadout.SelectedHudStatus : null;
            if (current == null)
            {
                if (weaponLabel != null && lastName != "NO WEAPON")
                {
                    weaponLabel.text = "NO WEAPON";
                    lastName = "NO WEAPON";
                }
                if (cooldownFill != null) SetFill(0f);
                return;
            }
            if (weaponLabel != null && lastName != current.HudName)
            {
                lastName = current.HudName;
                weaponLabel.text = lastName;
            }
            if (cooldownFill != null)
            {
                float ready = Mathf.Clamp01(current.Readiness01(Time.time));
                SetFill(ready);
                cooldownFill.color = ready >= .999f
                    ? new Color(.58f, .92f, .75f, 1f)
                    : new Color(1f, .73f, .30f, 1f);
            }
        }

        private void SetFill(float fraction)
        {
            var rect = cooldownFill.rectTransform;
            rect.anchorMax = new Vector2(fraction, 1f);
        }
    }
}
