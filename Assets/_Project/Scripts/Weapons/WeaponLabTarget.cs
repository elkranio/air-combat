using UnityEngine;

namespace AirCombat
{
    // Passive target with an immediate hit flash. No AI, health, combat or destruction.
    [DisallowMultipleComponent]
    public sealed class WeaponLabTarget : MonoBehaviour
    {
        [SerializeField] private Renderer[] visibleParts;
        private MaterialPropertyBlock flash;
        private float flashEnd;
        private bool flashing;

        public void Configure(Renderer[] parts) { visibleParts = parts; }

        public void Hit(Vector3 position)
        {
            flashEnd = Time.time + .12f;
            if (flash == null) flash = new MaterialPropertyBlock();
            flash.Clear();
            flash.SetColor("_BaseColor", new Color(1f, .93f, .5f));
            if (visibleParts != null)
                foreach (var r in visibleParts)
                    if (r != null) r.SetPropertyBlock(flash);
            flashing = true;
        }

        private void Update()
        {
            if (!flashing || Time.time < flashEnd) return;
            if (visibleParts != null)
                foreach (var r in visibleParts)
                    if (r != null) r.SetPropertyBlock(null);
            flashing = false;
        }
    }
}
