using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // A contact supplies its world position and (optional) attack radius.
    // Radar detection is owned by the aircraft, never by the HUD or the target.
    public interface IRadarContact
    {
        Vector3 RadarPosition { get; }
        float RadarThreatRadius { get; }
        bool IsRadarVisible { get; }
    }

    public readonly struct RadarDetection
    {
        public readonly Vector2 RelativePosition;
        public readonly float ThreatRadius;

        public RadarDetection(Vector2 relativePosition, float threatRadius)
        {
            RelativePosition = relativePosition;
            ThreatRadius = threatRadius;
        }
    }

    [DisallowMultipleComponent]
    public sealed class AircraftRadar : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float range = 460f;

        private readonly List<IRadarContact> contacts = new List<IRadarContact>();

        public float Range => Mathf.Max(1f, range);

        public void Register(IRadarContact contact)
        {
            if (contact != null && !contacts.Contains(contact)) contacts.Add(contact);
        }

        // Returns only contacts whose centres fall inside the aircraft's 2D scan.
        // X is aircraft-right, Y is aircraft-forward: heading stays at radar top.
        // No allocations or world-wide object search on every HUD update.
        public void Sample(List<RadarDetection> results)
        {
            results.Clear();
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            else forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float rangeSquared = Range * Range;

            foreach (var contact in contacts)
            {
                // Interface references to destroyed Unity objects are not C# null.
                if (contact == null || (contact is Object component && component == null) ||
                    !contact.IsRadarVisible) continue;
                Vector3 delta = contact.RadarPosition - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > rangeSquared) continue;
                Vector2 offset = new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward));
                results.Add(new RadarDetection(offset, Mathf.Max(0f, contact.RadarThreatRadius)));
            }
        }
    }
}
