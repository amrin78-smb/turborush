using System;
using TurboRush.AI;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEngine;

namespace TurboRush.Race
{
    public enum SurfaceZone { Road, Shoulder, Runoff }

    /// <summary>Track geometry + rules the simulation needs (no Unity objects, so it is unit-testable).</summary>
    public sealed class RaceTrack
    {
        public readonly TrackPath Path;
        public readonly float HalfWidth;
        public readonly float ShoulderWidth;
        public readonly float RunoffWidth;
        public readonly int Laps;
        public readonly int CheckpointsPerLap;
        private readonly LapRange[] m_Open;
        private readonly float[] m_JumpStart;  // lap distance where each ramp begins
        private readonly float[] m_JumpLength;
        private readonly float[] m_JumpHeight;
        public readonly PowerUpRow[] PowerUpRows;
        public readonly BoostPad[] BoostPads;

        public RaceTrack(TrackPath path, float halfWidth, float shoulderWidth, float runoffWidth, int laps, int checkpointsPerLap, LapRange[] openRunoff = null, JumpRamp[] jumps = null,
                         PowerUpRow[] powerUps = null, BoostPad[] boostPads = null)
        {
            PowerUpRows = powerUps ?? Array.Empty<PowerUpRow>();
            BoostPads = boostPads ?? Array.Empty<BoostPad>();
            Path = path;
            HalfWidth = halfWidth;
            ShoulderWidth = shoulderWidth;
            RunoffWidth = runoffWidth;
            Laps = Mathf.Max(1, laps);
            CheckpointsPerLap = Mathf.Max(1, checkpointsPerLap);
            m_Open = openRunoff ?? Array.Empty<LapRange>();
            jumps = jumps ?? Array.Empty<JumpRamp>();
            m_JumpStart = new float[jumps.Length];
            m_JumpLength = new float[jumps.Length];
            m_JumpHeight = new float[jumps.Length];
            for (int i = 0; i < jumps.Length; i++)
            {
                m_JumpLength[i] = Mathf.Max(1f, jumps[i].length);
                m_JumpHeight[i] = Mathf.Max(0f, jumps[i].height);
                m_JumpStart[i] = path.Wrap(jumps[i].lapFraction * path.Length - m_JumpLength[i]);
            }
        }

        public static RaceTrack From(TrackDefinition def, TrackPath path = null)
        {
            return new RaceTrack(path ?? def.BuildPath(), def.roadHalfWidth, def.shoulderWidth, def.runoffWidth, def.laps, def.checkpointsPerLap, def.openRunoff, def.jumps, def.powerUps, def.boostPads);
        }

        public int JumpCount => m_JumpStart.Length;

        /// <summary>Lap distance of a ramp's lip.</summary>
        public float JumpLip(int i) => Path.Wrap(m_JumpStart[i] + m_JumpLength[i]);

        /// <summary>Height of the drivable surface above the road centreline surface (ramps only cover the asphalt).</summary>
        public float SurfaceHeight(float distance, float lateral)
        {
            if (m_JumpStart.Length == 0 || Mathf.Abs(lateral) > HalfWidth) return 0f;
            float d = Path.Wrap(distance);
            for (int i = 0; i < m_JumpStart.Length; i++)
            {
                float x = d - m_JumpStart[i];
                if (x < 0f) x += Path.Length;
                if (x <= m_JumpLength[i]) return JumpRamp.Profile(x / m_JumpLength[i], m_JumpHeight[i]);
            }
            return 0f;
        }

        public float LapLength => Path.Length;
        public float RaceDistance => Laps * Path.Length;
        public float CheckpointSpacing => Path.Length / CheckpointsPerLap;
        public float ShoulderEdge => HalfWidth + ShoulderWidth;

        public bool HasBarrier(float distance)
        {
            if (m_Open.Length == 0) return true;
            float f = Path.Wrap(distance) / Path.Length;
            for (int i = 0; i < m_Open.Length; i++) if (m_Open[i].Contains(f)) return false;
            return true;
        }

        /// <summary>Hard lateral limit (barrier, or the edge of the open runoff).</summary>
        public float LateralLimit(float distance) => HasBarrier(distance) ? ShoulderEdge : ShoulderEdge + RunoffWidth;

