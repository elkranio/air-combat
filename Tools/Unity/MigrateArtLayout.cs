using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 09a.1: move authored visuals with GUIDs intact; remove only retired aircraft.
// Existing Battlefield, procedural-landscape references and missile art remain
// until their replacements are ready. Never delete a referenced asset.
public static class MigrateArtLayout
{
    private const string Project = "Assets/_Project";
    private const string Art = Project + "/Art";
    private const string ScenePath = Project + "/Scenes/Sandbox.unity";
    private const string Jet = "Assets/ThirdParty/PolyPizza/Jet";
    private const string Fighter = "Assets/ThirdParty/PolyPizza/LowPolyFighter";
    private const string Sample = "Assets/Scenes/SampleScene.unity";
    private const string EnemyMaterial = Project + "/Materials/EnemyFighter.mat";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Missing Sandbox.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = RequireRoot(scene, "Player");
        var enemy = RequireRoot(scene, "Enemy");
        var pv = RequireVisual(player, "JetVisual");
        var ev = RequireVisual(enemy, "EnemyVisual");
        CheckVisualReference(player.GetComponent<FlightController>(), pv);
        CheckVisualReference(enemy.GetComponent<EnemyFlightController>(), ev);
        // Validate destinations before any move (also permits a repeat after success).
        var moves = new (string from, string to)[] {
            (Project + "/Models", Art),
            (Project + "/Prefabs/Weapons", Art + "/Weapons"),
            (Project + "/Materials/MissileBody.mat", Art + "/Weapons/MissileBody.mat"),
            (Project + "/Materials/MissileEffect.mat", Art + "/VFX/MissileEffect.mat"),
            (Project + "/Materials/SandboxGrid.mat", Art + "/Terrain/SandboxGrid.mat"),
            (Project + "/Materials/SandboxGround.mat", Art + "/Terrain/SandboxGround.mat"),
            (Project + "/Materials/SkyboxDay.mat", Art + "/Terrain/SkyboxDay.mat")
        };
        foreach (var move in moves)
        {
            bool source = Exists(move.from), dest = Exists(move.to);
            if (source == dest)
                throw new InvalidOperationException("Expected exactly one asset path: " + move.from + " -> " + move.to);
        }
        // Keep the nested Kestrel prefab and the existing bank pivot/VFX component.
        // Only the outer, retired model instance is unpacked.
        if (Under(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(pv.gameObject), Jet))
            PrefabUtility.UnpackPrefabInstance(pv.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        pv = RequireVisual(player, "JetVisual");
        // A model prefab can carry the old mesh directly on the bank pivot.
        RemoveRetiredMeshes(pv, Jet);
        var oldEnemy = ev.Find("FighterModel");
        if (oldEnemy != null)
        {
            if (!Under(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(oldEnemy.gameObject), Fighter))
                throw new InvalidOperationException("Unexpected legacy enemy model; refusing to delete it.");
            UnityEngine.Object.DestroyImmediate(oldEnemy.gameObject);
        }
        RemoveRetiredMeshes(ev, Fighter);
        // Unpack operates on scene objects, not their authored assets. Ensure the
        // controller still references the same pivot and the twin-nozzle VFX survived.
        pv = RequireVisual(player, "JetVisual");
        ev = RequireVisual(enemy, "EnemyVisual");
        CheckVisualReference(player.GetComponent<FlightController>(), pv);
        CheckVisualReference(enemy.GetComponent<EnemyFlightController>(), ev);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Sandbox.");

        foreach (var move in moves) Move(move.from, move.to);
        EnsureFolder(Art + "/Nature");
        EnsureFolder(Art + "/Terrain");
        EnsureFolder(Art + "/VFX");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // Replace the template build scene without changing any other entries.
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != Sample).ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        var retired = new[] { Jet, Fighter, EnemyMaterial, Sample }.Where(Exists).ToArray();
        AssertNoDependencies(retired);
        foreach (string path in retired)
            if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("Failed to delete " + path);
        DeleteEmpty(Project + "/Materials");
        DeleteEmpty(Project + "/Prefabs");
        DeleteEmpty("Assets/Scenes");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Selection.activeGameObject = player;
        return "09a.1 complete. Art moved without changing GUIDs; retired aircraft/template removed. " +
            "Sandbox and original battlefield preserved; Kenney and missile dependencies remain for their replacements.";
    }

    private static void RemoveRetiredMeshes(Transform visual, string retiredFolder)
    {
        var kestrel = visual.Find("KestrelModel");
        var removed = new List<Transform>();
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.transform == kestrel || filter.transform.IsChildOf(kestrel)) continue;
            if (!Under(AssetDatabase.GetAssetPath(filter.sharedMesh), retiredFolder)) continue;
            removed.Add(filter.transform);
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
            UnityEngine.Object.DestroyImmediate(filter);
        }
        foreach (var collider in visual.GetComponentsInChildren<MeshCollider>(true))
            if (Under(AssetDatabase.GetAssetPath(collider.sharedMesh), retiredFolder))
                UnityEngine.Object.DestroyImmediate(collider);
        // Drop empty old model leaves, never the pivot or the nested Kestrel.
        foreach (var t in removed.Distinct().Reverse())
        {
            if (t == null || t == visual || t == kestrel || t.IsChildOf(kestrel) || t.childCount != 0) continue;
            if (t.GetComponents<Component>().Length == 1) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    private static Transform RequireVisual(GameObject root, string name)
    {
        var visual = root.transform.Find(name);
        if (visual == null || visual.GetComponent<AfterburnerVfx>() == null || visual.Find("KestrelModel") == null ||
            !visual.Find("KestrelModel").GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled))
            throw new InvalidOperationException("Missing installed Kestrel or VFX on " + root.name);
        return visual;
    }

    private static void CheckVisualReference(Component controller, Transform expected)
    {
        if (controller == null) throw new InvalidOperationException("Missing flight controller.");
        var reference = new SerializedObject(controller).FindProperty("visual");
        if (reference == null || reference.objectReferenceValue != expected)
            throw new InvalidOperationException("Flight controller lost its bank pivot: " + controller.name);
    }

    private static GameObject RequireRoot(Scene scene, string name) =>
        scene.GetRootGameObjects().FirstOrDefault(o => o.name == name) ?? throw new InvalidOperationException("Missing " + name);

    private static bool Exists(string path) => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));
    private static bool Under(string path, string root) => path != null && (path == root || path.StartsWith(root + "/", StringComparison.Ordinal));

    private static void Move(string from, string to)
    {
        if (!Exists(from)) return;
        EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(from + ": " + error);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void DeleteEmpty(string path)
    {
        if (AssetDatabase.IsValidFolder(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            AssetDatabase.DeleteAsset(path);
    }

    private static void AssertNoDependencies(string[] retired)
    {
        if (retired.Length == 0) return;
        bool IsRetired(string path) => retired.Any(root => Under(path, root));
        var blockers = new List<string>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.IsValidFolder(path) || IsRetired(path)) continue;
            foreach (string dependency in AssetDatabase.GetDependencies(path, true).Where(IsRetired))
                blockers.Add(path + " -> " + dependency);
        }
        if (blockers.Count > 0)
            throw new InvalidOperationException("Retired assets NOT deleted; still referenced:\n" + string.Join("\n", blockers));
    }
}
