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
        [SerializeField] private Transform[] passiveTargets; // V0 generator compatibility only
        [SerializeField] private WeaponLabEnemy fighter;
        [SerializeField, Min(1000f)] private float mapHalfExtent = 6000f;

        private RenderPipelineAsset originalPipeline;
        private bool ownsOverride;
        private bool showUI = true;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private GUIStyle text;
        private WeaponLabCameraLead cameraLead;
        private TopDownCameraFollow cameraFollow;
        private WeaponLabFreeAim aim;
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

        // Upgrade existing range without recreating it or discarding imported audio.
        public void ConfigureFighter(WeaponLabEnemy opponent)
        {
            fighter = opponent;
            passiveTargets = System.Array.Empty<Transform>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (studyPipeline == null || player == null || flight == null || boost == null || cannon == null)
            { Debug.LogError("Weapon Lab missing flight/weapon references.", this); enabled = false; return; }
            // Runtime-only input remapping; other scenes keep their existing pitch hotkeys.
            cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
            var viewCamera = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : null;
            if (viewCamera == null)
            { Debug.LogError("Weapon Lab needs a TopDownCameraFollow camera.", this); enabled = false; return; }
            cameraFollow.UseWeaponLabPitchKeys(true);
            cameraLead = GetComponent<WeaponLabCameraLead>();
            if (cameraLead == null) cameraLead = gameObject.AddComponent<WeaponLabCameraLead>();
            cameraLead.Configure(viewCamera, player, boost);
            cameraLead.enabled = true;
            aim = player.GetComponent<WeaponLabFreeAim>();
            if (aim == null) aim = player.gameObject.AddComponent<WeaponLabFreeAim>();
            aim.Configure(viewCamera, player);
            aim.enabled = true;
            cannon.ConfigureAim(aim);
            spawnPosition = player.position;
            spawnRotation = player.rotation;
            originalPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = studyPipeline;
            ownsOverride = true;
            if (fighter == null) PlaceAllTargets();
        }

        private void OnDisable()
        {
            if (cameraFollow != null) cameraFollow.UseWeaponLabPitchKeys(false);
            if (cameraLead != null) cameraLead.enabled = false;
            if (aim != null) aim.enabled = false;
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
                if (k.cKey.wasPressedThisFrame && cameraLead != null) cameraLead.Toggle();
                if (k.rKey.wasPressedThisFrame) ResetToStart();
            }
            if (Mathf.Abs(player.position.x) > mapHalfExtent - 350f ||
                Mathf.Abs(player.position.z) > mapHalfExtent - 350f)
                ResetToStart();
        }

        private void LateUpdate()
        {
            if (fighter != null || player == null || passiveTargets == null) return;
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
            if (fighter != null) fighter.ResetEncounter();
            else PlaceAllTargets();
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
            GUI.Box(new Rect(12, 12, 500, 152), GUIContent.none);
            string enemyStatus = fighter != null
                ? $"FIGHTER {fighter.Hp}/{fighter.MaxHp} HP   Kills {fighter.Kills}" : "NO ENEMY";
            GUI.Label(new Rect(22, 17, 475, 22), "WEAPON LAB / " + enemyStatus +
                " / B EVADE: " + (fighter != null ? fighter.EvasionLabel : "OFF"), text);
            GUI.Label(new Rect(22, 40, 475, 22), "WASD fly   Space boost   LMB fire   V aim: " + cannon.AimModeLabel, text);
            GUI.Label(new Rect(22, 63, 450, 22), "1 BURST / 2 GAUSS   M auto A/B: " + cannon.FireModeLabel, text);
            GUI.Label(new Rect(22, 86, 450, 22), "G gun SFX: " + cannon.SoundModeLabel + "   T heat   R reset", text);
            string cameraMode = cameraLead != null && cameraLead.LookAheadEnabled ? "LOOK-AHEAD" : "CENTER";
            GUI.Label(new Rect(22, 109, 450, 22), "Shift+1/2/3 pitch   C: " + cameraMode + "   H hide HUD", text);
            string heat = cannon.HeatLimiterEnabled
                ? $"Heat {cannon.Heat01:P0}" + (cannon.IsOverheated ? " [COOLING]" : "") : "Heat OFF";
            GUI.Label(new Rect(22, 132, 450, 22),
                $"Speed {flight.CurrentSpeed:F0}  Boost {boost.Charge01:P0}  {heat}  Shots {cannon.FiredCount}  Hits {cannon.HitCount}", text);
        }
    }
}