        public SurfaceZone ZoneAt(float lateral)
        {
            float a = Mathf.Abs(lateral);
            return a <= HalfWidth ? SurfaceZone.Road : a <= ShoulderEdge ? SurfaceZone.Shoulder : SurfaceZone.Runoff;
        }
    }

    /// <summary>Mutable per-racer race state. Track-relative: distance along the race and lateral offset.</summary>
    public sealed class RacerState
    {
        public int Index;
        public bool IsPlayer;
        public CarTuning Tuning;

        public float Distance;          // race distance from the start line (negative on the grid)
        public float Lateral;           // metres right of the centreline
        public float LateralVelocity;
        public float Drift;             // corner push applied this step (for visuals)
        public float Speed;             // m/s along the track
        public float Height;            // metres above the road surface (ramps and jumps)
        public float VerticalSpeed;     // m/s, positive = rising
        public bool Airborne;
        public float AirTime;           // seconds in the air during the current (or last) jump
        public float LandingImpact;     // > 0 only on the step the car lands (downward speed, m/s)
        public bool TookOff;            // true only on the step the car leaves a ramp
        public float Steer;             // smoothed steering −1..1
        public SurfaceZone Zone;

        // Power-ups
        public float TurboTimer;        // seconds of turbo left
        public float PadTimer;          // seconds of boost-pad burst left
        public float ShieldTimer;       // seconds of shield left
        public int PowerUpsCollected;
        public int PadsHit;
        public bool Boosting => TurboTimer > 0f || PadTimer > 0f;
        public bool Shielded => ShieldTimer > 0f;

        public bool Finished;
        public float FinishTime;
        public int FinishOrder;
        public int Position;            // 1-based race position

        public float StartDistance;
        public float CheckpointDistance; // race distance of the last checkpoint passed (respawn point)
        public int CheckpointCount;
        public int Lap;                 // 1-based current lap (clamped to Laps)

        public float OutsideTimer;
        public float StuckTimer;
        public float GhostTimer;
        public int RespawnCount;

        // AI
        public bool Autopilot;          // AI-controlled (always true for AI; player after finishing)
        public float Skill = 1f;
        public float Phase;
        public int LaneIndex;
        public float LaneCooldown;
        public float SteerScale = 1f;   // player sensitivity setting

        public float SpeedKmh => Speed * 3.6f;
    }

    /// <summary>
    /// Deterministic, allocation-free race simulation shared by player and AI. The MonoBehaviour layer
    /// (RaceManager) only calls <see cref="Step"/> while the race state is Racing/Finishing, so countdown and pause
    /// can never advance progress or the clock.
    /// </summary>
    public sealed class RaceSimulation
    {
        public readonly RaceTrack Track;
        public readonly RaceTuning Tuning;
        public readonly AIProfile AI;
        public readonly RacerState[] Racers;
        public readonly int PlayerIndex;

        /// <summary>Racer indices ordered by race position (Order[0] is leading).</summary>
        public readonly int[] Order;

        public float Clock { get; private set; }
        public int FinishedCount { get; private set; }
        public RacerState Player => Racers[PlayerIndex];

        public event Action<int> RacerFinished;
        public event Action<int, int> PlayerPositionChanged; // old, new
        public event Action<int> PlayerCheckpoint;           // checkpoint count
        public event Action<int> PlayerLap;                  // new lap number
        public event Action<int> Respawned;                  // racer index
        public event Action<float> PlayerHitBarrier;         // impact speed
        public event Action<int> PlayerBumped;               // other racer index
        public event Action PlayerTookOff;
        public event Action<float> PlayerLanded;             // air time in seconds
        public event Action<int, int> PowerUpCollected;      // racer index, pickup slot
        public event Action PlayerBoostPad;
        public event Action PlayerShieldBlock;               // the shield saved the player from a bump or a scrape

        public readonly PowerUpField PowerUps;
        public readonly Difficulty Difficulty;
        private readonly DifficultyTuning m_Diff;

        private float m_PlayerBumpCooldown;
        private float m_PlayerBarrierCooldown;

