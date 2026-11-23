using UnityEngine;
using UnityEngine.UI;

namespace AirCombat
{
    // Compact status instrument over the full-screen camera. No viewport crop.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class FlightHud : MonoBehaviour
    {
        public const float DefaultBarFraction = 0.04f;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Afterburner afterburner;
        [SerializeField] private RectTransform bar;
        [SerializeField] private Image chargeFill;
        [SerializeField] private Text chargeText;
        private int lastPercent = -1;
        private bool lastActive;
        // Optional. Sandbox keeps its original fuel-only UI without these links.
        [SerializeField] private RectTransform hpSection;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text hpText;
        private PlayerHitPoints health;
        private int lastHp = -1;

        // Prefab authoring sets only local UI references. Scene binding is runtime-only.
        public void ConfigureVisuals(RectTransform statusBar, Image fuelBar, Text fuelLabel,
            RectTransform healthSection, Image healthBar, Text healthLabel)
        {
            bar = statusBar;
            chargeFill = fuelBar;
            chargeText = fuelLabel;
            hpSection = healthSection;
            hpFill = healthBar;
            hpText = healthLabel;
        }

        public void Bind(Camera camera, Afterburner boost, PlayerHitPoints hitPoints)
        {
            // Older HUD cropped the camera to make room for a screen-wide bar.
            // Always restore full-screen presentation before binding this UI.
            if (viewCamera != null)
            {
                viewCamera.rect = new Rect(0f, 0f, 1f, 1f);
                viewCamera.ResetAspect();
            }
            viewCamera = camera;
            if (viewCamera != null)
            {
                viewCamera.rect = new Rect(0f, 0f, 1f, 1f);
                viewCamera.ResetAspect();
            }
            afterburner = boost;
            health = hitPoints;
            if (hpSection != null) hpSection.gameObject.SetActive(health != null);
            if (hpFill != null) hpFill.enabled = false; // Numeric HP, not a second bar.
            lastPercent = lastHp = -1;
        }

        private void OnEnable()
        {
            lastPercent = lastHp = -1;
        }

        private void LateUpdate()
        {
            if (afterburner == null) return;
            float charge = afterburner.Charge01;
            bool active = afterburner.IsActive;
            if (chargeFill != null)
            {
                chargeFill.rectTransform.anchorMax = new Vector2(charge, 1f);
                chargeFill.color = active ? new Color(1f, 0.67f, 0.25f) : new Color(0.6f, 0.81f, 0.73f);
            }
            int percent = Mathf.RoundToInt(charge * 100f);
            if (chargeText != null && (percent != lastPercent || active != lastActive))
                chargeText.text = "BOOST " + percent + "%";
            lastPercent = percent;
            lastActive = active;

            if (health != null && hpText != null)
            {
                float fraction = health.MaxHp > 0 ? (float)health.Hp / health.MaxHp : 0f;
                hpText.color = fraction <= .3f ? new Color(1f, .39f, .32f) :
                    new Color(.94f, .98f, .96f);
                if (lastHp != health.Hp)
                    hpText.text = "HP " + health.Hp + "/" + health.MaxHp;
                lastHp = health.Hp;
            }
        }

        // The prefab owns position and dimensions; the HUD never alters Camera.rect.
    }
}
