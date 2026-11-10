using TurboRush.Track;
using UnityEngine;

namespace TurboRush.Race
{
    /// <summary>
    /// The power-ups on one track: floating pickups (rows of up to three across the road) and boost pads painted on
    /// the asphalt. Plain data + rules, owned by <see cref="RaceSimulation"/> and fully deterministic.
    /// Pickups are personal: when a car takes one it disappears for that car only, so the cars at the front can
    /// never starve the ones behind (the player starts last).
    /// </summary>
    public sealed class PowerUpField
    {
        /// <summary>Kind of each pickup slot.</summary>
        public readonly PowerUpKind[] Kind;
        /// <summary>Lap distance of each pickup slot.</summary>
        public readonly float[] SlotDistance;
        /// <summary>Lateral offset (m) of each pickup slot.</summary>
        public readonly float[] SlotLateral;
        /// <summary>Row index of each pickup slot.</summary>
        public readonly int[] SlotRow;
        /// <summary>Seconds until the slot is available again for each racer: index racer × SlotCount + slot (≤ 0 = available).</summary>
        public readonly float[] Cooldown;

        public readonly float[] PadStart;
        public readonly float[] PadLateral;

        private readonly TrackPath m_Path;
        private readonly RaceTuning m_Tuning;
        private readonly float m_HalfWidth;

        public int SlotCount => Kind.Length;
        public int PadCount => PadStart.Length;
        public int RowCount { get; }
        public int RacerCount { get; }
        public bool Available(int racer, int slot) => Cooldown[racer * Kind.Length + slot] <= 0f;
        public float CooldownOf(int racer, int slot) => Cooldown[racer * Kind.Length + slot];

        public PowerUpField(RaceTrack track, RaceTuning tuning, PowerUpRow[] rows, BoostPad[] pads, int racers)
        {
            RacerCount = Mathf.Max(1, racers);
            m_Path = track.Path;
            m_Tuning = tuning;
            m_HalfWidth = track.HalfWidth;
            rows = rows ?? System.Array.Empty<PowerUpRow>();
            pads = pads ?? System.Array.Empty<BoostPad>();
            RowCount = rows.Length;
            int n = 0;
            foreach (PowerUpRow r in rows)
                for (int s = 0; s < 3; s++) if (r.Slot(s) != PowerUpKind.None) n++;
            Kind = new PowerUpKind[n];
            SlotDistance = new float[n];
            SlotLateral = new float[n];
            SlotRow = new int[n];
            Cooldown = new float[n * RacerCount];
            int i = 0;
            for (int ri = 0; ri < rows.Length; ri++)
                for (int s = 0; s < 3; s++)
                {
                    PowerUpKind k = rows[ri].Slot(s);
                    if (k == PowerUpKind.None) continue;
                    Kind[i] = k;
                    SlotDistance[i] = m_Path.Wrap(rows[ri].lapFraction * m_Path.Length);
                    SlotLateral[i] = PowerUpRow.SlotLane(s) * m_HalfWidth;
                    SlotRow[i] = ri;
                    i++;
                }
            PadStart = new float[pads.Length];
            PadLateral = new float[pads.Length];
            for (int p = 0; p < pads.Length; p++)
            {
                PadStart[p] = m_Path.Wrap(pads[p].lapFraction * m_Path.Length);
                PadLateral[p] = Mathf.Clamp(pads[p].lane, -1f, 1f) * m_HalfWidth;
            }
        }

        public void Reset()
        {
            for (int i = 0; i < Cooldown.Length; i++) Cooldown[i] = 0f;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < Cooldown.Length; i++) if (Cooldown[i] > 0f) Cooldown[i] -= dt;
        }

        /// <summary>True when lap position <paramref name="at"/> lies in (prev, prev + travelled] along the loop.</summary>
        private bool Crossed(float prevDistance, float travelled, float at)
        {
            if (travelled <= 0f) return false;
            float x = m_Path.Wrap(at - m_Path.Wrap(prevDistance));
            return x > 0f && x <= travelled;
        }

