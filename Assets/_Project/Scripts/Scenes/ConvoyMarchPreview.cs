using UnityEngine;

namespace AirCombat
{
    // CV02 sandbox-only composition. One progress value maintains spacing
    // independently of frame rate. Units remain in their unarmed march pose.
    internal sealed class ConvoyMarchPreview
    {
        private const int Count = 6;
        private readonly PicketDeploymentVisual prefab;
        private readonly Terrain terrain;
        private readonly AircraftRadar radar;
        private readonly Transform owner;
        private readonly Vector3 start, forward;
        private readonly float spacing, speed, distance;
        private readonly PicketDeploymentVisual[] units = new PicketDeploymentVisual[Count];
        private readonly PicketHealth[] health = new PicketHealth[Count];
        private readonly MarchContact[] contacts = new MarchContact[Count];
        private readonly GameObject[] destruction = new GameObject[Count];
        private float progress;
        private bool ready;

        private sealed class MarchContact : IRadarContact
        {
            private readonly Transform unit;
            public MarchContact(Transform unit) { this.unit = unit; }
            public Vector3 RadarPosition => unit != null ? unit.position : Vector3.zero;
            public float RadarThreatRadius => 0f;
            public bool IsRadarVisible => unit != null && unit.gameObject.activeInHierarchy;
            public RadarContactType RadarKind => RadarContactType.GroundDefense;
        }

        public ConvoyMarchPreview(PicketDeploymentVisual prefab, Terrain terrain,
            AircraftRadar radar, Transform owner, Vector3 start, float yaw,
            float spacing, float speed, float distance)
        {
            this.prefab = prefab;
            this.terrain = terrain;
            this.radar = radar;
            this.owner = owner;
            this.start = start;
            forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            this.spacing = Mathf.Max(16f, spacing);
            this.speed = Mathf.Max(.1f, speed);
            this.distance = Mathf.Max(1f, distance);
        }

        public int AliveCount
        {
            get
            {
                int alive = 0;
                foreach (var unit in health) if (unit != null && unit.IsAlive) alive++;
                return alive;
            }
        }

        public string DebugStatus
        {
            get
            {
                if (!ready) return "CONVOY UNAVAILABLE";
                string hp = "";
                for (int i = 0; i < Count; i++)
                    hp += (i == 0 ? "" : "/") + (health[i] != null ? health[i].Hp : 0);
                return $"Pickets {AliveCount}/{Count} | HP {hp} | " +
                    $"{(progress >= distance ? "STOPPED" : "MARCH")} | {progress:F0}/{distance:F0} m";
            }
        }

        // Exposed for the next sandbox atom, not a campaign or deployment rule.
        public Vector3 HeadPosition => start + forward * progress;
        public Vector3 Forward => forward;
        public float Speed => ready && progress < distance ? speed : 0f;

        public bool Restart()
        {
            ready = false;
            ClearDestruction();
            if (prefab == null || prefab.GetComponent<PicketHealth>() == null ||
                terrain == null || terrain.terrainData == null || radar == null ||
                !InsideTerrain(start - forward * (spacing * (Count - 1))) ||
                !InsideTerrain(start + forward * distance))
            {
                Debug.LogError("CV02: missing convoy references or route outside terrain.", owner);
                Dispose();
                return false;
            }
            progress = 0f;
            for (int i = 0; i < Count; i++)
            {
                if (units[i] == null)
                {
                    radar.Unregister(contacts[i]);
                    units[i] = Object.Instantiate(prefab, owner);
                    units[i].name = $"CV02 / Picket {i + 1:00}";
                    health[i] = units[i].GetComponent<PicketHealth>();
                    health[i].Killed += OnPicketKilled;
                    contacts[i] = new MarchContact(units[i].transform);
                }
                health[i].ResetHealth();
                Place(i);
                units[i].gameObject.SetActive(true);
                // Also cancels any opt-in visual deployment preview.
                units[i].SetDeploymentImmediate(false);
                radar.Register(contacts[i]);
                units[i].GetComponent<GroundMovementDust>()?.ResetTrail();
            }
            ready = true;
            return true;
        }

        public void Tick(float dt)
        {
            if (!ready || dt <= 0f || progress >= distance) return;
            progress = Mathf.Min(distance, progress + speed * dt);
            for (int i = 0; i < Count; i++)
                if (units[i] != null && health[i] != null && health[i].IsAlive) Place(i);
        }

        private void OnPicketKilled(PicketHealth killed)
        {
            // Keep the slot empty: do not move or compact surviving vehicles.
            for (int i = 0; i < Count; i++)
            {
                if (health[i] != killed) continue;
                radar.Unregister(contacts[i]);
                destruction[i] = PicketDestructionFx.Spawn(killed.transform, terrain, Forward * Speed);
                break;
            }
        }

        private bool InsideTerrain(Vector3 point)
        {
            Vector3 local = point - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return local.x >= 0f && local.x <= size.x && local.z >= 0f && local.z <= size.z;
        }

        private void Place(int i)
        {
            Vector3 p = HeadPosition - forward * (spacing * i);
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            p.y = terrain.SampleHeight(p) + origin.y;
            Vector3 normal = terrain.transform.TransformDirection(terrain.terrainData.GetInterpolatedNormal(
                (p.x - origin.x) / size.x, (p.z - origin.z) / size.z)).normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(forward, normal).normalized;
            if (tangent.sqrMagnitude < .001f) { tangent = forward; normal = Vector3.up; }
            units[i].transform.SetPositionAndRotation(p, Quaternion.LookRotation(tangent, normal));
        }

        private void ClearDestruction()
        {
            for (int i = 0; i < Count; i++)
            {
                if (destruction[i] != null)
                {
                    destruction[i].SetActive(false);
                    Object.Destroy(destruction[i]);
                }
                destruction[i] = null;
            }
        }

        public void Dispose()
        {
            ready = false;
            ClearDestruction();
            for (int i = 0; i < Count; i++)
            {
                if (radar != null) radar.Unregister(contacts[i]);
                contacts[i] = null;
                if (health[i] != null) health[i].Killed -= OnPicketKilled;
                health[i] = null;
                if (units[i] != null)
                {
                    units[i].gameObject.SetActive(false);
                    Object.Destroy(units[i].gameObject);
                }
                units[i] = null;
            }
        }
    }
}
