using UnityEngine;

namespace AirCombat
{
    // Temporary selectable AAM module. No projectile, ammo or damage in 01a.
    [DisallowMultipleComponent]
    public sealed class MissileLockPreview : MonoBehaviour, ITriggeredWeapon, IWeaponHudStatus
    {
        [SerializeField, Min(.01f)] private float acquireSeconds = 1f;
        [SerializeField, Min(0f)] private float partialGraceSeconds = .5f;
        [SerializeField, Min(0f)] private float lockedGraceSeconds = 1f;
        private readonly WeaponLock seeker = new WeaponLock();
        private readonly TargetDesignation designation = new TargetDesignation();
        private TargetingCursor cursor;
        private Camera view;
        private bool selected, heldLast = true;
        private float releasedUntil;
        private GUIStyle label;

        public string HudName => "AAM / LOCK TEST";
        public float Readiness01(float now) => seeker.Progress;
        public PlayerHitPoints LockedTarget => selected && seeker.IsLocked ? seeker.Target : null;

        public void Bind(Camera camera, TargetingCursor pointer) { view = camera; cursor = pointer; }
        public void Register(PlayerHitPoints target) { designation.Register(target); }
        public void Unregister(PlayerHitPoints target)
        {
            if (seeker.Target == target) seeker.Reset();
            designation.Unregister(target);
        }

        // PlayerWeaponTrigger invokes this after camera and cursor movement.
        public void TickTrigger(bool held, float now)
        {
            selected = true;
            if (!Application.isFocused) { seeker.Reset(); heldLast = true; return; }
            var hover = cursor != null && cursor.HasPointer
                ? designation.Pick(view, cursor.Position, seeker.Target) : null;
            seeker.Step(hover, Time.deltaTime, acquireSeconds, partialGraceSeconds, lockedGraceSeconds);
            if (held && !heldLast && TryConsumeLock(out _)) releasedUntil = now + .45f;
            heldLast = held;
        }

        public bool TryConsumeLock(out PlayerHitPoints target)
        {
            target = LockedTarget;
            if (target == null) return false;
            seeker.Reset();
            return true;
        }

        public void CeaseFire(float now)
        {
            selected = false;
            heldLast = true;
            releasedUntil = 0f;
            seeker.Reset();
        }

        private void OnDisable() { CeaseFire(Time.time); }

        private void OnGUI()
        {
            if (!selected || view == null || Event.current.type != EventType.Repaint) return;
            Rect pixels = view.pixelRect;
            float s = Mathf.Clamp(pixels.height / 720f, .75f, 3f);
            if (label == null) label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            label.fontSize = Mathf.RoundToInt(13f * s);
            Color tint = seeker.IsLocked ? new Color(1f, .35f, .25f) : new Color(1f, .78f, .35f);
            Color old = GUI.color;
            int depth = GUI.depth;
            GUI.depth = -20;
            GUI.BeginGroup(new Rect(pixels.xMin, Screen.height - pixels.yMax, pixels.width, pixels.height));
            if (designation.TryBounds(view, seeker.Target, out Rect b))
            {
                float pad = 5f * s;
                var box = new Rect(b.xMin - pixels.xMin - pad, pixels.yMax - b.yMax - pad,
                    b.width + 2f * pad, b.height + 2f * pad);
                float arm = Mathf.Min(12f * s, Mathf.Min(box.width, box.height) * .4f);
                float w = 2f * s;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    float xx = x == 0 ? box.xMin : box.xMax - w;
                    float yy = y == 0 ? box.yMin : box.yMax - w;
                    Paint(new Rect(x == 0 ? xx : xx + w - arm, yy, arm, w), tint);
                    Paint(new Rect(xx, y == 0 ? yy : yy + w - arm, w, arm), tint);
                }
                Paint(new Rect(box.x, box.yMax + 4f * s, box.width, 3f * s), new Color(.08f, .09f, .10f, .8f));
                Paint(new Rect(box.x, box.yMax + 4f * s, box.width * seeker.Progress, 3f * s), tint);
                DrawLabel(new Rect(box.center.x - 80f * s, box.yMin - 22f * s, 160f * s, 20f * s),
                    seeker.IsLocked ? "LOCKED" : seeker.IsAiming ? "LOCKING" : "HOLD", tint);
            }
            // Offscreen grace must be observable before the audio pass.
            string status = Time.time < releasedUntil ? "TEST RELEASE" : seeker.IsLocked ? "LOCKED / LMB" :
                seeker.Target != null ? $"{(seeker.IsAiming ? "LOCKING" : "HOLD")} {seeker.Progress:P0}" : "ACQUIRE TARGET";
            DrawLabel(new Rect(pixels.width * .5f - 120f * s, pixels.height - 50f * s, 240f * s, 22f * s), status, tint);
            GUI.EndGroup();
            GUI.color = old;
            GUI.depth = depth;
        }

        private void DrawLabel(Rect rect, string text, Color color)
        {
            GUI.color = Color.white;
            label.normal.textColor = new Color(.05f, .07f, .08f, .95f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, label);
            label.normal.textColor = color;
            GUI.Label(rect, text, label);
        }

        private static void Paint(Rect rect, Color tint)
        {
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
