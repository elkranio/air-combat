using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirCombat
{
    // Isolated large-map test arena. No changes to Sandbox or the art-study scenes.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class WeaponLabSession : MonoBehaviour
    {
        [SerializeField] private UniversalRenderPipelineAsset studyPipeline;
        [SerializeField] private Transform player;
        [SerializeField] private FlightController flight;
        [SerializeField] private Afterburner boost;
        [SerializeField] private WeaponLabCannon cannon;
        [SerializeField] private Transform[] passiveTargets;
        [SerializeField, Min(1000f)] private float mapHalfExtent = 6000f;

        private RenderPipelineAsset originalPipeline;
        private bool ownsOverride;
        private bool showUI = true;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private GUIStyle text;
        private static readonly float[] SideLanes = { 0f, -16f, 19f, -30f, 29f, -8f, 9f };

        public void Configure(UniversalRenderPipelineAsset pipeline, Transform aircraft,
            FlightController controller, Afterburner afterburner,
            WeaponLabCannon weapon, Transform[] targets, float halfExtent)
        {
            studyPipeline = pipeline;
            player = aircraft;
            flight = controller;
            boost = afterburner;
            cannon = weapon;
            passiveTargets = targets;
            mapHalfExtent = halfExtent;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (studyPipeline == null || player == null || flight == null || boost == null || cannon == null)
            { Debug.LogError("Weapon Lab missing flight/weapon references.", this); enabled = false; return; }
            spawnPosition = player.position;
            spawnRotation = player.rotation;
            originalPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = studyPipeline;
            ownsOverride = true;
            PlaceAllTargets();
        }

        private void OnDisable()
        {
            if (ownsOverride && QualitySettings.renderPipeline == studyPipeline)
                QualitySettings.renderPipeline = originalPipeline;
            ownsOverride = false;
        }

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && Application.isFocused)
            {
                if (k.hKey.wasPressedThisFrame) showUI = !showUI;
                if (k.rKey.wasPressedThisFrame) ResetToStart();
            }
            if (Mathf.Abs(player.position.x) > mapHalfExtent - 350f ||
                Mathf.Abs(player.position.z) > mapHalfExtent - 350f)
                ResetToStart();
        }

        private void LateUpdate()
        {
            if (player == null || passiveTargets == null) return;
            for (int i = 0; i < passiveTargets.Length; i++)
            {
                var target = passiveTargets[i];
                if (target == null) continue;
                Vector3 displacement = target.position - player.position;
                if (Vector3.Dot(displacement, player.forward) < -35f || displacement.sqrMagnitude > 360f * 360f)
                    PlaceTarget(target, i);
            }
        }

        private void ResetToStart()
        {
            player.SetPositionAndRotation(spawnPosition, spawnRotation);
            PlaceAllTargets();
        }

        private void PlaceAllTargets()
        {
            if (passiveTargets == null) return;
            for (int i = 0; i < passiveTargets.Length; i++)
                if (passiveTargets[i] != null) PlaceTarget(passiveTargets[i], i);
        }

        private void PlaceTarget(Transform target, int index)
        {
            float ahead = 85f + 31f * index;
            float side = SideLanes[index % SideLanes.Length];
            Vector3 where = player.position + player.forward * ahead + player.right * side;
            where.y = player.position.y;
            target.SetPositionAndRotation(where, Quaternion.identity);
        }

        private void OnGUI()
        {
            if (!showUI || Event.current.type != EventType.Repaint) return;
            if (text == null)
                text = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 480, 129), GUIContent.none);
            GUI.Label(new Rect(22, 17, 450, 22), "WEAPON LAB / 12 km training range / no enemy AI", text);
            GUI.Label(new Rect(22, 40, 450, 22), "WASD fly   Space boost   HOLD LMB autocannon", text);
            GUI.Label(new Rect(22, 63, 450, 22), "T toggle overheating   R reset   H hide HUD", text);
            GUI.Label(new Rect(22, 86, 450, 22), "1/2/3 camera 90/75/65   Static targets recycle ahead", text);
            string heat = cannon.HeatLimiterEnabled
                ? $"Heat {cannon.Heat01:P0}" + (cannon.IsOverheated ? " [COOLING]" : "") : "Heat OFF";
            GUI.Label(new Rect(22, 109, 450, 22),
                $"Speed {flight.CurrentSpeed:F0}  Boost {boost.Charge01:P0}  {heat}  Shots {cannon.FiredCount}  Hits {cannon.HitCount}", text);
        }
    }
}
