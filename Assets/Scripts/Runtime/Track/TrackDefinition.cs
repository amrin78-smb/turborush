using System;
using UnityEngine;

namespace TurboRush.Track
{
    public enum TrackTheme { Skyway, Desert, Coastal, Hills }

    /// <summary>A range of the lap expressed as fractions 0..1 (start may be greater than end to wrap past the line).</summary>
    [Serializable]
    public struct LapRange
    {
        public float start;
        public float end;
        public LapRange(float s, float e) { start = s; end = e; }

        public bool Contains(float lapFraction)
        {
            return start <= end ? lapFraction >= start && lapFraction <= end : lapFraction >= start || lapFraction <= end;
        }
    }

    /// <summary>
    /// A kicker ramp across the asphalt. The car climbs <see cref="length"/> metres to <see cref="height"/> and then the
    /// ground drops away at the lip, so it flies a short, gentle arc and lands on the road ahead.
    /// </summary>
    [Serializable]
    public struct JumpRamp
    {
        [Tooltip("Lap fraction (0..1) of the ramp lip (its highest point).")]
        public float lapFraction;
        [Tooltip("Ramp length in metres, from the road surface up to the lip.")]
        public float length;
        [Tooltip("Lip height in metres above the road.")]
        public float height;
        public JumpRamp(float lapFraction, float length = 10f, float height = 0.7f) { this.lapFraction = lapFraction; this.length = length; this.height = height; }

        /// <summary>Ramp profile: rises gently and steepens towards the lip (u 0..1 → 0..height; 0 outside the ramp).</summary>
        public static float Profile(float u, float height) => u <= 0f || u > 1f ? 0f : height * (0.6f * u + 0.4f * u * u);
    }

    /// <summary>Drive-through power-ups (no button: they work as soon as a car touches them).</summary>
    public enum PowerUpKind
    {
        None = 0,
        /// <summary>Turbo: a few seconds above top speed.</summary>
        Turbo = 1,
        /// <summary>Shield: bumps and barrier scrapes do not slow the car for a few seconds.</summary>
        Shield = 2
    }

    /// <summary>
    /// A row of up to three floating power-ups across the road (left, centre and right lane). A collected power-up
    /// comes back after <c>RaceTuning.pickupRespawnSeconds</c>, so cars further back can still get one.
    /// </summary>
    [Serializable]
    public struct PowerUpRow
    {
        [Tooltip("Lap fraction (0..1) of the row.")]
        public float lapFraction;
        public PowerUpKind left, centre, right;
        public PowerUpRow(float lapFraction, PowerUpKind left, PowerUpKind centre, PowerUpKind right)
        {
            this.lapFraction = lapFraction; this.left = left; this.centre = centre; this.right = right;
        }

        /// <summary>Lane offsets as a fraction of the road half-width, for slots 0 (left), 1 (centre), 2 (right).</summary>
        public static float SlotLane(int slot) => (slot - 1) * 0.58f;

        public PowerUpKind Slot(int slot) => slot == 0 ? left : slot == 1 ? centre : right;
    }

    /// <summary>A glowing boost strip painted on the road: a short speed burst for any car that drives over it.</summary>
    [Serializable]
    public struct BoostPad
    {
        /// <summary>Pad length along the road and half its width (metres). Shared by the rules and the geometry.</summary>
        public const float Length = 9f, HalfWidth = 1.8f;

        [Tooltip("Lap fraction (0..1) of the start of the pad.")]
        public float lapFraction;
        [Tooltip("Centre of the pad as a fraction of the road half-width (−1 left edge .. 1 right edge).")]
        public float lane;
        public BoostPad(float lapFraction, float lane) { this.lapFraction = lapFraction; this.lane = lane; }
    }

    [Serializable]
    public sealed class TrackPalette
    {
        public Color skyTop = new Color(0.16f, 0.45f, 0.92f);
        public Color skyHorizon = new Color(0.72f, 0.88f, 1f);
        public Color skyBottom = new Color(0.55f, 0.75f, 0.95f);
        public Color fog = new Color(0.72f, 0.86f, 0.98f);
        public float fogStart = 180f;
        public float fogEnd = 900f;
        public Color ambient = new Color(0.62f, 0.68f, 0.78f);
        public Color sun = new Color(1f, 0.96f, 0.88f);
        public Vector3 sunEuler = new Vector3(48f, -35f, 0f);
        public Color ground = new Color(0.36f, 0.66f, 0.3f);
        public Color groundAlt = new Color(0.44f, 0.74f, 0.34f);
        public Color verge = new Color(0.35f, 0.72f, 0.3f);
        public Color road = new Color(0.33f, 0.34f, 0.38f);
        public Color roadLine = new Color(1f, 1f, 1f);
        public Color curbA = new Color(0.9f, 0.13f, 0.13f);
        public Color curbB = Color.white;
        public Color barrierA = new Color(0.12f, 0.45f, 0.95f);
        public Color barrierB = Color.white;
        public Color structure = new Color(0.78f, 0.78f, 0.8f);
        public Color scenery = new Color(0.2f, 0.55f, 0.25f);
        public Color sceneryAlt = new Color(0.55f, 0.42f, 0.3f);
        public Color mountain = new Color(0.45f, 0.58f, 0.72f);
        public Color water = new Color(0.1f, 0.5f, 0.8f);
    }

