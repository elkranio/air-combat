using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirCombat
{
    // ArtLab-only scene glue. Flight and camera controllers remain authoritative.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class ArtLabLandscapeFlight : MonoBehaviour
    {
        [SerializeField] private UniversalRenderPipelineAsset studyPipeline;
        [SerializeField] private Transform player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private FlightController flight;
        [SerializeField] private Afterburner boost;
        [SerializeField] private float halfMap = 600f;
        private RenderPipelineAsset previousPipeline;
        private bool ownsOverride;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private bool showLegend = true;
        private GUIStyle label;

        public void Configure(UniversalRenderPipelineAsset pipeline, Transform aircraft, Camera camera,
            FlightController controller, Afterburner afterburner, float halfExtent)
        {
            studyPipeline = pipeline; player = aircraft; viewCamera = camera;
            flight = controller; boost = afterburner; halfMap = halfExtent;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (studyPipeline == null || player == null || viewCamera == null)
            { Debug.LogError("Incomplete V10 study configuration.", this); enabled = false; return; }
            startPosition = player.position;
            startRotation = player.rotation;
            previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = studyPipeline;
            ownsOverride = true;
        }

        private void OnDisable()
        {
            if (!ownsOverride) return;
            if (QualitySettings.renderPipeline == studyPipeline) QualitySettings.renderPipeline = previousPipeline;
            ownsOverride = false;
        }

        private void Update()
        {
            var keys = Keyboard.current;
            bool reset = false;
            if (keys != null && Application.isFocused)
            {
                if (keys.hKey.wasPressedThisFrame) showLegend = !showLegend;
                reset = keys.rKey.wasPressedThisFrame;
            }
            // Leave enough terrain outside the reachable area for the moving viewport.
            float margin = Mathf.Max(180f, viewCamera.orthographicSize * viewCamera.aspect + 40f);
            float limit = Mathf.Max(40f, halfMap - margin);
            var p = player.position;
            if (reset || Mathf.Abs(p.x) > limit || Mathf.Abs(p.z) > limit)
                player.SetPositionAndRotation(startPosition, startRotation);
        }

        private void OnGUI()
        {
            if (!showLegend || Event.current.type != EventType.Repaint) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 415, 106), GUIContent.none);
            GUI.Label(new Rect(22, 16, 395, 22), "ArtLab v10 / Landscape flight", label);
            GUI.Label(new Rect(22, 38, 395, 22), "WASD: flight   Space: boost   R: recenter", label);
            GUI.Label(new Rect(22, 60, 395, 22), "1/2/3: camera 90/75/65 deg   H: hide", label);
            GUI.Label(new Rect(22, 82, 395, 22), $"Speed {flight.CurrentSpeed:F0}   Boost {boost.Charge01:P0}   1200 x 1200", label);
        }
    }
}
