using UnityEngine;

public class soundsCheck : MonoBehaviour
{
    public AudioSource musicSource;
    public AudioClip samplesound;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void PlaySound(AudioClip clip)
    {
        if (clip != null && musicSource != null)
        {
            musicSource.PlayOneShot(clip);
        }
    }

    public void PlayLoseSound()
    {
        PlaySound(samplesound);
    }
}
