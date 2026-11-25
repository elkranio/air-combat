using UnityEngine;

namespace AirCombat
{
    // Player-only, non-spatial cockpit mix. No effect on thrust or fuel.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Mission01LaunchSequence))]
    [DefaultExecutionOrder(140)]
    public sealed class Mission01EngineAudio : MonoBehaviour
    {
        [SerializeField] private FlightController flight;
        [SerializeField] private AudioClip cruiseLoop;
        [SerializeField] private AudioClip boostLoop;
        [Header("Mix (v0)")]
        [SerializeField, Range(0f, 1f)] private float idleVolume = .16f;
        [SerializeField, Range(0f, 1f)] private float cruiseVolume = .38f;
        [SerializeField, Range(0f, 1f)] private float boostVolume = .55f;
        [SerializeField, Min(.02f)] private float attackSeconds = .22f;
        [SerializeField, Min(.02f)] private float releaseSeconds = .35f;
        [SerializeField, Range(.5f, 2f)] private float cruisePitch = 1f;
        [SerializeField, Range(.5f, 2f)] private float boostPitch = 1f;
        private Mission01LaunchSequence launch;
        private Afterburner boost;
        private AudioSource engine, thrust;
        private float blend, engineLevel, pitch;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(FlightController player, AudioClip cruise, AudioClip afterburner)
        { flight = player; cruiseLoop = cruise; boostLoop = afterburner; }
#endif

        private void Awake()
        {
            launch = GetComponent<Mission01LaunchSequence>();
            if (flight == null || cruiseLoop == null || boostLoop == null)
            {
                Debug.LogWarning("AUD-01: missing player or engine clips; run the engine audio installer.", this);
                enabled = false;
                return;
            }
            boost = flight.GetComponent<Afterburner>();
            if (boost == null) { enabled = false; return; }
            engine = CreateSource("Cruise engine", cruiseLoop);
            thrust = CreateSource("Afterburner engine", boostLoop);
            pitch = cruisePitch;
        }

        private AudioSource CreateSource(string objectName, AudioClip clip)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = false;
            source.volume = 0f;
            return source;
        }

        private void LateUpdate()
        {
            if (engine == null || thrust == null || launch == null) return;
            var phase = launch.State;
            bool active = launch.isActiveAndEnabled && (phase == Mission01LaunchSequence.Phase.Ready ||
                phase == Mission01LaunchSequence.Phase.Takeoff || phase == Mission01LaunchSequence.Phase.Flight ||
                phase == Mission01LaunchSequence.Phase.Landing);
            if (!active) { StopLoops(); return; }
            // Tactical map already owns AudioListener.pause and timeScale.
            // Do not restart the playback heads while Unity reports them paused.
            if (AudioListener.pause || TacticalMapView.IsOpenFor(gameObject.scene)) return;
            if (!engine.isPlaying) engine.Play();
            if (!thrust.isPlaying) thrust.Play();

            bool takingOff = phase == Mission01LaunchSequence.Phase.Takeoff;
            bool flying = phase == Mission01LaunchSequence.Phase.Flight;
            bool powered = takingOff || (flying && boost.IsActive);
            float targetBlend = powered ? 1f : 0f;
            float dt = Time.deltaTime;
            blend = Mathf.MoveTowards(blend, targetBlend, dt / Mathf.Max(.02f,
                powered ? attackSeconds : releaseSeconds));
            float targetLevel = phase == Mission01LaunchSequence.Phase.Ready ? idleVolume :
                phase == Mission01LaunchSequence.Phase.Landing ? Mathf.Lerp(idleVolume, cruiseVolume, .35f) : cruiseVolume;
            engineLevel = Mathf.MoveTowards(engineLevel, targetLevel, dt / .3f);
            // Pitch follows the actual motor speed, not the key or debug travel.
            float ratio = flying ? flight.CurrentSpeed / Mathf.Max(1f, flight.CruiseSpeed) : 1f;
            float targetPitch = cruisePitch * (takingOff ? 1.06f : flying ? Mathf.Clamp(1f + (ratio - 1f) * .12f, .9f, 1.12f) : .9f);
            pitch = Mathf.MoveTowards(pitch, targetPitch, dt * .65f);
            // Base stays audible while the extra roar fades over it. Keep headroom.
            engine.volume = engineLevel * Mathf.Lerp(1f, .8f, blend);
            engine.pitch = pitch;
            thrust.volume = boostVolume * Mathf.SmoothStep(0f, 1f, blend);
            thrust.pitch = boostPitch;
        }

        private void StopLoops()
        {
            if (engine != null) { engine.Stop(); engine.volume = 0f; }
            if (thrust != null) { thrust.Stop(); thrust.volume = 0f; }
            blend = engineLevel = 0f;
            pitch = cruisePitch;
        }

        private void OnDisable() => StopLoops();
        private void OnDestroy()
        {
            if (engine != null) Destroy(engine.gameObject);
            if (thrust != null) Destroy(thrust.gameObject);
        }
    }
}
