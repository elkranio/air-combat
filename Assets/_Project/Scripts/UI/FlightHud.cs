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

        private void OnEnable()
        {
            lastWidth = lastHeight = -1;
            lastPercent = -1;
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
                chargeText.text = percent + "%";
            lastPercent = percent;
            lastActive = active;
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
