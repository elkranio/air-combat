using System;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Upgrade the existing V4 dogfight scene in place. Never regenerate the range,
// player, audio, aim settings, environment or previously committed art assets.
public static class ConfigureWeaponLabEvasion
{
    private const string ScenePath = "Assets/_Project/Scenes/WeaponLab.unity";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before enabling enemy evasion.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save all open scenes before upgrading Weapon Lab.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("WeaponLab.unity is missing.");

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "Player");
        var fighter = UnityEngine.Object.FindFirstObjectByType<WeaponLabEnemy>();
        if (player == null || fighter == null || fighter.GetComponent<EnemyFlightController>() == null)
            throw new InvalidOperationException("Run ConfigureWeaponLabDogfight first: no V4 fighter found.");
        var playerVfx = player.GetComponentInChildren<AfterburnerVfx>(true);
        var visual = fighter.transform.Find("JetVisual");
        if (playerVfx == null || visual == null)
            throw new InvalidOperationException("Player afterburner VFX or enemy JetVisual missing.");

        var engine = fighter.GetComponent<Afterburner>();
        if (engine == null) engine = fighter.gameObject.AddComponent<Afterburner>();
        var evasion = fighter.GetComponent<WeaponLabEvasion>();
        if (evasion == null) evasion = fighter.gameObject.AddComponent<WeaponLabEvasion>();
        evasion.Configure(player.transform);
        fighter.GetComponent<EnemyFlightController>().ConfigureLabEvasion(evasion, engine);
        fighter.ConfigureEvasion(evasion, engine);

        // Reuse our EXISTING afterburner flame authoring, but bind its resource to
        // the ENEMY engine. V4 intentionally stripped the player-bound VFX clone.
        var vfx = visual.GetComponent<AfterburnerVfx>();
        if (vfx == null) vfx = visual.gameObject.AddComponent<AfterburnerVfx>();
        EditorUtility.CopySerialized(playerVfx, vfx);
        var settings = new SerializedObject(vfx);
        settings.FindProperty("afterburner").objectReferenceValue = engine;
        settings.FindProperty("playIgnitionSound").boolValue = false; // visual indication, no offscreen noise
        settings.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(engine);
        EditorUtility.SetDirty(evasion);
        EditorUtility.SetDirty(fighter);
        EditorUtility.SetDirty(vfx);
        EditorUtility.SetDirty(fighter.GetComponent<EnemyFlightController>());
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save WeaponLab evasion upgrade.");
        Selection.activeGameObject = fighter.gameObject;
        return "Weapon Lab V5: B toggles reactive escape AI. Intermittent sector breaks, optional afterburner; 5 HP, player/audio/aim unchanged.";
    }
}
