using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AirCombat
{
    // Local directional terrain warning. Uses the same world-space altitude slice
    // as the radar; no collider, raycast layer, or camera changes are needed.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(260)] // After the flight motor and camera follow.
    public sealed class TerrainCollisionWarning : MaskableGraphic
    {
        private const float LookAheadSeconds = 1.3f;
        private const float SideClearance = 18f;
        private const float SweepClearance = 10f;
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
            Vector3 f = flight.transform.forward;
            Vector2 forward = new Vector2(f.x, f.z).normalized;
            float speed = Mathf.Max(0f, flight.CurrentSpeed);
#if UNITY_EDITOR
            var keyboard = Keyboard.current;
            if (flight.DebugFastTravel && keyboard != null && keyboard.spaceKey.isPressed) speed *= 3f;
#endif
            if (!FindDanger(origin, forward, speed, p.y, out Vector2 obstacle, out float urgency))
            {
                ClearWarning(); return;
            }
            Vector3 a = viewCamera.WorldToScreenPoint(p);
            Vector3 b = viewCamera.WorldToScreenPoint(new Vector3(obstacle.x, p.y, obstacle.y));
            Vector2 screenDirection = new Vector2(b.x - a.x, b.y - a.y);
            if (screenDirection.sqrMagnitude < Epsilon)
            {
                Vector3 ahead = viewCamera.WorldToScreenPoint(p + f * 10f);
                screenDirection = new Vector2(ahead.x - a.x, ahead.y - a.y);
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
            arcRadius = 43f * pixelScale * unitsPerPixel;
            arcWidth = 3.6f * pixelScale * unitsPerPixel;
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
            intensity = Mathf.Lerp(.35f, 1f, pulse);
            SetVerticesDirty();
        }

        private bool FindDanger(Vector2 origin, Vector2 forward, float speed, float altitude,
            out Vector2 point, out float urgency)
        {
            point = default; urgency = 0f;
            float best = float.PositiveInfinity;
            float maxDistance = speed * LookAheadSeconds;
            foreach (var rock in RadarTerrainObstacle.Active)
            {
                if (rock == null || rock.gameObject.scene != flight.gameObject.scene) continue;
                rock.Slice(altitude);
                var edges = rock.Edges;
                for (int i = 0; i + 1 < edges.Count; i += 2)
                {
                    Vector2 a = edges[i], b = edges[i + 1];
                    Vector2 nearest = Closest(origin, a, b);
                    float distance = Vector2.Distance(origin, nearest);
                    if (distance <= SideClearance)
                    {
                        // Immediate lateral proximity takes precedence over projected hits.
                        float score = distance / SideClearance - 2f;
                        if (score < best)
                        {
                            best = score; point = nearest;
                            urgency = Mathf.Lerp(1f, .5f, distance / SideClearance);
                        }
                        continue;
                    }
                    if (best < 0f || maxDistance <= Epsilon || forward.sqrMagnitude < Epsilon) continue;
                    float hit = SweepEdge(origin, forward, a, b, SweepClearance);
                    if (hit > maxDistance) continue;
                    float timeFraction = hit / maxDistance;
                    if (timeFraction < best)
                    {
                        best = timeFraction;
                        point = Closest(origin + forward * hit, a, b);
                        urgency = 1f - timeFraction;
                    }
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        private static Vector2 Closest(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float lengthSq = edge.sqrMagnitude;
            return lengthSq < Epsilon ? a : a + edge * Mathf.Clamp01(Vector2.Dot(p - a, edge) / lengthSq);
        }

        // Earliest intersection of a forward ray with an edge expanded into a
        // capsule. Includes both endpoints, parallel approaches and side grazes.
        private static float SweepEdge(Vector2 origin, Vector2 direction, Vector2 a, Vector2 b, float radius)
        {
            float hit = Mathf.Min(SweepCircle(origin, direction, a, radius),
                SweepCircle(origin, direction, b, radius));
            Vector2 edge = b - a;
            float length = edge.magnitude;
            if (length < Epsilon) return hit;
            Vector2 axis = edge / length;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            float side = Vector2.Dot(origin - a, normal);
            float rate = Vector2.Dot(direction, normal);
            if (Mathf.Abs(rate) < Epsilon) return hit;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float t = (sign * radius - side) / rate;
                if (t < 0f || t >= hit) continue;
                float along = Vector2.Dot(origin + direction * t - a, axis);
                if (along >= 0f && along <= length) hit = t;
            }
            return hit;
        }

        private static float SweepCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius)
        {
            Vector2 offset = origin - center;
            float c = offset.sqrMagnitude - radius * radius;
            if (c <= 0f) return 0f;
            float b = Vector2.Dot(offset, direction);
            float discriminant = b * b - c;
            if (b >= 0f || discriminant < 0f) return float.PositiveInfinity;
            return Mathf.Max(0f, -b - Mathf.Sqrt(discriminant));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!danger) return;
            const int segments = 24;
            const float halfAngle = 34f * Mathf.Deg2Rad;
            float angle = Mathf.Atan2(arcDirection.y, arcDirection.x);
            Color32 tint = new Color(1f, .19f, .13f, intensity);
            for (int i = 0; i <= segments; i++)
            {
                float t = angle + Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                Vector2 d = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                int first = mesh.currentVertCount;
                mesh.AddVert(arcCenter + d * (arcRadius - arcWidth * .5f), tint, Vector2.zero);
                mesh.AddVert(arcCenter + d * (arcRadius + arcWidth * .5f), tint, Vector2.zero);
                if (i > 0)
                {
                    mesh.AddTriangle(first - 2, first - 1, first);
                    mesh.AddTriangle(first - 1, first + 1, first);
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
