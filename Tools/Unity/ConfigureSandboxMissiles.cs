using System;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Run once after the runtime scripts compile. Safe to repeat without resetting Inspector values.
public static class ConfigureSandboxMissiles
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
    private const string ModelPath = "Assets/ThirdParty/PolyPizza/Missile/Missile.obj";
    private const string PrefabPath = "Assets/_Project/Prefabs/Weapons/Missile.prefab";
    private const string MaterialFolder = "Assets/_Project/Materials";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Missing scene: " + ScenePath);
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene changes first.");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var effectShader = Shader.Find("AirCombat/SandboxEffect");
        if (model == null || lit == null || effectShader == null)
            throw new InvalidOperationException("Missing missile model or shaders. Check imports and compilation.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = RequireRoot(scene, "Player");
        var enemy = RequireRoot(scene, "Enemy");
        var cameraObject = RequireRoot(scene, "Main Camera");
        var camera = cameraObject.GetComponent<Camera>();
        if (camera == null || !camera.orthographic || enemy.GetComponent<EnemyFlightController>() == null)
            throw new InvalidOperationException("Configure the sandbox enemy and orthographic camera first.");

        EnsureFolder("Assets/_Project/Prefabs/Weapons");
        EnsureFolder(MaterialFolder);
        var effect = GetMaterial(MaterialFolder + "/MissileEffect.mat", effectShader);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            var body = GetMaterial(MaterialFolder + "/MissileBody.mat", lit);
            body.SetColor("_BaseColor", new Color32(232, 228, 202, 255));
            body.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(body);
            prefab = BuildPrefab(model, body, effect, scene);
        }
        var projectile = prefab.GetComponent<MissileProjectile>();
        if (projectile == null) throw new InvalidOperationException("Missile prefab has no MissileProjectile.");

        var targetLock = GetOrAdd<TargetLock>(cameraObject);
        SetReference(targetLock, "target", enemy.transform);
        GetOrAdd<TargetLockHud>(cameraObject);
        var launcher = GetOrAdd<MissileLauncher>(player);
        SetReference(launcher, "targetLock", targetLock);
        SetReference(launcher, "missilePrefab", projectile);
        SetReference(launcher, "effectMaterial", effect);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Sandbox.");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = cameraObject;
        return "Missile sandbox configured. Hover enemy until LOCKED, then click LMB.";
    }

    private static GameObject BuildPrefab(GameObject model, Material body, Material effect, Scene scene)
    {
        var root = new GameObject("Missile");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            instance.name = "MissileVisual";
            instance.transform.SetParent(root.transform, false);
            var meshes = instance.GetComponentsInChildren<MeshFilter>(true);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (meshes.Length == 0 || renderers.Length == 0)
                throw new InvalidOperationException("Missile model has no mesh.");

            // Determine the long axis and nose from imported geometry, not the OBJ's handedness.
            // The small-radius end is the nose; the large-radius end has stabilizer fins.
            var bounds = BoundsOf(renderers);
            int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 :
                bounds.size.y >= bounds.size.z ? 1 : 2;
            float extent = bounds.extents[axis];
            if (extent <= 0.001f) throw new InvalidOperationException("Invalid missile bounds.");
            float lowRadius = 0f, highRadius = 0f;
            foreach (var filter in meshes)
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                var p = filter.transform.TransformPoint(vertex) - bounds.center;
                float longitudinal = p[axis];
                p[axis] = 0f;
                if (longitudinal < -extent * 0.70f) lowRadius = Mathf.Max(lowRadius, p.sqrMagnitude);
                if (longitudinal > extent * 0.70f) highRadius = Mathf.Max(highRadius, p.sqrMagnitude);
            }
            var nose = Vector3.zero;
            nose[axis] = highRadius < lowRadius ? 1f : -1f;
            var align = axis == 1 ? Quaternion.FromToRotation(nose, Vector3.forward) :
                Quaternion.AngleAxis(Vector3.SignedAngle(nose, Vector3.forward, Vector3.up), Vector3.up);
            instance.transform.rotation = align * instance.transform.rotation;
            bounds = BoundsOf(renderers);
            instance.transform.localScale *= 4f / Mathf.Max(0.001f, bounds.size.z);
            bounds = BoundsOf(renderers);
            instance.transform.position += root.transform.position - bounds.center;
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = body;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            var exhaust = new GameObject("Exhaust");
            exhaust.transform.SetParent(root.transform, false);
            exhaust.transform.localPosition = new Vector3(0f, 0f, -1.9f);
            var trail = exhaust.AddComponent<TrailRenderer>();
            trail.sharedMaterial = effect;
            trail.time = 0.8f;
            trail.minVertexDistance = 0.5f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.7f, 1f, 1.7f);
            trail.numCornerVertices = 2;
            trail.numCapVertices = 3;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(new Color(1f, 0.77f, 0.38f), 0f),
                new GradientColorKey(new Color(0.94f, 0.94f, 0.88f), 0.16f),
                new GradientColorKey(new Color(0.75f, 0.77f, 0.72f), 1f)
            }, new[] {
                new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.18f), new GradientAlphaKey(0f, 1f)
            });
            trail.colorGradient = gradient;
            trail.emitting = false;
            var projectile = root.AddComponent<MissileProjectile>();
            SetReference(projectile, "trail", trail);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Failed to create missile prefab.");
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Bounds BoundsOf(MeshRenderer[] renderers)
    {
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static Material GetMaterial(string path, Shader shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject RequireRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root;
        throw new InvalidOperationException("Missing Sandbox root: " + name);
    }

    private static T GetOrAdd<T>(GameObject owner) where T : Component
    {
        var component = owner.GetComponent<T>();
        return component != null ? component : owner.AddComponent<T>();
    }

    private static void SetReference(Component component, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("Missing serialized field: " + field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var child = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent, parts[i]);
            parent = child;
        }
    }
}
