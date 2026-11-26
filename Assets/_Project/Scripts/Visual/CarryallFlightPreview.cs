using UnityEngine;

namespace AirCombat
{
    // Temporary, scene-owned flight visual. No mission target, damage or pickup logic.
    [DisallowMultipleComponent]
    public sealed class CarryallFlightPreview : MonoBehaviour
    {
        [SerializeField] private Transform carrier;
        [SerializeField] private GameObject payload;
        [SerializeField] private bool showPayload = true;
        [SerializeField, Min(5f)] private float radius = 32f;
        [SerializeField, Min(0f)] private float speed = 18f;
        [SerializeField, Min(0f)] private float rotorDegreesPerSecond = 720f;
        [SerializeField, Min(0f)] private float hoverAmplitude = 1.2f;
        [SerializeField, Range(0f, 20f)] private float bank = 5f;
        private CarryallRotorVisual rotors;
        private float angle, elapsed;
        private bool ready;

        public void Configure(Transform transport, GameObject cargo)
        { carrier = transport; payload = cargo; }

        private void Awake()
        {
            if (carrier == null || payload == null ||
                !payload.transform.IsChildOf(carrier))
            {
                Debug.LogError("CA01 flight preview: missing carrier or attached test cargo.", this);
                enabled = false; return;
            }
            rotors = carrier.GetComponent<CarryallRotorVisual>();
            if (rotors == null || !rotors.Initialize())
            {
                Debug.LogError("CA01 flight preview: rotor initialization failed.", this);
                enabled = false; return;
            }
            ready = true;
            RestartPreview();
        }

        public void RestartPreview()
        {
            if (!ready) return;
            angle = elapsed = 0f;
            rotors.ResetPhase();
            ApplyPose();
        }

        private void Update()
        {
            if (!ready || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            angle = Mathf.Repeat(angle + Mathf.Max(0f, speed) / Mathf.Max(5f, radius) * Mathf.Rad2Deg * dt, 360f);
            rotors.Advance(rotorDegreesPerSecond * dt);
            ApplyPose();
        }

        private void ApplyPose()
        {
            float r = Mathf.Max(5f, radius), a = angle * Mathf.Deg2Rad;
            carrier.localPosition = new Vector3(r * Mathf.Sin(a),
                hoverAmplitude * Mathf.Sin(elapsed * 1.6f), -r * Mathf.Cos(a));
            Quaternion heading = Quaternion.Euler(0f, 90f - angle, 0f);
            carrier.localRotation = heading * Quaternion.Euler(0f, 0f, -bank * Mathf.Clamp01(speed / 18f));
            if (payload.activeSelf != showPayload) payload.SetActive(showPayload);
        }
    }
}
