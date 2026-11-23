#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AirCombat.Editor
{
    // One-time authoring, not runtime UI generation. Produces a normal editable
    // prefab, then links one instance into GroundAttackLab. Safe to reopen later.
    public static class InstallCombatHud
    {
        private const string ScenePath = "Assets/_Project/Scenes/GroundAttackLab.unity";
        private const string PrefabFolder = "Assets/_Project/Prefabs/UI";
        private const string PrefabPath = PrefabFolder + "/CombatHUD.prefab";

        [MenuItem("Air Combat/HUD/Install Shared Combat HUD")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogError("Open GroundAttackLab in Edit Mode before installing Combat HUD.");
                return;
            }
            var session = Object.FindAnyObjectByType<GroundAttackLabSession>();
            if (session == null)
            {
                Debug.LogError("GroundAttackLabSession is missing.");
                return;
            }
            if (Object.FindAnyObjectByType<CombatHud>() != null)
            {
                Debug.LogWarning("Combat HUD is already present. Do not install twice.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = AuthorPrefab();
            if (prefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,
                SceneManager.GetActiveScene());
            var hud = instance.GetComponent<CombatHud>();
            session.ConfigureHud(hud);
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Combat HUD installed. Commit CombatHUD.prefab, its .meta and GroundAttackLab.unity.");
        }

        private static GameObject AuthorPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
            var root = new GameObject("Combat HUD", typeof(RectTransform), typeof(Canvas),
                typeof(FlightHud), typeof(CombatHud));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                // Constant pixel scale: both FlightHud and RadarHud use Screen.height.
                // No GraphicRaycaster: this HUD never consumes weapon mouse clicks.

                var bar = MakeRect("Status Bar", root.transform, new Vector2(0f, 0f),
                    new Vector2(1f, FlightHud.DefaultBarFraction));
                MakeImage(bar.gameObject, new Color(.09f, .13f, .13f, 1f));

                var fuelTrack = MakeRect("Fuel Track", bar, new Vector2(.025f, .19f),
                    new Vector2(.485f, .81f));
                MakeImage(fuelTrack.gameObject, new Color(.17f, .25f, .25f));
                var fuelFill = MakeRect("Fuel Fill", fuelTrack, Vector2.zero, Vector2.one);
                var fuelImage = MakeImage(fuelFill.gameObject, new Color(.6f, .81f, .73f));
                var fuelText = MakeRect("Fuel Label", fuelTrack, Vector2.zero, Vector2.one);
                var fuelLabel = MakeText(fuelText.gameObject, "FUEL 100%");

                var hpTrack = MakeRect("HP Track", bar, new Vector2(.515f, .19f),
                    new Vector2(.975f, .81f));
                MakeImage(hpTrack.gameObject, new Color(.24f, .18f, .19f));
                var hpFill = MakeRect("HP Fill", hpTrack, Vector2.zero, Vector2.one);
                var hpImage = MakeImage(hpFill.gameObject, new Color(.56f, .84f, .66f));
                var hpText = MakeRect("HP Label", hpTrack, Vector2.zero, Vector2.one);
                var hpLabel = MakeText(hpText.gameObject, "HP 20/20");

                var radarRect = MakeRect("Radar", root.transform, Vector2.one, Vector2.one);
                radarRect.pivot = Vector2.one;
                radarRect.sizeDelta = new Vector2(160f, 160f);
                radarRect.anchoredPosition = new Vector2(-18f, -16f);
                // Unlike ordinary rects, custom MaskableGraphic needs a
                // CanvasRenderer in the saved prefab to be drawn.
                radarRect.gameObject.AddComponent<CanvasRenderer>();
                var radar = radarRect.gameObject.AddComponent<RadarHud>();
                radar.raycastTarget = false;
                radar.maskable = false;

                var flightHud = root.GetComponent<FlightHud>();
                flightHud.ConfigureVisuals(bar, fuelImage, fuelLabel,
                    hpTrack, hpImage, hpLabel);
                root.GetComponent<CombatHud>().ConfigureParts(flightHud, radar);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform MakeRect(string name, Transform parent,
            Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image MakeImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text MakeText(GameObject go, string initial)
        {
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.text = initial;
            label.color = new Color(.94f, .98f, .96f);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 17;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 19;
            label.raycastTarget = false;
            return label;
        }
    }
}
#endif
