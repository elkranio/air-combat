using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Props belong to the terrain chunk, sharing its lifetime and material palette.
    internal static class LandscapePropSpawner
    {
        public static void Spawn(GameObject model, Transform parent, Vector3 point,
            float angle, float size, int kind, Material[] palette)
        {
            if (model == null) return;
            var instance = Object.Instantiate(model, parent, false);
            instance.name = model.name;
            var t = instance.transform;
            t.localPosition = point;
            t.localRotation = Quaternion.identity;
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) { Object.Destroy(instance); return; }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float extent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (extent < 0.0001f) { Object.Destroy(instance); return; }
            t.localScale *= size / extent;
            t.localRotation = Quaternion.Euler(0f, angle, 0f);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            t.position += parent.TransformPoint(point) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var name = slots[i] != null ? slots[i].name.ToLowerInvariant() : "";
                    int color = kind == 1 ? LandscapePalette.Stone : LandscapePalette.Leaf;
                    if (name.Contains("bark")) color = LandscapePalette.Bark;
                    else if (name.Contains("leafsdark")) color = LandscapePalette.Pine;
                    else if (name.Contains("grass")) color = kind == 1 ? LandscapePalette.DarkGrass : LandscapePalette.Leaf;
                    slots[i] = palette[color];
                }
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
