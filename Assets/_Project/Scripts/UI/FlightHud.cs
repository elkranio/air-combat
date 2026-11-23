using UnityEngine;
using UnityEngine.UI;

namespace AirCombat
{
    // Persistent aircraft instruments live outside the camera's combat viewport.
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
        [SerializeField, Range(0.025f, 0.1f)] private float barFraction = DefaultBarFraction;
        private int lastWidth = -1, lastHeight = -1;
        private float lastFraction = -1f;
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
            // Do not leave the old camera cropped if this HUD is rebound.
            if (viewCamera != null && viewCamera != camera)
            {
                viewCamera.rect = new Rect(0f, 0f, 1f, 1f);
                viewCamera.ResetAspect();
            }
            viewCamera = camera;
            afterburner = boost;
            health = hitPoints;
            if (hpSection != null) hpSection.gameObject.SetActive(health != null);
            lastWidth = lastHeight = -1;
            lastPercent = lastHp = -1;
            ApplyViewport();
        }

        private void OnEnable()
        {
            lastWidth = lastHeight = -1;
            lastPercent = lastHp = -1;
            ApplyViewport();
        }

        private void Update() { ApplyViewport(); }

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
                chargeText.text = hpSection != null ? "FUEL " + percent + "%" : percent + "%";
            lastPercent = percent;
            lastActive = active;

            if (health != null)
            {
                float fraction = health.MaxHp > 0 ? (float)health.Hp / health.MaxHp : 0f;
                if (hpFill != null)
                {
                    hpFill.rectTransform.anchorMax = new Vector2(fraction, 1f);
                    hpFill.color = fraction <= .3f ? new Color(1f, .39f, .32f) :
                        new Color(.56f, .84f, .66f);
                }
                if (hpText != null && lastHp != health.Hp)
                    hpText.text = "HP " + health.Hp + "/" + health.MaxHp;
                lastHp = health.Hp;
            }
        }

        private void ApplyViewport()
        {
            float fraction = Mathf.Clamp(barFraction, 0.025f, 0.1f);
            if (Screen.width == lastWidth && Screen.height == lastHeight && Mathf.Approximately(fraction, lastFraction)) return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastFraction = fraction;
            if (bar != null) bar.anchorMax = new Vector2(1f, fraction);
            if (viewCamera == null) return;
            viewCamera.rect = new Rect(0f, fraction, 1f, 1f - fraction);
            var pixels = viewCamera.pixelRect;
            if (pixels.height > 0f) viewCamera.aspect = pixels.width / pixels.height;
        }

        private void OnDisable()
        {
            if (viewCamera == null) return;
            viewCamera.rect = new Rect(0f, 0f, 1f, 1f);
            viewCamera.ResetAspect();
        }
    }
}
