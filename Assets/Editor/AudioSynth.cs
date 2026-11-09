using System;
using System.IO;
using TurboRush.Audio;
using UnityEditor;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Procedural, original placeholder audio (no external samples): engine and rumble loops, race cues, UI blips
    /// and two short cheerful music loops. Gentle envelopes and limited peaks (child-friendly mix).
    /// Replace any clip by assigning a different AudioClip in Assets/ScriptableObjects/AudioLibrary.asset.
    /// </summary>
    public static class AudioSynth
    {
        private const int Rate = 22050;
        private const string SfxDir = "Assets/Audio/SFX/";
        private const string MusicDir = "Assets/Audio/Music/";

        public static AudioLibrary BuildAll()
        {
            var lib = EditorAssets.LoadOrCreateSO<AudioLibrary>("Assets/ScriptableObjects/AudioLibrary.asset");
            lib.engineLoop = Save(SfxDir + "engine_loop.wav", Engine(), false, true);
            lib.offroadLoop = Save(SfxDir + "offroad_loop.wav", Rumble(), false, true);
            var sfx = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
            sfx[(int)Sfx.Click] = Save(SfxDir + "ui_click.wav", Tone(0.06f, 1200f, 0f, 0.35f, 0.002f, 0.05f), false);
            sfx[(int)Sfx.Back] = Save(SfxDir + "ui_back.wav", Tone(0.1f, 420f, -150f, 0.35f, 0.003f, 0.08f), false);
            sfx[(int)Sfx.Countdown] = Save(SfxDir + "countdown.wav", Notes(new[] { 659f }, 0.24f, 0.5f), false);
            sfx[(int)Sfx.Go] = Save(SfxDir + "go.wav", Chord(new[] { 880f, 1109f, 1319f }, 0.7f, 0.45f), false);
            sfx[(int)Sfx.Checkpoint] = Save(SfxDir + "checkpoint.wav", Notes(new[] { 988f, 1319f }, 0.12f, 0.35f), false);
            sfx[(int)Sfx.Lap] = Save(SfxDir + "lap.wav", Notes(new[] { 784f, 988f, 1175f }, 0.11f, 0.45f), false);
            sfx[(int)Sfx.Overtake] = Save(SfxDir + "overtake.wav", Tone(0.28f, 520f, 900f, 0.35f, 0.01f, 0.15f), false);
            sfx[(int)Sfx.Finish] = Save(SfxDir + "finish.wav", Fanfare(), false);
            sfx[(int)Sfx.Bump] = Save(SfxDir + "bump.wav", Thump(0.22f, 95f, 0.55f), false);
            sfx[(int)Sfx.Barrier] = Save(SfxDir + "barrier.wav", Scrape(), false);
            sfx[(int)Sfx.Unlock] = Save(SfxDir + "unlock.wav", Notes(new[] { 1047f, 1319f, 1568f, 2093f }, 0.08f, 0.4f, 0.5f), false);
            sfx[(int)Sfx.Star] = Save(SfxDir + "star.wav", Notes(new[] { 1568f }, 0.35f, 0.4f), false);
            lib.sfx = sfx;
            lib.menuMusic = Save(MusicDir + "menu_music.wav", Music(118f, 16, false), true, true);
            lib.raceMusic = Save(MusicDir + "race_music.wav", Music(138f, 16, true), true, true);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        // ---------- generators ----------
        private static float Env(float t, float len, float attack, float release)
        {
            if (t < attack) return t / attack;
            if (t > len - release) return Mathf.Max(0f, (len - t) / release);
            return 1f;
        }

        private static float[] Tone(float len, float f0, float sweep, float vol, float attack, float release)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = f0 + sweep * (t / len);
                ph += 2 * Math.PI * f / Rate;
                d[i] = (float)(Math.Sin(ph) + 0.25 * Math.Sin(2 * ph)) * vol * Env(t, len, attack, release);
            }
            return d;
        }

        private static float[] Notes(float[] freqs, float each, float vol, float tail = 0.25f)
        {
            float len = freqs.Length * each + tail;
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = (int)(k * each * Rate);
                float nl = each + tail;
                for (int i = 0; i < (int)(nl * Rate) && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    double ph = 2 * Math.PI * freqs[k] * t;
                    float e = Mathf.Exp(-t * 7f) * Mathf.Min(1f, t / 0.004f);
                    d[start + i] += (float)(Math.Sin(ph) * 0.8 + Math.Sin(2 * ph) * 0.2 + Math.Sin(3 * ph) * 0.08) * vol * e;
                }
            }
            return d;
        }

        private static float[] Chord(float[] freqs, float len, float vol)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0;
                foreach (float f in freqs) s += Mathf.Sin(2 * Mathf.PI * f * t) + 0.2f * Mathf.Sin(4 * Mathf.PI * f * t);
                d[i] = s / freqs.Length * vol * Env(t, len, 0.005f, 0.4f);
            }
            return d;
        }

        private static float[] Fanfare()
        {
            float[] mel = { 523f, 659f, 784f, 1047f };
            float[] a = Notes(mel, 0.14f, 0.42f, 0.1f);
            float[] c = Chord(new[] { 523f, 659f, 784f, 1047f }, 0.9f, 0.38f);
            var d = new float[a.Length + c.Length - (int)(0.1f * Rate)];
            Array.Copy(a, d, a.Length);
            int off = a.Length - (int)(0.1f * Rate);
            for (int i = 0; i < c.Length; i++) d[off + i] += c[i];
            return d;
        }

        private static float[] Thump(float len, float f, float vol)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            var rng = new System.Random(5);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                ph += 2 * Math.PI * (f * (1f - t * 1.5f)) / Rate;
                float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 60f) * 0.4f;
                d[i] = ((float)Math.Sin(ph) * Mathf.Exp(-t * 14f) + noise) * vol;
            }
            return d;
        }

        private static float[] Scrape()
        {
            float len = 0.3f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var rng = new System.Random(9);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.25f;
                d[i] = (lp * 0.7f + Mathf.Sin(2 * Mathf.PI * 140f * t) * 0.4f * Mathf.Exp(-t * 20f)) * 0.5f * Env(t, len, 0.005f, 0.2f);
            }
            return d;
        }

        /// <summary>1 s seamless loop: every partial is an integer number of Hz, so the loop point is phase-continuous.</summary>
        private static float[] Engine()
        {
            int n = Rate;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 62f;
                float s = Mathf.Sin(2 * Mathf.PI * f * t) * 0.55f + Mathf.Sin(2 * Mathf.PI * 2 * f * t) * 0.3f + Mathf.Sin(2 * Mathf.PI * 3 * f * t) * 0.16f
                          + Mathf.Sin(2 * Mathf.PI * 4 * f * t) * 0.08f + Mathf.Sin(2 * Mathf.PI * 31f * t) * 0.12f;
                float am = 0.82f + 0.18f * Mathf.Sin(2 * Mathf.PI * 31f * t);
                d[i] = s * am * 0.42f;
            }
            return d;
        }

        private static float[] Rumble()
        {
            int n = Rate, fade = Rate / 5;
            var raw = new float[n + fade];
            var rng = new System.Random(13);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.2f;
                raw[i] = lp2 * 1.6f;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = raw[n + i] * (1 - k) + raw[i] * k; // crossfade tail into head → seamless
            }
            return d;
        }

        /// <summary>Original cheerful loop: I–vi–IV–V progression, triangle bass, soft square lead, light drums.</summary>
        private static float[] Music(float bpm, int bars, bool race)
        {
            float beat = 60f / bpm;
            int beats = bars * 4;
            int n = (int)(beats * beat * Rate);
            var d = new float[n];
            int[][] chords = { new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 } };
            // Lead melody (scale degrees relative to C5), two bars per phrase, original.
            int[] menuMel = { 72, 76, 79, 76, 77, 76, 74, 72, 69, 72, 76, 74, 72, 74, 76, 79, 77, 76, 74, 72, 71, 72, 74, 79, 76, 74, 72, 74, 76, 72, 74, 71 };
            int[] raceMel = { 72, 72, 79, 79, 76, 77, 79, 76, 72, 74, 76, 74, 72, 71, 72, 74, 77, 77, 76, 74, 72, 74, 76, 79, 74, 76, 77, 79, 81, 79, 76, 74 };
            int[] mel = race ? raceMel : menuMel;
            float Midi(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);
            var rng = new System.Random(race ? 2 : 1);
            for (int b = 0; b < beats; b++)
            {
                int bar = b / 4;
                int[] ch = chords[bar % 4];
                int s0 = (int)(b * beat * Rate);
                int len = (int)(beat * Rate);
                // bass: root on every beat, octave on off-beats for race
                float bf = Midi(ch[0] - 24);
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float ph = Mathf.Repeat(bf * t, 1f);
                    float tri = 4f * Mathf.Abs(ph - 0.5f) - 1f;
                    d[s0 + i] += tri * 0.22f * Mathf.Exp(-t * 3f);
                }
                // chord pad (soft), arpeggiated in race music
                for (int k = 0; k < 3; k++)
                {
                    float f = Midi(ch[k]);
                    int off = race ? (int)(k * beat / 3f * Rate) : 0;
                    for (int i = 0; i < len - off && s0 + off + i < n; i++)
                    {
                        float t = i / (float)Rate;
                        d[s0 + off + i] += Mathf.Sin(2 * Mathf.PI * f * t) * 0.05f * Mathf.Exp(-t * (race ? 6f : 2f));
                    }
                }
                // lead: two eighth notes per beat from the melody (repeat with variation)
                for (int e = 0; e < 2; e++)
                {
                    int idx = (b * 2 + e) % mel.Length;
                    if ((b + e) % 7 == 6) continue; // breathing space
                    float f = Midi(mel[idx]);
                    int st = s0 + (int)(e * beat * 0.5f * Rate);
                    int nl = (int)(beat * 0.5f * Rate);
                    for (int i = 0; i < nl && st + i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float sq = Mathf.Sin(2 * Mathf.PI * f * t) + Mathf.Sin(6 * Mathf.PI * f * t) / 3f * 0.6f + Mathf.Sin(10 * Mathf.PI * f * t) / 5f * 0.3f;
                        d[st + i] += sq * 0.09f * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 4f);
                    }
                }
                // drums: kick on 1 & 3, hat on every 8th, clap on 2 & 4 (soft)
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    float t = i / (float)Rate;
                    if (b % 2 == 0) d[s0 + i] += Mathf.Sin(2 * Mathf.PI * (110f - t * 260f) * t) * 0.3f * Mathf.Exp(-t * 18f);
                    float th = Mathf.Repeat(t, beat * 0.5f);
                    d[s0 + i] += (float)(rng.NextDouble() * 2 - 1) * 0.035f * Mathf.Exp(-th * 70f);
                    if (b % 2 == 1) d[s0 + i] += (float)(rng.NextDouble() * 2 - 1) * 0.07f * Mathf.Exp(-t * 30f);
                }
            }
            // gentle limiter
            float peak = 0;
            foreach (float s in d) peak = Mathf.Max(peak, Mathf.Abs(s));
            float g = peak > 0.8f ? 0.8f / peak : 1f;
            for (int i = 0; i < n; i++) d[i] *= g;
            return d;
        }

        // ---------- WAV + import ----------
        private static AudioClip Save(string path, float[] data, bool music, bool loop = false)
        {
            EditorAssets.EnsureFolder(Path.GetDirectoryName(path));
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = data.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(bytes);
                foreach (float s in data) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32000f));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            imp.forceToMono = true;
            imp.loadInBackground = music;
            AudioImporterSampleSettings s2 = imp.defaultSampleSettings;
            s2.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s2.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            s2.quality = 0.45f;
            s2.preloadAudioData = true;
            imp.defaultSampleSettings = s2;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
