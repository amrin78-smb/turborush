using UnityEngine;

namespace TurboRush.AI
{
    /// <summary>Tuning for AI opponents. Deterministic: no randomness at runtime beyond per-racer fixed phases.</summary>
    [CreateAssetMenu(menuName = "Turbo Rush/AI Profile", fileName = "AIProfile")]
    public sealed class AIProfile : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("Each AI's top speed as a fraction of its car's top speed, assigned in grid order (cycled).")]
        public float[] skill = { 0.93f, 0.91f, 0.95f, 0.89f, 0.92f, 0.9f, 0.94f };
        [Tooltip("Slow sinusoidal speed wobble (fraction of target speed).")]
        public float speedVariation = 0.03f;
        public float variationPeriod = 9f;

        [Header("Lanes")]
        [Tooltip("Lane offsets as a fraction of the road half-width (−1 = left edge, 1 = right edge).")]
        public float[] lanes = { -0.62f, -0.22f, 0.22f, 0.62f };
        public float lookAhead = 20f;
        public float laneChangeCooldown = 2.2f;
        [Tooltip("Steering gain on lateral error (per metre).")]
        public float steerGain = 0.55f;
        [Tooltip("Damping on lateral velocity.")]
        public float steerDamping = 0.12f;

        [Header("Catch-up (subtle, bounded)")]
        public bool rubberBand = true;
        [Tooltip("Max fraction an AI far ahead of the player slows by.")]
        [Range(0f, 0.15f)] public float maxSlowdown = 0.06f;
        [Tooltip("Max fraction an AI far behind the player speeds up by.")]
        [Range(0f, 0.15f)] public float maxBoost = 0.04f;
        [Tooltip("Gap (metres) at which the full adjustment applies.")]
        public float fullEffectGap = 180f;

        public static AIProfile CreateDefault()
        {
            var p = CreateInstance<AIProfile>();
            p.name = "AIProfile (default)";
            return p;
        }
    }
}
