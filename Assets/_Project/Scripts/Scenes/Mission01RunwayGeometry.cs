using UnityEngine;

namespace AirCombat
{
    // Mission01's authored pavement adapter. No capture, trajectory or UI logic.
    internal static class Mission01RunwayGeometry
    {
        internal static LandingRunway Resolve(Transform owner, Vector3 runwayPosition,
            Quaternion runwayRotation, float fallbackNear)
        {
            Vector3 origin = runwayPosition;
            Vector3 runwayForward = Vector3.ProjectOnPlane(runwayRotation * Vector3.forward, Vector3.up).normalized;
            float physicalWidth = 24f;
            float cueY = runwayPosition.y + .2f;
            float threshold = fallbackNear;
            if (TryResolveRunway(owner, runwayPosition, runwayForward, out Vector3 center,
                out Vector3 axis, out float actualWidth, out Vector3 entry))
            {
                runwayForward = axis;
                Vector3 lateral = Vector3.Cross(Vector3.up, axis);
                origin += lateral * Vector3.Dot(center - origin, lateral);
                physicalWidth = actualWidth;
                threshold = Vector3.Dot(entry - origin, axis);
                cueY = entry.y + .2f;
            }
            else Debug.LogWarning("LAND-01A: pavement not resolved; using 24-unit runway and configured approach offsets.");
            return new LandingRunway(origin, runwayForward, physicalWidth, threshold, cueY);
        }

        // Active C6 Mission01_HangarPass pavement: runway local X 113..137,
        // Z -120..120. The apron is part of the same mesh, so its aggregate
        // bounds cannot be used as runway bounds. Transform the runway anchors
        // into world space; do not assume the scene's yaw, scale or translation.
        private static bool TryResolveRunway(Transform owner, Vector3 spawn, Vector3 takeoff,
            out Vector3 center, out Vector3 axis, out float width, out Vector3 entry)
        {
            center = axis = entry = Vector3.zero;
            width = 0f;
            MeshFilter pavement = null;
            foreach (var root in owner.gameObject.scene.GetRootGameObjects())
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null || filter.sharedMesh.name != "Mission01_HangarPass") continue;
                    if (pavement != null) return false;
                    pavement = filter;
                }
            if (pavement == null) return false;
            Transform t = pavement.transform;
            Vector3 a = t.TransformPoint(new Vector3(125f, 0f, -120f));
            Vector3 b = t.TransformPoint(new Vector3(125f, 0f, 120f));
            center = (a + b) * .5f;
            axis = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            if (Vector3.Dot(axis, takeoff) < 0f) { axis = -axis; entry = a; }
            else entry = b;
            Vector3 lateral = Vector3.Cross(Vector3.up, axis);
            Vector3 edge = t.TransformVector(new Vector3(24f, 0f, 0f));
            width = Mathf.Abs(Vector3.Dot(edge, lateral));
            return width > 2f && Vector3.Dot(axis, takeoff) > .95f &&
                Mathf.Abs(Vector3.Dot(spawn - center, lateral)) <= width * .5f + 2f;
        }

    }
}
