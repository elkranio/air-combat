using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Lightweight temporary feedback; no baked illumination or world decals.
    public sealed class BombImpactFx : MonoBehaviour
    {
        private Transform flash;
        private float elapsed;
        private float radius;

        public static GameObject Spawn(Vector3 point, Material flashMaterial, Material scorchMaterial, float blastRadius)
        {
            var root = new GameObject("Bomb impact");
            root.transform.position = point + Vector3.up * .12f;
            var fx = root.AddComponent<BombImpactFx>();
            fx.radius = blastRadius;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Flash";
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localScale = Vector3.one * .1f;
            var flashCollider = sphere.GetComponent<Collider>();
            flashCollider.enabled = false;
            Object.Destroy(flashCollider);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = flashMaterial;
            sphere.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            fx.flash = sphere.transform;

            var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mark.name = "Temporary scorch";
            mark.transform.SetParent(root.transform, false);
            mark.transform.localPosition = new Vector3(0, -.08f, 0);
            mark.transform.localScale = new Vector3(6f, .035f, 6f);
            var scorchCollider = mark.GetComponent<Collider>();
            scorchCollider.enabled = false;
            Object.Destroy(scorchCollider);
            mark.GetComponent<MeshRenderer>().sharedMaterial = scorchMaterial;
            mark.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (flash != null)
            {
                if (elapsed < .28f)
                    flash.localScale = Vector3.one * Mathf.Lerp(.2f, radius * 1.25f, elapsed / .28f);
                else flash.gameObject.SetActive(false);
            }
            if (elapsed >= 15f) Destroy(gameObject);
        }
    }
}
