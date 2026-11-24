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
        [Header("Authored visual mount")]
        [SerializeField] private Transform launchSocket;
        [SerializeField, Min(1f)] private float detectionRange = 210f;

        private PlayerHitPoints target;
        private AircraftRadar radar;
        private Material tracer, rocketHull;
        private readonly List<MantisVerticalRocket> verticalRockets = new List<MantisVerticalRocket>();
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
            if (player == null || sensor == null || launchSocket == null)
            { Debug.LogError("Mantis: missing player, radar or launch socket.", this); return; }
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null) { Debug.LogError("Mantis: tracer shader missing.", this); return; }
            if (tracer == null) tracer = new Material(shader) { name = "Mantis tracer / runtime" };
            if (rocketHull == null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) { Debug.LogError("Mantis: URP Lit missing.", this); return; }
                rocketHull = new Material(lit) { name = "Mantis / vertical missile hull" };
                rocketHull.SetColor("_BaseColor", new Color(.52f, .59f, .57f));
                rocketHull.SetFloat("_Smoothness", .08f);
            }
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
        }

        public void Unbind()
        {
            if (radar != null) radar.Unregister(this);
            ClearRockets();
            bound = false;
            target = null;
            radar = null;
        }

        private void OnEnable() { if (bound && radar != null) { radar.Register(this); ResetCombat(); } }
        private void OnDisable() { if (radar != null) radar.Unregister(this); ClearRockets(); }
        private void OnDestroy()
        {
            Unbind();
            if (tracer != null) Destroy(tracer);
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

        private void TickVertical(bool inRange)
        {
            if (launchSocket == null || !inRange || Time.time < nextBurst) return;
            FireVertical(launchSocket);
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
