using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Shared blast damage policy for ground-domain targets.
    // Weapons own their radii; this helper applies each blast once per target.
    internal static class GroundBlastDamage
    {
        public static int Apply(Vector3 point, float radius, int damage)
        {
            int hitCount = 0;
            var visited = new HashSet<DamageableTarget>();
            foreach (var collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<DamageableTarget>();
                if (target == null || target.Domain != CombatTargetDomain.Ground ||
                    !visited.Add(target)) continue;
                if (target.ApplyDamage(point, damage)) hitCount++;
            }
            return hitCount;
        }
    }
}
