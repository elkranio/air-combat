using System;
using UnityEngine;

namespace AirCombat
{
    // Scene composition and shared optional-task outcome. The existing M02 sortie
    // clock drives both sites; no extra Update loop, key hooks or mission scoring.
    [DisallowMultipleComponent]
    public sealed class Mission02CargoEvacuation : MonoBehaviour
    {
        public enum TestSite { FullSortie, C1, C2 }
        [Header("CA07 / cargo evacuation (R applies)")]
        [SerializeField] private TestSite testSite = TestSite.C1;
        [SerializeField] private bool holdTestAircraft = true;
        [SerializeField, Min(0f)] private float c1DispatchSeconds = 23f;
        [SerializeField, Min(0f)] private float c2DispatchSeconds = 83f;
        [SerializeField, Min(1f)] private float carryallSpeed = 60f;
        [SerializeField, Min(5f)] private float reserveDelay = 25f;
        [SerializeField, Min(1)] private int hullHp = 5;
        [SerializeField, Min(1)] private int cargoHp = 2;
        [SerializeField] private Terrain terrain;
        [SerializeField] private GameObject carrierPrefab, cargoPrefab;
        [SerializeField] private Vector3[] c1Path, c2Path;
        private CarryallEvacuationSite first, second;
        private TestSite activeTest;
        internal bool Active => first != null && second != null;
        internal bool Failed => Active && (first.Result == CarryallEvacuationSite.CargoResult.Escaped || second.Result == CarryallEvacuationSite.CargoResult.Escaped);
        internal bool Succeeded => Active && first.Result == CarryallEvacuationSite.CargoResult.Destroyed && second.Result == CarryallEvacuationSite.CargoResult.Destroyed;
        internal bool HoldTestAircraft { get; private set; }
        internal string Status => Failed ? "OPTIONAL FAILED / CARGO ESCAPED" : Succeeded ? "OPTIONAL COMPLETE / BOTH CARGO DESTROYED" : "OPTIONAL / DESTROY BOTH CARGO";
        internal string FirstStatus(float time) => first != null ? first.Status(time) : "C1 unavailable";
        internal string SecondStatus(float time) => second != null ? second.Status(time) : "C2 unavailable";

        public void Configure(Terrain surface, GameObject transport, GameObject cargo, Vector3[] firstPath, Vector3[] secondPath)
        { terrain = surface; carrierPrefab = transport; cargoPrefab = cargo; c1Path = firstPath; c2Path = secondPath; }

        internal bool Restart(AircraftRadar radar)
        {
            Clear();
            if (!isActiveAndEnabled) return true;
            activeTest = testSite; HoldTestAircraft = holdTestAircraft;
            try
            {
                if (terrain == null || terrain.terrainData == null || carrierPrefab == null || cargoPrefab == null ||
                    c1Path == null || c1Path.Length < 2 || c2Path == null || c2Path.Length < 2)
                    throw new InvalidOperationException("CA07: missing prefab, terrain or route. Run installer.");
                float firstAt = activeTest == TestSite.FullSortie ? c1DispatchSeconds : activeTest == TestSite.C1 ? 2f : float.PositiveInfinity;
                float secondAt = activeTest == TestSite.FullSortie ? c2DispatchSeconds : activeTest == TestSite.C2 ? 2f : float.PositiveInfinity;
                first = new CarryallEvacuationSite("C1", transform, carrierPrefab, cargoPrefab, terrain, radar, c1Path,
                    firstAt, carryallSpeed, reserveDelay, hullHp, cargoHp);
                second = new CarryallEvacuationSite("C2", transform, carrierPrefab, cargoPrefab, terrain, radar, c2Path,
                    secondAt, carryallSpeed, reserveDelay, hullHp, cargoHp);
                Physics.SyncTransforms();
                return true;
            }
            catch (Exception ex)
            { Clear(); Debug.LogError("CA07: " + ex, this); return false; }
        }

        internal bool TryTestPose(out Vector3 ground, out Quaternion heading)
        {
            ground = default; heading = Quaternion.identity;
            if (!Active || activeTest == TestSite.FullSortie) return false;
            Vector3[] path = activeTest == TestSite.C1 ? c1Path : c2Path;
            ground = path[0]; ground.y = terrain.SampleHeight(ground) + terrain.transform.position.y + .15f;
            heading = Quaternion.LookRotation(Vector3.ProjectOnPlane(path[0] - path[1], Vector3.up), Vector3.up);
            return true;
        }

        internal void Tick(float dt, float clock)
        {
            if (!Active) return;
            first.Tick(dt, clock, !Failed);
            second.Tick(dt, clock, !Failed);
            Physics.SyncTransforms();
        }

        internal void Clear()
        { first?.Dispose(); second?.Dispose(); first = second = null; }
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
    }
}
