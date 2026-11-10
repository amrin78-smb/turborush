using UnityEngine;

namespace TurboRush.Audio
{
    /// <summary>All clips in one place. Generated placeholders live in Assets/Audio; replace clips here to re-skin.</summary>
    [CreateAssetMenu(menuName = "Turbo Rush/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public AudioClip menuMusic;
        public AudioClip raceMusic;
        public AudioClip engineLoop;
        public AudioClip offroadLoop;
        public AudioClip[] sfx = new AudioClip[14]; // indexed by Sfx
        [Range(0f, 1f)] public float raceMusicVolume = 0.35f;
        [Range(0f, 1f)] public float engineVolume = 0.7f;

        public AudioClip Get(Sfx s)
        {
            int i = (int)s;
            return sfx != null && i < sfx.Length ? sfx[i] : null;
        }
    }
}
