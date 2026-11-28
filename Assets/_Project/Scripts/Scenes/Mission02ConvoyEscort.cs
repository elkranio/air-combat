using UnityEngine;

namespace AirCombat
{
    // M02 composition of the existing Warden combat module and CV06 orbit.
    // No SAM deployment, new weapon tuning or mandatory objective policy.
    internal sealed class Mission02ConvoyEscort
    {
        private const int Count = 2, ArcSamples = 256;
        private readonly ConvoyMarchPreview convoy;
        private readonly WardenCombat prefab;
        private readonly AircraftRadar radar;
        private readonly Transform owner;
        private readonly PlayerHitPoints player;
        private readonly FlightController flight;
        private readonly CombatHitFeedback feedback;
        private readonly float lateral, longitudinal, patrolSpeed;
        private readonly float[] acquireDelay, arc = new float[ArcSamples + 1], worldSpeed = new float[Count];
        private readonly WardenCombat[] units = new WardenCombat[Count];
        private float phaseDistance;
        private bool ready;
#if UNITY_EDITOR
        // Temporary movement diagnostics. One sample per placement, logs only
        // route turns, anomalous displacement or the onset of a long frame.
        private readonly bool[] traceValid = new bool[Count], traceHitch = new bool[Count];
        private readonly Vector3[] tracePosition = new Vector3[Count], traceHead = new Vector3[Count],
            traceForward = new Vector3[Count];
        private readonly float[] traceAngle = new float[Count];
        private float traceElapsed;
#endif

        internal Mission02ConvoyEscort(ConvoyMarchPreview convoy, WardenCombat prefab,
            AircraftRadar radar, Transform owner, PlayerHitPoints player, FlightController flight,
            CombatHitFeedback feedback, float lateralRadius, float endClearance, float speed,
            float alphaDelay, float bravoDelay)
        {
            this.convoy = convoy; this.prefab = prefab; this.radar = radar; this.owner = owner;
            this.player = player; this.flight = flight; this.feedback = feedback;
            lateral = Mathf.Max(10f, lateralRadius);
            longitudinal = convoy.HalfLength + Mathf.Max(10f, endClearance);
            patrolSpeed = Mathf.Max(1f, speed);
            acquireDelay = new[] { alphaDelay, bravoDelay };
            Vector2 previous = Offset(0f);
            for (int i = 1; i <= ArcSamples; i++)
            {
                Vector2 next = Offset(2f * Mathf.PI * i / ArcSamples);
                arc[i] = arc[i - 1] + Vector2.Distance(previous, next);
                previous = next;
            }
        }

        internal int AliveCount
        {
            get
            {
                int alive = 0;
                foreach (var unit in units) if (unit != null && unit.Alive) alive++;
                return alive;
            }
        }

        internal bool Spawn()
        {
            for (int i = 0; i < Count; i++)
            {
                var unit = units[i] = Object.Instantiate(prefab, owner);
                unit.name = i == 0 ? "M02 / Warden Alpha" : "M02 / Warden Bravo";
                if (!unit.Bind(player, flight, feedback)) { Dispose(); return false; }
                unit.ResetCombat(Time.time, false, acquireDelay[i]);
                Place(i);
                radar.Register(unit.Contact);
            }
            ready = true;
            return true;
        }

        internal bool TryGetPosition(int index, out Vector3 position)
        {
            position = default;
            if (!ready || index < 0 || index >= Count || units[index] == null || !units[index].Alive) return false;
            position = units[index].transform.position;
            return true;
        }

        internal void TickMovement(float dt)
        {
            if (!ready || dt <= 0f) return;
#if UNITY_EDITOR
            traceElapsed += dt;
#endif
            phaseDistance = Mathf.Repeat(phaseDistance + patrolSpeed * dt, arc[ArcSamples]);
            for (int i = 0; i < Count; i++)
            {
                var unit = units[i];
                if (unit == null) continue;
                if (unit.Alive) Place(i, dt);
                else if (unit.gameObject.activeSelf)
                {
                    ClearRounds(unit.transform);
                    radar.Unregister(unit.Contact);
                    unit.HideDestroyed(worldSpeed[i]);
                }
            }
        }

        internal void TickFire(float now, float dt)
        {
            if (!ready) return;
            foreach (var unit in units) if (unit != null && unit.Alive) unit.TickFire(now, dt);
        }

        private Vector2 Offset(float angle) => new Vector2(-lateral * Mathf.Cos(angle), longitudinal * Mathf.Sin(angle));

