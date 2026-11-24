using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Encounter supplies candidates once per spawn/removal; no scene-wide scans.
    public sealed class TargetDesignation
    {
        private readonly Dictionary<AirDamageableTarget, List<Renderer>> candidates =
            new Dictionary<AirDamageableTarget, List<Renderer>>();

        public void Register(AirDamageableTarget health)
        {
            if (health == null || candidates.ContainsKey(health)) return;
            var jet = health.GetComponent<JetAssembly>();
            Transform visual = jet != null ? jet.VisualRoot : health.transform;
            if (visual == null) return;
            var hull = new List<Renderer>();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(false))
                if (renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    hull.Add(renderer);
            candidates.Add(health, hull);
        }

        public void Unregister(AirDamageableTarget health)
        {
            if (health != null) candidates.Remove(health);
        }

        public bool TryBounds(Camera camera, AirDamageableTarget health, out Rect bounds)
        {
            bounds = default;
            if (camera == null || !WeaponLock.Alive(health) || !candidates.TryGetValue(health, out var hull)) return false;
            Vector3 centre = camera.WorldToScreenPoint(health.transform.position);
            if (centre.z <= camera.nearClipPlane || centre.z >= camera.farClipPlane ||
                !camera.pixelRect.Contains(new Vector2(centre.x, centre.y))) return false;
            float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity;
            float x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
            foreach (var renderer in hull)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds b = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = camera.WorldToScreenPoint(renderer.transform.TransformPoint(new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z)));
                    if (p.z <= camera.nearClipPlane) continue;
                    x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y);
                    x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
                }
            }
            if (float.IsInfinity(x0)) return false;
            bounds = Rect.MinMaxRect(x0, y0, x1, y1);
            return true;
        }

        private static Rect PickRect(Rect bounds, float scale)
        {
            float w = Mathf.Max(bounds.width, 24f * scale) + 12f * scale;
            float h = Mathf.Max(bounds.height, 24f * scale) + 12f * scale;
            return new Rect(bounds.center - new Vector2(w, h) * .5f, new Vector2(w, h));
        }

        public AirDamageableTarget Pick(Camera camera, Vector2 pointer, AirDamageableTarget current)
        {
            if (camera == null || !camera.pixelRect.Contains(pointer)) return null;
            float scale = Mathf.Clamp(camera.pixelHeight / 720f, .75f, 3f);
            // Keep the current target only while its own pick area contains the cursor.
            if (TryBounds(camera, current, out Rect old) && PickRect(old, scale).Contains(pointer)) return current;
            AirDamageableTarget closest = null;
            float best = float.PositiveInfinity;
            foreach (var pair in candidates)
            {
                if (!TryBounds(camera, pair.Key, out Rect b) || !PickRect(b, scale).Contains(pointer)) continue;
                float d = (b.center - pointer).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                closest = pair.Key;
            }
            return closest;
        }
    }
}
