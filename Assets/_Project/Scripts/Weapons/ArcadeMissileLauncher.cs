using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Production secondary AAM module. Acquisition, magazine and fire-and-forget launch.
    [DisallowMultipleComponent]
    public sealed class ArcadeMissileLauncher : MonoBehaviour, ITriggeredWeapon, IMagazineHudStatus
    {
        [SerializeField, Min(.01f)] private float acquireSeconds = .15f;
        [SerializeField, Min(0f)] private float partialGraceSeconds = .5f;
        [SerializeField, Min(0f)] private float lockedGraceSeconds = 1f;
        [SerializeField, Min(.1f)] private float reloadSeconds = 20f;
        [SerializeField, Min(1)] private int hitDamage = 5;
        [SerializeField, Min(.1f)] private float missileSpeed = 190f;
        [SerializeField, Min(1f)] private float missileTurnRate = 220f;
        [SerializeField, Min(0f)] private float missileTurnBudget = 270f;
        [SerializeField, Min(.1f)] private float missileLifetime = 3f;
        [SerializeField, Min(.1f)] private float missileHitRadius = 5f;
        private const int Capacity = 2;
        private int rounds = Capacity;
        private float reloadUntil;
        private MissileProjectile prefab;
        private JetAssembly aircraft;
        private CombatHitFeedback feedback;
        private readonly List<MissileProjectile> live = new List<MissileProjectile>();
        private AudioSource beepSource, launchSource;
        private AudioClip beepClip, launchClip;
        private float nextBeep;
        private bool sounding;
        private readonly WeaponLock seeker = new WeaponLock();
        private readonly TargetDesignation designation = new TargetDesignation();
        private TargetingCursor cursor;
        private Camera view;
        private bool selected, heldLast = true;
        private float releasedUntil;
        private GUIStyle label;

        public string HudName => "AAM LAUNCHER";
        public string AmmoName => "GUIDED MISSILE";
        public int AmmoRemaining { get { UpdateMagazine(Time.time); return rounds; } }
        public int AmmoCapacity => Capacity;
        public float ReloadRemaining(float now)
        {
            UpdateMagazine(now);
            return rounds == 0 ? Mathf.Max(0f, reloadUntil - now) : 0f;
        }
        public float Readiness01(float now)
        {
            UpdateMagazine(now);
            return rounds > 0 ? 1f : Mathf.Clamp01(1f - (reloadUntil - now) / reloadSeconds);
        }

        private void UpdateMagazine(float now)
        {
            if (rounds == 0 && now >= reloadUntil)
            {
                rounds = Capacity;
                reloadUntil = 0f;
            }
        }
        public AirDamageableTarget LockedTarget => selected && rounds > 0 && seeker.IsLocked ? seeker.Target : null;

        public void Bind(Camera camera, TargetingCursor pointer, JetAssembly owner,
            MissileProjectile missilePrefab, CombatHitFeedback hitFeedback)
        {
            view = camera;
            cursor = pointer;
            aircraft = owner;
            prefab = missilePrefab;
            feedback = hitFeedback;
            beepSource = gameObject.AddComponent<AudioSource>();
            launchSource = gameObject.AddComponent<AudioSource>();
            beepSource.playOnAwake = launchSource.playOnAwake = false;
            beepSource.spatialBlend = launchSource.spatialBlend = 0f;
            beepClip = MakeSound(false);
            launchClip = MakeSound(true);
        }
        public void Register(AirDamageableTarget target) { designation.Register(target); }
        public void Unregister(AirDamageableTarget target)
        {
            if (seeker.Target == target) { seeker.Reset(); StopBeep(); }
            designation.Unregister(target);
        }

        // PlayerWeaponTrigger invokes this after camera and cursor movement.
        public void TickTrigger(bool held, float now)
        {
            selected = true;
            UpdateMagazine(now);
            if (!Application.isFocused) { seeker.Reset(); StopBeep(); heldLast = true; return; }
            if (rounds > 0)
            {
                var hover = cursor != null && cursor.HasPointer
                    ? designation.Pick(view, cursor.Position, seeker.Target) : null;
                seeker.Step(hover, Time.deltaTime, acquireSeconds, partialGraceSeconds, lockedGraceSeconds);
                if (held && !heldLast && LockedTarget != null) Fire(now, LockedTarget);
            }
            else seeker.Reset();
            TickBeep(now);
            heldLast = held;
        }

        private void Fire(float now, AirDamageableTarget target)
        {
            if (prefab == null || aircraft == null || !WeaponLock.Alive(target)) return;
            var root = aircraft.transform;
            var mount = aircraft.Chassis != null ? aircraft.Chassis.MissileMount : null;
            Vector3 position = mount != null ? mount.position : root.position + root.forward * 6f;
            position.y = root.position.y;
            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            var missile = Instantiate(prefab, position, Quaternion.LookRotation(forward, Vector3.up));
            // Every shot owns independent spacing accumulators; no attachment to the aircraft.
            float coreDebt = 1f, cloudDebt = 1f;
            missile.ConfigureArcade(target, missileSpeed, missileTurnRate, missileTurnBudget,
                missileLifetime, missileHitRadius, (from, to) =>
                {
                    if (feedback != null) feedback.EmitMissileExhaust(from, to, ref coreDebt, ref cloudDebt);
                });
            missile.Launch(target.transform, (point, hit) =>
            {
                if (WeaponLock.Alive(target)) target.ApplyDamage(point, hitDamage);
            });
            live.RemoveAll(item => item == null);
            live.Add(missile);
            rounds--;
            if (rounds == 0) reloadUntil = now + reloadSeconds;
            seeker.Reset();
            StopBeep();
            releasedUntil = now + .45f;
            if (launchSource != null) launchSource.PlayOneShot(launchClip, .5f);
        }

        private void TickBeep(float now)
        {
            if (!seeker.IsLocked || rounds == 0) { StopBeep(); return; }
            if (!sounding) { sounding = true; nextBeep = now; }
            if (now < nextBeep || beepSource == null) return;
            beepSource.PlayOneShot(beepClip, .22f);
            nextBeep = now + .36f;
        }

        private void StopBeep()
        {
            if (sounding && beepSource != null) beepSource.Stop();
            sounding = false;
        }

        private static AudioClip MakeSound(bool launch)
        {
            const int rate = 22050;
            int count = Mathf.RoundToInt(rate * (launch ? .32f : .085f));
            var data = new float[count];
            var rng = new System.Random(131);
            float low = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Clamp01(t / .008f) * Mathf.Clamp01((count - 1 - i) / (rate * .035f));
                low = Mathf.Lerp(low, (float)rng.NextDouble() * 2f - 1f, .16f);
                data[i] = envelope * (launch
                    ? (.8f * low + .25f * Mathf.Sin(2f * Mathf.PI * (95f * t + 220f * t * t)))
                    : .55f * Mathf.Sin(2f * Mathf.PI * 740f * t));
            }
            var clip = AudioClip.Create(launch ? "AAM launch" : "AAM lock beep", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void CancelMissiles()
        {
            foreach (var missile in live) if (missile != null) missile.Cancel();
            live.Clear();
        }

        public void ResetMagazine()
        {
            CeaseFire(Time.time);
            CancelMissiles();
            rounds = Capacity;
            reloadUntil = 0f;
            if (launchSource != null) launchSource.Stop();
        }

        public void CeaseFire(float now)
        {
            selected = false;
            heldLast = true;
            releasedUntil = 0f;
            seeker.Reset();
            StopBeep();
        }

        private void OnDisable() { CeaseFire(Time.time); }
        private void OnApplicationFocus(bool focused) { if (!focused) CeaseFire(Time.time); }
        private void OnDestroy()
        {
            CancelMissiles();
            if (beepClip != null) Destroy(beepClip);
            if (launchClip != null) Destroy(launchClip);
        }

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
            // Full lock remains audible through the offscreen grace period.
            string status = Time.time < releasedUntil ? "MISSILE AWAY" : seeker.IsLocked ? "LOCKED / RMB" :
                seeker.Target != null ? $"{(seeker.IsAiming ? "LOCKING" : "HOLD")} {seeker.Progress:P0}" : null;
            // Idle/rearm state lives in the persistent secondary instrument, not the sight.
            if (status != null)
                DrawLabel(new Rect(pixels.width * .5f - 120f * s, pixels.height - 76f * s, 240f * s, 22f * s), status, tint);
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
