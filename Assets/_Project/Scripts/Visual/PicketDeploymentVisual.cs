using UnityEngine;

namespace AirCombat
{
    // Visual-only deployment. A later composition owns movement and SAM arming.
    [DisallowMultipleComponent]
    public sealed class PicketDeploymentVisual : MonoBehaviour
    {
        [SerializeField] private Transform leftArm, rightArm, leftLeg, rightLeg;
        [SerializeField] private Transform railPitch;
        [SerializeField, Min(.1f)] private float deploymentSeconds = 2.4f;
        [SerializeField] private bool startDeployed;
        [Header("Visual trial only")]
        [SerializeField] private bool previewDeployOnPlay;
        [SerializeField, Min(0f)] private float previewDelay = 2f;
        private float progress, target, previewAt;
        private bool previewPending;
        public bool IsDeployed => progress >= 1f;
        public bool IsMarching => progress <= 0f;

        private void Awake()
        {
            SetDeploymentImmediate(startDeployed);
            previewPending = previewDeployOnPlay && !startDeployed;
            previewAt = Time.time + previewDelay;
        }

        public void SetDeploymentImmediate(bool deployed)
        {
            previewPending = false;
            progress = target = deployed ? 1f : 0f;
            ApplyPose();
        }

        public void SetDeploymentTarget(bool deployed)
        {
            previewPending = false;
            target = deployed ? 1f : 0f;
        }

        private void Update()
        {
            if (previewPending && Time.time >= previewAt) SetDeploymentTarget(true);
            if (progress == target) return;
            progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(.1f, deploymentSeconds));
            ApplyPose();
        }

        private static float Stage(float value, float start, float end) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));

        private void ApplyPose()
        {
            if (leftArm == null || rightArm == null || leftLeg == null || rightLeg == null || railPitch == null) return;
            float extension = Stage(progress, 0f, .42f);
            float lowering = Stage(progress, .32f, .72f);
            float raising = Stage(progress, .45f, 1f);
            PoseSupport(leftArm, leftLeg, -1f, extension, lowering);
            PoseSupport(rightArm, rightLeg, 1f, extension, lowering);
            // The accepted rail meshes already have 35 degrees of elevation baked in.
            railPitch.localRotation = Quaternion.Euler(Mathf.Lerp(35f, 0f, raising), 0f, 0f);
        }

        private static void PoseSupport(Transform arm, Transform leg, float side, float extension, float lowering)
        {
            arm.localPosition = new Vector3(side * Mathf.Lerp(1.9f, 3f, extension), 0f, 0f);
            arm.localScale = new Vector3(Mathf.Lerp(.2f, 1f, extension), 1f, 1f);
            leg.localPosition = new Vector3(side * Mathf.Lerp(2.1f, 4.095f, extension), 1.35f * (1f - lowering), 0f);
        }
    }
}
