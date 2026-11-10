using TurboRush.Save;
using UnityEngine;

namespace TurboRush.Audio
{
    public enum Sfx { Click, Countdown, Go, Checkpoint, Lap, Overtake, Finish, Bump, Barrier, Unlock, Star, Back, Jump, Land }

    /// <summary>
    /// Persistent audio: music, UI/race one-shots, engine loop (pitch/volume follow speed) and off-road rumble.
    /// Pause uses AudioListener.pause; UI sounds ignore it so menus still click.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private AudioLibrary m_Lib;
        private AudioSource m_Music, m_Ui, m_Race, m_Engine, m_Rumble;
        private SettingsData m_Settings;
        private AudioClip m_CurrentMusic;
        private float m_MusicGain = 1f;

        public void Init(AudioLibrary lib, SettingsData settings)
        {
            m_Lib = lib;
            m_Settings = settings;
            m_Music = Make("Music", true);
            m_Music.ignoreListenerPause = true;
            m_Ui = Make("UI", false);
            m_Ui.ignoreListenerPause = true;
            m_Race = Make("RaceSfx", false);
            m_Engine = Make("Engine", true);
            m_Rumble = Make("Rumble", true);
            if (lib != null)
            {
                m_Engine.clip = lib.engineLoop;
                m_Rumble.clip = lib.offroadLoop;
            }
            ApplySettings();
        }

        private AudioSource Make(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        public void ApplySettings()
        {
            if (m_Settings == null || m_Music == null) return;
            m_Music.volume = (m_Settings.music ? m_Settings.musicVolume : 0f) * m_MusicGain;
            float sfx = m_Settings.sfx ? m_Settings.sfxVolume : 0f;
            m_Ui.volume = sfx;
            m_Race.volume = sfx;
            if (!m_Settings.sfx) { m_Engine.volume = 0f; m_Rumble.volume = 0f; }
        }

        public void PlayMusic(bool race)
        {
            if (m_Lib == null) return;
            AudioClip clip = race ? m_Lib.raceMusic : m_Lib.menuMusic;
            m_MusicGain = race ? m_Lib.raceMusicVolume : 1f;
            ApplySettings();
            if (clip == m_CurrentMusic && m_Music.isPlaying) return;
            m_CurrentMusic = clip;
            m_Music.clip = clip;
            if (clip != null) m_Music.Play(); else m_Music.Stop();
        }

        public void StopMusic()
        {
            m_Music.Stop();
            m_CurrentMusic = null;
        }

        public void Play(Sfx s, float volume = 1f, bool ui = false)
        {
            AudioClip c = m_Lib != null ? m_Lib.Get(s) : null;
            if (c == null || m_Settings == null || !m_Settings.sfx) return;
            (ui ? m_Ui : m_Race).PlayOneShot(c, volume);
        }

        /// <summary>Engine follows speed: pitch 0.75→1.9, volume rises gently. active=false fades it out.</summary>
        public void SetEngine(bool active, float speed01, bool offroad)
        {
            if (m_Engine == null) return;
            bool on = active && m_Settings != null && m_Settings.sfx && m_Lib != null;
            float sfx = on ? m_Settings.sfxVolume * m_Lib.engineVolume : 0f;
            if (on && !m_Engine.isPlaying && m_Engine.clip != null) m_Engine.Play();
            if (!on && m_Engine.isPlaying && m_Engine.volume < 0.01f) m_Engine.Stop();
            m_Engine.pitch = Mathf.Lerp(0.75f, 1.9f, speed01);
            m_Engine.volume = Mathf.MoveTowards(m_Engine.volume, sfx * Mathf.Lerp(0.55f, 1f, speed01), Time.unscaledDeltaTime * 2f);

            float rumble = on && offroad ? m_Settings.sfxVolume * 0.5f : 0f;
            if (rumble > 0f && !m_Rumble.isPlaying && m_Rumble.clip != null) m_Rumble.Play();
            m_Rumble.volume = Mathf.MoveTowards(m_Rumble.volume, rumble, Time.unscaledDeltaTime * 3f);
            if (m_Rumble.volume <= 0f && m_Rumble.isPlaying) m_Rumble.Stop();
        }

        public void StopEngine()
        {
            if (m_Engine == null) return;
            m_Engine.Stop();
            m_Engine.volume = 0f;
            m_Rumble.Stop();
            m_Rumble.volume = 0f;
        }

        public void SetPaused(bool paused) => AudioListener.pause = paused;
    }
}
