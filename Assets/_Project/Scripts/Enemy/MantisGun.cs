using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Mobile AA cold-launch rocket. Patrol motion is owned by GroundPatrolMotor.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(180)]
    public sealed class MantisGun : MonoBehaviour, IRadarContact
    {
        [Header("Vertical rocket / cold launch")]
        [SerializeField, Min(.1f)] private float verticalEjectionSeconds = .48f;
        [SerializeField, Min(.1f)] private float verticalTurnSeconds = .32f;
        [SerializeField, Min(.05f)] private float verticalIgnitionSeconds = .22f;
        [SerializeField, Min(1f)] private float verticalCruiseSpeed = 240f;
        [SerializeField, Min(1f)] private float verticalRange = 310f;
        [SerializeField, Range(0f, 90f)] private float verticalCorrectionRate = 25f;
        [SerializeField, Range(0f, 90f)] private float verticalCorrectionBudget = 20f;
        [SerializeField, Min(.1f)] private float verticalPause = 2.5f;
        [SerializeField, Min(1)] private int verticalDamage = 3;
        [Header("Temporary visual mount")]
        [SerializeField] private Transform turretYaw, gunPitch;
        [SerializeField, Min(1f)] private float detectionRange = 210f;

        private PlayerHitPoints target;
        private AircraftRadar radar;
        private Material tracer, rocketHull;
        private readonly List<MantisVerticalRocket> verticalRockets = new List<MantisVerticalRocket>();
        private GameObject verticalPod;
        private Transform verticalSocket;
        private Renderer[] barrelRenderers;
        private bool[] barrelVisibility;
        private bool bound;
        private float nextBurst;

        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public RadarContactType RadarKind => RadarContactType.GroundDefense;
        public Vector3 RadarPosition => transform.position;
        public float RadarThreatRadius => detectionRange;
        public bool IsRadarVisible => bound && isActiveAndEnabled;

        public void Bind(PlayerHitPoints player, AircraftRadar sensor)
        {
            Unbind();
            if (player == null || sensor == null || turretYaw == null || gunPitch == null)
            { Debug.LogError("Mantis: missing player, radar or visual mounts.", this); return; }
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null) { Debug.LogError("Mantis: tracer shader missing.", this); return; }
            if (tracer == null) tracer = new Material(shader) { name = "Mantis tracer / runtime" };
            target = player;
            radar = sensor;
            bound = true;
            if (isActiveAndEnabled) radar.Register(this);
            ResetCombat();
        }

        public void ResetCombat()
        {
            ClearRockets();
            FiredCount = HitCount = 0;
            nextBurst = Time.time + .5f;
            if (turretYaw != null) turretYaw.localRotation = Quaternion.identity;
            if (gunPitch != null) gunPitch.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            SetVerticalVisual(true);
        }

        public void Unbind()
        {
            if (radar != null) radar.Unregister(this);
            ClearRockets();
            bound = false;
            SetVerticalVisual(false);
            target = null;
            radar = null;
        }

        private void OnEnable() { if (bound && radar != null) { radar.Register(this); ResetCombat(); } }
        private void OnDisable() { if (radar != null) radar.Unregister(this); ClearRockets(); SetVerticalVisual(false); }
        private void OnDestroy()
        {
            Unbind();
            if (tracer != null) Destroy(tracer);
            if (verticalPod != null) Destroy(verticalPod);
            if (rocketHull != null) Destroy(rocketHull);
        }

        private void ClearRockets()
        {
            foreach (var rocket in verticalRockets)
                if (rocket != null) { rocket.gameObject.SetActive(false); Destroy(rocket.gameObject); }
            verticalRockets.Clear();
        }

        private void LateUpdate()
        {
            if (!bound || target == null) return;
            verticalRockets.RemoveAll(r => r == null);
            if (!target.IsAlive) return;
            Vector3 horizontal = target.transform.position - transform.position;
            horizontal.y = 0f;
            bool inRange = horizontal.sqrMagnitude <= detectionRange * detectionRange;
            TickVertical(inRange);
        }

        // Keep the accepted placeholder geometry until the new launcher art pass.
        private void SetVerticalVisual(bool visible)
        {
            if (gunPitch == null || turretYaw == null) return;
            if (visible && verticalPod == null)
            {
                barrelRenderers = gunPitch.GetComponentsInChildren<Renderer>(true);
                barrelVisibility = new bool[barrelRenderers.Length];
                for (int i = 0; i < barrelRenderers.Length; i++) barrelVisibility[i] = barrelRenderers[i].enabled;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) { Debug.LogError("Mantis: URP Lit missing.", this); return; }
                rocketHull = new Material(shader) { name = "Mantis / vertical missile hull" };
                rocketHull.SetColor("_BaseColor", new Color(.52f,.59f,.57f));
                rocketHull.SetFloat("_Smoothness", .08f);
                verticalPod = new GameObject("AA-01e / temporary vertical pod");
                verticalPod.transform.SetParent(turretYaw, false);
                verticalSocket = BuildTube(0f);
            }
            if (verticalPod != null) verticalPod.SetActive(visible);
            if (verticalPod != null && barrelRenderers != null)
                for (int i = 0; i < barrelRenderers.Length; i++)
                    if (barrelRenderers[i] != null) barrelRenderers[i].enabled = !visible && barrelVisibility[i];
        }

        private Transform BuildTube(float x)
        {
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "Launch canister";
            tube.transform.SetParent(verticalPod.transform, false);
            tube.transform.localPosition = new Vector3(x, 1.5f, 1.0f);
            tube.transform.localScale = new Vector3(1.65f, 1.55f, 1.65f);
            var collider = tube.GetComponent<Collider>();
            collider.enabled = false; Destroy(collider);
            tube.GetComponent<MeshRenderer>().sharedMaterial = rocketHull;
            var socket = new GameObject("Vertical launch socket").transform;
            socket.SetParent(verticalPod.transform, false);
            socket.localPosition = new Vector3(x, 3.05f, 1.0f);
            return socket;
        }

        private void TickVertical(bool inRange)
        {
            if (verticalSocket == null || !inRange || Time.time < nextBurst) return;
            FireVertical(verticalSocket);
            nextBurst = Time.time + verticalPause;
        }

        private void FireVertical(Transform socket)
        {
            var go = new GameObject("Mantis / cold-launch unguided rocket");
            go.transform.position = socket.position;
            var rocket = go.AddComponent<MantisVerticalRocket>();
            rocket.Launch(transform, target, verticalEjectionSeconds, verticalTurnSeconds,
                verticalIgnitionSeconds, verticalCruiseSpeed, verticalRange,
                verticalDamage, verticalCorrectionRate, verticalCorrectionBudget, rocketHull, tracer, () => HitCount++);
            verticalRockets.Add(rocket);
            FiredCount++;
        }
    }
}
