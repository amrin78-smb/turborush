using UnityEngine;

namespace TurboRush.AI
{
    public enum Difficulty { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>How hard the AI races at one difficulty level. Everything stays bounded and deterministic.</summary>
    [System.Serializable]
    public struct DifficultyTuning
    {
        [Tooltip("Multiplies every AI's skill (fraction of its car's top speed). 1 = the original Turbo Rush field.")]
        public float skillScale;
        [Tooltip("Max fraction an AI far ahead of the player slows by.")]
        [Range(0f, 0.15f)] public float maxSlowdown;
        [Tooltip("Max fraction an AI far behind the player speeds up by.")]
        [Range(0f, 0.15f)] public float maxBoost;
        [Tooltip("Share of power-up rows and boost pads an AI steers for (0..1). Deterministic per racer and row.")]
        [Range(0f, 1f)] public float seekPowerUps;
    }

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
        [Tooltip("Gap (metres) at which the full catch-up adjustment applies.")]
        public float fullEffectGap = 180f;

        [Header("Difficulty (chosen by the player on the Tracks screen)")]
        public DifficultyTuning easy = new DifficultyTuning { skillScale = 1f, maxSlowdown = 0.06f, maxBoost = 0.04f, seekPowerUps = 0.2f };
        public DifficultyTuning normal = new DifficultyTuning { skillScale = 1.045f, maxSlowdown = 0.03f, maxBoost = 0.05f, seekPowerUps = 0.55f };
        public DifficultyTuning hard = new DifficultyTuning { skillScale = 1.065f, maxSlowdown = 0f, maxBoost = 0.05f, seekPowerUps = 0.9f };

        public DifficultyTuning For(Difficulty d) => d == Difficulty.Hard ? hard : d == Difficulty.Normal ? normal : easy;

        public static AIProfile CreateDefault()
        {
            var p = CreateInstance<AIProfile>();
            p.name = "AIProfile (default)";
            return p;
        }
    }
}
