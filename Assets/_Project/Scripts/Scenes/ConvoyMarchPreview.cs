using UnityEngine;

namespace AirCombat
{
    // Shared convoy composition. One progress value maintains spacing
    // independently of frame rate. Optional depot spurs preserve each slot;
    // the sandbox's straight march and unarmed stop remain unchanged.
    internal sealed class ConvoyMarchPreview
    {
        public const int Count = 6;
        private readonly PicketDeploymentVisual prefab;
        private readonly Terrain terrain;
        private readonly AircraftRadar radar;
        private readonly Transform owner;
        private readonly Vector3 start, forward;
        private readonly float spacing, speed, distance;
        private readonly Vector3[] route;
        private readonly float[] routeDistances;
        private readonly PicketDeploymentVisual[] units = new PicketDeploymentVisual[Count];
        private readonly PicketHealth[] health = new PicketHealth[Count];
        private readonly MarchContact[] contacts = new MarchContact[Count];
        private readonly SurfaceToAirLauncher[] samSites = new SurfaceToAirLauncher[Count];
        private readonly bool[] samArmed = new bool[Count];
        internal SurfaceToAirLauncher[] SamSites => samSites;
        private readonly GameObject[] destruction = new GameObject[Count];
        private const float DepotInnerRowOffset = 5f, DepotOffset = 65f, DepotTurnRadius = 18f;
        private static readonly float DepotArc = DepotTurnRadius * Mathf.PI * .5f;
        private static readonly float DepotSpur = DepotArc + DepotOffset - DepotTurnRadius;
        private const float DepotVehicleRadius = 12f;
        private readonly bool[] parked = new bool[Count];
        private bool deployAtDepot, spreadDepotDefense;
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

        // Optional authored polyline; the straight sandbox constructor is unchanged.
        // Negative distances extend the first segment to preserve all six slots at A.
        public ConvoyMarchPreview(PicketDeploymentVisual prefab, Terrain terrain,
            AircraftRadar radar, Transform owner, Vector3[] points, float spacing, float speed)
            : this(prefab, terrain, radar, owner, points[0],
                Mathf.Atan2(points[1].x - points[0].x, points[1].z - points[0].z) * Mathf.Rad2Deg,
                spacing, speed, 1f)
        {
            route = (Vector3[])points.Clone();
            routeDistances = new float[route.Length];
            for (int i = 0; i < route.Length; i++) route[i].y = 0f;
            for (int i = 1; i < route.Length; i++)
                routeDistances[i] = routeDistances[i - 1] + Vector3.Distance(route[i - 1], route[i]);
            distance = routeDistances[route.Length - 1];
        }

        private Vector3 Sample(float along, out Vector3 heading)
        {
            heading = forward;
            if (route == null) return start + forward * along;
            if (along < 0f) return route[0] + forward * along;
            int i = 1;
            while (i < route.Length - 1 && along > routeDistances[i]) i++;
            heading = (route[i] - route[i - 1]).normalized;
            return route[i - 1] + heading * (Mathf.Min(along, distance) - routeDistances[i - 1]);
        }

