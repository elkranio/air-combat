using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AirCombat
{
    // Manual visual designation, not missile lock or aim assist.
    // Discover hostile aircraft only on RMB, not on every frame. This avoids
    // coupling the UI to a specific encounter or its spawn/reset lifecycle.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class TargetMarkerHud : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float pickPaddingPx = 16f;
        [SerializeField, Min(16f)] private float minimumPickSizePx = 40f;
        [SerializeField, Min(0f)] private float bracketGapPx = 7f;
        [SerializeField, Min(12f)] private float edgeInsetPx = 22f;
        [Header("Autocannon boresight")]
        [SerializeField, Min(1f)] private float cannonSightDistance = 24f;
        [SerializeField, Min(3f)] private float cannonSightRadiusPx = 6f;

        private sealed class Contact
        {
            public readonly PlayerHitPoints Health;
            public readonly Renderer[] Hull;

            public Contact(PlayerHitPoints health, JetAssembly aircraft)
            {
                Health = health;
                // Capture hull renderers once on spawn, never search the scene
                // while processing RMB or repainting the HUD.
                if (aircraft == null || aircraft.VisualRoot == null)
                {
                    Hull = new Renderer[0];
                    return;
                }
                // Only capture initially active hull geometry. Transient engine
                // plumes may become visible later but must not enlarge the frame.
                var geometry = aircraft.VisualRoot.GetComponentsInChildren<Renderer>(true);
                var parts = new List<Renderer>(geometry.Length);
                foreach (var renderer in geometry)
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                        (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                        parts.Add(renderer);
                Hull = parts.ToArray();
            }
        }

        private Camera viewCamera;
        private PlayerWeaponTrigger cannonLoadout;
        private PlayerHitPoints cannonHealth;
        private Contact selected;
        private AircraftRadar automaticRadar;
        private System.Func<Vector3?> navigationTarget;
        private readonly List<RadarDetection> detections = new List<RadarDetection>();
        private readonly List<Vector2> arrowPositions = new List<Vector2>();

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false; // Never intercept mouse clicks or other HUD controls.
        }

        public void Bind(Camera camera, AircraftRadar radar = null)
        {
            viewCamera = camera;
            automaticRadar = radar;
            selected = null;
            detections.Clear();
            SetVerticesDirty();
        }

        internal void BindCannonSight(PlayerWeaponTrigger weapons, PlayerHitPoints health)
        {
            cannonLoadout = weapons;
            cannonHealth = health;
            SetVerticesDirty();
        }

        // Mission-owned navigation, separate from hostile contacts and radar range.
        internal void BindNavigationTarget(System.Func<Vector3?> target)
        {
            navigationTarget = target;
            SetVerticesDirty();
        }

        private static bool Alive(Contact contact) => contact != null &&
            contact.Health != null && contact.Health.isActiveAndEnabled &&
            contact.Health.gameObject.activeInHierarchy && contact.Health.IsAlive;

        private void Update()
        {
            if (automaticRadar != null) return; // No RMB designation in production tracking test.
            if (selected != null && !Alive(selected))
            {
                selected = null;
                SetVerticesDirty();
            }
            var mouse = Mouse.current;
            if (viewCamera == null || mouse == null || !Application.isFocused ||
                !mouse.rightButton.wasPressedThisFrame) return;

            Vector2 point = mouse.position.ReadValue();
            Contact closest = null;
            float closestDistance = float.PositiveInfinity;
            // One scene discovery per RMB click. Only live hostile aircraft with
            // a configured visual hull are eligible. No magic radar-range lock.
            foreach (var team in FindObjectsByType<AircraftTeam>())
            {
                if (team == null || team.Side != AircraftSide.Hostile) continue;
                var health = team.GetComponent<PlayerHitPoints>();
                var assembly = team.GetComponent<JetAssembly>();
                if (health == null || assembly == null) continue;
                var contact = selected != null && selected.Health == health
                    ? selected : new Contact(health, assembly);
                if (!Alive(contact) || !TryGetBounds(contact, out Rect bounds)) continue;
                // New marks require the target's actual hull to be onscreen.
                if (!bounds.Overlaps(viewCamera.pixelRect, true)) continue;
                Vector2 centre = bounds.center;
                float width = Mathf.Max(bounds.width, minimumPickSizePx) + 2f * pickPaddingPx;
                float height = Mathf.Max(bounds.height, minimumPickSizePx) + 2f * pickPaddingPx;
                Rect hit = new Rect(centre.x - width * .5f, centre.y - height * .5f, width, height);
                if (!hit.Contains(point)) continue;
                float distance = (point - centre).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = contact;
            }
            // RMB on empty space clears the previous designation.
            selected = closest;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (automaticRadar != null)
            {
                automaticRadar.Sample(detections);
                SetVerticesDirty(); // Also clears arrows on kill, reset or range exit.
            }
            else if (selected != null || navigationTarget != null || cannonLoadout != null)
                SetVerticesDirty(); // Also clears the sight when the weapon changes.
        }

        private bool TryGetBounds(Contact contact, out Rect screenBounds)
        {
            screenBounds = default;
            if (viewCamera == null || !Alive(contact)) return false;
            Vector3 centre = viewCamera.WorldToScreenPoint(contact.Health.transform.position);
            if (centre.z <= viewCamera.nearClipPlane || centre.z >= viewCamera.farClipPlane) return false;
            float left = float.PositiveInfinity, bottom = float.PositiveInfinity;
            float right = float.NegativeInfinity, top = float.NegativeInfinity;
            bool found = false;
            foreach (var renderer in contact.Hull)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds b = renderer.bounds;
                // Project the eight box corners so oblique camera pitch and bank
                // affect the clickable shape, without physics-raycast precision.
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = viewCamera.WorldToScreenPoint(corner);
                    if (p.z <= viewCamera.nearClipPlane) continue;
                    left = Mathf.Min(left, p.x);
                    right = Mathf.Max(right, p.x);
                    bottom = Mathf.Min(bottom, p.y);
                    top = Mathf.Max(top, p.y);
                    found = true;
                }
            }
            if (!found)
            {
                left = centre.x - 8f; right = centre.x + 8f;
                bottom = centre.y - 8f; top = centre.y + 8f;
            }
            screenBounds = Rect.MinMaxRect(left, bottom, right, top);
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (viewCamera == null || Screen.width <= 0 || Screen.height <= 0) return;
            arrowPositions.Clear();
            DrawCannonSight(mesh);
            Vector3? destination = navigationTarget?.Invoke();
            if (destination.HasValue)
            {
                Vector3 p = viewCamera.WorldToScreenPoint(destination.Value);
                Vector2 pixel = new Vector2(p.x, p.y);
                if (p.z <= viewCamera.nearClipPlane || p.z >= viewCamera.farClipPlane ||
                    !viewCamera.pixelRect.Contains(pixel))
                    DrawTrackedArrow(mesh, pixel, p.z, new Color(.45f, .9f, 1f, .92f));
            }
            if (automaticRadar != null)
            {
                foreach (var detection in detections)
                {
                    if (!detection.InRange || detection.Kind != RadarContactType.Aircraft) continue;
                    Vector3 p = viewCamera.WorldToScreenPoint(detection.WorldPosition);
                    Vector2 pixel = new Vector2(p.x, p.y);
                    // No bracket, marker or lock for aircraft visible in the viewport.
                    if (p.z > viewCamera.nearClipPlane && p.z < viewCamera.farClipPlane &&
                        viewCamera.pixelRect.Contains(pixel)) continue;
                    DrawTrackedArrow(mesh, pixel, p.z, new Color(1f, .49f, .31f, .92f));
                }
                return;
            }
            if (!Alive(selected)) return;

            Vector3 projected = viewCamera.WorldToScreenPoint(selected.Health.transform.position);
            var screen = new Vector2(projected.x, projected.y);
            if (projected.z > viewCamera.nearClipPlane && viewCamera.pixelRect.Contains(screen))
            {
                if (!TryGetBounds(selected, out Rect bounds)) return;
                Vector2 middle = ScreenToLocal(bounds.center);
                float halfWidth = Mathf.Clamp(bounds.width + bracketGapPx * 2f, 28f, 78f) * .5f;
                float halfHeight = Mathf.Clamp(bounds.height + bracketGapPx * 2f, 28f, 70f) * .5f;
                DrawBrackets(mesh, middle, halfWidth, halfHeight);
                return;
            }

            // Direction in actual camera screen axes, not world north or radar
            // coordinates. Behind the camera, invert the projected bearing.
            Vector2 direction = screen - viewCamera.pixelRect.center;
            if (projected.z <= viewCamera.nearClipPlane) direction = -direction;
            if (direction.sqrMagnitude < .01f) direction = Vector2.down;
            direction.Normalize();

            Rect viewport = viewCamera.pixelRect;
            float halfX = Mathf.Max(1f, viewport.width * .5f - edgeInsetPx);
            float halfY = Mathf.Max(1f, viewport.height * .5f - edgeInsetPx);
            float tx = Mathf.Abs(direction.x) > .0001f
                ? halfX / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float ty = Mathf.Abs(direction.y) > .0001f
                ? halfY / Mathf.Abs(direction.y) : float.PositiveInfinity;
            Vector2 at = ScreenToLocal(viewport.center + direction * Mathf.Min(tx, ty));
            Vector2 wing = new Vector2(-direction.y, direction.x);
            var shadow = new Color(.07f, .09f, .11f, .68f);
            var highlight = new Color(1f, .49f, .31f, .92f);
            Triangle(mesh, at + direction * 10f, at - direction * 8f + wing * 8f,
                at - direction * 8f - wing * 8f, shadow);
            Triangle(mesh, at + direction * 7f, at - direction * 6f + wing * 5f,
                at - direction * 6f - wing * 5f, highlight);
        }

        private void DrawCannonSight(VertexHelper mesh)
        {
            if (cannonLoadout == null || !cannonLoadout.isActiveAndEnabled ||
                (cannonHealth != null && !cannonHealth.IsAlive) ||
                MissionPause.IsTacticalMapPausedFor(cannonLoadout.gameObject.scene)) return;
            var cannon = (cannonLoadout.UsesDualSlots
                ? cannonLoadout.PrimaryHudStatus : cannonLoadout.SelectedHudStatus) as AutocannonWeapon;
            if (cannonLoadout.UsesDualSlots && cannon == null)
                cannon = cannonLoadout.SecondaryHudStatus as AutocannonWeapon;
            if (cannon == null || !cannon.TryGetAimPoint(cannonSightDistance, out Vector3 point)) return;
            Vector3 projected = viewCamera.WorldToScreenPoint(point);
            Vector2 screen = new Vector2(projected.x, projected.y);
            if (projected.z <= viewCamera.nearClipPlane || projected.z >= viewCamera.farClipPlane ||
                !viewCamera.pixelRect.Contains(screen)) return;
            // Fixed screen size, hollow center and dark edge for bright terrain.
            DrawSightRing(mesh, screen, cannonSightRadiusPx, 3.5f, new Color(.06f, .09f, .11f, .68f));
            DrawSightRing(mesh, screen, cannonSightRadiusPx, 1.5f, new Color(.91f, 1f, .96f, .92f));
        }

        private void DrawSightRing(VertexHelper mesh, Vector2 centre, float radius, float width, Color tint)
        {
            const int segments = 24;
            float inner = Mathf.Max(.1f, radius - width * .5f);
            float outer = radius + width * .5f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * (2f * Mathf.PI / segments);
                float b = (i + 1) * (2f * Mathf.PI / segments);
                Vector2 u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 v = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int start = mesh.currentVertCount;
                AddVertex(mesh, ScreenToLocal(centre + u * inner), tint);
                AddVertex(mesh, ScreenToLocal(centre + u * outer), tint);
                AddVertex(mesh, ScreenToLocal(centre + v * outer), tint);
                AddVertex(mesh, ScreenToLocal(centre + v * inner), tint);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private void DrawTrackedArrow(VertexHelper mesh, Vector2 screen, float depth, Color highlight)
        {
            Rect viewport = viewCamera.pixelRect;
            Vector2 direction = screen - viewport.center;
            // Orthographic projection preserves the bearing even behind the camera.
            if (!viewCamera.orthographic && depth <= viewCamera.nearClipPlane) direction = -direction;
            if (direction.sqrMagnitude < .01f) direction = Vector2.down;
            direction.Normalize();
            float halfX = Mathf.Max(1f, viewport.width * .5f - edgeInsetPx);
            float halfY = Mathf.Max(1f, viewport.height * .5f - edgeInsetPx);
            float tx = Mathf.Abs(direction.x) > .0001f ? halfX / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float ty = Mathf.Abs(direction.y) > .0001f ? halfY / Mathf.Abs(direction.y) : float.PositiveInfinity;
            Vector2 at = ScreenToLocal(viewport.center + direction * Mathf.Min(tx, ty));
            Vector2 centre = ScreenToLocal(viewport.center);
            Vector2 inward = (centre - at).normalized;
            // Keep two nearly identical bearings readable without changing their direction.
            for (int lane = 0; lane < arrowPositions.Count; lane++)
            {
                bool overlap = false;
                foreach (var previous in arrowPositions)
                    if ((at - previous).sqrMagnitude < 22f * 22f) { overlap = true; break; }
                if (!overlap) break;
                at += inward * 24f;
            }
            arrowPositions.Add(at);
            Vector2 wing = new Vector2(-direction.y, direction.x);
            var shadow = new Color(.07f, .09f, .11f, .68f);
            Triangle(mesh, at + direction * 10f, at - direction * 8f + wing * 8f,
                at - direction * 8f - wing * 8f, shadow);
            Triangle(mesh, at + direction * 7f, at - direction * 6f + wing * 5f,
                at - direction * 6f - wing * 5f, highlight);
        }

        private Vector2 ScreenToLocal(Vector2 screen)
        {
            Rect r = rectTransform.rect;
            return new Vector2(r.xMin + screen.x * (r.width / Screen.width),
                r.yMin + screen.y * (r.height / Screen.height));
        }

        private void DrawBrackets(VertexHelper mesh, Vector2 centre, float halfWidth, float halfHeight)
        {
            var color = new Color(1f, .48f, .29f, .84f);
            var shadow = new Color(.06f, .09f, .11f, .58f);
            float corner = 8f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector2 at = centre + new Vector2(sx * halfWidth, sy * halfHeight);
                Vector2 xEnd = at - Vector2.right * (sx * corner);
                Vector2 yEnd = at - Vector2.up * (sy * corner);
                Segment(mesh, at, xEnd, 3.5f, shadow);
                Segment(mesh, at, yEnd, 3.5f, shadow);
                Segment(mesh, at, xEnd, 1.7f, color);
                Segment(mesh, at, yEnd, 1.7f, color);
            }
        }

        private void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < .0001f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * .5f);
            int start = mesh.currentVertCount;
            AddVertex(mesh, a - n, tint);
            AddVertex(mesh, b - n, tint);
            AddVertex(mesh, b + n, tint);
            AddVertex(mesh, a + n, tint);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = mesh.currentVertCount;
            AddVertex(mesh, a, tint);
            AddVertex(mesh, b, tint);
            AddVertex(mesh, c, tint);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private void AddVertex(VertexHelper mesh, Vector2 point, Color tint)
        {
            tint.a *= color.a;
            UIVertex v = UIVertex.simpleVert;
            v.position = point;
            v.color = tint;
            mesh.AddVert(v);
        }
    }
}
