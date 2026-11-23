using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AirCombat
{
    // Compact, north-up, mesh-based radar. Everything is drawn within its own
    // circular viewport; UI does not receive pointer events or affect the camera.
    [DisallowMultipleComponent]
    public sealed class RadarHud : MaskableGraphic
    {
        private const int CircleSegments = 72;
        private const float Padding = 3f;

        [Header("Layout (pixels, screen-top-right origin)")]
        [SerializeField, Range(.08f, .3f)] private float screenHeightFraction = .15f;
        [SerializeField] private Vector2 screenInset = new Vector2(18f, 16f);
        [SerializeField, Range(.1f, 1f)] private float opacity = .82f;

        private readonly List<RadarDetection> detected = new List<RadarDetection>(16);
        private AircraftRadar radar;

        // Called by the owning HUD prefab; the sensor remains aircraft-owned.
        public void Bind(AircraftRadar sensor)
        {
            radar = sensor;
            SetVerticesDirty();
        }
        private int lastScreenHeight = -1;
        private int lastScreenWidth = -1;
        private float lastSizeFraction = -1f;
        private Vector2 lastInset = new Vector2(float.NaN, float.NaN);

        // A scene can own this HUD without altering the camera prefab or creating
        // permanent scene assets. No GraphicRaycaster: the radar never eats clicks.
        public static RadarHud Create(Transform sceneOwner, AircraftRadar sensor)
        {
            var canvasObject = new GameObject("Aircraft Radar Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(sceneOwner, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;

            var graphicObject = new GameObject("Radar", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RadarHud));
            graphicObject.transform.SetParent(canvasObject.transform, false);
            var result = graphicObject.GetComponent<RadarHud>();
            result.raycastTarget = false;
            result.maskable = false;
            result.color = Color.white;
            result.radar = sensor;
            result.Layout();
            return result;
        }

        private void LateUpdate()
        {
            Layout();
            if (radar == null) return;
            radar.Sample(detected);
            SetVerticesDirty();
        }

        private void Layout()
        {
            if (lastScreenHeight == Screen.height && lastScreenWidth == Screen.width &&
                Mathf.Approximately(lastSizeFraction, screenHeightFraction) && lastInset == screenInset)
                return;
            lastScreenHeight = Screen.height;
            lastScreenWidth = Screen.width;
            lastSizeFraction = screenHeightFraction;
            lastInset = screenInset;
            float diameter = Mathf.Clamp(Screen.height * screenHeightFraction, 112f, 166f);
            rectTransform.anchorMin = Vector2.one;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = Vector2.one;
            rectTransform.sizeDelta = new Vector2(diameter, diameter);
            rectTransform.anchoredPosition = new Vector2(-screenInset.x, -screenInset.y);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f - Padding;
            if (radius <= 0f) return;
            Vector2 center = rectTransform.rect.center;

            Disk(mesh, center, radius, new Color(.025f, .075f, .095f, .32f));
            Ring(mesh, center, radius * .5f, .8f, new Color(.47f, .75f, .77f, .20f));
            Segment(mesh, center + Vector2.left * radius * .92f,
                center + Vector2.right * radius * .92f, .8f,
                new Color(.47f, .75f, .77f, .13f));
            Segment(mesh, center + Vector2.down * radius * .92f,
                center + Vector2.up * radius * .92f, .8f,
                new Color(.47f, .75f, .77f, .13f));
            Ring(mesh, center, radius, 1.4f, new Color(.52f, .83f, .84f, .54f));

            if (radar != null)
            {
                float scale = radius / radar.Range;
                for (int i = 0; i < detected.Count; i++)
                {
                    var contact = detected[i];
                    Vector2 at = center + contact.RelativePosition * scale;
                    float threatRadius = contact.ThreatRadius * scale;
                    if (contact.Kind == RadarContactType.GroundDefense && threatRadius > .1f)
                        ClippedThreatRing(mesh, at, threatRadius, center, radius);
                }
                for (int i = 0; i < detected.Count; i++)
                {
                    var contact = detected[i];
                    Vector2 at = center + contact.RelativePosition * scale;
                    if (contact.Kind == RadarContactType.IncomingMissile)
                    {
                        // Inbound missile is visibly different from a ground-site dot.
                        // Keep even its glow inside the radar boundary.
                        Vector2 fromCenter = at - center;
                        if (fromCenter.magnitude > radius - 7f)
                            at = center + fromCenter.normalized * (radius - 7f);
                        Disk(mesh, at, 6.6f, new Color(1f, .67f, .18f, .18f));
                        Vector2 toward = center - at;
                        toward = toward.sqrMagnitude > .0001f ? toward.normalized : Vector2.up;
                        Vector2 missileRight = new Vector2(toward.y, -toward.x);
                        Triangle(mesh, at + toward * 5.1f, at - toward * 3.4f - missileRight * 4.4f,
                            at - toward * 3.4f + missileRight * 4.4f,
                            new Color(1f, .82f, .31f, .98f));
                    }
                    else
                    {
                        Disk(mesh, at, 5.2f, new Color(1f, .22f, .15f, .20f));
                        Disk(mesh, at, 2.65f, new Color(1f, .31f, .20f, .95f));
                    }
                }
            }

            // World north stays at the top; only the player triangle rotates.
            Vector2 heading = radar != null ? radar.Heading : Vector2.up;
            Vector2 right = new Vector2(heading.y, -heading.x);
            Disk(mesh, center, 5.7f, new Color(.46f, .98f, .91f, .13f));
            Triangle(mesh, center + heading * 7f, center - heading * 5f - right * 4.2f,
                center - heading * 5f + right * 4.2f, new Color(.68f, 1f, .94f, .98f));
        }

        private void ClippedThreatRing(VertexHelper mesh, Vector2 at, float threatRadius,
            Vector2 radarCenter, float radarRadius)
        {
            Vector2 from = at + Vector2.right * threatRadius;
            for (int i = 1; i <= CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                Vector2 to = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * threatRadius;
                Vector2 a = from - radarCenter, b = to - radarCenter;
                if (ClipSegmentToDisc(ref a, ref b, radarRadius - 1.5f))
                    Segment(mesh, a + radarCenter, b + radarCenter, 1.35f,
                        new Color(1f, .25f, .18f, .48f));
                from = to;
            }
        }

        // Clips each straight ring segment to the radar disc. Zones never spill
        // into the game view outside the instrument, even near the scan boundary.
        private static bool ClipSegmentToDisc(ref Vector2 a, ref Vector2 b, float radius)
        {
            Vector2 d = b - a;
            float aa = Vector2.Dot(d, d);
            if (aa < 0.000001f) return false;
            float bb = 2f * Vector2.Dot(a, d);
            float cc = Vector2.Dot(a, a) - radius * radius;
            float discriminant = bb * bb - 4f * aa * cc;
            if (discriminant < 0f) return false;
            float root = Mathf.Sqrt(discriminant);
            float enter = (-bb - root) / (2f * aa);
            float exit = (-bb + root) / (2f * aa);
            float start = Mathf.Max(0f, enter);
            float end = Mathf.Min(1f, exit);
            if (end <= start) return false;
            Vector2 original = a;
            a = original + d * start;
            b = original + d * end;
            return true;
        }

        private void Ring(VertexHelper mesh, Vector2 at, float radius, float width, Color tint)
        {
            Vector2 from = at + Vector2.right * radius;
            for (int i = 1; i <= CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                Vector2 to = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Segment(mesh, from, to, width, tint);
                from = to;
            }
        }

        private void Disk(VertexHelper mesh, Vector2 at, float radius, Color tint)
        {
            Color32 c = Tint(tint);
            int first = mesh.currentVertCount;
            AddVertex(mesh, at, c);
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                AddVertex(mesh, at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, c);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.000001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
            Color32 c = Tint(tint);
            int first = mesh.currentVertCount;
            AddVertex(mesh, a - normal, c);
            AddVertex(mesh, b - normal, c);
            AddVertex(mesh, b + normal, c);
            AddVertex(mesh, a + normal, c);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int first = mesh.currentVertCount;
            Color32 vertexColor = Tint(tint);
            AddVertex(mesh, a, vertexColor);
            AddVertex(mesh, b, vertexColor);
            AddVertex(mesh, c, vertexColor);
            mesh.AddTriangle(first, first + 1, first + 2);
        }

        private Color32 Tint(Color source)
        {
            source.a *= opacity * color.a;
            return source;
        }

        private static void AddVertex(VertexHelper mesh, Vector2 position, Color32 tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            mesh.AddVert(vertex);
        }
    }
}
