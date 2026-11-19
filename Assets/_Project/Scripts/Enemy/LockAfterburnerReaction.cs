using UnityEngine;

namespace AirCombat
{
    // Temporary sandbox reaction policy, independent of cruise/catch-up navigation.
    [DisallowMultipleComponent]
    public sealed class LockAfterburnerReaction : MonoBehaviour
    {
        [SerializeField] private TargetLock incomingLock;
        [SerializeField] private Afterburner afterburner;
        [SerializeField, Range(0f, 1f)] private float reactionChance = 0.6f;
        [SerializeField, Min(0f)] private float reactionDelay = 0.4f;
        [SerializeField, Min(0f)] private float minBurstSeconds = 1f;
        [SerializeField, Min(0f)] private float maxBurstSeconds = 2f;

        private float aimedTime, burstRemaining;
        private int lastAttempt = -1;
        private bool decided;

        private void OnEnable()
        {
            aimedTime = burstRemaining = 0f;
            lastAttempt = -1;
            decided = false;
        }

        public bool WantsBoost(float dt)
        {
            if (!isActiveAndEnabled || afterburner == null) return false;
            bool threatened = incomingLock != null && incomingLock.Target == transform && incomingLock.Progress > 0f;
            if (threatened && lastAttempt != incomingLock.AttemptId)
            {
                lastAttempt = incomingLock.AttemptId;
                aimedTime = 0f;
                decided = false;
            }
            if (threatened && !decided && incomingLock.IsAiming)
            {
                aimedTime += dt;
                if (aimedTime >= reactionDelay)
                {
                    decided = true; // one roll per attempt, even during the grace period
                    if (burstRemaining <= 0f && afterburner.Charge01 > 0f && Random.value < reactionChance)
                        burstRemaining = Random.Range(minBurstSeconds, Mathf.Max(minBurstSeconds, maxBurstSeconds));
                }
            }
            if (burstRemaining <= 0f || afterburner.Charge01 <= 0f)
            {
                burstRemaining = 0f;
                return false;
            }
            burstRemaining = Mathf.Max(0f, burstRemaining - dt);
            return true;
        }
    }
}