        public RaceSimulation(RaceTrack track, RaceTuning tuning, AIProfile ai, CarTuning[] cars, int playerIndex, Difficulty difficulty = Difficulty.Easy)
        {
            Track = track;
            Tuning = tuning;
            AI = ai;
            Difficulty = difficulty;
            m_Diff = ai.For(difficulty);
            PowerUps = new PowerUpField(track, tuning, track.PowerUpRows, track.BoostPads, cars.Length);
            int n = cars.Length;
            Racers = new RacerState[n];
            Order = new int[n];
            PlayerIndex = Mathf.Clamp(playerIndex, 0, n - 1);
            for (int i = 0; i < n; i++)
            {
                Racers[i] = new RacerState { Index = i, IsPlayer = i == PlayerIndex, Tuning = cars[i] };
            }
            Reset();
        }

        /// <summary>Places every racer on the grid and clears all race state (used for start and restart).</summary>
        public void Reset()
        {
            Clock = 0f;
            FinishedCount = 0;
            m_PlayerBumpCooldown = 0f;
            m_PlayerBarrierCooldown = 0f;
            PowerUps.Reset();
            int n = Racers.Length;
            int playerSlot = Mathf.Clamp(Tuning.playerGridSlot, 0, n - 1);
            int aiSlot = 0, aiCount = 0;
            for (int i = 0; i < n; i++)
            {
                RacerState r = Racers[i];
                int slot;
                if (r.IsPlayer) slot = playerSlot;
                else
                {
                    if (aiSlot == playerSlot) aiSlot++;
                    slot = aiSlot++;
                }
                int row = slot / 2;
                bool left = slot % 2 == 0;
                r.Distance = -(row * Tuning.gridRowSpacing + (left ? 3f : 7.5f));
                r.StartDistance = r.Distance;
                r.Lateral = left ? -Tuning.gridLaneOffset : Tuning.gridLaneOffset;
                r.LateralVelocity = 0f;
                r.Drift = 0f;
                r.Speed = 0f;
                r.Steer = 0f;
                ClearVertical(r);
                r.Zone = SurfaceZone.Road;
                r.Finished = false;
                r.FinishTime = 0f;
                r.FinishOrder = 0;
                r.CheckpointDistance = r.Distance;
                r.CheckpointCount = 0;
                r.Lap = 1;
                r.OutsideTimer = 0f;
                r.StuckTimer = 0f;
                r.GhostTimer = 0f;
                r.RespawnCount = 0;
                r.TurboTimer = r.PadTimer = r.ShieldTimer = 0f;
                r.PowerUpsCollected = r.PadsHit = 0;
                r.Autopilot = !r.IsPlayer;
                r.LaneCooldown = 0f;
                if (!r.IsPlayer)
                {
                    float[] skills = AI.skill;
                    r.Skill = (skills.Length > 0 ? skills[aiCount % skills.Length] : 0.92f) * m_Diff.skillScale;
                    r.Phase = aiCount * 1.7f;
                    r.LaneIndex = NearestLane(r.Lateral);
                    aiCount++;
                }
                else r.Skill = 1f;
            }
            for (int i = 0; i < n; i++) Order[i] = i;
            SortPositions(false);
        }

        public float Progress(int racer) => Mathf.Clamp01(Racers[racer].Distance / Track.RaceDistance);

        /// <summary>Longest simulation sub-step. Slow frames are split so jumps and contacts behave the same at any frame rate.</summary>
        public const float MaxSubStep = 1f / 60f;

        /// <summary>Advances the race by dt seconds (clamped to 0.1 s, split into ≤ 1/60 s sub-steps). playerSteer is −1 (left) .. 1 (right).</summary>
        public void Step(float dt, float playerSteer)
        {
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);
            int n = Mathf.CeilToInt(dt / MaxSubStep - 1e-4f);
            float h = dt / Mathf.Max(1, n);
            for (int i = 0; i < n; i++) SubStep(h, playerSteer);
        }

