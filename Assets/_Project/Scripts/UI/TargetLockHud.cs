using UnityEngine;

namespace AirCombat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TargetLock), typeof(Camera))]
    public sealed class TargetLockHud : MonoBehaviour
    {
        private TargetLock targetLock;
        private Camera viewCamera;
        private GUIStyle label;

        private void Awake()
        {
            targetLock = GetComponent<TargetLock>();
            viewCamera = GetComponent<Camera>();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !targetLock.IsVisible ||
                (!targetLock.IsAiming && targetLock.Progress <= 0f)) return;

            var scale = Mathf.Clamp(viewCamera.pixelHeight / 720f, 0.75f, 1.5f);
            var screen = targetLock.TargetScreen;
            var center = new Vector2(screen.x, Screen.height - screen.y);
            var radius = targetLock.RadiusPixels;
            var box = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
            var color = !targetLock.IsAiming ? new Color(1f, 0.64f, 0.25f) :
                targetLock.IsLocked ? new Color(0.76f, 1f, 0.44f) : new Color(1f, 0.91f, 0.63f);
            float arm = Mathf.Min(radius * 0.45f, 14f * scale);
            float thickness = 2f * scale;
            var old = GUI.color;
            foreach (var x in new[] { box.xMin, box.xMax - thickness })
            foreach (var y in new[] { box.yMin, box.yMax - thickness })
            {
                float horizontalX = x == box.xMin ? x : x + thickness - arm;
                float verticalY = y == box.yMin ? y : y + thickness - arm;
                Paint(new Rect(horizontalX - 1, y - 1, arm + 2, thickness + 2), new Color(0.12f, 0.16f, 0.13f, 0.9f));
                Paint(new Rect(x - 1, verticalY - 1, thickness + 2, arm + 2), new Color(0.12f, 0.16f, 0.13f, 0.9f));
                Paint(new Rect(horizontalX, y, arm, thickness), color);
                Paint(new Rect(x, verticalY, thickness, arm), color);
            }
            var bar = new Rect(box.xMin, box.yMax + 5f * scale, box.width, 5f * scale);
            Paint(bar, new Color(0.08f, 0.12f, 0.09f, 0.85f));
            Paint(new Rect(bar.x, bar.y, bar.width * targetLock.Progress, bar.height), color);
            GUI.color = old;
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            label.fontSize = Mathf.RoundToInt(13f * scale);
            label.normal.textColor = color;
            var text = targetLock.IsLocked ? "LOCKED" : !targetLock.IsAiming ? "HOLD" : "LOCKING";
            GUI.Label(new Rect(box.xMin - 12f, box.yMin - 22f * scale, box.width + 24f, 20f * scale), text, label);
        }

        private static void Paint(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