        private float Angle(int index)
        {
            float length = arc[ArcSamples];
            float distance = Mathf.Repeat(phaseDistance + index * length * .5f, length);
            int low = 0, high = ArcSamples;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (arc[mid] <= distance) low = mid; else high = mid;
            }
            float fraction = Mathf.InverseLerp(arc[low], arc[high], distance);
            return 2f * Mathf.PI * (low + fraction) / ArcSamples;
        }

        private void Place(int index, float dt = 0f)
        {
            float angle = Angle(index);
            Vector2 offset = Offset(angle);
            Vector3 forward = convoy.Forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 head = convoy.HeadPosition;
            Vector3 p = head - forward * convoy.HalfLength + right * offset.x + forward * offset.y;
            p.y = 78f; // M02 fixed flight plane, not the runway spawn altitude.
            Vector3 tangent = (right * (lateral * Mathf.Sin(angle)) + forward * (longitudinal * Mathf.Cos(angle))).normalized;
            Vector3 velocity = tangent * patrolSpeed + forward * (convoy.AliveCount > 0 ? convoy.Speed : 0f);
            worldSpeed[index] = velocity.magnitude;
#if UNITY_EDITOR
            TracePlacement(index, p, head, forward, angle, dt);
#endif
            units[index].transform.SetPositionAndRotation(p,
                Quaternion.LookRotation(velocity.sqrMagnitude > .01f ? velocity : tangent, Vector3.up));
        }

#if UNITY_EDITOR
        private void TracePlacement(int i, Vector3 p, Vector3 head, Vector3 forward, float angle, float dt)
        {
            bool hitch = dt > .05f;
            if (traceValid[i] && dt > 0f)
            {
                Vector3 before = units[i].transform.position;
                float step = Vector3.Distance(p, tracePosition[i]);
                float external = Vector3.Distance(before, tracePosition[i]);
                float turn = Vector3.SignedAngle(traceForward[i], forward, Vector3.up);
                float headStep = Vector3.Distance(head, traceHead[i]);
                float limit = patrolSpeed * dt + headStep + .15f;
                // Recompute the same orbit point with the prior frame's basis.
                // The difference isolates a heading-induced displacement.
                Vector2 offset = Offset(angle);
                Vector3 oldForward = traceForward[i];
                Vector3 oldBasis = head - oldForward * convoy.HalfLength +
                    Vector3.Cross(Vector3.up, oldForward) * offset.x + oldForward * offset.y;
                oldBasis.y = p.y;
                float basisShift = Vector3.Distance(oldBasis, p);
                bool routeTurn = Mathf.Abs(turn) > .1f;
                bool jump = step > limit || external > .10f;
                if (jump || routeTurn || (hitch && !traceHitch[i]))
                {
                    Debug.Log($"[WARDEN_TRACE] {(i == 0 ? "Alpha" : "Bravo")} " +
                        $"t={traceElapsed:F3}s frame={Time.frameCount} dt={dt:F4} realDt={Time.unscaledDeltaTime:F4} " +
                        $"step={step:F3} limit={limit:F3} external={external:F3} " +
                        $"turn={turn:F2}deg basisShift={basisShift:F3} headStep={headStep:F3} " +
                        $"angle={traceAngle[i]*Mathf.Rad2Deg:F2}->{angle*Mathf.Rad2Deg:F2} " +
                        $"routeRemaining={convoy.RemainingDistance:F2} playerDist={Vector3.Distance(p, player.transform.position):F1} " +
                        $"prev={tracePosition[i].ToString("F3")} before={before.ToString("F3")} next={p.ToString("F3")} " +
                        $"head={head.ToString("F3")} flags={(jump ? "JUMP " : "")}{(routeTurn ? "TURN " : "")}{(hitch ? "HITCH" : "")}", owner);
                }
            }
            traceValid[i] = true;
            traceHitch[i] = hitch;
            tracePosition[i] = p;
            traceHead[i] = head;
            traceForward[i] = forward;
            traceAngle[i] = angle;
        }
#endif

        private static void ClearRounds(Transform source)
        {
            foreach (var round in Object.FindObjectsByType<BallisticProjectile>())
                if (round.IsOwnedBy(source)) { round.gameObject.SetActive(false); Object.Destroy(round.gameObject); }
        }

        internal void Stop()
        {
            ready = false;
            foreach (var unit in units)
                if (unit != null) { unit.StopCombat(); ClearRounds(unit.transform); }
        }

        internal void Dispose()
        {
            Stop();
            for (int i = 0; i < Count; i++)
            {
                var unit = units[i];
                if (unit == null) continue;
                if (unit.Contact != null) radar.Unregister(unit.Contact);
                if (feedback != null && unit.Health != null) feedback.Untrack(unit.Health);
                unit.gameObject.SetActive(false);
                Object.Destroy(unit.gameObject);
                units[i] = null;
            }
        }
    }
}