        /// <summary>
        /// Checks the racer's movement this step against every pickup and pad. Returns the pickup slot collected
        /// (−1 = none) and whether the racer entered a boost pad.
        /// </summary>
        public int Collect(RacerState r, float prevDistance, out bool enteredPad)
        {
            enteredPad = false;
            float travelled = r.Distance - prevDistance;
            int got = -1;
            if (r.Height < 1.6f)
            {
                int baseIndex = r.Index * Kind.Length;
                for (int i = 0; i < Kind.Length; i++)
                {
                    if (Cooldown[baseIndex + i] > 0f || Mathf.Abs(r.Lateral - SlotLateral[i]) > m_Tuning.pickupReach) continue;
                    if (!Crossed(prevDistance, travelled, SlotDistance[i])) continue;
                    Cooldown[baseIndex + i] = m_Tuning.pickupRespawnSeconds;
                    Apply(r, Kind[i]);
                    got = i;
                    break;
                }
            }
            if (!r.Airborne && r.Height < 0.3f)
            {
                for (int p = 0; p < PadStart.Length; p++)
                {
                    if (Mathf.Abs(r.Lateral - PadLateral[p]) > BoostPad.HalfWidth + 0.4f) continue;
                    float x = m_Path.Wrap(r.Distance - PadStart[p]);
                    if (x > BoostPad.Length) continue;
                    enteredPad = r.PadTimer <= 0f;
                    r.PadTimer = m_Tuning.padSeconds;
                    if (enteredPad) r.PadsHit++;
                    break;
                }
            }
            return got;
        }

        public void Apply(RacerState r, PowerUpKind kind)
        {
            r.PowerUpsCollected++;
            if (kind == PowerUpKind.Turbo) r.TurboTimer = m_Tuning.turboSeconds;
            else if (kind == PowerUpKind.Shield) r.ShieldTimer = m_Tuning.shieldSeconds;
        }

        /// <summary>
        /// AI helper: lateral target (m) of a power-up or pad this racer wants, 12–75 m ahead. Whether a racer goes for
        /// a given row is decided by a fixed hash of (racer, row, lap) against <paramref name="seek"/>, so races
        /// replay identically.
        /// </summary>
        public bool Seek(RacerState r, float seek, out float lateral)
        {
            lateral = 0f;
            if (seek <= 0f || r.Distance < 0f) return false;
            float best = float.MaxValue;
            bool found = false;
            float here = m_Path.Wrap(r.Distance);
            for (int i = 0; i < Kind.Length; i++)
            {
                float ahead = m_Path.Wrap(SlotDistance[i] - here);
                if (ahead < 12f || ahead > 75f) continue;
                // Skip what it cannot get in time or does not need.
                if (CooldownOf(r.Index, i) > ahead / Mathf.Max(5f, r.Speed)) continue;
                if (Kind[i] == PowerUpKind.Shield && r.ShieldTimer > 1f) continue;
                if (Hash01(r.Index, SlotRow[i], r.Lap) >= seek) continue;
                float cost = Mathf.Abs(SlotLateral[i] - r.Lateral) + ahead * 0.02f;
                if (cost < best) { best = cost; lateral = SlotLateral[i]; found = true; }
            }
            for (int p = 0; p < PadStart.Length; p++)
            {
                float ahead = m_Path.Wrap(PadStart[p] - here);
                if (ahead < 12f || ahead > 75f) continue;
                if (Hash01(r.Index, 100 + p, r.Lap) >= seek) continue;
                float cost = Mathf.Abs(PadLateral[p] - r.Lateral) + ahead * 0.02f;
                if (cost < best) { best = cost; lateral = PadLateral[p]; found = true; }
            }
            return found;
        }

        public static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xffff) / 65536f;
            }
        }
    }
}
