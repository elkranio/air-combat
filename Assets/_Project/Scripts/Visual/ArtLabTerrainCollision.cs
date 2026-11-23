using UnityEngine;

namespace AirCombat
{
    // ArtLab-only flight-height hazard. Detects the real collision mesh underneath
    // three aircraft probes, not a copy of the landscape generator's height math.
    // No Rigidbody/weapon/player prefab modifications. Outside this study: inactive.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(330)]
    public sealed class ArtLabTerrainCollision : MonoBehaviour
    {
        [SerializeField] private Transform aircraft;
        [SerializeField] private MeshCollider[] ground;
        [SerializeField, Min(0f)] private float clearance = 3f;
        [SerializeField, Min(0f)] private float probeWidth = 5f;
        [SerializeField, Min(0f)] private float resetGraceSeconds = 1.2f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float graceUntil;
        private float messageUntil;
        private int crashes;
        private GUIStyle style;

        public void Configure(Transform player, MeshCollider[] collisionMeshes)
        {
            aircraft = player;
            ground = collisionMeshes;
        }

        private void OnEnable()
        {
            // Unity can invoke OnEnable while authoring a scene in Edit Mode.
            // The binder runs after AddComponent; validate only in Play Mode.
            if (!Application.isPlaying) return;
            if (aircraft == null || ground == null || ground.Length == 0)
            {
                Debug.LogError("V2A4 collision: missing pilot or terrain MeshColliders.", this);
                enabled = false;
                return;
            }
            initialPosition = aircraft.position;
            initialRotation = aircraft.rotation;
            graceUntil = Time.time + 1.0f;
            crashes = 0;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || aircraft == null || Time.time < graceUntil) return;
            Vector3 p = aircraft.position;
            Vector3 r = aircraft.right * probeWidth;
            if (TouchesTerrain(p) || TouchesTerrain(p + r) || TouchesTerrain(p - r))
            {
                crashes++;
                messageUntil = Time.time + .9f;
                graceUntil = Time.time + resetGraceSeconds;
                aircraft.SetPositionAndRotation(initialPosition, initialRotation);
                Debug.Log("V2A4: mountain / canyon collision. Reset aircraft to start. #" + crashes, this);
            }
        }

        private bool TouchesTerrain(Vector3 point)
        {
            // Ray starts above even the tallest authored mountains (112 units).
            // On terrain mesh colliders a downward hit reports the real top surface.
            Ray ray = new Ray(new Vector3(point.x, 190f, point.z), Vector3.down);
            for (int i = 0; i < ground.Length; i++)
            {
                var collider = ground[i];
                if (collider != null && collider.enabled &&
                    collider.Raycast(ray, out RaycastHit hit, 270f) &&
                    point.y - clearance <= hit.point.y)
                    return true;
            }
            return false;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || Time.time > messageUntil || Event.current.type != EventType.Repaint) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, .85f, .65f) }
                };
            GUI.Box(new Rect(Screen.width / 2f - 195f, 28f, 390f, 43f), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2f - 185f, 33f, 370f, 32f),
                "TERRAIN COLLISION — RESET", style);
        }
    }
}
