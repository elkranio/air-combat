using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(200)] // after aircraft and camera movement
    public sealed class TargetLock : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.01f)] private float lockSeconds = 1.5f;
        [SerializeField, Min(0f)] private float graceSeconds = 1f;
        [SerializeField, Min(0.1f)] private float aimRadius = 8.5f;
        // Optional Weapon Lab rule. Zero by default, so Sandbox behaves as before.
        [SerializeField, Min(0f)] private float minimumLockDistance;
        private Transform rangeOrigin;
        private WeaponLabEnemy labEnemy;
        private bool lockingEnabled = true;
        [SerializeField] private bool soundEnabled = true;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.2f;

        public Transform Target => target;
        public float Progress { get; private set; }
        public bool IsLocked => Progress >= 1f;
        public bool IsAiming { get; private set; }
        public bool IsVisible { get; private set; }
        public bool IsPointerInViewport { get; private set; }
        public int AttemptId { get; private set; }
        public bool IsTooClose { get; private set; }
        public float MinimumLockDistance => minimumLockDistance;
        public bool LockingEnabled => lockingEnabled;
        public bool CanFire => lockingEnabled && !IsTooClose && IsVisible && IsLocked && IsPointerInViewport;
        public Vector2 TargetScreen { get; private set; }
        public float RadiusPixels { get; private set; }
        private Camera viewCamera;
        private float lostTime, beepTimer;
        private AudioSource audioSource;
        private AudioClip acquireClip, lockedClip;

        // Configured at runtime by WeaponLabSession; never changes Sandbox scene values.
        public void ConfigureWeaponLab(Transform lockTarget, Transform player)
        {
            target = lockTarget;
            rangeOrigin = player;
            labEnemy = lockTarget != null ? lockTarget.GetComponent<WeaponLabEnemy>() : null;
            minimumLockDistance = 40f;
            lockSeconds = 1f;
            graceSeconds = .5f;
            SetLockingEnabled(false);
        }

        public void SetLockingEnabled(bool enabled)
        {
            if (lockingEnabled == enabled) return;
            lockingEnabled = enabled;
            IsVisible = IsAiming = IsTooClose = false;
            ResetLock();
        }

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            acquireClip = MakeTone("Acquiring", 720f, 0.07f);
            lockedClip = MakeTone("Locked", 1250f, 0.20f);
        }

        private void LateUpdate()
        {
            var mouse = Mouse.current;
            IsVisible = false;
            IsAiming = false;
            IsTooClose = false;
            if (!lockingEnabled)
            {
                ResetLock();
                return;
            }
            IsPointerInViewport = mouse != null && Application.isFocused &&
                viewCamera.pixelRect.Contains(mouse.position.ReadValue());
            if (target == null || !target.gameObject.activeInHierarchy ||
                (labEnemy != null && labEnemy.IsDown))
            {
                ResetLock();
                return;
            }

            var screen = viewCamera.WorldToScreenPoint(target.position);
            TargetScreen = new Vector2(screen.x, screen.y);
            RadiusPixels = aimRadius * viewCamera.pixelHeight / (2f * viewCamera.orthographicSize);
            IsVisible = screen.z > 0f && viewCamera.pixelRect.Contains(TargetScreen);
            if (IsPointerInViewport && IsVisible)
            {
                var pointer = mouse.position.ReadValue();
                IsAiming = (pointer - TargetScreen).sqrMagnitude <= RadiusPixels * RadiusPixels;
            }

            // Close-range safety zone: reset instantly, never use the 0.5 s aim-loss grace.
            if (rangeOrigin != null && minimumLockDistance > 0f)
            {
                Vector3 separation = target.position - rangeOrigin.position;
                separation.y = 0f;
                IsTooClose = separation.sqrMagnitude < minimumLockDistance * minimumLockDistance;
                if (IsTooClose)
                {
                    ResetLock();
                    return;
                }
            }

            var dt = Time.deltaTime;
            if (IsAiming)
            {
                lostTime = 0f;
                if (IsLocked) return;
                if (Progress <= 0f && dt > 0f) AttemptId++;
                Progress = Mathf.Min(1f, Progress + dt / Mathf.Max(0.01f, lockSeconds));
                if (IsLocked)
                {
                    PlayTone(lockedClip);
                    return;
                }
                beepTimer -= dt;
                if (beepTimer <= 0f)
                {
                    PlayTone(acquireClip);
                    beepTimer = 0.28f;
                }
            }
            else if (Progress > 0f)
            {
                lostTime += dt;
                if (lostTime >= graceSeconds) ResetLock();
            }
        }

        public void ResetLock()
        {
            Progress = 0f;
            lostTime = 0f;
            beepTimer = 0f;
        }

        private void PlayTone(AudioClip clip)
        {
            if (soundEnabled && audioSource != null) audioSource.PlayOneShot(clip, soundVolume);
        }

        private static AudioClip MakeTone(string name, float pitch, float duration)
        {
            const int rate = 22050;
            var data = new float[Mathf.RoundToInt(rate * duration)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Min(1f, Mathf.Min(t / 0.008f, (duration - t) / 0.015f));
                data[i] = Mathf.Sin(2f * Mathf.PI * pitch * t) * envelope * 0.6f;
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (acquireClip != null) Destroy(acquireClip);
            if (lockedClip != null) Destroy(lockedClip);
            if (audioSource != null) Destroy(audioSource);
        }
    }
}
