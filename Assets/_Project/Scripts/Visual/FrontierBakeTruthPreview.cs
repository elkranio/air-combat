using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirCombat
{
    // Isolated diagnostic: the left target never changes. The right uses explicit material assets.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class FrontierBakeTruthPreview : MonoBehaviour
    {
        [SerializeField] private UniversalRenderPipelineAsset pipeline;
        [SerializeField] private Camera view;
        [SerializeField] private Light sun;
        [SerializeField] private MeshRenderer right;
        [SerializeField] private Material[] variants;
        private static readonly string[] Names = {
            "Base only", "AO only", "Normal only", "AO + normal",
            "AO amplified (contrast map)", "Normal x4", "Amplified AO + normal x4",
            "Raw AO (unlit)", "Raw normal RGB (unlit)", "CONTROL: AO + normal",
            "CONTROL: AO only", "CONTROL: normal only"
        };
        private int mode = 3;
        private int lightMode;
        private bool close = true, show = true, ownsOverride;
        private RenderPipelineAsset previousPipeline;
        private GUIStyle label;

        public void Configure(UniversalRenderPipelineAsset asset, Camera cameraTarget, Light lightTarget,
            MeshRenderer rightTarget, Material[] modeMaterials)
        {
            pipeline = asset; view = cameraTarget; sun = lightTarget; right = rightTarget; variants = modeMaterials;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (pipeline == null || view == null || sun == null || right == null || variants == null || variants.Length != Names.Length)
            { Debug.LogError("Incomplete bake truth configuration.", this); enabled = false; return; }
            previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = pipeline; ownsOverride = true;
            mode = 3; lightMode = 0; close = true;
            ApplyMode(); ApplyLight(); ApplyCamera();
        }

        private void OnDisable()
        {
            if (ownsOverride && QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousPipeline;
            ownsOverride = false;
        }

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && Application.isFocused)
            {
                int next = -1;
                if (k.digit1Key.wasPressedThisFrame) next = 0;
                else if (k.digit2Key.wasPressedThisFrame) next = 1;
                else if (k.digit3Key.wasPressedThisFrame) next = 2;
                else if (k.digit4Key.wasPressedThisFrame) next = 3;
                else if (k.digit5Key.wasPressedThisFrame) next = 4;
                else if (k.digit6Key.wasPressedThisFrame) next = 5;
                else if (k.digit7Key.wasPressedThisFrame) next = 6;
                else if (k.digit8Key.wasPressedThisFrame) next = 7;
                else if (k.digit9Key.wasPressedThisFrame) next = 8;
                else if (k.digit0Key.wasPressedThisFrame) next = 9;
                else if (k.tKey.wasPressedThisFrame) next = mode < 9 || mode >= 11 ? 9 : mode + 1;
                if (next >= 0) { mode = next; ApplyMode(); }
                if (k.cKey.wasPressedThisFrame) close = !close;
                if (k.lKey.wasPressedThisFrame) { lightMode = (lightMode + 1) % 3; ApplyLight(); }
                if (k.hKey.wasPressedThisFrame) show = !show;
            }
            ApplyCamera(); // Fit both subjects if the Game View aspect changes.
        }

        private void ApplyMode()
        {
            right.sharedMaterial = variants[mode];
            var m = right.sharedMaterial;
            Debug.Log($"BakeTruth {Names[mode]} | shader={m.shader.name} | AO={m.IsKeywordEnabled("_OCCLUSIONMAP")} normal={m.IsKeywordEnabled("_NORMALMAP")}", this);
        }

        private void ApplyLight()
        {
            // Normal changes affect direct lighting. Material AO affects indirect lighting.
            // Deliberately isolate both channels without changing the scene assets or baking any light.
            sun.intensity = lightMode == 1 ? 0f : 1.25f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = lightMode == 2 ? Color.black :
                lightMode == 1 ? new Color(.85f,.85f,.85f,1) : new Color(.52f,.52f,.52f,1);
            RenderSettings.ambientIntensity = 1f;
        }

        private void ApplyCamera()
        {
            var angle = Quaternion.Euler(65f, 0f, 0f);
            var target = new Vector3(0, 2.2f, 0);
            view.transform.SetPositionAndRotation(target - angle * Vector3.forward * 115f, angle);
            view.orthographicSize = close ? Mathf.Max(14f, 39f / Mathf.Max(.1f,view.aspect)) : 80f;
        }

        private void OnGUI()
        {
            if (!show || Event.current.type != EventType.Repaint) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            var m = right.sharedMaterial;
            bool lit = m.HasProperty("_BumpScale") && m.HasProperty("_OcclusionStrength");
            string strengths = lit ? $"AO {(m.IsKeywordEnabled("_OCCLUSIONMAP") ? m.GetFloat("_OcclusionStrength") : 0):F1} | normal {(m.IsKeywordEnabled("_NORMALMAP") ? m.GetFloat("_BumpScale") : 0):F1}" : "Raw texture / no lighting";
            string lighting = lightMode == 0 ? "mixed light" : lightMode == 1 ? "ambient only" : "sun only";
            GUI.Box(new Rect(10,10,Mathf.Min(620,Screen.width-20),106),GUIContent.none);
            GUI.Label(new Rect(20,14,600,22),$"V18 / Bake truth | {(close ? "Close" : "Gameplay size 80")} | {lighting}",label);
            GUI.Label(new Rect(20,36,600,22),"1 base  2 AO  3 normal  4 full  5 strong AO  6 normal x4  7 both strong",label);
            GUI.Label(new Rect(20,58,600,22),"8 raw AO  9 raw normal  0 control  T cycle controls  C camera  L light  H hide",label);
            GUI.Label(new Rect(20,80,600,22),$"RIGHT: {Names[mode]} | {strengths}",label);
            WorldLabel(new Vector3(-18.5f, 3.4f,-11f), "LEFT / BASE ONLY");
            WorldLabel(new Vector3(18.5f, 3.4f,-11f), "RIGHT / " + Names[mode]);
        }

        private void WorldLabel(Vector3 position,string text)
        {
            var p = view.WorldToScreenPoint(position);
            if (p.z <= 0) return;
            float width = Mathf.Max(120,label.CalcSize(new GUIContent(text)).x+20);
            var rect = new Rect(p.x-width*.5f,Screen.height-p.y+4,width,25);
            GUI.Box(rect,GUIContent.none);
            GUI.Label(new Rect(rect.x+10,rect.y+1,width-20,22),text,label);
        }
    }
}
