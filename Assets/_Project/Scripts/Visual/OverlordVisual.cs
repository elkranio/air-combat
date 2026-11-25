using UnityEngine;

namespace AirCombat
{
    // Visual-only prefab. Escape logic will own the engine materials in AE-01B.
    [DisallowMultipleComponent]
    public sealed class OverlordVisual : MonoBehaviour
    {
        [SerializeField] private Transform radarPivot;
        [SerializeField] private float radarDegreesPerSecond = 55f;

        private void Update()
        {
            if (radarPivot != null)
                radarPivot.Rotate(Vector3.up, radarDegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
