using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Shared post-flight-motor safety check. The mission owns phase changes,
    // death, retry and pause; this helper resolves the reachable segment.
    internal sealed class MissionFlightSafety
    {
        internal enum Result { Clear, TerrainImpact, BoundaryExit }

        private readonly MissionBoundaryGuard boundary;
        private Vector3 previousPosition;

        internal MissionFlightSafety(MissionBoundaryGuard boundary)
        {
            this.boundary = boundary;
        }

        // Initial handover validates the boundary only. A terrain sweep starts
        // on the first normal flight tick. True means outside the operation.
        internal bool Begin(Vector3 position, out Vector3 stop)
        {
            previousPosition = position;
            bool exited = boundary.TryClipExit(position, position, out stop);
            if (exited) previousPosition = stop;
            return exited;
        }

        internal Result Check(Scene scene, Vector3 next, bool checkTerrain,
            float terrainCrashRadius, out Vector3 stop)
        {
            bool exited = boundary.TryClipExit(previousPosition, next, out stop);
            // Test only the reachable segment. A rock before the edge wins;
            // a rock beyond the operation cannot cause a crash.
            if (checkTerrain && TerrainFlightCollision.TryHit(scene, previousPosition,
                stop, Mathf.Max(.1f, terrainCrashRadius), out Vector3 impact))
            {
                stop = impact;
                return Result.TerrainImpact;
            }
            if (exited)
            {
                previousPosition = stop;
                return Result.BoundaryExit;
            }
            previousPosition = next;
            boundary.Tick(next);
            return Result.Clear;
        }
    }
}
