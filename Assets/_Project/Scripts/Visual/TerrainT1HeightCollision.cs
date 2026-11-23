using UnityEngine;

namespace AirCombat
{
    // Study-only collision probe; the native TerrainCollider is still present.
    // FlightController has no physics body, so it will not generate OnCollisionEnter.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(320)]
    public sealed class TerrainT1HeightCollision : MonoBehaviour
    {
        [SerializeField] private Transform aircraft;
        [SerializeField] private Terrain terrain;
        [SerializeField, Min(0f)] private float wingHalfWidth = 4f;
        [SerializeField, Min(0f)] private float clearance = 2.5f;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float graceUntil;
        private float messageUntil;

        public void Configure(Transform pilot, Terrain nativeTerrain)
        {
            aircraft = pilot;
            terrain = nativeTerrain;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (aircraft == null || terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("T1: missing aircraft or native Terrain reference.", this);
                enabled = false;
                return;
            }
            startPosition = aircraft.position;
            startRotation = aircraft.rotation;
            graceUntil = Time.time + .8f;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || Time.time < graceUntil || aircraft == null) return;
            var pos = aircraft.position;
            var wing = aircraft.right * wingHalfWidth;
            if (!Hits(pos) && !Hits(pos + wing) && !Hits(pos - wing) && !Hits(pos + aircraft.forward * 4f))
                return;
            aircraft.SetPositionAndRotation(startPosition, startRotation);
            graceUntil = Time.time + 1.0f;
            messageUntil = Time.time + 1.2f;
            Debug.Log("T1: terrain collision; aircraft reset to safe spawn.", this);
        }

        private bool Hits(Vector3 point)
        {
            if (terrain == null) return false;
            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;
            if (point.x < origin.x || point.x > origin.x + size.x ||
                point.z < origin.z || point.z > origin.z + size.z) return false;
            float x01 = (point.x - origin.x) / size.x;
            float z01 = (point.z - origin.z) / size.z;
            float worldHeight = origin.y + terrain.terrainData.GetInterpolatedHeight(x01, z01);
            return point.y - clearance <= worldHeight;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || Time.time > messageUntil || Event.current.type != EventType.Repaint) return;
            GUI.Box(new Rect(Screen.width * .5f - 140, 16, 280, 35), "TERRAIN COLLISION — RESET");
        }
    }
}
