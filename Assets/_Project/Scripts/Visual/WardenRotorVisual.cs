using UnityEngine;

namespace AirCombat
{
    // Presentation only: shrouds and hubs remain fixed, blade pivots rotate locally.
    [DisallowMultipleComponent]
    public sealed class WardenRotorVisual : MonoBehaviour
    {
        [SerializeField] private Transform left;
        [SerializeField] private Transform right;
        [SerializeField, Min(0f)] private float degreesPerSecond = 840f;
        private Quaternion leftRest, rightRest;
        private float phase;
        private bool ready;

        private void Awake()
        {
            if (left == null || right == null || left == right)
            {
                Debug.LogError("Warden rotor pivots are missing or duplicated.", this);
                enabled = false;
                return;
            }
            leftRest = left.localRotation;
            rightRest = right.localRotation;
            ready = true;
        }

        private void Update()
        {
            if (!ready) return;
            phase = Mathf.Repeat(phase + degreesPerSecond * Time.deltaTime, 360f);
            ApplyPose();
        }

        public void ResetPhase()
        {
            phase = 0f;
            if (ready) ApplyPose();
        }

        private void ApplyPose()
        {
            left.localRotation = leftRest * Quaternion.AngleAxis(phase, Vector3.up);
            right.localRotation = rightRest * Quaternion.AngleAxis(-phase, Vector3.up);
        }
    }
}
