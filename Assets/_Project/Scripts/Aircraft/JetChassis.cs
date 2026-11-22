using UnityEngine;

namespace AirCombat
{
    // Visual-only airframe identity: models, animation roots and mount sockets.
    // Flight, pilot input/AI, durability and weapon rules belong to the jet root,
    // not to this component. All sockets are in the chassis' local hierarchy.
    [DisallowMultipleComponent]
    public sealed class JetChassis : MonoBehaviour
    {
        [Header("Air-to-air weapon sockets")]
        [SerializeField] private Transform autocannonLeft;
        [SerializeField] private Transform autocannonRight;
        [SerializeField] private Transform gaussMuzzle;
        [SerializeField] private Transform missileMount;

        [Header("Engine sockets")]
        [SerializeField] private Transform engineLeft;
        [SerializeField] private Transform engineRight;

        public Transform Visual => transform;
        public Transform AutocannonLeft => autocannonLeft;
        public Transform AutocannonRight => autocannonRight;
        public Transform GaussMuzzle => gaussMuzzle;
        public Transform MissileMount => missileMount;
        public Transform EngineLeft => engineLeft;
        public Transform EngineRight => engineRight;

        public bool HasRequiredSockets => autocannonLeft != null && autocannonRight != null &&
            gaussMuzzle != null && missileMount != null && engineLeft != null && engineRight != null;

#if UNITY_EDITOR
        // Called ONLY by prefab authoring, never from gameplay or on scene load.
        public void SetAuthoringSockets(Transform leftGun, Transform rightGun,
            Transform gauss, Transform missile, Transform leftNozzle, Transform rightNozzle)
        {
            autocannonLeft = leftGun;
            autocannonRight = rightGun;
            gaussMuzzle = gauss;
            missileMount = missile;
            engineLeft = leftNozzle;
            engineRight = rightNozzle;
        }
#endif
    }
}
