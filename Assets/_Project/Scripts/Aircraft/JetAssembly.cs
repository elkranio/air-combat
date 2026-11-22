using UnityEngine;

namespace AirCombat
{
    // Role root -> swappable visual chassis. No scene or pilot references live here.
    [DisallowMultipleComponent]
    public sealed class JetAssembly : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private JetChassis chassis;

        public Transform VisualRoot => visualRoot;
        public JetChassis Chassis => chassis;
        public bool IsConfigured => visualRoot != null && chassis != null && chassis.HasRequiredSockets;

#if UNITY_EDITOR
        public void SetAuthoringReferences(Transform visual, JetChassis airframe)
        {
            visualRoot = visual;
            chassis = airframe;
        }
#endif
    }
}