        private void SubStep(float dt, float playerSteer)
        {
            Clock += dt;
            m_PlayerBumpCooldown -= dt;
            m_PlayerBarrierCooldown -= dt;
            RacerState player = Player;
            PowerUps.Tick(dt);

            for (int i = 0; i < Racers.Length; i++)
            {
                RacerState r = Racers[i];
                float prevDistance = r.Distance;
                bool wasShielded = r.Shielded;
                float steer, target;
                if (r.Autopilot) AIControl(r, player, out steer, out target);
                else { steer = playerSteer; target = r.Tuning.MaxSpeed; }

                bool hit = RacerMotor.Step(r, Track, Tuning, steer, target, dt, out float impact);
                if (hit && r.IsPlayer && m_PlayerBarrierCooldown <= 0f)
                {
                    m_PlayerBarrierCooldown = 0.6f;
                    PlayerHitBarrier?.Invoke(impact);
                    if (wasShielded) PlayerShieldBlock?.Invoke();
                }
                int slot = PowerUps.Collect(r, prevDistance, out bool pad);
                if (slot >= 0) PowerUpCollected?.Invoke(r.Index, slot);
                if (pad && r.IsPlayer) PlayerBoostPad?.Invoke();
                if (r.IsPlayer)
                {
                    if (r.TookOff) PlayerTookOff?.Invoke();
                    if (r.LandingImpact > 0f) PlayerLanded?.Invoke(r.AirTime);
                }
                UpdateRecovery(r, dt);
            }

            ResolveContacts();

            for (int i = 0; i < Racers.Length; i++) UpdateProgress(Racers[i]);
            SortPositions(true);
        }

        private void AIControl(RacerState r, RacerState player, out float steer, out float target)
        {
            AIProfile ai = AI;
            float hw = Track.HalfWidth;
            float laneTarget = ai.lanes.Length > 0 ? ai.lanes[Mathf.Clamp(r.LaneIndex, 0, ai.lanes.Length - 1)] * hw : 0f;
            // Go for a power-up or boost pad ahead (how often depends on the difficulty).
            bool seeking = PowerUps.Seek(r, r.IsPlayer ? 0f : m_Diff.seekPowerUps, out float seekLateral);
            if (seeking) laneTarget = seekLateral;

            // Look for a slower car ahead in our lane.
            float blockerSpeed = float.MaxValue;
            bool blocked = false;
            for (int j = 0; j < Racers.Length; j++)
            {
                if (j == r.Index) continue;
                RacerState o = Racers[j];
                float dd = Track.Path.DeltaOnLoop(o.Distance, r.Distance);
                if (dd > 0f && dd < ai.lookAhead && Mathf.Abs(o.Lateral - laneTarget) < Tuning.carWidth * 1.25f && o.Speed < r.Speed + 1.5f)
                {
                    blocked = true;
                    if (o.Speed < blockerSpeed) blockerSpeed = o.Speed;
                }
            }

            if (blocked && !seeking && r.LaneCooldown <= 0f && ai.lanes.Length > 1)
            {
                // Deterministic preference: even racers try left first, odd try right first.
                int first = r.Index % 2 == 0 ? -1 : 1;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int cand = r.LaneIndex + (attempt == 0 ? first : -first);
                    if (cand < 0 || cand >= ai.lanes.Length) continue;
                    if (LaneFree(r, ai.lanes[cand] * hw, -9f, ai.lookAhead))
                    {
                        r.LaneIndex = cand;
                        r.LaneCooldown = ai.laneChangeCooldown;
                        laneTarget = ai.lanes[cand] * hw;
                        blocked = false;
                        break;
                    }
                }
            }

            float skill = r.IsPlayer ? 0.7f : r.Skill; // finished player cruises
            float wobble = 1f + ai.speedVariation * Mathf.Sin(Clock * (6.2831853f / Mathf.Max(1f, ai.variationPeriod)) + r.Phase);
            float band = 1f;
            if (ai.rubberBand && !r.IsPlayer && !player.Finished)
            {
                float gap = r.Distance - player.Distance;
                float k = Mathf.Clamp01(Mathf.Abs(gap) / Mathf.Max(1f, ai.fullEffectGap));
                band = gap > 0f ? 1f - m_Diff.maxSlowdown * k : 1f + m_Diff.maxBoost * k;
            }
            target = r.Tuning.MaxSpeed * skill * wobble * band;
            if (blocked) target = Mathf.Min(target, Mathf.Max(blockerSpeed - 0.5f, r.Tuning.MaxSpeed * 0.5f));

