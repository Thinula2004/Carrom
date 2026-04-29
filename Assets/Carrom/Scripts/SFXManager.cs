using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BEKStudio
{
    /// <summary>
    /// Lightweight pooled SFX manager that plays AudioClips at world positions so playback
    /// isn't tied to the original GameObject (prevents clipped/vanishing sounds when the coin is
    /// disabled/destroyed right after collision).
    /// - Create a single SFXManager in your scene (put on an empty GameObject).
    /// - It will create a small pool of AudioSources. The pool expands if needed.
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance { get; private set; }

        [Header("Pool")]
        [SerializeField] private int initialPoolSize = 16;

        // created child GameObjects with AudioSources
        private Queue<AudioSource> pool = new Queue<AudioSource>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitPool();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitPool()
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreatePooledSource();
            }
        }

        private AudioSource CreatePooledSource()
        {
            var go = new GameObject("SFXSource");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            // default 2D (unspatialized). Caller can override src.spatialBlend if required.
            src.spatialBlend = 0f;
            pool.Enqueue(src);
            go.SetActive(false);
            return src;
        }

        /// <summary>
        /// Play an AudioClip at world position with given volume and pitch. spatialBlend 0 = 2D, 1 = 3D.
        /// </summary>
        public void PlayAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatialBlend = 0f)
        {
            if (clip == null) return;

            AudioSource src = GetSourceFromPool();
            var go = src.gameObject;
            go.SetActive(true);
            go.transform.position = position;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = Mathf.Clamp(pitch, -3f, 3f);
            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.Play();

            float playTime = clip.length / Mathf.Max(0.01f, Mathf.Abs(src.pitch));
            StartCoroutine(ReleaseAfter(src, playTime));
        }

        private AudioSource GetSourceFromPool()
        {
            if (pool.Count == 0)
            {
                // expand pool on demand
                return CreatePooledSource();
            }
            var src = pool.Dequeue();
            return src;
        }

        private IEnumerator ReleaseAfter(AudioSource src, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (src == null) yield break;
            // stop and reset
            src.Stop();
            src.clip = null;
            src.gameObject.SetActive(false);
            pool.Enqueue(src);
        }
    }
}