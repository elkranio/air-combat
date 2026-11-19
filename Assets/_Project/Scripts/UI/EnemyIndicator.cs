using UnityEngine;

namespace AirCombat
{
    // Temporary sandbox HUD. A camera-relative bearing arrow, not a flight-path cue.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class EnemyIndicator : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform enemy;
        [SerializeField, Min(12f)] private float arrowSize = 36f;
        [SerializeField, Min(0f)] private float edgePadding = 12f;
        [SerializeField] private Color arrowColor = new Color(1f, 0.30f, 0.24f, 1f);

        private Camera viewCamera;
        private Texture2D arrow;
        private GUIStyle distanceStyle;

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            arrow = CreateArrow();
        }

        private void OnDestroy()
        {
            if (arrow != null)
                Destroy(arrow);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || player == null || enemy == null || arrow == null)
                return;

            var viewport = viewCamera.WorldToViewportPoint(enemy.position);
            if (viewport.z <= 0f || (viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f))
                return;

            var pixels = viewCamera.pixelRect;
            if (pixels.width <= 0f || pixels.height <= 0f)
                return;
            var center = new Vector2(pixels.center.x, Screen.height - pixels.center.y);
            var direction = new Vector2((viewport.x - 0.5f) * pixels.width,
                (0.5f - viewport.y) * pixels.height).normalized;
            if (direction.sqrMagnitude < 0.001f)
                return;

            var scale = Mathf.Clamp(pixels.height / 720f, 0.75f, 1.5f);
            var size = arrowSize * scale;
            var inset = (edgePadding + arrowSize * 0.7f) * scale;
            var halfWidth = Mathf.Max(0f, pixels.width * 0.5f - inset);
            var halfHeight = Mathf.Max(0f, pixels.height * 0.5f - inset);
            var xLimit = Mathf.Abs(direction.x) > 0.0001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
            var yLimit = Mathf.Abs(direction.y) > 0.0001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
            var position = center + direction * Mathf.Min(xLimit, yLimit);

            var angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, position);
            GUI.DrawTexture(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), arrow);
            GUI.matrix = oldMatrix;

            if (distanceStyle == null)
            {
                distanceStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                distanceStyle.normal.textColor = Color.white;
            }
            distanceStyle.fontSize = Mathf.RoundToInt(15f * scale);
            var offset = enemy.position - player.position;
            offset.y = 0f;
            var text = Mathf.RoundToInt(offset.magnitude).ToString();
            var labelCenter = position - direction * (size * 0.95f);
            var labelWidth = 56f * scale;
            var labelHeight = 22f * scale;
            var label = new Rect(labelCenter.x - labelWidth * 0.5f, labelCenter.y - labelHeight * 0.5f, labelWidth, labelHeight);
            label.x = Mathf.Clamp(label.x, pixels.xMin, Mathf.Max(pixels.xMin, pixels.xMax - labelWidth));
            var top = Screen.height - pixels.yMax;
            label.y = Mathf.Clamp(label.y, top, Mathf.Max(top, Screen.height - pixels.yMin - labelHeight));
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.Box(label, GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(label, text, distanceStyle);
        }

        private Texture2D CreateArrow()
        {
            const int resolution = 64;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "EnemyBearingArrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[resolution * resolution];
            var outline = new Color(0.22f, 0.08f, 0.07f, 1f);
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float dx = Mathf.Abs(x - 31.5f);
                float outerEdge = (59f - y) * 27f / 52f - dx;
                float notchEdge = y - (20f - dx * 13f / 27f);
                float distance = Mathf.Min(outerEdge, Mathf.Min(notchEdge, y - 7f));
                if (distance >= 0f)
                    pixels[y * resolution + x] = distance < 2f ? outline : arrowColor;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
