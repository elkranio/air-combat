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
            phaseDistance = Mathf.Repeat(phaseDistance + patrolSpeed * dt, arc[ArcSamples]);
            for (int i = 0; i < Count; i++)
            {
                var unit = units[i];
                if (unit == null) continue;
                if (unit.Alive) Place(i);
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

        private void Place(int index)
        {
            float angle = Angle(index);
            Vector2 offset = Offset(angle);
            Vector3 forward = convoy.Forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 p = convoy.HeadPosition - forward * convoy.HalfLength + right * offset.x + forward * offset.y;
            p.y = 78f; // M02 fixed flight plane, not the runway spawn altitude.
            Vector3 tangent = (right * (lateral * Mathf.Sin(angle)) + forward * (longitudinal * Mathf.Cos(angle))).normalized;
            Vector3 velocity = tangent * patrolSpeed + forward * (convoy.AliveCount > 0 ? convoy.Speed : 0f);
            worldSpeed[index] = velocity.magnitude;
            units[index].transform.SetPositionAndRotation(p,
                Quaternion.LookRotation(velocity.sqrMagnitude > .01f ? velocity : tangent, Vector3.up));
        }

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
