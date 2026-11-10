using NUnit.Framework;
using TurboRush.Audio;
using UnityEditor;
using UnityEngine;

namespace TurboRush.Tests
{
    public class AudioMixTests
    {
        /// <summary>
        /// Owner feedback (0.3.1 on the Redmi A3): the engine was too soft. Phone speakers barely play anything
        /// below ~300 Hz, so the loop must be loud overall AND carry energy in the upper harmonics.
        /// </summary>
        [Test]
        public void EngineLoopIsAudibleOnAPhoneSpeaker()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/ScriptableObjects/AudioLibrary.asset");
            Assert.NotNull(lib, "run setup first");
            Assert.NotNull(lib.engineLoop);
            Assert.GreaterOrEqual(lib.engineVolume, 0.65f, "engine mix level");

            var data = new float[lib.engineLoop.samples];
            Assert.IsTrue(lib.engineLoop.GetData(data, 0));
            double sum = 0, diff = 0;
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i] * data[i];
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                float dd = data[i] - data[(i + data.Length - 1) % data.Length];
                diff += dd * dd;
            }
            float rms = Mathf.Sqrt((float)(sum / data.Length));
            // The first difference is a gentle high-pass (old 0.3.1 loop: 0.027, new: about 0.037); its RMS relative to the signal shows how much upper-harmonic content there is.
            float brightness = Mathf.Sqrt((float)(diff / data.Length)) / rms;
            Debug.Log($"[TR] Engine loop: rms {rms:F3}, peak {peak:F3}, brightness {brightness:F3}");
            Assert.Greater(rms, 0.3f, "loud enough");
            Assert.LessOrEqual(peak, 1f, "no clipping");
            Assert.Greater(brightness, 0.032f, "enough energy above the speaker's low cut-off");
            float seam = Mathf.Abs(data[0] - data[data.Length - 1]);
            Assert.Less(seam, 0.1f, "loop seam is smooth");
        }
    }
}
