using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // A contact supplies its world position and (optional) attack radius.
    // Radar detection is owned by the aircraft, never by the HUD or the target.
    public enum RadarContactType { GroundDefense, IncomingMissile }

    public interface IRadarContact
    {
        Vector3 RadarPosition { get; }
        float RadarThreatRadius { get; }
        bool IsRadarVisible { get; }
        RadarContactType RadarKind { get; }
    }

    public readonly struct RadarDetection
    {
        public readonly Vector2 RelativePosition;
        public readonly float ThreatRadius;
        public readonly RadarContactType Kind;

        public RadarDetection(Vector2 relativePosition, float threatRadius, RadarContactType kind)
        {
            RelativePosition = relativePosition;
            ThreatRadius = threatRadius;
            Kind = kind;
        }
    }

    [DisallowMultipleComponent]
    public sealed class AircraftRadar : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float range = 460f;

        private readonly List<IRadarContact> contacts = new List<IRadarContact>();

        public float Range => Mathf.Max(1f, range);
        // North is fixed on the display; only the player glyph follows this heading.
        public Vector2 Heading
        {
            get
            {
                Vector3 forward = transform.forward;
                Vector2 heading = new Vector2(forward.x, forward.z);
                return heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector2.up;
            }
        }

        public void Register(IRadarContact contact)
        {
            if (contact != null && !contacts.Contains(contact)) contacts.Add(contact);
        }

        public void Unregister(IRadarContact contact) { contacts.Remove(contact); }

        // Returns only contacts whose centres fall inside the aircraft's 2D scan.
        // X is world east (+X), Y is world north (+Z): no heading rotation.
        // No allocations or world-wide object search on every HUD update.
        public void Sample(List<RadarDetection> results)
        {
            results.Clear();
            float rangeSquared = Range * Range;

            foreach (var contact in contacts)
            {
                // Interface references to destroyed Unity objects are not C# null.
                if (contact == null || (contact is Object component && component == null) ||
                    !contact.IsRadarVisible) continue;
                Vector3 delta = contact.RadarPosition - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > rangeSquared) continue;
                Vector2 offset = new Vector2(delta.x, delta.z);
                results.Add(new RadarDetection(offset, Mathf.Max(0f, contact.RadarThreatRadius),
                    contact.RadarKind));
            }
        }
    }
}
