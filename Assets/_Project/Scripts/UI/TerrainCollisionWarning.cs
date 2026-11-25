using UnityEngine;
using UnityEngine.UI;

namespace AirCombat
{
    // Local directional terrain warning. Uses the same world-space altitude slice
    // as the radar; no collider, raycast layer, or camera changes are needed.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(260)] // After the flight motor and camera follow.
    public sealed class TerrainCollisionWarning : MaskableGraphic
    {
        private const float SideClearance = 22.5f; // H1i proximity x1.25; no prediction.
        private const float Epsilon = .0001f;
        private FlightController flight;
        private Camera viewCamera;
        private AudioSource speaker;
        private AudioClip chime;
        private bool danger;
        private Vector2 arcCenter, arcDirection;
        private float arcRadius, arcWidth, intensity, nextChime, pulsePhase;

        public static void Create(Transform hud, FlightController controller, Camera camera)
        {
            if (controller == null || camera == null ||
                hud.GetComponentInChildren<TerrainCollisionWarning>(true) != null) return;
            var go = new GameObject("Terrain collision warning", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TerrainCollisionWarning), typeof(AudioSource));
            go.transform.SetParent(hud, false);
            go.layer = hud.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var warning = go.GetComponent<TerrainCollisionWarning>();
            warning.flight = controller;
            warning.viewCamera = camera;
            warning.raycastTarget = false;
            warning.maskable = false;
            warning.speaker = go.GetComponent<AudioSource>();
            warning.speaker.playOnAwake = false;
            warning.speaker.spatialBlend = 0f;
            warning.speaker.volume = .16f;
            warning.chime = MakeChime();
        }

