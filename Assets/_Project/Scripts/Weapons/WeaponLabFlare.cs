using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Physical, short-lived IR decoy for the Weapon Lab missile seeker.
    // No collision or damage; the missile uses its existing swept target hit test.
    [DisallowMultipleComponent]
    public sealed class WeaponLabFlare : MonoBehaviour
    {
        private static readonly List<WeaponLabFlare> Active = new List<WeaponLabFlare>();
        private Vector3 velocity;
        private LineRenderer streak;
        private float life;
        private float age;
        private bool live;

        public void Initialize(Material sharedMaterial, Vector3 launchVelocity, float lifetime)
        {
            velocity = launchVelocity;
            life = Mathf.Max(.1f, lifetime);
            age = 0f;
            live = true;
            streak = gameObject.AddComponent<LineRenderer>();
            streak.sharedMaterial = sharedMaterial;
            streak.useWorldSpace = true;
            streak.positionCount = 2;
            streak.widthMultiplier = 1.7f;
            streak.numCapVertices = 3;
            streak.shadowCastingMode = ShadowCastingMode.Off;
            streak.receiveShadows = false;
            Render();
            Active.Add(this);
        }

        // Deterministic acquisition; selection depends only on actual world geometry.
        public static WeaponLabFlare FindInSeeker(Vector3 missilePosition, Vector3 missileHeading)
        {
            const float radius = 60f;
            const float cosineHalfFov = .6427876f; // cos(50 degrees)
            Vector3 forward = Vector3.ProjectOnPlane(missileHeading, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) return null;
            float nearest = radius * radius;
            WeaponLabFlare result = null;
            for (int i = 0; i < Active.Count; i++)
            {
                var flare = Active[i];
                if (flare == null || !flare.live) continue;
                Vector3 relative = flare.transform.position - missilePosition;
                relative.y = 0f;
                float d2 = relative.sqrMagnitude;
                if (d2 < .001f || d2 >= nearest) continue;
                if (Vector3.Dot(forward, relative / Mathf.Sqrt(d2)) < cosineHalfFov) continue;
                nearest = d2;
                result = flare;
            }
            return result;
        }

        private void Update()
        {
            if (!live) return;
            float dt = Time.deltaTime;
            age += dt;
            if (age >= life)
            {
                live = false;
                Destroy(gameObject);
                return;
            }
            transform.position += velocity * dt;
            velocity *= Mathf.Exp(-.85f * dt); // flare separates then loses velocity
            Render();
        }

        private void Render()
        {
            if (streak == null) return;
            float fade = Mathf.Clamp01((life - age) / .4f);
            var head = new Color(1f, .96f, .74f, .95f * fade);
            var tail = new Color(1f, .43f, .12f, .36f * fade);
            streak.startColor = tail;
            streak.endColor = head;
            Vector3 along = velocity.sqrMagnitude > .01f ? velocity.normalized : Vector3.back;
            streak.SetPosition(0, transform.position - along * 3.2f);
            streak.SetPosition(1, transform.position);
        }

        private void OnDestroy() { Active.Remove(this); }
    }
}
