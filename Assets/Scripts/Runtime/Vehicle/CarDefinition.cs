using System;
using UnityEngine;

namespace TurboRush.Vehicle
{
    public enum CarBodyStyle { Wedge, Bubble, Muscle }

    /// <summary>Handling numbers for one car. All racers use the same motor; only these values differ.</summary>
    [Serializable]
    public struct CarTuning
    {
        [Tooltip("Top speed in km/h.")] public float maxSpeedKmh;
        [Tooltip("Acceleration from standstill in m/s².")] public float acceleration;
        [Tooltip("Sideways speed at full steer and top speed (m/s).")] public float lateralSpeed;
        [Tooltip("How fast the steering input builds (per second).")] public float steerRate;
        [Tooltip("How fast steering returns to centre when released (per second).")] public float returnRate;
        [Tooltip("How quickly sideways velocity follows the steering (m/s²).")] public float lateralAccel;

        public float MaxSpeed => maxSpeedKmh / 3.6f;

        public static CarTuning Default => new CarTuning
        {
            maxSpeedKmh = 150f, acceleration = 11f, lateralSpeed = 10f, steerRate = 4.5f, returnRate = 6f, lateralAccel = 38f
        };
    }

    /// <summary>Cosmetic and tuning data for a car (player-selectable or AI).</summary>
    [CreateAssetMenu(menuName = "Turbo Rush/Car Definition", fileName = "Car")]
    public sealed class CarDefinition : ScriptableObject
    {
        public string id = "car";
        public string displayName = "Car";
        public CarBodyStyle bodyStyle;
        public Color paint = Color.red;
        public Color accent = Color.white;
        [Tooltip("Stars needed to unlock in the Garage (0 = owned from the start).")]
        public int unlockStars;
        public GameObject prefab;
        public Sprite portrait;
        public CarTuning tuning = CarTuning.Default;
    }
}
