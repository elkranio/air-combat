using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Arcade cold-launch projectile. Only the committed horizontal flight is
    // damaging. Powered flight permits a small, finite horizontal correction.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(190)]
    public sealed class MantisVerticalRocket : MonoBehaviour
    {
        private enum Phase { Ejection, Align, Ignition, Flight, Puff }
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private static int syncedFrame = -1;
        private Phase phase;
        private Transform owner;
        private PlayerHitPoints target;
        private Transform body;
        private LineRenderer exhaust, glow, ejectSmoke, jetFront, jetRear;
        private TrailRenderer trail;
        private float height, ejectSeconds, alignSeconds, ignitionSeconds, speed, remaining, age;
        private float riseDistance, riseEndSpeed, riseStartSpeed, coastSpeed, flightAge;
        private float correctionRate, correctionRemaining, steering;
        private Vector3 launchPoint, horizontalAim;
        private int damage;
        private Vector3 heading;
        private Action onHit;
        private bool launched;

        public void Launch(Transform shooter, PlayerHitPoints player, float ejectionTime, float alignmentTime,
            float accelerationTime, float cruiseSpeed, float range, int hitDamage,
            float turnRate, float turnBudget, Material hull, Material effects, Action hitCallback)
        {
            owner = shooter; target = player; height = player.transform.position.y;
            ejectSeconds = Mathf.Max(.1f, ejectionTime);
            alignSeconds = Mathf.Max(.1f, alignmentTime);
            ignitionSeconds = Mathf.Max(.05f, accelerationTime);
            speed = Mathf.Max(1f, cruiseSpeed); remaining = Mathf.Max(1f, range);
            damage = hitDamage; onHit = hitCallback;
            correctionRate = Mathf.Max(0f, turnRate);
            correctionRemaining = Mathf.Max(0f, turnBudget);
            launchPoint = transform.position;
            height = Mathf.Max(launchPoint.y, height);
            float climb = height - launchPoint.y;
            // Two continuous velocity segments. The final part of the climb
            // overlaps the attitude turn, rather than stopping at the plane.
            float turnRise = Mathf.Min(climb * .10f, climb * alignSeconds / (2f * ejectSeconds + alignSeconds));
            riseDistance = climb - turnRise;
            riseEndSpeed = 2f * turnRise / alignSeconds;
            riseStartSpeed = 2f * riseDistance / ejectSeconds - riseEndSpeed;
            coastSpeed = speed * .08f;
            heading = Vector3.up;
            horizontalAim = Vector3.forward;
            phase = Phase.Ejection;
            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Rocket body";
            model.transform.SetParent(transform, false);
            model.transform.localScale = new Vector3(1.1f, 2.6f, 1.1f);
            var collider = model.GetComponent<Collider>();
            collider.enabled = false; Destroy(collider);
            var renderer = model.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = hull;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            body = model.transform;
            exhaust = MakeLine("Motor core", effects, 1.1f);
            glow = MakeLine("Motor glow", effects, 2.3f);
            ejectSmoke = MakeLine("Cold ejection smoke", effects, 3f);
            jetFront = MakeLine("Attitude jet / nose", effects, .65f);
            jetRear = MakeLine("Attitude jet / tail", effects, .65f);
            var trailObject = new GameObject("Rocket smoke");
            trailObject.transform.SetParent(transform, false);
            trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = effects;
            trail.time = .42f;
            trail.minVertexDistance = 1f;
            trail.startWidth = 1.8f; trail.endWidth = .25f;
            trail.startColor = new Color(.69f,.71f,.68f,.64f);
            trail.endColor = new Color(.55f,.57f,.54f,0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.Clear(); trail.emitting = false;
            // Cold-launch experiment intentionally silent. Never reuse Flak audio.
            launched = true;
            DrawMotor();
        }

        private LineRenderer MakeLine(string label, Material material, float width)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true; line.positionCount = 2;
            line.widthMultiplier = width; line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void LateUpdate()
        {
            if (!launched) return;
            float dt = Time.deltaTime;
            if (phase == Phase.Puff)
            {
                age += dt;
                if (age >= .55f) { Destroy(gameObject); return; }
                DrawPuff(); return;
            }
            // Carry leftover frame time across phase boundaries. The launch is
            // a continuous trajectory even at low frame rates.
            while (dt > .000001f && phase != Phase.Puff)
            {
                if (phase == Phase.Ejection)
                {
                    float slice = Mathf.Min(dt, ejectSeconds - age);
                    age += slice; dt -= slice;
                    Vector3 p = launchPoint;
                    p.y += riseStartSpeed * age + .5f * (riseEndSpeed - riseStartSpeed) / ejectSeconds * age * age;
                    transform.position = p;
                    if (age >= ejectSeconds - .000001f)
                    { phase = Phase.Align; age = 0f; }
                    continue;
                }
                if (phase == Phase.Align)
                {
                    if (target == null || !target.IsAlive) { BeginPuff(transform.position); return; }
                    float slice = Mathf.Min(dt, alignSeconds - age);
                    float before = age / alignSeconds;
                    age += slice; dt -= slice;
                    float t = Mathf.Clamp01(age / alignSeconds);
                    Vector3 delta = target.transform.position - transform.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > .0001f) horizontalAim = delta.normalized;
                    // Decelerate the vertical ejection to zero while adding a
                    // small sideways drift; main engine is not active yet.
                    Vector3 p = transform.position + horizontalAim * (coastSpeed * alignSeconds * .5f * (t*t - before*before));
                    p.y = launchPoint.y + riseDistance + riseEndSpeed * alignSeconds * (t - .5f*t*t);
                    transform.position = p;
                    heading = Vector3.Slerp(Vector3.up, horizontalAim, Mathf.SmoothStep(0f, 1f, t)).normalized;
                    if (age >= alignSeconds - .000001f)
                    {
                        delta = target.transform.position - transform.position; delta.y = 0f;
                        if (delta.sqrMagnitude > .0001f) horizontalAim = delta.normalized;
                        heading = horizontalAim; // initial powered heading; no predictive lead
                        p.y = height; transform.position = p;
                        phase = Phase.Ignition; age = 0f; flightAge = 0f;
                        trail.Clear(); trail.emitting = true;
                    }
                    continue;
                }
                if (phase == Phase.Ignition)
                {
                    float slice = Mathf.Min(dt, ignitionSeconds - age);
                    float a = age; age += slice; dt -= slice;
                    float distance = coastSpeed * slice + .5f * (speed - coastSpeed) / ignitionSeconds * (age*age - a*a);
                    flightAge += slice;
                    if (!MoveCommitted(distance, slice)) return;
                    if (age >= ignitionSeconds - .000001f) { phase = Phase.Flight; age = 0f; }
                    continue;
                }
                flightAge += dt;
                if (!MoveCommitted(speed * dt, dt)) return;
                dt = 0f;
            }
            DrawMotor();
        }

        private bool MoveCommitted(float distance, float dt)
        {
            // No change in flight height, no lead, no unlimited pursuit. Once
            // the total angular budget is spent the rocket flies straight.
            steering = 0f;
            if (target != null && target.IsAlive && correctionRate > 0f && correctionRemaining > .001f && dt > 0f)
            {
                Vector3 delta = target.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > .0001f)
                {
                    float error = Vector3.SignedAngle(heading, delta, Vector3.up);
                    float limit = Mathf.Min(correctionRemaining, correctionRate * dt);
                    float turn = Mathf.Clamp(error, -limit, limit);
                    heading = (Quaternion.AngleAxis(turn, Vector3.up) * heading).normalized;
                    correctionRemaining = Mathf.Max(0f, correctionRemaining - Mathf.Abs(turn));
                    steering = turn / (correctionRate * dt);
                }
            }
            if (syncedFrame != Time.frameCount) { Physics.SyncTransforms(); syncedFrame = Time.frameCount; }
            float step = Mathf.Min(remaining, distance);
            Vector3 start = transform.position;
            if (step > .0001f)
            {
                int count = Physics.RaycastNonAlloc(start, heading, Hits, step, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                RaycastHit closest = default;
                for (int i = 0; i < count; i++)
                {
                    var hit = Hits[i];
                    if (hit.collider == null || hit.distance >= nearest ||
                        (owner != null && hit.collider.transform.IsChildOf(owner))) continue;
                    nearest = hit.distance; closest = hit;
                }
                if (!float.IsPositiveInfinity(nearest))
                {
                    if (target != null && closest.collider.GetComponentInParent<PlayerHitPoints>() == target &&
                        target.ApplyDamage(closest.point, damage)) onHit?.Invoke();
                    BeginPuff(closest.point); return false;
                }
            }
            transform.position = start + heading * step;
            remaining -= step;
            if (remaining <= .001f) { BeginPuff(transform.position); return false; }
            return true;
        }

        private void DrawMotor()
        {
            body.rotation = Quaternion.FromToRotation(Vector3.up, heading);
            bool powered = phase == Phase.Ignition || phase == Phase.Flight;
            exhaust.enabled = glow.enabled = powered;
            jetFront.enabled = jetRear.enabled = phase == Phase.Align || (powered && Mathf.Abs(steering) > .02f);
            float sinceLaunch = phase == Phase.Ejection ? age : ejectSeconds + age;
            ejectSmoke.enabled = phase == Phase.Ejection || (phase == Phase.Align && sinceLaunch < .64f);
            if (ejectSmoke.enabled)
            {
                float t = Mathf.Clamp01(sinceLaunch / .64f);
                Vector3 p = launchPoint + Vector3.up * (t * 1.5f);
                SetLine(ejectSmoke, p - Vector3.right * (1f+t*2f), p + Vector3.right * (1f+t*2f),
                    Mathf.Lerp(3f, 8f, t), new Color(.65f,.64f,.59f,.55f*(1f-t)));
            }
            if (phase == Phase.Align)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, horizontalAim).normalized;
                Vector3 tangent = Vector3.Cross(axis, heading).normalized;
                Vector3 front = transform.position + heading * 1.5f;
                Vector3 rear = transform.position - heading * 1.5f;
                float pulse = .65f + .35f * Mathf.Abs(Mathf.Sin(age * 65f));
                Color hot = new Color(1f,.77f,.35f,.9f);
                SetLine(jetFront, front - tangent*.55f, front - tangent*(.55f+3.5f*pulse), .9f, hot);
                SetLine(jetRear, rear + tangent*.55f, rear + tangent*(.55f+3.5f*pulse), .9f, hot);
            }
            if (!powered) return;
            if (jetFront.enabled)
            {
                Vector3 side = Vector3.Cross(Vector3.up, heading).normalized * Mathf.Sign(steering);
                Vector3 front = transform.position + heading * 1.5f;
                Vector3 rear = transform.position - heading * 1.5f;
                float jetLength = Mathf.Lerp(.5f, 2.3f, Mathf.Abs(steering));
                Color hot = new Color(1f, .77f, .35f, .8f);
                SetLine(jetFront, front - side * .55f, front - side * (.55f + jetLength), .65f, hot);
                SetLine(jetRear, rear + side * .55f, rear + side * (.55f + jetLength), .65f, hot);
            }
            Vector3 nozzle = transform.position - heading * 2.4f;
            float length = Mathf.Lerp(16f, 7f, Mathf.Clamp01(flightAge / .24f));
            SetLine(exhaust, nozzle - heading * length, nozzle, 1.15f, new Color(1f,.91f,.55f,1f));
            SetLine(glow, nozzle - heading * length, nozzle, 2.7f, new Color(1f,.43f,.10f,.50f));
        }

        private void BeginPuff(Vector3 p)
        {
            transform.position = p; phase = Phase.Puff; age = 0f;
            body.gameObject.SetActive(false); trail.emitting = false;
            ejectSmoke.enabled = jetFront.enabled = jetRear.enabled = false;
            exhaust.enabled = glow.enabled = true;
            DrawPuff();
        }

        private void DrawPuff()
        {
            float t = Mathf.Clamp01(age / .55f);
            Vector3 p = transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, heading).normalized;
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            float r = Mathf.Lerp(1.5f, 5f, t);
            SetLine(glow, p - side * r * .25f, p + side * r * .25f, r * 2f,
                new Color(.46f,.43f,.39f,(1f-t)*.65f));
            SetLine(exhaust, p - side * .15f, p + side * .15f, Mathf.Lerp(7f,1f,t),
                new Color(1f,.82f,.36f,Mathf.Clamp01(1f-age/.13f)));
        }

        private static void SetLine(LineRenderer line, Vector3 a, Vector3 b, float width, Color color)
        {
            line.SetPosition(0,a); line.SetPosition(1,b); line.widthMultiplier = width;
            Color tail = color; tail.a *= .45f;
            line.startColor = tail; line.endColor = color;
        }
    }
}
