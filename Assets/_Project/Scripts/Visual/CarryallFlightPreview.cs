using UnityEngine;

namespace AirCombat
{
    // Removable scene-owned pickup trial, using the existing CA02 installation and retry hook.
    [DisallowMultipleComponent]
    public sealed class CarryallFlightPreview : MonoBehaviour
    {
        [SerializeField] private Transform carrier;
        [SerializeField] private GameObject payload;
        [Header("CA03 / Pickup tuning (R applies changes)")]
        [SerializeField, Min(0f)] private float groundClearance = .15f;
        [SerializeField, Min(5f)] private float cruiseAltitude = 78f;
        [SerializeField, Min(15f)] private float approachDistance = 56f;
        [SerializeField] private float approachLateral = 14f;
        [SerializeField, Min(.05f)] private float approachSeconds = 3.5f;
        [SerializeField, Min(.05f)] private float alignSeconds = 1.2f;
        [SerializeField, Min(.05f)] private float descendSeconds = 5f;
        [SerializeField, Min(.05f)] private float clampSeconds = 1.1f;
        [SerializeField, Min(.05f)] private float liftSeconds = 6.5f;
        [SerializeField, Min(.05f)] private float departSeconds = 9f;
        [SerializeField, Min(20f)] private float departDistance = 150f;
        [SerializeField, Min(0f)] private float rotorDegreesPerSecond = 720f;
        [SerializeField] private string phaseStatus;
        private CarryallRotorVisual rotors;
        private CarryallPickupSequence sequence;
        private Vector3 ground;
        private Quaternion heading;
        private Transform mount;
        private bool ready;

        public void Configure(Transform transport, GameObject cargo)
        { carrier = transport; payload = cargo; }

        private void Awake()
        {
            mount = carrier != null ? carrier.Find("CargoMount") : null;
            if (carrier == null || payload == null || mount == null || mount.parent != carrier ||
                !payload.transform.IsChildOf(carrier) ||
                (carrier.lossyScale - Vector3.one).sqrMagnitude > .000001f ||
                (payload.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f ||
                (transform.lossyScale - Vector3.one).sqrMagnitude > .000001f ||
                (mount.localScale - Vector3.one).sqrMagnitude > .000001f ||
                Quaternion.Angle(mount.localRotation, Quaternion.identity) > .01f)
            {
                Debug.LogError("CA01 pickup preview: missing carrier/mount/cargo or unexpected scale/rotation.", this);
                enabled = false; return;
            }
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f || !TryGround(out ground))
            {
                Debug.LogError("CA01 pickup preview: no terrain beneath preview root or invalid heading.", this);
                enabled = false; return;
            }
            heading = Quaternion.LookRotation(forward, Vector3.up);
            rotors = carrier.GetComponent<CarryallRotorVisual>();
            if (rotors == null || !rotors.Initialize())
            {
                Debug.LogError("CA01 pickup preview: rotor initialization failed.", this);
                enabled = false; return;
            }
            ready = true;
            RestartPreview();
        }

        private bool TryGround(out Vector3 point)
        {
            point = transform.position;
            // Restrict the terrain lookup to this authored scene and the existing preview pad.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var terrain in root.GetComponentsInChildren<Terrain>(false))
            {
                if (terrain.terrainData == null) continue;
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x < origin.x || point.x > origin.x + size.x ||
                    point.z < origin.z || point.z > origin.z + size.z) continue;
                point.y = terrain.SampleHeight(point) + origin.y;
                return true;
            }
            return false;
        }

        public void RestartPreview()
        {
            if (!ready) return;
            sequence = new CarryallPickupSequence(carrier, payload.transform, mount, transform,
                ground + Vector3.up * Mathf.Max(0f, groundClearance), heading);
            sequence.Reset(new CarryallPickupSequence.Settings
            {
                cruiseY = cruiseAltitude, approachDistance = approachDistance, approachLateral = approachLateral,
                approachSeconds = approachSeconds, alignSeconds = alignSeconds, descendSeconds = descendSeconds,
                clampSeconds = clampSeconds, liftSeconds = liftSeconds, departSeconds = departSeconds,
                departDistance = departDistance
            });
            rotors.ResetPhase();
            UpdateStatus();
        }

        private void Update()
        {
            if (!ready || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            float dt = Time.deltaTime;
            rotors.Advance(rotorDegreesPerSecond * dt);
            sequence.Advance(dt);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            phaseStatus = sequence.CurrentPhase + (sequence.IsLoaded ? " / LOADED" : " / EMPTY");
        }
    }
}
