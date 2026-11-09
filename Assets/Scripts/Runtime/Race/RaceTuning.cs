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

        [Header("Collisions")]
        [Tooltip("Speed the rear car keeps relative to the front car after a bump.")]
        public float bumpSpeedKeep = 0.94f;
        public float bumpLateralKick = 2.5f;
    }
}