        // Pairs turn off the authored road into fixed slots. M02 may use a
        // distributed perimeter; the original compact layout remains selectable.
        public bool EnableDepotDeployment(Rect safeArea, bool spreadDefense = false)
        {
            if (ready || route == null || distance - routeDistances[route.Length - 2] <= 100f ||
                (spreadDefense && distance < 950f))
                return false;
            spreadDepotDefense = spreadDefense;
            // Validate a conservative swept envelope over the actual shared-progress
            // schedule, including parked vehicles. Fixed slots survive convoy losses.
            var buildings = new System.Collections.Generic.List<BoxCollider>();
            foreach (var root in owner.gameObject.scene.GetRootGameObjects())
                foreach (var target in root.GetComponentsInChildren<DepotBuildingTarget>(true))
                {
                    var box = target.GetComponent<BoxCollider>();
                    if (box != null) buildings.Add(box); // include destroyed buildings too
                }
            float finish = distance;
            for (int i = 0; i < Count; i++) finish = Mathf.Max(finish, DepotBranch(i) + spacing*i + DepotSpurFor(i));
            // Cover the earliest branch and the tail, not just the final depot approach.
            float earliestBranch = distance - 400f;
            for (int i = 0; i < Count; i++) earliestBranch = Mathf.Min(earliestBranch, DepotBranch(i) - 100f);
            float begin = Mathf.Max(0f, earliestBranch - spacing*(Count-1));
            var positions = new Vector3[Count];
            int steps = Mathf.CeilToInt(finish-begin);
            for (int step = 0; step <= steps; step++)
            {
                float along = Mathf.Min(finish, begin+step);
                for (int i = 0; i < Count; i++)
                {
                    float travel = along-spacing*i-DepotBranch(i);
                    Vector3 p = travel >= 0f ? SampleDepot(i, travel, out _) : Sample(along-spacing*i, out _);
                    positions[i] = p;
                    if (!safeArea.Contains(new Vector2(p.x,p.z)) || !InsideTerrain(p)) return false;
                    foreach (var box in buildings)
                    {
                        Vector3 local = box.transform.InverseTransformPoint(p)-box.center;
                        Vector3 half = box.size*.5f;
                        float dx = Mathf.Max(0f, Mathf.Abs(local.x)-half.x);
                        float dz = Mathf.Max(0f, Mathf.Abs(local.z)-half.z);
                        if (dx*dx+dz*dz < 13f*13f)
                        { Debug.LogError($"BD04: Picket {i+1} route intersects {box.name} at progress {along:F1}.", owner); return false; }
                    }
                    for (int j=0;j<i;j++)
                        if ((positions[j]-p).sqrMagnitude < (DepotVehicleRadius*2f+1f)*(DepotVehicleRadius*2f+1f))
                        { Debug.LogError($"BD04: Pickets {j+1}/{i+1} clearance at progress {along:F1}.", owner); return false; }
                }
            }
            deployAtDepot = true;
            return true;
        }

        private float DepotLateralFor(int index)
        {
            if (!spreadDepotDefense || index < 2) return index >= 4 ? 32f : DepotOffset;
            if (index == 2) return 180f;
            if (index == 3) return 145f;
            return index == 4 ? 190f : 135f;
        }
        private float DepotSpurFor(int index) => index < 2
            ? DepotSpur + DepotArc + (index == 0 ? 115f : 103f) - 2f*DepotTurnRadius
            : DepotArc + DepotLateralFor(index) - DepotTurnRadius;
        private float DepotBranch(int index) => distance - (spreadDepotDefense
            ? (index < 2 ? 0f : index < 4 ? 360f : 850f)
            : (index >= 4 ? DepotInnerRowOffset : 0f));
        private float DepotTravel(int index) => progress - spacing * index - DepotBranch(index);
        private float MoveLimit
        {
            get
            {
                if (!deployAtDepot) return distance;
                float end = distance;
                for (int i = 0; i < Count; i++)
                    end = Mathf.Max(end, DepotBranch(i) + spacing * i + DepotSpurFor(i));
                return end;
            }
        }

        private Vector3 SampleDepot(int index, float travel, out Vector3 heading)
        {
            Vector3 origin = Sample(DepotBranch(index), out Vector3 axis);
            Vector3 side = Vector3.Cross(Vector3.up, axis) * (index % 2 == 0 ? 1f : -1f);
            travel = Mathf.Clamp(travel, 0f, DepotSpurFor(index));
            if (index < 2 && travel > DepotSpur)
            {
                // First pair turns along the outside of the warehouse row.
                float extra = travel - DepotSpur;
                float a = Mathf.Clamp(extra / DepotTurnRadius, 0f, Mathf.PI*.5f);
                heading = (side*Mathf.Cos(a)+axis*Mathf.Sin(a)).normalized;
                return origin + side*(DepotOffset+DepotTurnRadius*Mathf.Sin(a)) +
                    axis*(DepotTurnRadius+DepotTurnRadius*(1f-Mathf.Cos(a))+Mathf.Max(0f,extra-DepotArc));
            }
            float angle = Mathf.Clamp(travel / DepotTurnRadius, 0f, Mathf.PI * .5f);
            heading = (axis * Mathf.Cos(angle) + side * Mathf.Sin(angle)).normalized;
            float lateral = DepotLateralFor(index);
            return origin + axis * (DepotTurnRadius * Mathf.Sin(angle)) + side *
                (DepotTurnRadius * (1f - Mathf.Cos(angle)) + Mathf.Clamp(travel - DepotArc, 0f, lateral - DepotTurnRadius));
        }

