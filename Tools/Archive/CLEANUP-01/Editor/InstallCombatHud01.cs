#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One-shot scene wiring. The prefab itself remains scene-neutral.
    public static class InstallCombatHud01
    {
        private const string HudPath = "Assets/_Project/Prefabs/UI/CombatHUD.prefab";
        private const string MainPath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string AirPath = "Assets/_Project/Scenes/WeaponLab.unity";
        private const string TigerTrial = "D1 / EF-01 Tiger - VISUAL TRIAL (toggle)";

        [MenuItem("Air Combat/HUD/HUD-01 - Install in Open Combat Scene")]
        private static void Install()
        {
            try { Debug.Log(InstallOpenScene()); }
            catch (Exception exception) { Debug.LogError("HUD-01: " + exception); }
        }

        private static string InstallOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing HUD-01.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || (scene.path != MainPath && scene.path != AirPath))
                throw new InvalidOperationException("Open ArcadeDesertE1 or WeaponLab first. GroundAttackLab already has CombatHUD.");
            if (scene.isDirty)
                throw new InvalidOperationException("Save or discard scene changes before installing HUD-01.");
            if (FindInScene<CombatHud>(scene) != null)
                return "CombatHUD is already installed; skipped to preserve scene overrides.";

            E1MapFlightSession main = null;
            WeaponLabSession lab = null;
            GameObject tiger = null;
            WeaponLabEnemy fighter = null;
            if (scene.path == MainPath)
            {
                main = FindInScene<E1MapFlightSession>(scene);
                tiger = scene.GetRootGameObjects().FirstOrDefault(x => x.name == TigerTrial);
                if (main == null || tiger == null)
                    throw new InvalidOperationException("Main session or static D1 Tiger trial is missing.");
            }
            else
            {
                lab = FindInScene<WeaponLabSession>(scene);
                fighter = lab != null ? lab.Fighter : null;
                if (fighter == null)
                    throw new InvalidOperationException("WeaponLab session/fighter reference is missing.");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (prefab == null || prefab.GetComponent<CombatHud>() == null)
                throw new InvalidOperationException("Reusable CombatHUD.prefab not found.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var hud = instance.GetComponent<CombatHud>();
            if (main != null)
            {
                var contact = tiger.GetComponent<RadarAirContact>();
                if (contact == null) contact = Undo.AddComponent<RadarAirContact>(tiger);
                main.ConfigureHud(hud, new[] { contact });
                EditorUtility.SetDirty(main);
            }
            else
            {
                if (fighter.GetComponent<RadarAirContact>() == null)
                    Undo.AddComponent<RadarAirContact>(fighter.gameObject);
                lab.ConfigureHud(hud);
                EditorUtility.SetDirty(lab);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scene.path))
                throw new InvalidOperationException("Couldn't save the HUD scene composition.");
            return "HUD-01 installed into " + scene.name + ". Check radar and HP/BOOST in Play Mode, then commit the scene + metas.";
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }
    }
}
#endif
