using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // A contact supplies its world position and (optional) attack radius.
    // Radar detection is owned by the aircraft, never by the HUD or the target.
    public enum RadarContactType { GroundDefense, IncomingMissile, Aircraft }

    public interface IRadarContact
    {
        Vector3 RadarPosition { get; }
        float RadarThreatRadius { get; }
        bool IsRadarVisible { get; }
        RadarContactType RadarKind { get; }
    }

    // Aircraft additionally expose the direction of their nose in world space.
    // Ground sites and missiles keep the original, smaller IRadarContact contract.
    public interface IRadarAirContact : IRadarContact
    {
        Vector3 RadarForward { get; }
    }

    public readonly struct RadarDetection
    {
        public readonly Vector2 RelativePosition;
        public readonly float ThreatRadius;
        public readonly RadarContactType Kind;
        public readonly Vector2 Heading;
        public readonly bool InRange;
        public readonly Vector3 WorldPosition;

        public RadarDetection(Vector2 relativePosition, float threatRadius, RadarContactType kind,
            Vector2 heading, bool inRange, Vector3 worldPosition = default)
        {
            RelativePosition = relativePosition;
            ThreatRadius = threatRadius;
            Kind = kind;
            Heading = heading;
            InRange = inRange;
            WorldPosition = worldPosition;
        }
    }

    [DisallowMultipleComponent]
    public sealed class AircraftRadar : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float range = 460f;

        private readonly List<IRadarContact> contacts = new List<IRadarContact>();

        public float Range => Mathf.Max(1f, range);
        // World-space heading; RadarHud transforms it through the presentation camera.
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

        // Only registered aircraft get an off-range bearing cue. Ground defenses
        // and missiles remain sensor-limited, as in the original regression lab.
        // X is world east (+X), Y is world north (+Z); presentation is separate.
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
                bool inRange = delta.sqrMagnitude <= rangeSquared;
                var air = contact as IRadarAirContact;
                if (!inRange && air == null) continue;

                Vector2 heading = Vector2.zero;
                if (air != null)
                {
                    Vector3 forward = air.RadarForward;
                    heading = new Vector2(forward.x, forward.z).normalized;
                }
                results.Add(new RadarDetection(new Vector2(delta.x, delta.z),
                    Mathf.Max(0f, contact.RadarThreatRadius), contact.RadarKind,
                    heading, inRange, contact.RadarPosition));
            }
        }
    }
}