        // M02 opts in after Restart. Sandbox convoy remains visual-only.
        internal bool EnableSam(SurfaceToAirLauncher template, Transform player, PlayerHitPoints playerHealth)
        {
            if (!ready || !deployAtDepot || template == null || !template.IsConfigured) return false;
            for (int i = 0; i < Count; i++)
            {
                if (units[i] == null || samSites[i] != null) return false;
                var child = new GameObject("BD05 / SAM controller");
                child.transform.SetParent(units[i].transform, false);
                var site = child.AddComponent<SurfaceToAirLauncher>();
                samSites[i] = site;
                if (!site.BindMobilePicket(template, units[i].transform, player, playerHealth, radar))
                {
                    Debug.LogError($"BD05: invalid mobile rail or SAM template on Picket {i+1}.", owner);
                    return false; // caller disposes the entire failed composition
                }
            }
            return true;
        }

        // Called even when march progress has reached its cap: the last unit
        // finishes unfolding after movement ends. Swap, don't duplicate radar contacts.
        internal void TickSamArming()
        {
            for (int i = 0; i < Count; i++)
            {
                var site = samSites[i];
                if (site == null || samArmed[i] || !parked[i] || health[i] == null ||
                    !health[i].IsAlive || units[i] == null || !units[i].IsDeployed) continue;
                samArmed[i] = true;
                radar.Unregister(contacts[i]);
                site.SetArmed(true);
                radar.Register(site);
            }
        }

        private void DisarmSam(int i)
        {
            var site = samSites[i];
            samArmed[i] = false;
            if (site == null) return;
            radar?.Unregister(site);
            site.SetArmed(false);
            site.CancelProjectiles();
        }

        internal void StopSam()
        {
            for (int i = 0; i < Count; i++) DisarmSam(i);
        }

        internal string SamStatus
        {
            get
            {
                int armedCount = 0, shots = 0;
                for (int i = 0; i < Count; i++)
                {
                    if (samArmed[i]) armedCount++;
                    if (samSites[i] != null) shots += samSites[i].LaunchedCount;
                }
                return $"SAM armed {armedCount}/{AliveCount} | Launches {shots}";
            }
        }

