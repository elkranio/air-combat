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
        private RectTransform secondaryRoot, statusBar;
        private Text secondaryLabel, ammoLabel;
        private Image secondaryFill;
        private bool dualLayout;
        private static readonly Color ReadyColor = new Color(.58f, .92f, .75f, 1f);
        private static readonly Color ReloadColor = new Color(1f, .73f, .30f, 1f);

        public void Bind(PlayerWeaponTrigger source)
        {
            loadout = source;
            lastName = null;
            gameObject.SetActive(source != null);
            if (source != null && source.UsesDualSlots) BuildDualLayout();
        }

        private void LateUpdate()
        {
            if (loadout != null && loadout.UsesDualSlots)
            {
                BuildDualLayout();
                if (dualLayout) DrawDual();
                return;
            }
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

        // Only production opts in. Reuse the authored font, track and fill style;
        // no prefab duplication or scene-specific references in the shared HUD.
        private void BuildDualLayout()
        {
            if (dualLayout || weaponLabel == null || cooldownFill == null) return;
            var primaryRoot = (RectTransform)transform;
            statusBar = primaryRoot.parent as RectTransform;
            var primaryTrack = cooldownFill.rectTransform.parent as RectTransform;
            if (statusBar == null || primaryTrack == null) return;

            var secondary = new GameObject("Secondary Weapon", typeof(RectTransform));
            secondaryRoot = (RectTransform)secondary.transform;
            secondaryRoot.SetParent(statusBar, false);
            secondaryRoot.gameObject.layer = gameObject.layer;
            secondaryLabel = Instantiate(weaponLabel, secondaryRoot);
            secondaryLabel.name = "Secondary Name";
            ammoLabel = Instantiate(weaponLabel, secondaryRoot);
            ammoLabel.name = "Ammo and Rearm";
            var secondaryTrack = Instantiate(primaryTrack, secondaryRoot);
            secondaryTrack.name = "Magazine Track";
            secondaryFill = secondaryTrack.GetChild(0).GetComponent<Image>();
            if (secondaryFill == null)
            {
                Destroy(secondary);
                return;
            }

            // Preserve the HP/BOOST instruments on the left and give the two
            // weapon slots identical widths, aligned resource bars in the same panel.
            for (int i = 0; i < statusBar.childCount; i++)
            {
                var child = statusBar.GetChild(i) as RectTransform;
                if (child == null || child == primaryRoot || child == secondaryRoot) continue;
                var min = child.anchorMin;
                var max = child.anchorMax;
                min.x *= .64f; max.x *= .64f;
                min.y = .29f + min.y * .42f;
                max.y = .29f + max.y * .42f;
                child.anchorMin = min;
                child.anchorMax = max;
            }
            Place(primaryRoot, .34f, .04f, .665f, .96f);
            Place(secondaryRoot, .67f, .04f, .995f, .96f);
            Place(weaponLabel.rectTransform, .02f, .35f, .98f, .99f);
            Place(primaryTrack, .02f, .07f, .98f, .26f);
            Place(secondaryLabel.rectTransform, .02f, .62f, .98f, .99f);
            Place(ammoLabel.rectTransform, .02f, .29f, .98f, .62f);
            Place(secondaryTrack, .02f, .07f, .98f, .26f);
            ammoLabel.resizeTextForBestFit = true;
            ammoLabel.resizeTextMinSize = 8;
            ammoLabel.resizeTextMaxSize = 14;
            dualLayout = true;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void DrawDual()
        {
            var canvasRect = statusBar.parent as RectTransform;
            float width = canvasRect != null ? Mathf.Min(760f, canvasRect.rect.width) : 760f;
            if (width <= 0f) width = 760f;
            statusBar.sizeDelta = new Vector2(width, 50f);
            float now = Time.time;
            var primary = loadout.PrimaryHudStatus;
            var secondary = loadout.SecondaryHudStatus;
            weaponLabel.text = "LMB  " + (primary != null ? primary.HudName : "EMPTY");
            float primaryReady = primary != null ? Mathf.Clamp01(primary.Readiness01(now)) : 0f;
            PaintFill(cooldownFill, primaryReady, primaryReady >= .999f ? ReadyColor : ReloadColor);
            secondaryLabel.text = "RMB  " + (secondary != null ? secondary.HudName : "EMPTY");
            if (secondary is IMagazineHudStatus magazine)
            {
                int count = magazine.AmmoRemaining;
                int capacity = magazine.AmmoCapacity;
                bool rearming = count == 0;
                float remaining = magazine.ReloadRemaining(now);
                ammoLabel.text = $"{magazine.AmmoName}  {count}/{capacity}" +
                    (rearming ? $"  {remaining:F0}s" : "");
                float fill = rearming ? magazine.Readiness01(now) : count / (float)Mathf.Max(1, capacity);
                PaintFill(secondaryFill, fill, rearming ? ReloadColor : ReadyColor);
            }
            else
            {
                ammoLabel.text = secondary == null ? "" : "READINESS";
                float ready = secondary != null ? secondary.Readiness01(now) : 0f;
                PaintFill(secondaryFill, ready, ready >= .999f ? ReadyColor : ReloadColor);
            }
        }

        private static void PaintFill(Image fill, float fraction, Color color)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            fill.color = color;
        }

        private void SetFill(float fraction)
        {
            var rect = cooldownFill.rectTransform;
            rect.anchorMax = new Vector2(fraction, 1f);
        }
    }
}
