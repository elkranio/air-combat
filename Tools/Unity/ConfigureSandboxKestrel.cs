using System;
using System.Linq;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Replaces only the visible meshes. Keeps both existing bank pivots and their VFX.
public static class ConfigureSandboxKestrel
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
    private const string ModelPath = "Assets/_Project/Models/Aircraft/XF-01-Kestrel.fbx";
    private const string InstanceName = "KestrelModel";
    private const string MaterialFolder = "Assets/_Project/Models/Aircraft/EnemyMaterials";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes first.");
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Generate the Kestrel FBX first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Missing Sandbox scene.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var player = RequireRoot(scene, "Player");
        var enemy = RequireRoot(scene, "Enemy");
        var pv = RequireVisual(player, "JetVisual");
        var ev = RequireVisual(enemy, "EnemyVisual");
        // Check the imported names before touching either aircraft.
        foreach (var name in new[] { "Canopy", "Nozzle_L", "Nozzle_R" })
            FindPart(model.transform, name);
        var a = Install(scene, player.transform, pv, model, false);
        var b = Install(scene, enemy.transform, ev, model, true);

        // Hide standalone copies placed in Sandbox for the earlier art comparison.
        int hidden = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == player || root == enemy || !root.activeSelf) continue;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) != ModelPath) continue;
            root.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            hidden++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Sandbox.");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        return $"Kestrel ready. Player span/length max {a:F2}, enemy {b:F2}. Hidden previews: {hidden}. Twin nozzles configured.";
    }

    private static Transform RequireVisual(GameObject aircraft, string name)
    {
        var visual = aircraft.transform.Find(name);
        if (visual == null || visual.GetComponent<AfterburnerVfx>() == null)
            throw new InvalidOperationException("Missing " + name + " or its AfterburnerVfx.");
        return visual;
    }

    private static float Install(Scene scene, Transform aircraft, Transform visual, GameObject model, bool enemy)
    {
        var previous = visual.Find(InstanceName);
        var legacy = visual.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => previous == null || !r.transform.IsChildOf(previous)).ToArray();
        if (legacy.Length == 0) throw new InvalidOperationException("Missing original meshes on " + visual.name);
        var old = BoundsIn(legacy, aircraft);
        float size = Mathf.Max(old.size.x, old.size.z);
        if (size <= 0.01f) throw new InvalidOperationException("Invalid original aircraft bounds.");

        // Build the replacement before disabling any original mesh.
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        if (instance == null) throw new InvalidOperationException("Could not instantiate Kestrel.");
        try
        {
            instance.name = InstanceName;
            var t = instance.transform;
            t.SetParent(visual, false);
            t.localPosition = Vector3.zero;
            t.rotation = aircraft.rotation;
            t.localScale = Vector3.one;
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            var left = FindPart(t, "Nozzle_L");
            var right = FindPart(t, "Nozzle_R");
            var canopy = FindPart(t, "Canopy");
            // The FBX importer may flip the source forward axis. Derive heading
            // from the actual imported geometry, not a hardcoded Euler angle.
            var rear = (BoundsIn(left, aircraft).center + BoundsIn(right, aircraft).center) * 0.5f;
            var forward = BoundsIn(canopy, aircraft).center - rear;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) throw new InvalidOperationException("Invalid Kestrel nose direction.");
            t.rotation = aircraft.rotation * Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up));
            var bounds = BoundsIn(renderers, aircraft);
            float extent = Mathf.Max(bounds.size.x, bounds.size.z);
            if (extent <= 0.01f) throw new InvalidOperationException("Invalid Kestrel bounds.");
            t.localScale *= size / extent;
            bounds = BoundsIn(renderers, aircraft);
            t.position += aircraft.TransformVector(old.center - bounds.center);

            foreach (var r in renderers)
            {
                r.shadowCastingMode = ShadowCastingMode.Off; // existing soft blob shadow is authoritative
                if (enemy)
                    r.sharedMaterials = r.sharedMaterials.Select(EnemyMaterial).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            SetNozzles(visual, left, right);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach (var r in legacy)
            {
                r.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            return size;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(instance);
            throw;
        }
    }

    private static void SetNozzles(Transform visual, Transform left, Transform right)
    {
        var vfx = visual.GetComponent<AfterburnerVfx>();
        var settings = new SerializedObject(vfx);
        var positions = settings.FindProperty("nozzlePositions");
        var radius = settings.FindProperty("flameRadius");
        if (positions == null || radius == null) throw new InvalidOperationException("Missing VFX settings.");
        positions.arraySize = 2;
        float diameter = 0f;
        var nozzles = new[] { left, right };
        for (int i = 0; i < 2; i++)
        {
            var b = BoundsIn(nozzles[i], visual);
            positions.GetArrayElementAtIndex(i).vector3Value = new Vector3(b.center.x, b.center.y, b.min.z + 0.05f);
            diameter += Mathf.Min(b.size.x, b.size.y);
        }
        // The plume starts at 0.67 of its configured radius; match the inner rim.
        radius.floatValue = diameter * 0.5f * 0.48f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(vfx);
    }

    private static Material EnemyMaterial(Material source)
    {
        if (source == null) throw new InvalidOperationException("Kestrel has an empty material slot.");
        Color32 color;
        switch (source.name)
        {
            case "Kestrel_Hull": color = new Color32(157, 72, 62, 255); break;
            case "Kestrel_Upper": color = new Color32(105, 39, 37, 255); break;
            case "Kestrel_Panels": color = new Color32(67, 36, 35, 255); break;
            case "Kestrel_Amber": color = new Color32(241, 184, 111, 255); break;
            default: return source; // retain the glass, exhaust, and dark interior
        }
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/_Project/Models/Aircraft", "EnemyMaterials");
        string path = MaterialFolder + "/Enemy_" + source.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source) { name = "Enemy_" + source.name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static Transform FindPart(Transform root, string name)
    {
        var part = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        if (part == null) throw new InvalidOperationException("Missing FBX part: " + name);
        return part;
    }

    private static Bounds BoundsIn(Transform part, Transform space) => BoundsIn(part.GetComponentsInChildren<MeshRenderer>(true), space);

    // Mesh-local bounds work even on disabled legacy renderers and avoid inflated
    // world AABBs when the aircraft is rotated.
    private static Bounds BoundsIn(MeshRenderer[] renderers, Transform space)
    {
        bool found = false;
        Bounds result = default;
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var b = filter.sharedMesh.bounds;
            var toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                    (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                p = toSpace.MultiplyPoint3x4(p);
                if (!found) { result = new Bounds(p, Vector3.zero); found = true; }
                else result.Encapsulate(p);
            }
        }
        if (!found) throw new InvalidOperationException("No mesh bounds found.");
        return result;
    }

    private static GameObject RequireRoot(Scene scene, string name) =>
        scene.GetRootGameObjects().FirstOrDefault(o => o.name == name) ?? throw new InvalidOperationException("Missing " + name);
}