            // Steering: PD on lateral error with feed-forward against the corner drift.
            float speedFrac = Mathf.Clamp01(r.Speed / Mathf.Max(1f, r.Tuning.MaxSpeed));
            float latMax = r.Tuning.lateralSpeed * Mathf.Lerp(Tuning.lowSpeedSteerScale, 1f, speedFrac);
            Track.Path.Sample(r.Distance, out TrackSample s);
            float drift = -s.curvature * r.Speed * r.Speed * Tuning.cornerDrift;
            float ff = latMax > 0.01f ? -drift / latMax : 0f;
            steer = Mathf.Clamp((laneTarget - r.Lateral) * ai.steerGain - r.LateralVelocity * ai.steerDamping + ff, -1f, 1f);
        }

        private bool LaneFree(RacerState r, float lane, float behind, float ahead)
        {
            for (int j = 0; j < Racers.Length; j++)
            {
                if (j == r.Index) continue;
                RacerState o = Racers[j];
                float dd = Track.Path.DeltaOnLoop(o.Distance, r.Distance);
                if (dd > behind && dd < ahead && Mathf.Abs(o.Lateral - lane) < Tuning.carWidth * 1.4f) return false;
            }
            return true;
        }

        private int NearestLane(float lateral)
        {
            float[] lanes = AI.lanes;
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < lanes.Length; i++)
            {
                float d = Mathf.Abs(lanes[i] * Track.HalfWidth - lateral);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private void UpdateRecovery(RacerState r, float dt)
        {
            r.GhostTimer = Mathf.Max(0f, r.GhostTimer - dt);
            r.LaneCooldown -= dt;
            r.OutsideTimer = r.Zone == SurfaceZone.Runoff ? r.OutsideTimer + dt : 0f;
            r.StuckTimer = r.Speed < Tuning.stuckSpeed && !r.Airborne ? r.StuckTimer + dt : 0f;
            bool invalid = float.IsNaN(r.Distance) || float.IsInfinity(r.Distance) || float.IsNaN(r.Lateral) || float.IsInfinity(r.Lateral)
                           || float.IsNaN(r.Height) || float.IsInfinity(r.Height) || r.Height > 30f;
            if (invalid || r.OutsideTimer > Tuning.recoveryDelay || r.StuckTimer > Tuning.stuckDelay) Respawn(r);
        }

        /// <summary>Puts a racer back on the road at its last checkpoint, facing along the track.</summary>
        public void Respawn(RacerState r)
        {
            float d = r.CheckpointDistance;
            if (float.IsNaN(d) || float.IsInfinity(d)) d = r.StartDistance;
            r.Distance = d;
            float side = r.Lateral >= 0f && !float.IsNaN(r.Lateral) ? 1f : -1f;
            r.Lateral = side * Tuning.respawnLaneFraction * Track.HalfWidth;
            // Avoid respawning on top of another car: shift to the other side if occupied.
            for (int j = 0; j < Racers.Length; j++)
            {
                if (j == r.Index) continue;
                RacerState o = Racers[j];
                if (Mathf.Abs(Track.Path.DeltaOnLoop(o.Distance, d)) < Tuning.carLength * 1.5f && Mathf.Abs(o.Lateral - r.Lateral) < Tuning.carWidth)
                {
                    r.Lateral = -r.Lateral;
                    break;
                }
            }
            r.LateralVelocity = 0f;
            r.Drift = 0f;
            r.Steer = 0f;
            ClearVertical(r);
            r.Speed = r.Tuning.MaxSpeed * Tuning.respawnSpeedFactor;
            r.Zone = SurfaceZone.Road;
            r.OutsideTimer = 0f;
            r.StuckTimer = 0f;
            r.GhostTimer = Tuning.respawnGhostSeconds;
            r.TurboTimer = r.PadTimer = 0f;
            r.RespawnCount++;
            if (!r.IsPlayer) r.LaneIndex = NearestLane(r.Lateral);
            Respawned?.Invoke(r.Index);
        }

        private static void ClearVertical(RacerState r)
        {
            r.Height = 0f;
            r.VerticalSpeed = 0f;
            r.Airborne = false;
            r.AirTime = 0f;
            r.LandingImpact = 0f;
            r.TookOff = false;
        }

        private void ResolveContacts()
        {
            float len = Tuning.carLength, wid = Tuning.carWidth;
            for (int i = 0; i < Racers.Length; i++)
            {
                RacerState a = Racers[i];
                if (a.GhostTimer > 0f) continue;
                for (int j = i + 1; j < Racers.Length; j++)
                {
                    RacerState b = Racers[j];
                    if (b.GhostTimer > 0f) continue;
                    float dd = Track.Path.DeltaOnLoop(a.Distance, b.Distance); // > 0: a ahead
                    float dl = a.Lateral - b.Lateral;
                    if (Mathf.Abs(dd) >= len || Mathf.Abs(dl) >= wid) continue;
                    if (Mathf.Abs(a.Height - b.Height) > Tuning.contactHeight) continue; // one car is flying over the other

                    RacerState front = dd >= 0f ? a : b;
                    RacerState rear = dd >= 0f ? b : a;
                    float latDir = Mathf.Abs(dl) > 0.01f ? Mathf.Sign(front.Lateral - rear.Lateral) : (front.Index < rear.Index ? 1f : -1f);
                    bool frontShield = front.Shielded && !rear.Shielded, rearShield = rear.Shielded && !front.Shielded;
                    if (Mathf.Abs(dd) < len * 0.6f)
                    {
                        // Side by side: push apart sideways. A shielded car holds its line and the other car moves.
                        float overlap = (wid - Mathf.Abs(dl)) * 0.5f;
                        float frontShare = frontShield ? 0f : rearShield ? 2f : 1f;
                        float rearShare = 2f - frontShare;
                        front.Lateral += latDir * overlap * frontShare;
                        rear.Lateral -= latDir * overlap * rearShare;
                        if (!frontShield) front.LateralVelocity = latDir * Mathf.Abs(front.LateralVelocity) * 0.5f;
                        if (!rearShield) rear.LateralVelocity = -latDir * Mathf.Abs(rear.LateralVelocity) * 0.5f;
                    }
                    else if (rearShield)
                    {
                        // A shielded car nudges the car in front aside and keeps its own speed.
                        rear.Distance -= len - Mathf.Abs(dd);
                        front.LateralVelocity += latDir * Tuning.bumpLateralKick * 1.6f;
                    }
                    else
                    {
                        // Nose to tail: the rear car is held back, nudged aside and loses a little speed.
                        rear.Distance -= len - Mathf.Abs(dd);
                        rear.Speed = Mathf.Min(rear.Speed, front.Speed * Tuning.bumpSpeedKeep);
                        rear.LateralVelocity -= latDir * Tuning.bumpLateralKick;
                    }
                    ClampLateral(front);
                    ClampLateral(rear);
                    if ((a.IsPlayer || b.IsPlayer) && m_PlayerBumpCooldown <= 0f)
                    {
                        m_PlayerBumpCooldown = 0.5f;
                        PlayerBumped?.Invoke(a.IsPlayer ? b.Index : a.Index);
                        if (Player.Shielded) PlayerShieldBlock?.Invoke();
                    }
                }
            }
        }

        private void ClampLateral(RacerState r)
        {
            float lim = Track.LateralLimit(r.Distance) - Tuning.carWidth * 0.5f;
            r.Lateral = Mathf.Clamp(r.Lateral, -lim, lim);
        }

        private void UpdateProgress(RacerState r)
        {
            float spacing = Track.CheckpointSpacing;
            if (r.Distance >= 0f)
            {
                int cp = Mathf.FloorToInt(r.Distance / spacing);
                int maxCp = Track.Laps * Track.CheckpointsPerLap;
                if (cp > maxCp) cp = maxCp;
                if (cp > r.CheckpointCount)
                {
                    r.CheckpointCount = cp;
                    r.CheckpointDistance = cp * spacing;
                    if (r.IsPlayer) PlayerCheckpoint?.Invoke(cp);
                }
            }
            int lap = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, r.Distance) / Track.LapLength) + 1, 1, Track.Laps);
            if (lap != r.Lap)
            {
                bool advanced = lap > r.Lap;
                r.Lap = lap;
                if (advanced && r.IsPlayer) PlayerLap?.Invoke(lap);
            }
            if (!r.Finished && r.Distance >= Track.RaceDistance)
            {
                r.Finished = true;
                float over = r.Speed > 0.1f ? (r.Distance - Track.RaceDistance) / r.Speed : 0f;
                r.FinishTime = Mathf.Max(0f, Clock - over);
                r.FinishOrder = ++FinishedCount;
                if (r.IsPlayer) r.Autopilot = true;
                RacerFinished?.Invoke(r.Index);
            }
        }

        /// <summary>Finished racers first (by finish order), then by distance; ties broken by racer index (stable).</summary>
        public static int Compare(RacerState a, RacerState b)
        {
            if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
            if (a.Finished) return a.FinishOrder.CompareTo(b.FinishOrder);
            if (a.Distance > b.Distance + 0.001f) return -1;
            if (b.Distance > a.Distance + 0.001f) return 1;
            return a.Index.CompareTo(b.Index);
        }

        private void SortPositions(bool notify)
        {
            int n = Order.Length;
            for (int i = 1; i < n; i++)
            {
                int key = Order[i];
                int j = i - 1;
                while (j >= 0 && Compare(Racers[Order[j]], Racers[key]) > 0)
                {
                    Order[j + 1] = Order[j];
                    j--;
                }
                Order[j + 1] = key;
            }
            int oldPlayerPos = Player.Position;
            for (int i = 0; i < n; i++) Racers[Order[i]].Position = i + 1;
            if (notify && oldPlayerPos != Player.Position && oldPlayerPos != 0) PlayerPositionChanged?.Invoke(oldPlayerPos, Player.Position);
        }

        /// <summary>World pose of a racer on the track (used by visuals and tests).</summary>
        public void GetPose(RacerState r, out Vector3 position, out Quaternion rotation)
        {
            Track.Path.Sample(r.Distance, out TrackSample s);
            position = s.position + s.right * r.Lateral + s.up * r.Height;
            float yaw = Mathf.Atan2(r.LateralVelocity + r.Drift, Mathf.Max(r.Speed, 4f)) * Mathf.Rad2Deg;
            rotation = Quaternion.LookRotation(s.forward, s.up) * Quaternion.Euler(0f, Mathf.Clamp(yaw, -25f, 25f), 0f);
        }

        /// <summary>Debug/test helper: moves a racer to a race distance (e.g. just before the finish).</summary>
        public void Teleport(int racer, float distance, float lateral = 0f)
        {
            RacerState r = Racers[racer];
            r.Distance = distance;
            r.Lateral = lateral;
            ClearVertical(r);
            r.CheckpointCount = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, distance) / Track.CheckpointSpacing), 0, Track.Laps * Track.CheckpointsPerLap);
            r.CheckpointDistance = distance >= 0f ? r.CheckpointCount * Track.CheckpointSpacing : r.StartDistance;
            r.Lap = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, distance) / Track.LapLength) + 1, 1, Track.Laps);
            SortPositions(false);
        }
    }

    /// <summary>Shared arcade motor: automatic acceleration, smoothed steering, corner drift, surface slowdown, barriers.</summary>
    public static class RacerMotor
    {
        /// <returns>True when the car struck a barrier this step.</returns>
        public static bool Step(RacerState r, RaceTrack track, RaceTuning tun, float steerTarget, float targetSpeed, float dt, out float impact)
        {
            impact = 0f;
            CarTuning t = r.Tuning;
            steerTarget = Mathf.Clamp(steerTarget, -1f, 1f);

            // Power-ups: turbo and boost pads raise the target speed for a moment; the shield runs down.
            float boost = 1f;
            if (r.TurboTimer > 0f) { r.TurboTimer -= dt; boost = Mathf.Max(boost, tun.turboSpeedFactor); }
            if (r.PadTimer > 0f) { r.PadTimer -= dt; boost = Mathf.Max(boost, tun.padSpeedFactor); }
            if (r.ShieldTimer > 0f) r.ShieldTimer -= dt;

            // Steering builds at steerRate, and returns/reverses at the (faster) returnRate.
            bool building = Mathf.Abs(steerTarget) > Mathf.Abs(r.Steer) && (r.Steer == 0f || Mathf.Sign(steerTarget) == Mathf.Sign(r.Steer));
            r.Steer = Mathf.MoveTowards(r.Steer, steerTarget, (building ? t.steerRate : t.returnRate) * dt);

            track.Path.Sample(r.Distance, out TrackSample s);
            float maxV = Mathf.Max(1f, t.MaxSpeed);
            float speedFrac = Mathf.Clamp01(r.Speed / maxV);
            float latMax = t.lateralSpeed * Mathf.Lerp(tun.lowSpeedSteerScale, 1f, speedFrac) * (r.Autopilot ? 1f : r.SteerScale);
            // In the air the tyres have no grip: steering is softened and there is no corner push.
            float grip = r.Airborne ? tun.airSteerControl : 1f;
            r.LateralVelocity = Mathf.MoveTowards(r.LateralVelocity, r.Steer * latMax, t.lateralAccel * grip * dt);
            r.Drift = r.Airborne ? 0f : -s.curvature * r.Speed * r.Speed * tun.cornerDrift;
            r.Lateral += (r.LateralVelocity + r.Drift) * dt;

            // Barrier / recovery-area limit.
            bool hit = false;
            float limit = track.LateralLimit(r.Distance) - tun.carWidth * 0.5f;
            if (Mathf.Abs(r.Lateral) > limit)
            {
                float side = Mathf.Sign(r.Lateral);
                float outward = (r.LateralVelocity + r.Drift) * side;
                r.Lateral = side * limit;
                if (outward > 0f)
                {
                    impact = outward;
                    r.LateralVelocity = -side * outward * tun.barrierBounce;
                    if (!r.Shielded) r.Speed *= Mathf.Lerp(1f, tun.barrierSpeedKeep, Mathf.Clamp01(outward / 6f));
                    hit = outward > 1.5f;
                }
            }

            r.Zone = track.ZoneAt(r.Lateral);
            if (!r.Airborne) // in the air the car simply carries its speed
            {
                float zoneFactor = r.Zone == SurfaceZone.Road ? 1f : r.Zone == SurfaceZone.Shoulder ? tun.shoulderSpeedFactor : tun.runoffSpeedFactor;
                // Slopes: a little slower uphill, a little faster downhill (the same for every car).
                float slope = Mathf.Clamp(s.forward.y, -0.2f, 0.2f);
                float target = Mathf.Max(0f, targetSpeed * boost * zoneFactor * (1f - slope * tun.slopeSpeedEffect));
                if (boost > 1f && r.Speed < target) r.Speed = Mathf.MoveTowards(r.Speed, target, tun.boostAcceleration * dt);
                else if (r.Speed < target)
                {
                    float a = t.acceleration * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(r.Speed / Mathf.Max(1f, target)));
                    r.Speed = Mathf.Min(target, r.Speed + a * dt);
                }
                else r.Speed = Mathf.Max(target, r.Speed - tun.deceleration * dt);
            }

            r.Distance += r.Speed * dt;
            Vertical(r, track, tun, dt);
            return hit;
        }

        /// <summary>
        /// Height above the road: the car rides up ramps, and when the surface drops away faster than it can follow
        /// (the lip of a ramp) it flies a ballistic arc under <see cref="RaceTuning.jumpGravity"/> until it lands.
        /// </summary>
        public static void Vertical(RacerState r, RaceTrack track, RaceTuning tun, float dt)
        {
            r.TookOff = false;
            r.LandingImpact = 0f;
            float ground = track.SurfaceHeight(r.Distance, r.Lateral);
            if (r.Airborne)
            {
                r.AirTime += dt;
                r.VerticalSpeed -= tun.jumpGravity * dt;
                r.Height += r.VerticalSpeed * dt;
                if (r.Height <= ground)
                {
                    r.LandingImpact = Mathf.Max(0.01f, -r.VerticalSpeed);
                    r.Height = ground;
                    r.VerticalSpeed = 0f;
                    r.Airborne = false;
                    r.Speed *= tun.landingSpeedKeep;
                }
                return;
            }
            if (ground < r.Height - tun.takeoffDrop && r.Height > 0.05f)
            {
                // The surface fell away under us: launch with the vertical speed we had on the ramp.
                r.Airborne = true;
                r.TookOff = true;
                r.AirTime = 0f;
                r.VerticalSpeed = Mathf.Max(0f, r.VerticalSpeed) - tun.jumpGravity * dt;
                r.Height += r.VerticalSpeed * dt;
                return;
            }
            r.VerticalSpeed = dt > 0f ? (ground - r.Height) / dt : 0f;
            r.Height = ground;
        }
    }
}
