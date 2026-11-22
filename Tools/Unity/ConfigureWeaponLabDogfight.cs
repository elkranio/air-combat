using System;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Upgrades the EXISTING WeaponLab scene: keep its imported cannon audio, ground,
// camera and player settings. Do not regenerate the old dummy range from V20.
public static class ConfigureWeaponLabDogfight
{
    private const string ScenePath = "Assets/_Project/Scenes/WeaponLab.unity";
    private const string RootName = "Weapon Lab / range and passive targets";
    private const string EnemyName = "Dogfight Opponent";
    private const string Materials = "Assets/_Project/Art/Aircraft/EnemyMaterials/";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before configuring the dogfight.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scenes before configuring the dogfight.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Build WeaponLab.unity first.");

        // Validate all external assets before making scene changes.
        var hull = RequireMaterial("Enemy_Kestrel_Hull.mat");
        var upper = RequireMaterial("Enemy_Kestrel_Upper.mat");
        var panels = RequireMaterial("Enemy_Kestrel_Panels.mat");
        var amber = RequireMaterial("Enemy_Kestrel_Amber.mat");

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "Player");
        var root = scene.GetRootGameObjects().SingleOrDefault(go => go.name == RootName);
        if (player == null || root == null)
            throw new InvalidOperationException("Expected existing Weapon Lab player and range root.");
        var originalVisual = player.transform.Find("JetVisual");
        var session = root.GetComponent<WeaponLabSession>();
        if (originalVisual == null || session == null || player.GetComponent<WeaponLabCannon>() == null)
            throw new InvalidOperationException("Weapon Lab player visual, cannon or session missing.");

        // Remove every passive drone, including its obsolete colliders and target logic.
        var dummies = root.transform.Find("Floating static shooting dummies");
        if (dummies != null) Object.DestroyImmediate(dummies.gameObject);
        var previous = root.transform.Find(EnemyName);
        if (previous != null) Object.DestroyImmediate(previous.gameObject);

        var enemy = new GameObject(EnemyName);
        enemy.transform.SetParent(root.transform, false);
        var visual = Object.Instantiate(originalVisual.gameObject, enemy.transform, false);
        visual.name = "JetVisual";
        // JetVisual has a player-bound AfterburnerVfx: do not clone that dependency.
        foreach (var vfx in visual.GetComponentsInChildren<AfterburnerVfx>(true))
            Object.DestroyImmediate(vfx);
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Cloned aircraft visual contains no renderers.");
        foreach (var renderer in renderers)
        {
            var originals = renderer.sharedMaterials;
            for (int j = 0; j < originals.Length; j++)
            {
                var material = originals[j];
                if (material == null) continue;
                string label = material.name;
                if (label.IndexOf("Hull", StringComparison.OrdinalIgnoreCase) >= 0) originals[j] = hull;
                else if (label.IndexOf("Upper", StringComparison.OrdinalIgnoreCase) >= 0) originals[j] = upper;
                else if (label.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0) originals[j] = panels;
                else if (label.IndexOf("Amber", StringComparison.OrdinalIgnoreCase) >= 0) originals[j] = amber;
                // Unknown slots retain their original shared material (no edits to player assets).
            }
            renderer.sharedMaterials = originals;
        }

        var hitbox = enemy.AddComponent<SphereCollider>();
        hitbox.radius = 6f;
        var flight = enemy.AddComponent<EnemyFlightController>();
        flight.Configure(player.transform, visual.transform);
        var sparring = enemy.AddComponent<WeaponLabEnemy>();
        sparring.Configure(player.transform, flight, hitbox, renderers);
        session.ConfigureFighter(sparring);
        EditorUtility.SetDirty(enemy);
        EditorUtility.SetDirty(flight);
        EditorUtility.SetDirty(sparring);
        EditorUtility.SetDirty(session);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Failed to save updated WeaponLab scene.");
        Selection.activeGameObject = enemy;
        return "Weapon Lab dogfight: 7 passive dummies removed, one unarmed Kestrel with 5 HP " +
            "using Sandbox EnemyFlightController Cruise/CatchUp. Audio and player configuration retained.";
    }

    private static Material RequireMaterial(string file)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + file);
        if (material == null) throw new InvalidOperationException("Missing enemy aircraft material: " + file);
        return material;
    }
}