        public int ParkedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Count; i++)
                    if (parked[i] && health[i] != null && health[i].IsAlive) count++;
                return count;
            }
        }
        public int DeployedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Count; i++)
                    if (parked[i] && health[i] != null && health[i].IsAlive && units[i] != null && units[i].IsDeployed) count++;
                return count;
            }
        }
        public bool AllSurvivorsDeployed => ready && deployAtDepot && AliveCount > 0 && DeployedCount == AliveCount;
        public bool ReachedEnd => ready && progress >= distance;
        public float RouteLength => distance;
        public float RemainingDistance => Mathf.Max(0f, distance - progress);
        public bool TryGetPosition(int index, out Vector3 position)
        {
            position = default;
            if (!ready || index < 0 || index >= Count || units[index] == null ||
                health[index] == null || !health[index].IsAlive) return false;
            position = units[index].transform.position;
            return true;
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
                string state = AllSurvivorsDeployed ? "DEPLOYED" : deployAtDepot && progress >= distance ? "DEPOT" :
                    progress >= distance ? "STOPPED" : "MARCH";
                return $"Pickets {AliveCount}/{Count} | HP {hp} | {state} | {Mathf.Min(progress, distance):F0}/{distance:F0} m";
            }
        }

        // Exposed for the next sandbox atom, not a campaign or deployment rule.
        public Vector3 HeadPosition => Sample(progress, out _);
        public Vector3 Forward { get { Sample(progress, out Vector3 heading); return heading; } }
        public float HalfLength => spacing * (Count - 1) * .5f;
        public float Speed => ready && progress < distance ? speed : 0f;

        public bool Restart(float initialProgress = 0f, bool startDeployed = false)
        {
            ready = false;
            ClearDestruction();
            if (prefab == null || prefab.GetComponent<PicketHealth>() == null ||
                terrain == null || terrain.terrainData == null || radar == null ||
                !InsideTerrain(Sample(-spacing * (Count - 1), out _)) ||
                !InsideTerrain(Sample(distance, out _)) || !RouteInsideTerrain())
            {
                Debug.LogError("CV02: missing convoy references or route outside terrain.", owner);
                Dispose();
                return false;
            }
            if (startDeployed && !deployAtDepot) return false;
            progress = startDeployed ? MoveLimit : Mathf.Clamp(initialProgress, 0f, Mathf.Max(0f,distance-101f));
            System.Array.Clear(parked, 0, parked.Length);
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
                units[i].SetDeploymentImmediate(startDeployed);
                // A close-start trial can begin after an outer pair has parked.
                if (!startDeployed && parked[i]) units[i].SetDeploymentTarget(true);
                radar.Register(contacts[i]);
                units[i].GetComponent<GroundMovementDust>()?.ResetTrail();
            }
            ready = true;
            return true;
        }

        public void Tick(float dt)
        {
            float limit = MoveLimit;
            if (!ready || dt <= 0f || progress >= limit) return;
            progress = Mathf.Min(limit, progress + speed * dt);
            for (int i = 0; i < Count; i++)
                if (!parked[i] && units[i] != null && health[i] != null && health[i].IsAlive) Place(i);
        }

        private void OnPicketKilled(PicketHealth killed)
        {
            // Keep the slot empty: do not move or compact surviving vehicles.
            for (int i = 0; i < Count; i++)
            {
                if (health[i] != killed) continue;
                radar.Unregister(contacts[i]);
                DisarmSam(i);
                float movementSpeed = deployAtDepot ? (parked[i] ? 0f : speed) : Speed;
                destruction[i] = PicketDestructionFx.Spawn(killed.transform, terrain,
                    killed.transform.forward * movementSpeed);
                break;
            }
        }

        private bool RouteInsideTerrain()
        {
            if (route == null) return true;
            foreach (Vector3 point in route) if (!InsideTerrain(point)) return false;
            return true;
        }

        private bool InsideTerrain(Vector3 point)
        {
            Vector3 local = point - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return local.x >= 0f && local.x <= size.x && local.z >= 0f && local.z <= size.z;
        }

        private void Place(int i)
        {
            float depotTravel = DepotTravel(i);
            Vector3 p = Sample(progress - spacing * i, out Vector3 heading);
            if (deployAtDepot && depotTravel >= 0f)
                p = SampleDepot(i, depotTravel, out heading);
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            p.y = terrain.SampleHeight(p) + origin.y;
            Vector3 normal = terrain.transform.TransformDirection(terrain.terrainData.GetInterpolatedNormal(
                (p.x - origin.x) / size.x, (p.z - origin.z) / size.z)).normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(heading, normal).normalized;
            if (tangent.sqrMagnitude < .001f) { tangent = heading; normal = Vector3.up; }
            units[i].transform.SetPositionAndRotation(p, Quaternion.LookRotation(tangent, normal));
            // Stop before unfolding. Allow a small epsilon at the capped final
            // travel distance to avoid a float-rounding stall on the last slot.
            if (deployAtDepot && !parked[i] && depotTravel >= DepotSpurFor(i) - .001f)
            {
                parked[i] = true;
                units[i].SetDeploymentTarget(true);
            }
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
            StopSam();
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
