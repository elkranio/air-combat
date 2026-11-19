using System;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run once after compilation; repeatable without replacing existing aircraft tuning.
public static class ConfigureSandboxAfterburner
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
    private const string EffectPath = "Assets/_Project/Materials/MissileEffect.mat";

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new InvalidOperationException("Missing Sandbox scene.");
        var effect = AssetDatabase.LoadAssetAtPath<Material>(EffectPath);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (effect == null || font == null) throw new InvalidOperationException("Missing effect material or built-in UI font.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = RequireRoot(scene, "Player");
        var enemy = RequireRoot(scene, "Enemy");
        var cameraOwner = RequireRoot(scene, "Main Camera");
        var camera = cameraOwner.GetComponent<Camera>();
        var follow = cameraOwner.GetComponent<TopDownCameraFollow>();
        var targetLock = cameraOwner.GetComponent<TargetLock>();
        var playerController = player.GetComponent<FlightController>();
        var enemyController = enemy.GetComponent<EnemyFlightController>();
        var playerVisual = player.transform.Find("JetVisual");
        var enemyVisual = enemy.transform.Find("EnemyVisual");
        if (camera == null || !camera.orthographic || follow == null || targetLock == null || playerController == null ||
            enemyController == null || playerVisual == null || enemyVisual == null)
            throw new InvalidOperationException("Sandbox flight and missile setup must be completed first.");

        var playerBoost = GetOrAdd<Afterburner>(player);
        var enemyBoost = GetOrAdd<Afterburner>(enemy);
        SetReference(playerController, "afterburner", playerBoost);
        SetReference(enemyController, "afterburner", enemyBoost);
        var reaction = GetOrAdd<LockAfterburnerReaction>(enemy);
        SetReference(reaction, "afterburner", enemyBoost);
        SetReference(reaction, "incomingLock", targetLock);
        SetReference(enemyController, "lockReaction", reaction);
        SetReference(follow, "afterburner", playerBoost);

        ConfigureVfx(player.transform, playerVisual, playerBoost, effect, true,
            new[] { new Vector3(-0.71f, -0.44f, -5.5f), new Vector3(0.71f, -0.44f, -5.5f) }, 0.42f);
        ConfigureVfx(enemy.transform, enemyVisual, enemyBoost, effect, false,
            new[] { new Vector3(0f, -0.7f, -3.9f) }, 0.65f);
        var existing = FindRoot(scene, "Flight HUD");
        if (existing == null)
        {
            var root = new GameObject("Flight HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(FlightHud));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var rect = (RectTransform)root.transform;
            var bar = Panel(rect, "Status Bar", new Vector2(0f, 0f), new Vector2(1f, FlightHud.DefaultBarFraction), new Color(0.09f, 0.13f, 0.13f, 1f));
            Panel(bar.rectTransform, "Top Border", new Vector2(0f, 0.96f), Vector2.one, new Color(0.28f, 0.37f, 0.34f, 1f));
            var section = Child(bar.rectTransform, "Afterburner", new Vector2(0.009f, 0.05f), new Vector2(0.29f, 0.95f));
            Label(section, "Label", "AFTERBURNER [SPACE]", font, new Vector2(0f, 0f), new Vector2(0.46f, 1f), TextAnchor.MiddleLeft);
            var track = Panel(section, "Charge Track", new Vector2(0.47f, 0.32f), new Vector2(0.84f, 0.68f), new Color(0.21f, 0.28f, 0.27f, 1f));
            var fill = Panel(track.rectTransform, "Charge Fill", Vector2.zero, Vector2.one, new Color(0.6f, 0.81f, 0.73f, 1f));
            var percent = Label(section, "Charge Percent", "100%", font, new Vector2(0.85f, 0f), Vector2.one, TextAnchor.MiddleRight);
            var hud = root.GetComponent<FlightHud>();
            SetReference(hud, "bar", bar.rectTransform);
            SetReference(hud, "chargeFill", fill);
            SetReference(hud, "chargeText", percent);
            existing = root;
        }
        var flightHud = existing.GetComponent<FlightHud>();
        if (flightHud == null) throw new InvalidOperationException("Existing Flight HUD has no FlightHud component.");
        SetReference(flightHud, "viewCamera", camera);
        SetReference(flightHud, "afterburner", playerBoost);
        float fraction = new SerializedObject(flightHud).FindProperty("barFraction").floatValue;
        camera.rect = new Rect(0f, fraction, 1f, 1f - fraction);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save Sandbox.");
        Selection.activeGameObject = player;
        return "Afterburner configured: hold Space, release to recharge. Enemy can boost on lock. HUD reserves the bottom strip.";
    }

    private static void ConfigureVfx(Transform aircraft, Transform visual, Afterburner boost, Material material, bool sound, Vector3[] points, float radius)
    {
        var vfx = visual.GetComponent<AfterburnerVfx>();
        bool created = vfx == null;
        if (created) vfx = visual.gameObject.AddComponent<AfterburnerVfx>();
        SetReference(vfx, "afterburner", boost);
        SetReference(vfx, "effectMaterial", material);
        if (!created) return;
        var settings = new SerializedObject(vfx);
        var positions = settings.FindProperty("nozzlePositions");
        positions.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
            positions.GetArrayElementAtIndex(i).vector3Value = visual.InverseTransformPoint(aircraft.TransformPoint(points[i]));
        settings.FindProperty("flameRadius").floatValue = radius;
        settings.FindProperty("playIgnitionSound").boolValue = sound;
        settings.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(vfx)) PrefabUtility.RecordPrefabInstancePropertyModifications(vfx);
    }

    private static RectTransform Child(RectTransform parent, string name, Vector2 min, Vector2 max)
    {
        var child = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Panel(RectTransform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        var image = Child(parent, name, min, max).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text Label(RectTransform parent, string name, string content, Font font, Vector2 min, Vector2 max, TextAnchor alignment)
    {
        var text = Child(parent, name, min, max).gameObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = 18;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = new Color(0.87f, 0.91f, 0.88f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }
    private static GameObject RequireRoot(Scene scene, string name) => FindRoot(scene, name) ?? throw new InvalidOperationException("Missing Sandbox root: " + name);
    private static T GetOrAdd<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
    private static void SetReference(Component owner, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(owner);
        var property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("Missing serialized field: " + field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
