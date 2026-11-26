using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Time and listener pause are global Unity state, so ownership is exclusive.
    // Clients may release only their own acquisition. No scene or prefab data.
    internal static class MissionPause
    {
        internal enum Reason { TacticalMap, BoundaryFailure }

        private static MonoBehaviour owner;
        private static Scene ownerScene;
        private static Reason reason;
        private static float previousTimeScale;
        private static bool previousAudioPause;

        internal static bool CanOpenTacticalMap => owner == null && Time.timeScale > 0f;

        internal static bool IsTacticalMapPausedFor(Scene scene) =>
            owner != null && ownerScene == scene && reason == Reason.TacticalMap;

        internal static bool IsOwnedBy(MonoBehaviour requester, Reason requestedReason) =>
            !object.ReferenceEquals(owner, null) && object.ReferenceEquals(owner, requester) && reason == requestedReason;

        internal static bool TryAcquire(MonoBehaviour requester, Reason requestedReason)
        {
            if (requester == null || owner != null ||
                (requestedReason == Reason.TacticalMap && !CanOpenTacticalMap)) return false;
            owner = requester;
            ownerScene = requester.gameObject.scene;
            reason = requestedReason;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            return true;
        }

        internal static void Release(MonoBehaviour requester)
        {
            // ReferenceEquals also recognizes the owner during Unity destruction.
            if (object.ReferenceEquals(owner, null) || !object.ReferenceEquals(owner, requester)) return;
            owner = null;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            owner = null;
            ownerScene = default;
            reason = default;
            previousTimeScale = 1f;
            previousAudioPause = false;
        }
    }

    // An accepted retry is a scene-local, one-frame signal, not a second input
    // source. The controller publishes it once; consumers keep their tick order.
    internal static class MissionRetry
    {
        private sealed class Entry
        {
            internal MonoBehaviour Owner;
            internal int Frame = -1;
        }

        private static readonly Dictionary<SceneHandle, Entry> entries = new Dictionary<SceneHandle, Entry>();

        internal static bool Register(MonoBehaviour owner)
        {
            SceneHandle key = owner.gameObject.scene.handle;
            if (entries.TryGetValue(key, out Entry existing) && existing.Owner != null)
            {
                if (existing.Owner == owner) return true;
                Debug.LogError("Mission retry: only one lifecycle owner is allowed per scene.", owner);
                return false;
            }
            entries[key] = new Entry { Owner = owner };
            return true;
        }

        internal static void Unregister(MonoBehaviour owner)
        {
            SceneHandle key = owner.gameObject.scene.handle;
            if (entries.TryGetValue(key, out Entry entry) && object.ReferenceEquals(entry.Owner, owner))
                entries.Remove(key);
        }

        internal static bool IsOwnedFor(Scene scene) =>
            entries.TryGetValue(scene.handle, out Entry entry) && entry.Owner != null && entry.Owner.isActiveAndEnabled;

        internal static void MarkRequested(MonoBehaviour owner)
        {
            if (entries.TryGetValue(owner.gameObject.scene.handle, out Entry entry) && entry.Owner == owner)
                entry.Frame = Time.frameCount;
        }

        internal static bool WasRequestedThisFrame(Scene scene) =>
            entries.TryGetValue(scene.handle, out Entry entry) && entry.Owner != null &&
            entry.Owner.isActiveAndEnabled && entry.Frame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => entries.Clear();
    }
}