        private void LateUpdate()
        {
            if (flight == null || !flight.isActiveAndEnabled || viewCamera == null ||
                !Application.isFocused)
            {
                ClearWarning(); return;
            }
            Vector3 p = flight.transform.position;
            Vector2 origin = new Vector2(p.x, p.z);
            if (!FindDanger(origin, p.y, out Vector2 obstacle, out float urgency))
            {
                ClearWarning(); return;
            }
            Vector3 a = viewCamera.WorldToScreenPoint(p);
            Vector3 b = viewCamera.WorldToScreenPoint(new Vector3(obstacle.x, p.y, obstacle.y));
            Vector2 screenDirection = new Vector2(b.x - a.x, b.y - a.y);
            if (screenDirection.sqrMagnitude < Epsilon)
            {
                // Exactly on the boundary the bearing is undefined; retain the
                // last valid direction rather than switching to the flight course.
                screenDirection = danger ? arcDirection : Vector2.up;
            }
            if (screenDirection.sqrMagnitude < Epsilon || a.z <= 0f ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                    new Vector2(a.x,a.y), null, out arcCenter) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                    new Vector2(a.x,a.y) + screenDirection.normalized * 100f, null, out Vector2 localEnd))
            {
                ClearWarning(); return;
            }
            Vector2 localDelta = localEnd - arcCenter;
            arcDirection = localDelta.normalized;
            float unitsPerPixel = localDelta.magnitude / 100f;
            float pixelScale = Mathf.Clamp(Screen.height / 900f, .75f, 1.5f);
            arcRadius = 47f * pixelScale * unitsPerPixel;
            arcWidth = 5.2f * pixelScale * unitsPerPixel;
            float now = Time.unscaledTime;
            if (!danger || now >= nextChime)
            {
                if (speaker != null && chime != null) speaker.PlayOneShot(chime);
                nextChime = now + Mathf.Lerp(.95f, .45f, urgency);
            }
            if (!danger) pulsePhase = Mathf.PI * .5f;
            danger = true;
            pulsePhase = Mathf.Repeat(pulsePhase + Time.unscaledDeltaTime * Mathf.PI * 2f *
                Mathf.Lerp(1.4f, 2.5f, urgency), Mathf.PI * 2f);
            float pulse = .5f + .5f * Mathf.Sin(pulsePhase);
            intensity = Mathf.Lerp(.52f, 1f, pulse);
            SetVerticesDirty();
        }

        // Pure spatial caution: pick the nearest boundary of any marked rock at
        // flight altitude. Heading, throttle and editor fast travel do not affect it.
        private bool FindDanger(Vector2 origin, float altitude, out Vector2 point, out float urgency)
        {
            point = default; urgency = 0f;
            float best = float.PositiveInfinity;
            foreach (var rock in RadarTerrainObstacle.Active)
            {
                if (rock == null || rock.gameObject.scene != flight.gameObject.scene) continue;
                rock.Slice(altitude);
                var edges = rock.Edges;
                for (int i = 0; i + 1 < edges.Count; i += 2)
                {
                    Vector2 nearest = Closest(origin, edges[i], edges[i + 1]);
                    float distanceSq = (origin - nearest).sqrMagnitude;
                    if (distanceSq > SideClearance * SideClearance || distanceSq >= best) continue;
                    best = distanceSq;
                    point = nearest;
                }
            }
            if (float.IsPositiveInfinity(best)) return false;
            urgency = Mathf.Lerp(1f, .5f, Mathf.Sqrt(best) / SideClearance);
            return true;
        }

        private static Vector2 Closest(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float lengthSq = edge.sqrMagnitude;
            return lengthSq < Epsilon ? a : a + edge * Mathf.Clamp01(Vector2.Dot(p - a, edge) / lengthSq);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!danger) return;
            // Dark underlay keeps the signal legible over sand and stone. The
            // foreground is a single continuous, softly tapered HUD accent.
            DrawArc(mesh, arcWidth * 1.75f, new Color(.13f, .055f, .045f, .70f * intensity));
            DrawArc(mesh, arcWidth, new Color(1f, .29f, .20f, intensity));
        }

        private void DrawArc(VertexHelper mesh, float width, Color tint)
        {
            const int segments = 48;
            const float halfAngle = 40f * Mathf.Deg2Rad;
            float angle = Mathf.Atan2(arcDirection.y, arcDirection.x);
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float t = angle + Mathf.Lerp(-halfAngle, halfAngle, u);
                Vector2 d = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                // Rounded-looking tapered ends plus alpha feather on both edges.
                float end = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f-u) / .13f));
                float halfWidth = width * .5f * Mathf.Lerp(.12f, 1f, Mathf.Sqrt(end));
                Color32 solid = new Color(tint.r, tint.g, tint.b, tint.a * end);
                Color32 clear = new Color(tint.r, tint.g, tint.b, 0f);
                int first = mesh.currentVertCount;
                mesh.AddVert(arcCenter + d * (arcRadius - halfWidth), clear, Vector2.zero);
                mesh.AddVert(arcCenter + d * (arcRadius - halfWidth * .72f), solid, Vector2.zero);
                mesh.AddVert(arcCenter + d * (arcRadius + halfWidth * .72f), solid, Vector2.zero);
                mesh.AddVert(arcCenter + d * (arcRadius + halfWidth), clear, Vector2.zero);
                if (i == 0) continue;
                for (int j = 0; j < 3; j++)
                {
                    int a = first - 4 + j, b = first + j;
                    mesh.AddTriangle(a, a + 1, b);
                    mesh.AddTriangle(a + 1, b + 1, b);
                }
            }
        }

        private void ClearWarning()
        {
            if (!danger) return;
            danger = false;
            nextChime = 0f;
            // Let the short, faded chime finish rather than cutting its waveform.
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            ClearWarning();
            if (speaker != null) speaker.Stop();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (chime != null) Destroy(chime);
            base.OnDestroy();
        }

        private static AudioClip MakeChime()
        {
            const int rate = 22050;
            const float duration = .19f;
            int count = Mathf.RoundToInt(rate * duration);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                // Low, soft sine tone; eased attack/release prevent clicks.
                float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .025f)) *
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((duration - t) / .075f));
                samples[i] = Mathf.Sin(2f * Mathf.PI * 520f * t) * envelope * .65f;
            }
            var clip = AudioClip.Create("Soft terrain caution", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
