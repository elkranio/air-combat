using UnityEngine;

namespace AirCombat
{
    // Enemy-only experimental IR countermeasures. Random TIMING, not random missile misses.
    // A salvo is six real decoys. The missile's seeker decides from distance and heading.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)] // after enemy steering (10), before missiles (30)
    public sealed class WeaponLabFlares : MonoBehaviour
    {
        [SerializeField, Range(3, 8)] private int piecesPerSalvo = 6;
        [SerializeField, Min(.1f)] private float flareLifetime = 1.5f;
        [SerializeField, Min(.1f)] private float minimumSalvoGap = .45f;

        private WeaponLabEnemy owner;
        private MissileProjectile incoming;
        private Material material;
        private float elapsed;
        private float earlyDelay;
        private float nearDistance;
        private float lastSalvoAt;
        private bool earlyPending;
        private bool nearPending;

        public void Configure(WeaponLabEnemy fighter)
        {
            owner = fighter;
            if (material != null) return;
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null)
            {
                Debug.LogError("Weapon Lab flare shader missing; decoys disabled.", this);
                return;
            }
            material = new Material(shader) { name = "Weapon Lab IR flares (runtime)" };
        }

        public void OnIncomingMissile(MissileProjectile missile)
        {
            incoming = missile;
            elapsed = 0f;
            lastSalvoAt = -100f;
            earlyDelay = Random.Range(.12f, .62f);
            nearDistance = Random.Range(42f, 95f);
            float roll = Random.value;
            earlyPending = roll < .35f || roll >= .75f;
            nearPending = roll >= .35f;
            // 35% early, 40% late, 25% early + late. Not a perfect countermeasure AI.
        }

        private void Update()
        {
            if (incoming == null || owner == null || owner.IsDown || material == null)
            {
                if (owner != null && owner.IsDown) incoming = null;
                return;
            }
            elapsed += Time.deltaTime;
            if (earlyPending && elapsed >= earlyDelay)
            {
                earlyPending = false;
                EmitSalvo();
            }
            // In a two-salvo pattern the early attempt goes first; never release
            // both salvos in one frame merely because the missile starts nearby.
            if (nearPending && !earlyPending && elapsed - lastSalvoAt >= minimumSalvoGap)
            {
                Vector3 toMissile = incoming.transform.position - transform.position;
                toMissile.y = 0f;
                if (toMissile.sqrMagnitude <= nearDistance * nearDistance)
                {
                    nearPending = false;
                    EmitSalvo();
                }
            }
            if (!earlyPending && !nearPending) incoming = null;
        }

        private void EmitSalvo()
        {
            lastSalvoAt = elapsed;
            Vector3 rear = -Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            int count = Mathf.Clamp(piecesPerSalvo, 3, 8);
            for (int i = 0; i < count; i++)
            {
                // Two emitters at the rear; each flare fans outward independently.
                float side = i % 2 == 0 ? -1f : 1f;
                float spread = ((i / 2f) - (count - 1) / 4f) * 10f;
                Vector3 origin = transform.position + rear * 5f + right * (side * 2.5f);
                origin.y -= .4f;
                var go = new GameObject("IR Flare");
                go.transform.position = origin;
                var flare = go.AddComponent<WeaponLabFlare>();
                Vector3 velocity = rear * Random.Range(25f, 42f)
                    + right * (spread + side * Random.Range(4f, 13f));
                flare.Initialize(material, velocity, flareLifetime);
            }
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
