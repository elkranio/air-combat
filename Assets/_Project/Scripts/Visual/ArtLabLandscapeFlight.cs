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
        [SerializeField] private bool resetOutsideBounds = true;
        [SerializeField] private string studyTitle = "ArtLab v10 / Landscape flight";
        [SerializeField] private string cameraHelp;
        [SerializeField] private string controlsHelp;
        private RenderPipelineAsset previousPipeline;
        private bool ownsOverride;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private bool showLegend = true;
        private GUIStyle label;

        public void Configure(UniversalRenderPipelineAsset pipeline, Transform aircraft, Camera camera,
            FlightController controller, Afterburner afterburner, float halfExtent, string title = "ArtLab v10 / Landscape flight")
        {
            studyPipeline = pipeline; player = aircraft; viewCamera = camera;
            flight = controller; boost = afterburner; halfMap = halfExtent; studyTitle = title;
        }

        public void SetStudyLegend(string title, string cameraLine)
        {
            studyTitle = title;
            cameraHelp = cameraLine;
        }

        public void SetControlsLegend(string controls) => controlsHelp = controls;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (studyPipeline == null || player == null || viewCamera == null)
            { Debug.LogError("Incomplete visual flight study configuration.", this); enabled = false; return; }
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
            if (reset || (resetOutsideBounds && (Mathf.Abs(p.x) > limit || Mathf.Abs(p.z) > limit)))
                player.SetPositionAndRotation(startPosition, startRotation);
        }

        private void OnGUI()
        {
            if (!showLegend || Event.current.type != EventType.Repaint) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 415, 106), GUIContent.none);
            GUI.Label(new Rect(22, 16, 395, 22), string.IsNullOrEmpty(studyTitle) ? "ArtLab v10 / Landscape flight" : studyTitle, label);
            GUI.Label(new Rect(22, 38, 395, 22),
                string.IsNullOrEmpty(controlsHelp) ? "WASD: flight   Space: boost   R: recenter" : controlsHelp, label);
            GUI.Label(new Rect(22, 60, 395, 22),
                string.IsNullOrEmpty(cameraHelp) ? "1/2/3: camera 90/75/65 deg   H: hide" : cameraHelp, label);
            GUI.Label(new Rect(22, 82, 395, 22), $"Speed {flight.CurrentSpeed:F0}   Boost {boost.Charge01:P0}   {halfMap * 2f:F0} x {halfMap * 2f:F0}", label);
        }
    }
}
