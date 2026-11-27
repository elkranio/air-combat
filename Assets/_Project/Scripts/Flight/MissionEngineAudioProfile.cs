using UnityEngine;

namespace AirCombat
{
    [CreateAssetMenu(menuName = "Air Combat/Engine Audio Profile")]
    public sealed class MissionEngineAudioProfile : ScriptableObject
    {
        public AudioClip cruiseLoop;
        public AudioClip boostLoop;
        [Range(0f, 1f)] public float idleVolume = .16f;
        [Range(0f, 1f)] public float cruiseVolume = .38f;
        [Range(0f, 1f)] public float boostVolume = .55f;
        [Min(.02f)] public float attackSeconds = .22f;
        [Min(.02f)] public float releaseSeconds = .35f;
        [Range(.5f, 2f)] public float cruisePitch = 1f;
        [Range(.5f, 2f)] public float boostPitch = 1f;
    }
}
