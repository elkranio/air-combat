using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Play Mode only: compare actual URP shadows without saving scene, prefab
    // or material overrides. The shared lighting rig remains the authority.
    [InitializeOnLoad]
    internal static class Mission01AircraftShadowTrial
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Menu = "Air Combat/Missions/Aircraft Shadow Trial/";
        private static CombatLightingRig rig;
        private static Light sun;
        private static JetAssembly jet;
        private static MeshRenderer[] receivers;
        private static bool[] originalReception;
        private static float originalStrength, originalAmbient;

        static Mission01AircraftShadowTrial()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    if (rig != null && sun != null) Restore();
                    Clear();
                }
                else if (state == PlayModeStateChange.EnteredPlayMode ||
                         state == PlayModeStateChange.EnteredEditMode) Clear();
            };
        }

        [MenuItem(Menu + "A - Original", false, 1)]
        private static void Original() { Apply(0); }
        [MenuItem(Menu + "B - Stronger real shadows", false, 2)]
        private static void Stronger() { Apply(1); }
        [MenuItem(Menu + "C - Stronger and less ambient", false, 3)]
        private static void Contrast() { Apply(2); }
        [MenuItem(Menu + "D - Receiver off (compare with B)", false, 4)]
        private static void ReceiverOff() { Apply(3); }

        private static bool Capture()
        {
            var scene = SceneManager.GetActiveScene();
            if (!EditorApplication.isPlaying || scene.path != ScenePath)
            {
                Debug.LogWarning("Aircraft shadow trial: enter Play Mode in Mission01_FirstSortie first.");
                return false;
            }
            var rigs = UnityEngine.Object.FindObjectsByType<CombatLightingRig>(FindObjectsSortMode.None)
                .Where(x => x.gameObject.scene == scene && x.isActiveAndEnabled).ToArray();
            var jets = UnityEngine.Object.FindObjectsByType<JetAssembly>(FindObjectsSortMode.None)
                .Where(x => x.gameObject.scene == scene && x.GetComponent<PlayerHitPoints>() != null && x.IsConfigured).ToArray();
            if (rigs.Length != 1 || jets.Length != 1 || RenderSettings.sun != rigs[0].GetComponent<Light>())
            {
                Debug.LogWarning("Aircraft shadow trial: expected one active mission sun and one configured combat player.");
                return false;
            }
            if (rig == rigs[0] && jet == jets[0] && sun != null && receivers != null && receivers.All(x => x != null)) return true;
            Clear();
            var meshRenderers = jets[0].Chassis.GetComponentsInChildren<MeshRenderer>(true);
            if (meshRenderers.Length == 0)
            {
                Debug.LogWarning("Aircraft shadow trial: no chassis mesh renderers found.");
                return false;
            }
            var ambient = new SerializedObject(rigs[0]).FindProperty("ambientMultiplier");
            if (ambient == null) return false;
            originalAmbient = ambient.floatValue;
            rig = rigs[0]; jet = jets[0]; sun = rig.GetComponent<Light>();
            originalStrength = sun.shadowStrength;
            receivers = meshRenderers;
            originalReception = receivers.Select(x => x.receiveShadows).ToArray();
            Debug.Log($"Aircraft shadow trial baseline: {receivers.Length} chassis renderers, " +
                $"{originalReception.Count(x => x)} receiving, sun={sun.shadows}, strength={originalStrength:F2}, ambient multiplier={originalAmbient:F2}.", jet);
            foreach (var renderer in receivers)
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && (material.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF") ||
                        (material.HasProperty("_ReceiveShadows") && material.GetFloat("_ReceiveShadows") < .5f)))
                        Debug.LogWarning($"Aircraft shadow trial: material {material.name} disables shadow reception; renderer toggle alone cannot fix it.", renderer);
            return true;
        }

        private static void SetAmbient(float value)
        {
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("ambientMultiplier").floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo(); // rig applies on its next Update
        }

        private static void Restore()
        {
            sun.shadowStrength = originalStrength;
            SetAmbient(originalAmbient);
            if (receivers != null)
                for (int i = 0; i < receivers.Length; i++)
                    if (receivers[i] != null) receivers[i].receiveShadows = originalReception[i];
        }

        private static void Apply(int mode)
        {
            if (!Capture()) return;
            Restore();
            if (mode != 0)
            {
                sun.shadowStrength = Mathf.Max(originalStrength, .95f);
                if (mode == 2) SetAmbient(Mathf.Max(.1f, originalAmbient * .8f));
                foreach (var renderer in receivers) renderer.receiveShadows = mode != 3;
            }
            Debug.Log($"Aircraft shadow trial: {(char)('A' + mode)}, sun strength={sun.shadowStrength:F2}, " +
                $"ambient multiplier={(mode == 2 ? Mathf.Max(.1f, originalAmbient * .8f) : originalAmbient):F2}, " +
                $"receivers on={receivers.Count(x => x.receiveShadows)}/{receivers.Length}. Play Mode only.", jet);
        }

        private static void Clear()
        {
            rig = null; sun = null; jet = null; receivers = null; originalReception = null;
        }
    }
}