    /// <summary>
    /// Data-only description of a track. Adding a track = new TrackDefinition asset in GameConfig.tracks;
    /// geometry, scenery, checkpoints and AI lanes are all generated from this at load time.
    /// </summary>
    [CreateAssetMenu(menuName = "Turbo Rush/Track Definition", fileName = "Track")]
    public sealed class TrackDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "track";
        public string displayName = "Track";
        public TrackTheme theme;
        [Tooltip("Total stars needed to unlock (0 = available from the start).")]
        public int unlockStars;
        public Sprite preview;

        [Header("Layout (looping centreline, metres)")]
        public Vector3[] controlPoints = Array.Empty<Vector3>();
        [Min(1)] public int laps = 2;
        [Tooltip("Degrees of bank per (curvature × 100). 0 = flat corners.")]
        public float autoBankFactor = 6f;
        public float maxBank = 10f;

        [Header("Road")]
        [Tooltip("Half width of the drivable asphalt.")]
        public float roadHalfWidth = 7f;
        public float curbWidth = 1.2f;
        [Tooltip("Width beyond the asphalt edge up to the barrier (curb + verge). Driving here only slows you a little.")]
        public float shoulderWidth = 3.2f;
        [Tooltip("Where there is no barrier the car may run this far beyond the shoulder before hitting the recovery limit.")]
        public float runoffWidth = 18f;
        [Tooltip("Lap ranges with no side barriers (open runoff). Outside the shoulder there for too long → respawn.")]
        public LapRange[] openRunoff = Array.Empty<LapRange>();
        [Min(2)] public int checkpointsPerLap = 8;

        [Header("Structures")]
        [Tooltip("Ground plane height. Road higher than this by > 3 m is drawn as a bridge with pillars.")]
        public float groundHeight = -30f;
        public float pillarSpacing = 36f;
        [Tooltip("Road this far above the ground plane is drawn as an elevated deck on pillars.")]
        public float bridgeMinHeight = 4f;
        [Tooltip("Lap ranges forced to be bridges (the ground/sea below is not raised to meet the road).")]
        public LapRange[] bridges = Array.Empty<LapRange>();
        public LapRange[] tunnels = Array.Empty<LapRange>();
        [Tooltip("Gentle kicker ramps. Place them on straights with at least 60 m of straight road after the lip.")]
        public JumpRamp[] jumps = Array.Empty<JumpRamp>();
        public bool sea;
        public float seaLevel = -2f;

        [Header("Power-ups")]
        [Tooltip("Rows of floating power-ups. Keep them on the road, away from ramps and at least 40 m from the start line.")]
        public PowerUpRow[] powerUps = Array.Empty<PowerUpRow>();
        [Tooltip("Boost strips on the road surface.")]
        public BoostPad[] boostPads = Array.Empty<BoostPad>();

        [Header("Scenery")]
        public int seed = 1;
        public int treeCount = 140;
        public int rockCount = 30;
        public int cactusCount;
        public int cloudCount = 24;
        public int buildingCount;
        public int windmillCount;
        public bool mountainRing = true;
        public float sceneryRadius = 650f;

        [Header("Look")]
        public TrackPalette palette = new TrackPalette();

        public bool IsOpenRunoff(float lapFraction)
        {
            for (int i = 0; i < openRunoff.Length; i++) if (openRunoff[i].Contains(lapFraction)) return true;
            return false;
        }

        public bool IsTunnel(float lapFraction)
        {
            for (int i = 0; i < tunnels.Length; i++) if (tunnels[i].Contains(lapFraction)) return true;
            return false;
        }

        public bool IsBridge(float lapFraction)
        {
            for (int i = 0; i < bridges.Length; i++) if (bridges[i].Contains(lapFraction)) return true;
            return false;
        }

        public TrackPath BuildPath() => TrackPath.Build(controlPoints, autoBankFactor, maxBank);
    }
}
