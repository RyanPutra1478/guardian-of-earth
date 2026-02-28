using UnityEngine;

namespace NinuNinu.Systems
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        public AudioSource sfxSource;
        public AudioSource bgmSource;

        [Header("Common SFX Clips")]
        public AudioClip clickSFX;
        
        [Header("BGM Settings")]
        public AudioClip bgmClip;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // Auto-setup AudioSources if not assigned
                SetupAudioSources();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Auto-play BGM if clip is assigned
            if (bgmClip != null)
            {
                PlayMusic(bgmClip);
            }
        }

        private void SetupAudioSources()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
            }
        }

        /// <summary>
        /// Memutar musik latar.
        /// </summary>
        public void PlayMusic(AudioClip clip, float volume = 0.5f)
        {
            if (clip == null || bgmSource == null) return;

            // Jangan restart jika musik sudah memutar clip yang sama
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.volume = volume;
            bgmSource.Play();
        }

        /// <summary>
        /// Plays a sound effect once.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Global shortcut to play the standard click sound.
        /// </summary>
        public void PlayClick()
        {
            if (clickSFX != null)
            {
                PlaySFX(clickSFX);
            }
        }
    }
}
