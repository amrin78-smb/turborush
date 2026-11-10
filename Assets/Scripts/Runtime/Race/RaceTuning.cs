using System;
using UnityEngine;

namespace TurboRush.Race
{
    /// <summary>Global race and handling thresholds (inspector-configurable on GameConfig).</summary>
    [Serializable]
    public sealed class RaceTuning
    {
        [Header("Field")]
        [Range(2, 8)] public int racerCount = 6;
        [Tooltip("Grid slot of the player (0 = pole). Starting further back gives more overtakes.")]
        public int playerGridSlot = 5;
        public float gridRowSpacing = 9f;
        public float gridLaneOffset = 2.6f;
        public float countdownSeconds = 3f;
        [Tooltip("Seconds of 'Finishing' (car drives on, camera keeps following) before the results appear.")]
        public float finishingSeconds = 3f;

        [Header("Handling")]
        [Tooltip("Outward push in corners: v² × curvature × this. Gives the player something to steer against.")]
        public float cornerDrift = 0.05f;
        [Tooltip("Steering scale at zero speed (scales up to 1 at top speed).")]
        public float lowSpeedSteerScale = 0.55f;
        public float deceleration = 9f;
        public float carLength = 4.3f;
        public float carWidth = 2f;

        [Header("Off-road")]
        [Tooltip("Target speed multiplier on the curb/verge beyond the asphalt.")]
        public float shoulderSpeedFactor = 0.72f;
        [Tooltip("Target speed multiplier in open runoff beyond the shoulder.")]
        public float runoffSpeedFactor = 0.45f;
        [Tooltip("Speed kept when scraping a barrier (per impact).")]
        public float barrierSpeedKeep = 0.9f;
        [Tooltip("Lateral bounce off a barrier (fraction of impact speed).")]
        public float barrierBounce = 0.35f;

        [Header("Recovery")]
        [Tooltip("Seconds outside the recovery area (beyond the shoulder) before respawning at the last checkpoint.")]
        public float recoveryDelay = 3f;
        [Tooltip("Seconds below stuckSpeed before an automatic respawn.")]
        public float stuckDelay = 4f;
        public float stuckSpeed = 3f;
        [Tooltip("Speed after respawn as a fraction of top speed.")]
        public float respawnSpeedFactor = 0.45f;
        [Tooltip("Seconds a respawned car ignores car-to-car contact.")]
        public float respawnGhostSeconds = 2f;
        [Tooltip("Lateral offset (fraction of road half-width) the car is placed at on respawn.")]
        public float respawnLaneFraction = 0.25f;

        [Header("Slopes and jumps")]
        [Tooltip("Target speed change per unit of road grade: uphill slower, downhill faster (1.2 → ±11 % on a 9 % grade).")]
        public float slopeSpeedEffect = 1.2f;
        [Tooltip("Gravity while airborne (m/s²). A bit above real gravity keeps jumps short and floaty-but-snappy.")]
        public float jumpGravity = 14f;
        [Tooltip("Steering authority while airborne (fraction of normal lateral acceleration).")]
        public float airSteerControl = 0.35f;
        [Tooltip("Speed kept when landing.")]
        public float landingSpeedKeep = 0.985f;
        [Tooltip("The car takes off when the surface drops away by more than this in one step (metres).")]
        public float takeoffDrop = 0.05f;
        [Tooltip("Cars more than this far apart vertically do not collide (one is flying over the other).")]
        public float contactHeight = 0.9f;

        [Header("Power-ups")]
        [Tooltip("Turbo power-up: seconds of extra speed.")]
        public float turboSeconds = 2.6f;
        [Tooltip("Turbo power-up: target speed as a multiple of top speed.")]
        public float turboSpeedFactor = 1.3f;
        [Tooltip("Boost pad: seconds of extra speed.")]
        public float padSeconds = 1.3f;
        [Tooltip("Boost pad: target speed as a multiple of top speed.")]
        public float padSpeedFactor = 1.22f;
        [Tooltip("Acceleration while a turbo or pad boost is active (m/s²).")]
        public float boostAcceleration = 24f;
        [Tooltip("Shield power-up: seconds during which bumps and barrier scrapes do not slow the car.")]
        public float shieldSeconds = 6f;
        [Tooltip("Seconds before a collected power-up appears again.")]
        public float pickupRespawnSeconds = 4f;
        [Tooltip("A car collects a power-up when its centre passes within this many metres sideways.")]
        public float pickupReach = 1.7f;

        [Header("Collisions")]
        [Tooltip("Speed the rear car keeps relative to the front car after a bump.")]
        public float bumpSpeedKeep = 0.94f;
        public float bumpLateralKick = 2.5f;
    }
}
