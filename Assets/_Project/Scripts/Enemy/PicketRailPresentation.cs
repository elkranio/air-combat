using UnityEngine;

namespace AirCombat
{
    // Presentation of the fictional Picket twin-rail prop, independent of mission.
    // The launcher clock and projectile's gameplay transform remain authoritative.
    internal sealed class PicketRailPresentation
    {
        private static readonly string[] Names = { "Launcher", "Accent", "LauncherDark", "MissileL", "MissileR" };
        // C2 artwork is baked in truck-local coordinates, with +Z toward the cab.
        private static readonly Vector3 Pivot = new Vector3(0f, 3.15f, -2.625f);
        private const float RailHeight = 6.006f;
        private const float RailAngle = 35f;
        private const float TurnSpeed = 180f;
        private readonly Transform model;
        private readonly Transform mobileYaw;
        private readonly Transform[] parts;
        private readonly Vector3[] restPositions;
        private readonly Quaternion[] restRotations;
        private readonly float[] refillAt = new float[2];
        private readonly Vector3[] rocketCenters = new Vector3[2];
        private float yaw;
        private int next;

        private PicketRailPresentation(Transform model, Transform[] parts, Transform mobileYaw = null)
        {
            this.model = model;
            this.mobileYaw = mobileYaw;
            this.parts = parts;
            restPositions = new Vector3[parts.Length];
            restRotations = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                restPositions[i] = parts[i].localPosition;
                restRotations[i] = parts[i].localRotation;
            }
            for (int i = 0; i < 2; i++)
            {
                // Mesh origins are baked at truck origin, not the rocket center.
                var mesh = parts[i + 3].GetComponent<MeshFilter>().sharedMesh;
                rocketCenters[i] = new Vector3(mesh.bounds.center.x, RailHeight, Pivot.z);
            }
            Reset();
        }

        // Mobile art has a real yaw pivot and a separate deployment pitch pivot.
        // The missile meshes retain the same truck-local baked vertex coordinates.
        internal static PicketRailPresentation TryBindMobile(Transform vehicle)
        {
            var yaw = vehicle.Find("LauncherYaw");
            var pitch = yaw != null ? yaw.Find("RailPitch") : null;
            if (pitch == null) return null;
            var names = new[] { "RailLauncher", "RailAccent", "RailLauncherDark", "MissileL", "MissileR" };
            var parts = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                parts[i] = pitch.Find(names[i]);
                if (parts[i] == null) return null;
                if (i >= 3)
                {
                    var mesh = parts[i].GetComponent<MeshFilter>();
                    if (mesh == null || mesh.sharedMesh == null || parts[i].GetComponent<MeshRenderer>() == null)
                        return null;
                }
            }
            return new PicketRailPresentation(vehicle, parts, yaw);
        }

        public static PicketRailPresentation TryBind(Transform site)
        {
            // Existing sites may wrap the environment prefab in a scene parent.
            // Bind only the complete accepted C2 branch; legacy SAM1 stays untouched.
            foreach (var candidate in site.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "MissileL" || candidate.parent == null) continue;
                var model = candidate.parent;
                var parts = new Transform[Names.Length];
                bool complete = true;
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i] = model.Find(Names[i]);
                    if (parts[i] == null) { complete = false; break; }
                    if (i >= 3)
                    {
                        var filter = parts[i].GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null || parts[i].GetComponent<MeshRenderer>() == null)
                        { complete = false; break; }
                    }
                }
                if (complete) return new PicketRailPresentation(model, parts);
            }
            Debug.LogWarning("SAM01D: twin rail visual not found; legacy launch presentation retained.", site);
            return null;
        }

        private void ApplyYaw()
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            if (mobileYaw != null)
            {
                mobileYaw.localRotation = turn;
                return; // Do not overwrite the separate RailPitch deployment animation.
            }
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].localPosition = Pivot + turn * (restPositions[i] - Pivot);
                parts[i].localRotation = turn * restRotations[i];
            }
        }

        public void Track(Vector3 target, float dt)
        {
            Vector3 local = model.InverseTransformPoint(target) - Pivot;
            if (local.x * local.x + local.z * local.z < .001f) return;
            float desired = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, desired, TurnSpeed * dt);
            ApplyYaw();
        }

        public void TickReload(float now)
        {
            for (int i = 0; i < 2; i++)
                if (!parts[i + 3].gameObject.activeSelf && now >= refillAt[i])
                    parts[i + 3].gameObject.SetActive(true);
        }

        public void PresentLaunch(SurfaceToAirMissile missile, float now, float reloadSeconds)
        {
            TickReload(now);
            int slot = next;
            Transform source = parts[slot + 3];
            if (!source.gameObject.activeSelf) return; // visual-only: never delay combat
            float angle = RailAngle * Mathf.Deg2Rad;
            Vector3 forward = source.TransformDirection(new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle)));
            Vector3 center = source.TransformPoint(rocketCenters[slot]);
            Quaternion rotation = Quaternion.LookRotation(forward, source.up);
            float tailLength = 1.84f * 1.92f * source.TransformVector(Vector3.forward).magnitude;
            if (!missile.AttachRailVisual(source, center, rotation, tailLength)) return;
            source.gameObject.SetActive(false);
            // Reappear between this rail's two scheduled uses. Each slot is independent.
            refillAt[slot] = now + Mathf.Max(.1f, reloadSeconds) * 1.6f;
            next = 1 - slot;
        }

        public void Reset()
        {
            yaw = 0f;
            next = 0;
            ApplyYaw();
            for (int i = 0; i < 2; i++)
            {
                refillAt[i] = 0f;
                parts[i + 3].gameObject.SetActive(true);
            }
        }
    }
}
