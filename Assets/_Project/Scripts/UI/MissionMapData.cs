using UnityEngine;

namespace AirCombat
{
    // Static, scene-derived mission geography. X = east, Z = north.
    // UV is bottom-left origin, matching Unity textures. UI inverts Y only
    // when drawing into a top-left-origin rect. Never clamp positions here.
    public sealed class MissionMapData : ScriptableObject
    {
        [SerializeField] private string sourceSceneGuid;
        [SerializeField] private Rect worldBounds;
        [SerializeField] private float sectionAltitude;
        [SerializeField] private Texture2D geography;
        [SerializeField] private int obstacleCount;
        [SerializeField] private int sectionTriangleCount;
        [SerializeField] private bool hasAirfield;
        [SerializeField] private Vector2 airfieldPosition;
        [SerializeField] private Vector2 airfieldForward;

        public string SourceSceneGuid => sourceSceneGuid;
        public Rect WorldBounds => worldBounds;
        public float SectionAltitude => sectionAltitude;
        public Texture2D Geography => geography;
        public int ObstacleCount => obstacleCount;
        public int SectionTriangleCount => sectionTriangleCount;
        public bool HasAirfield => hasAirfield;
        public Vector2 AirfieldPosition => airfieldPosition;
        public Vector2 AirfieldForward => airfieldForward;

        public Vector2 WorldToUV(Vector2 xz) => ToUV(worldBounds, xz);
        public Vector2 WorldToUV(Vector3 world) => WorldToUV(new Vector2(world.x, world.z));
        public Vector2 UVToWorld(Vector2 uv) => worldBounds.min + Vector2.Scale(uv, worldBounds.size);
        public static Vector2 ToUV(Rect bounds, Vector2 xz) => new Vector2(
            (xz.x - bounds.xMin) / bounds.width, (xz.y - bounds.yMin) / bounds.height);

#if UNITY_EDITOR
        public void SetBake(string sceneGuid, Rect bounds, float altitude, Texture2D image,
            int obstacles, int triangles, bool airfield, Vector2 position, Vector2 forward)
        {
            sourceSceneGuid = sceneGuid; worldBounds = bounds; sectionAltitude = altitude;
            geography = image; obstacleCount = obstacles; sectionTriangleCount = triangles;
            hasAirfield = airfield; airfieldPosition = position; airfieldForward = forward;
        }
#endif
    }
}
