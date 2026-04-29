using Photon.Pun;
using UnityEngine;

namespace BEKStudio
{
    public class Board : MonoBehaviourPun
    {
        [Header("Velocity Settings")]
        [SerializeField] private float minVelocity = 0.5f;  // Minimum velocity to play sound
        [SerializeField] private float maxVelocity = 10f;   // Velocity for maximum sound intensity
        [SerializeField] private float minVolume = 0.3f;
        [SerializeField] private float maxVolume = 1f;

        private PhotonView photonView;

        [Header("Disk and board collide sounds")]
        public AudioClip discCollideSlow;
        public AudioClip discCollideMedium;
        public AudioClip discCollideHard;

        [Header("coin and board collide sounds")]
        public AudioClip CoinCollideSlow;
        public AudioClip CoinCollideMedium;
        public AudioClip CoinCollideHard;

        // fallback AudioSources (optional) if you don't use SFXManager
        public AudioSource DiskCollideSoundSource;
        public AudioSource CoinCollideSoundSource;

        void Start()
        {
            photonView = GetComponent<PhotonView>();

            // Add PhotonView component if it doesn't exist
            if (photonView == null)
            {
                photonView = gameObject.AddComponent<PhotonView>();
            }
        }

        // Compute volume only from velocity — pitch is no longer changed by velocity
        private float GetVolumeFromVelocity(float velocity)
        {
            float normalized = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);
            float volume = Mathf.Lerp(minVolume, maxVolume, normalized);
            return Mathf.Clamp01(volume);
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            string tag = collision.gameObject.tag;
            Vector3 contactPos = transform.position;
            if (collision.contacts != null && collision.contacts.Length > 0)
                contactPos = collision.contacts[0].point;

            if (tag == "Player")
            {
                float velocity = collision.relativeVelocity.magnitude;

                if (velocity >= minVelocity)
                {
                    string soundType = GetSoundTypeFromVelocity(velocity);
                    if (!PhotonController.Instance.offlineMatch)
                    {
                        photonView.RPC("PlayBoardCollisionSoundRPC", RpcTarget.All, soundType, velocity, contactPos);
                    }
                    else
                    {
                        PlayBoardCollisionSoundRPC(soundType, velocity, contactPos);
                    }
                }
            }
            else if (tag == "White" || tag == "Black" || tag == "Red")
            {
                float velocity = collision.relativeVelocity.magnitude;

                if (velocity >= minVelocity)
                {
                    string soundType = GetSoundTypeFromVelocity(velocity);
                    if (!PhotonController.Instance.offlineMatch)
                    {
                        photonView.RPC("PlayBoardCollisionCoinSoundRPC", RpcTarget.All, soundType, velocity, contactPos);
                    }
                    else
                    {
                        PlayBoardCollisionCoinSoundRPC(soundType, velocity, contactPos);
                    }
                }
            }
        }

        [PunRPC]
        void PlayBoardCollisionSoundRPC(string soundType, float velocity, Vector3 pos)
        {
            AudioClip clip = discCollideSlow;
            switch (soundType)
            {
                case "Small": clip = discCollideSlow; break;
                case "Medium": clip = discCollideMedium != null ? discCollideMedium : discCollideSlow; break;
                case "Hard": clip = discCollideHard != null ? discCollideHard : discCollideSlow; break;
            }

            float volume = GetVolumeFromVelocity(velocity);
            float fixedPitch = 1f; // keep pitch constant

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayAtPosition(clip, pos, volume, fixedPitch, 0f);
                return;
            }

            if (DiskCollideSoundSource != null)
            {
                // Do not modify DiskCollideSoundSource.pitch so pitch remains whatever is configured
                DiskCollideSoundSource.PlayOneShot(clip, volume);
            }
        }

        [PunRPC]
        void PlayBoardCollisionCoinSoundRPC(string soundType, float velocity, Vector3 pos)
        {
            AudioClip clip = CoinCollideSlow;
            switch (soundType)
            {
                case "Small": clip = CoinCollideSlow; break;
                case "Medium": clip = CoinCollideMedium != null ? CoinCollideMedium : CoinCollideSlow; break;
                case "Hard": clip = CoinCollideHard != null ? CoinCollideHard : CoinCollideSlow; break;
            }

            float volume = GetVolumeFromVelocity(velocity);
            float fixedPitch = 1f; // keep pitch constant

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayAtPosition(clip, pos, volume, fixedPitch, 0f);
                return;
            }

            if (CoinCollideSoundSource != null)
            {
                // Do not modify CoinCollideSoundSource.pitch so pitch remains whatever is configured
                CoinCollideSoundSource.PlayOneShot(clip, volume);
            }
        }

        private string GetSoundTypeFromVelocity(float velocity)
        {
            float normalizedVelocity = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);

            if (normalizedVelocity < 0.33f)
                return "Small";
            else if (normalizedVelocity < 0.66f)
                return "Medium";
            else
                return "Hard";
        }
    }
}