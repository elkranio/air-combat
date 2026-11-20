using UnityEngine;

namespace AirCombat
{
    // Art preview only. No weapons, detection, damage, or gameplay state.
    [DisallowMultipleComponent]
    public sealed class TurretHeadTracker : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform aimForward;
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float turnSpeed = 120f;

        private void LateUpdate()
        {
            if (head == null || aimForward == null || target == null || !target.gameObject.activeInHierarchy)
                return;
            Vector3 current = aimForward.position - head.position;
            Vector3 desired = target.position - head.position;
            current.y = desired.y = 0f;
            if (current.sqrMagnitude < 0.0001f || desired.sqrMagnitude < 0.0001f)
                return;
            float error = Vector3.SignedAngle(current, desired, Vector3.up);
            float step = Mathf.MoveTowards(0f, error, turnSpeed * Time.deltaTime);
            // The imported FBX may have a different local axis convention.
            // Rotate around world up, preserving pitch/roll of the authored parts.
            head.rotation = Quaternion.AngleAxis(step, Vector3.up) * head.rotation;
        }
    }
}
