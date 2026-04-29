using UnityEngine;

namespace BEKStudio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [Header("Audio Sources")]
        public AudioSource sfxSource; // For one-shot sound effects
        public AudioSource loopSource; // For looping sounds
        public AudioSource musicSource; // For background music (optional)

        [Header("Sound Effects")]
     
        public AudioClip coinCollectingSound;
        public AudioClip winSound;
        public AudioClip loseSound;
        //public AudioClip click;
        // public AudioClip BreakSmall;
        // public AudioClip BreakMedium;
        // public AudioClip BreakHard;

        // public AudioClip shootSmall;
        // public AudioClip shootMedium;
        // public AudioClip shootHard;

        // public AudioClip collideSmall;
        // public AudioClip collideMedium;
        // public AudioClip collideHard;
        public AudioClip rollingSound; // Looping
        public AudioClip timeoutSound; // Looping


        // public AudioClip discCollideSlow;
        // public AudioClip discCollideMedium;
        // public AudioClip discCollideHard;

        // public AudioClip coinPot;
        public AudioClip redPotSound;

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        public float sfxVolume = 1f;
        [Range(0f, 1f)]
        public float musicVolume = 0.5f;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            Application.runInBackground = true;
            // Load saved volume settings
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            UpdateVolumes();
            StopRollingSound();
        }

        // === LOOPING SOUNDS ===

       
        public void PlayRollingSound()
        {
            if (rollingSound != null && loopSource != null)
            {
                // Only start if not already playing this sound
                if (loopSource.clip != rollingSound || !loopSource.isPlaying)
                {
                    loopSource.clip = rollingSound;
                    loopSource.loop = true;
                    loopSource.volume = sfxVolume;
                    loopSource.Play();
                }
            }
        }

        public void StopRollingSound()
        {
            if (loopSource != null && loopSource.clip == rollingSound)
            {
                loopSource.Stop();
                loopSource.clip = null;
            }
        }



        public void PlayTimeoutSound()
        {
            if (timeoutSound != null && loopSource != null)
            {
                // Only start if not already playing this sound
                if (loopSource.clip != timeoutSound || !loopSource.isPlaying)
                {
                    loopSource.clip = timeoutSound;
                    loopSource.loop = true;
                    loopSource.volume = sfxVolume;
                    loopSource.Play();
                }
            }
        }

        public void StopTimeoutSound()
        {
            if (loopSource != null && loopSource.clip == timeoutSound)
            {
                loopSource.Stop();
                loopSource.clip = null;
            }
        }


  

        // Stop any looping sound
        public void StopLoopingSound()
        {
            if (loopSource != null)
            {
                loopSource.Stop();
                loopSource.clip = null;
            }
        }

        public void StopSFXSound()
        {
            if (sfxSource != null )
            {
                sfxSource.Stop();
                sfxSource.clip = null;
            }
        }

        // === ONE-SHOT SOUNDS ===

        public void PlaySound(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }

     

        public void PlayCoinCollectSound()
        {
            PlaySound(coinCollectingSound);
        }

      

        public void PlayWinSound()
        {
            PlaySound(winSound);
        }

        public void PlayLoseSound()
        {
            PlaySound(loseSound);
        }

  

 


        


        public void PlayRedCoinPottedSound()
        {
            PlaySound(redPotSound);
        }

        // === VOLUME CONTROLS ===

        public void SetSFXVolume(float volume)
        {
            sfxVolume = volume;
            PlayerPrefs.SetFloat("SFXVolume", volume);
            UpdateVolumes();
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = volume;
            PlayerPrefs.SetFloat("MusicVolume", volume);
            UpdateVolumes();
        }

        private void UpdateVolumes()
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
            if (loopSource != null)
            {
                loopSource.volume = sfxVolume;
            }
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
        }
    }
}