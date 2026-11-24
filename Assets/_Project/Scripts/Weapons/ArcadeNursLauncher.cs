using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Production-only secondary module. One launch state supplies both actual
    // randomized shots and the deterministic next-shot ground footprint.
    [DisallowMultipleComponent]
    public sealed class ArcadeNursLauncher : MonoBehaviour, ITriggeredWeapon, IMagazineHudStatus
    {
        [Header("Salvo")]
        [SerializeField, Range(1, 12)] private int rocketCount = 6;
        [SerializeField, Min(.02f)] private float releaseInterval = .08f;
        [SerializeField, Min(.1f)] private float cooldown = 5f;
        [SerializeField, Min(1f)] private float muzzleSpeed = 260f;
        [SerializeField, Range(5f, 85f)] private float downwardAngle = 55f;
        [SerializeField, Min(.1f)] private float blastRadius = 8f;
        [SerializeField] private Vector3 leftPylon = new Vector3(-3.2f, -1.2f, 1.9f);
        [SerializeField] private Vector3 rightPylon = new Vector3(3.2f, -1.2f, 1.9f);
        [Header("Dispersion / degrees and multipliers")]
        [SerializeField, Range(.1f, 15f)] private float yawSpread = 5f;
        [SerializeField, Range(.1f, 15f)] private float pitchSpread = 5f;
        [SerializeField, Min(1f)] private float boostSpread = 2.5f;
        [SerializeField, Range(.1f, 1f)] private float tightSpread = .5f;
        [SerializeField, Min(1f)] private float turnSpread = 1.5f;
        [SerializeField, Min(1f)] private float fullTurnRate = 125f;
        [SerializeField, Min(0f)] private float spreadPerShot = .1f;
        [SerializeField, Min(.1f)] private float settleSeconds = .35f;

        private FlightController flight;
        private Afterburner boost;
        private PlayerWeaponTrigger loadout;
        private Transform owner;
        private Material shell, trail, flash, scorch;
        private AudioSource audioSource;
        private AudioClip launchSound, impactSound;
        private int pending, firedInSalvo, fired, impacts;
        private float nextShot, readyAt, turn01, residual, lastYaw;
        private bool heldLast = true, configured, sampledYaw;
        private readonly List<NursProjectile> live = new List<NursProjectile>();
        private readonly List<GameObject> effects = new List<GameObject>();

        public string HudName => "NURS RACK";
        public string AmmoName => "UNGUIDED ROCKETS";
        public int AmmoCapacity => Mathf.Max(1, rocketCount);
        public int AmmoRemaining => pending > 0 ? pending : Time.time >= readyAt ? AmmoCapacity : 0;
        public float ReloadRemaining(float now) => Mathf.Max(0f, readyAt - now);
        public float Readiness01(float now) => pending > 0 ? pending / (float)AmmoCapacity :
            Mathf.Clamp01(1f - ReloadRemaining(now) / Mathf.Max(.1f, cooldown));
        public string DebugStatus => $"NURS {impacts}/{fired} impacts/fired | spread x{Spread():F2}";
        public bool MarkerVisible => configured && isActiveAndEnabled && loadout != null &&
            loadout.isActiveAndEnabled && (ReferenceEquals(loadout.SecondaryHudStatus, this) ||
            ReferenceEquals(loadout.PrimaryHudStatus, this));

        public void Bind(FlightController controller, Afterburner fuel, PlayerWeaponTrigger trigger, Camera view)
        {
            flight = controller;
            boost = fuel;
            loadout = trigger;
            if (flight == null || loadout == null) { enabled = false; return; }
            owner = flight.transform;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var effect = Shader.Find("AirCombat/SandboxEffect");
            if (lit == null || effect == null)
            {
                Debug.LogError("NURS-01: missing ordnance shaders.", this);
                enabled = false;
                return;
            }
            shell = MakeMaterial(lit, "NURS shell", new Color(.26f, .32f, .29f));
            trail = new Material(effect) { name = "NURS streak" };
            flash = MakeMaterial(lit, "NURS impact", new Color(1f, .57f, .17f));
            scorch = MakeMaterial(lit, "NURS scorch", new Color(.13f, .11f, .09f));
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            launchSound = MakeSound(false);
            impactSound = MakeSound(true);
            configured = true;
            gameObject.AddComponent<NursImpactMarker>().Bind(this, view);
        }

        public void TickTrigger(bool held, float now)
        {
            if (!configured) return;
            live.RemoveAll(p => p == null);
            effects.RemoveAll(e => e == null);
            float yaw = owner.eulerAngles.y;
            turn01 = sampledYaw && Time.deltaTime > .0001f ? Mathf.Clamp01(
                Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / (Time.deltaTime * Mathf.Max(1f, fullTurnRate))) : 0f;
            sampledYaw = true;
            lastYaw = yaw;
            if (!Application.isFocused) { CeaseFire(now); return; }
            if (pending == 0)
            {
                residual = Mathf.MoveTowards(residual, 0f, Time.deltaTime *
                    Mathf.Max(.01f, spreadPerShot * (AmmoCapacity - 1)) / Mathf.Max(.1f, settleSeconds));
                if (held && !heldLast && now >= readyAt)
                {
                    pending = AmmoCapacity;
                    firedInSalvo = 0;
                    residual = 0f;
                    nextShot = now;
                }
            }
            heldLast = held;
            while (pending > 0 && now >= nextShot)
            {
                Fire(firedInSalvo);
                residual = firedInSalvo * spreadPerShot;
                firedInSalvo++;
                pending--;
                nextShot += Mathf.Max(.02f, releaseInterval);
                if (pending == 0) readyAt = now + Mathf.Max(.1f, cooldown);
            }
        }

        private float Spread()
        {
            float mode = boost != null && boost.IsActive ? boostSpread :
                boost != null && boost.IsManeuvering ? tightSpread : 1f;
            float recoil = pending > 0 ? firedInSalvo * spreadPerShot : residual;
            return mode * Mathf.Lerp(1f, turnSpread, turn01) * (1f + recoil);
        }

        private Vector3 Origin(int index) => owner.TransformPoint((index & 1) == 0 ? leftPylon : rightPylon);
        private Vector3 Velocity(float yaw, float pitch) => NursBallistics.LaunchVelocity(owner.forward,
            owner.right, flight.CurrentSpeed, muzzleSpeed, -downwardAngle, yaw, pitch);

        private void Fire(int index)
        {
            float spread = Spread();
            Vector3 velocity = Velocity(Random.Range(-yawSpread, yawSpread) * spread,
                Random.Range(-pitchSpread, pitchSpread) * spread);
            var root = new GameObject("Arcade NURS");
            root.transform.SetPositionAndRotation(Origin(index), Quaternion.LookRotation(velocity.normalized, Vector3.up));
            var rocket = root.AddComponent<NursProjectile>();
            rocket.Launch(velocity, owner, shell, trail, flash, scorch, OnImpact, blastRadius, false, e => effects.Add(e));
            live.Add(rocket);
            fired++;
            audioSource.PlayOneShot(launchSound, .48f);
        }

        private void OnImpact(int damagedParts)
        {
            impacts++;
            if (audioSource != null) audioSource.PlayOneShot(impactSound, .4f);
        }

        // Ground footprint only. Buildings/vehicles can intercept the real
        // rocket earlier. No target snapping or projectile correction.
        public bool TryGetFootprint(TerrainCollider ground, out Vector3 centre,
            out Vector3 forward, out Vector2 radii)
        {
            centre = default; forward = Vector3.forward; radii = default;
            if (!MarkerVisible || ground == null) return false;
            forward = Vector3.ProjectOnPlane(owner.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 origin = Origin(pending > 0 ? firedInSalvo : 0);
            float spread = Spread();
            if (!Predict(ground, origin, Velocity(0f, 0f), out Vector3 nominal)) return false;
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
            // Include edge midpoints as well as corners of the actual launch
            // error box. A sqrt(2) expansion encloses this sampled rectangle.
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                if (!Predict(ground, origin, Velocity(x * yawSpread * spread, y * pitchSpread * spread), out Vector3 hit))
                    return false;
                Vector3 delta = hit - nominal;
                float u = Vector3.Dot(delta, right), v = Vector3.Dot(delta, forward);
                minX = Mathf.Min(minX, u); maxX = Mathf.Max(maxX, u);
                minZ = Mathf.Min(minZ, v); maxZ = Mathf.Max(maxZ, v);
            }
            centre = nominal + right * ((minX + maxX) * .5f) + forward * ((minZ + maxZ) * .5f);
            radii = new Vector2(Mathf.Max(.6f, (maxX - minX) * .707107f),
                Mathf.Max(.8f, (maxZ - minZ) * .707107f));
            return true;
        }

        private static bool Predict(TerrainCollider ground, Vector3 point, Vector3 velocity, out Vector3 hit)
        {
            for (float t = 0f; t < 7f; t += NursBallistics.IntegrationStep)
            {
                Vector3 next = NursBallistics.Integrate(ref velocity, point, NursBallistics.IntegrationStep);
                Vector3 delta = next - point;
                if (delta.sqrMagnitude > .000001f && ground.Raycast(new Ray(point, delta.normalized), out RaycastHit result, delta.magnitude))
                { hit = result.point; return true; }
                if (next.y <= 0f) break;
                point = next;
            }
            hit = default;
            return false;
        }

        public void CeaseFire(float now)
        {
            if (pending > 0) readyAt = now + Mathf.Max(.1f, cooldown);
            pending = 0;
            heldLast = true;
        }

        public void ResetMagazine()
        {
            CeaseFire(Time.time);
            foreach (var rocket in live) if (rocket != null) rocket.Cancel();
            foreach (var effect in effects) if (effect != null) { effect.SetActive(false); Destroy(effect); }
            live.Clear(); effects.Clear();
            readyAt = residual = turn01 = 0f;
            fired = impacts = firedInSalvo = 0;
            sampledYaw = false;
            if (audioSource != null) audioSource.Stop();
        }

        private static Material MakeMaterial(Shader shader, string label, Color tint)
        {
            var material = new Material(shader) { name = label };
            material.SetColor("_BaseColor", tint);
            return material;
        }

        private static AudioClip MakeSound(bool impact)
        {
            const int rate = 22050;
            float duration = impact ? .22f : .13f;
            var data = new float[Mathf.RoundToInt(rate * duration)];
            var rng = new System.Random(impact ? 43 : 29);
            float low = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                low = Mathf.Lerp(low, (float)rng.NextDouble() * 2f - 1f, impact ? .11f : .24f);
                float envelope = Mathf.Min(1f, t / .005f) * Mathf.Pow(1f - t / duration, 2f);
                data[i] = envelope * (.75f * low + .3f * Mathf.Sin(2f * Mathf.PI * (impact ? 70f : 130f) * t));
            }
            var clip = AudioClip.Create(impact ? "NURS impact" : "NURS launch", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDisable() { CeaseFire(Time.time); }
        private void OnDestroy()
        {
            ResetMagazine();
            if (shell != null) Destroy(shell);
            if (trail != null) Destroy(trail);
            if (flash != null) Destroy(flash);
            if (scorch != null) Destroy(scorch);
            if (launchSound != null) Destroy(launchSound);
            if (impactSound != null) Destroy(impactSound);
        }
    }
}
