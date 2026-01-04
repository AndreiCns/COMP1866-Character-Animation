using System.Collections;
using UnityEngine;

public class JukeboxAudio : MonoBehaviour
{
    [Header("Clips")]
    public AudioClip[] bgSongs;          // set 3 songs here
    public AudioClip[] ambienceClips;    // set your ambience one-shots here

    [Header("Volumes")]
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    [Range(0f, 1f)] public float ambienceVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    [Header("Ambience Timing (seconds)")]
    public float ambienceDelayMin = 4f;
    public float ambienceDelayMax = 10f;

    [Header("Optional")]
    public bool dontDestroyOnLoad = true;

    private AudioSource musicSource;
    private AudioSource ambienceSource;
    private AudioSource sfxSource;

    private static JukeboxAudio instance;

    void Awake()
    {
        // Simple singleton so other scripts can call PlaySFX easily
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        // Create 3 AudioSources on this one GameObject
        musicSource = gameObject.AddComponent<AudioSource>();
        ambienceSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();

        // Music config
        musicSource.loop = false;            // we do manual "next random"
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        // Ambience config
        ambienceSource.loop = false;
        ambienceSource.playOnAwake = false;
        ambienceSource.volume = ambienceVolume;

        // SFX config
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;
    }

    void Start()
    {
        StartCoroutine(MusicLoop());
        StartCoroutine(AmbienceLoop());
    }

    // ---------------- MUSIC ----------------
    IEnumerator MusicLoop()
    {
        while (true)
        {
            if (bgSongs != null && bgSongs.Length > 0)
            {
                var clip = bgSongs[Random.Range(0, bgSongs.Length)];
                musicSource.clip = clip;
                musicSource.volume = musicVolume;
                musicSource.Play();

                // wait until this track finishes
                yield return new WaitForSeconds(clip.length);
            }
            else
            {
                yield return null;
            }
        }
    }

    // ---------------- AMBIENCE ----------------
    IEnumerator AmbienceLoop()
    {
        while (true)
        {
            // wait a random delay between ambience sounds
            float wait = Random.Range(ambienceDelayMin, ambienceDelayMax);
            yield return new WaitForSeconds(wait);

            if (ambienceClips != null && ambienceClips.Length > 0)
            {
                var clip = ambienceClips[Random.Range(0, ambienceClips.Length)];
                ambienceSource.PlayOneShot(clip, ambienceVolume);
            }
        }
    }

    // ---------------- SFX API ----------------
    public static void PlaySFX(AudioClip clip, float volumeMul = 1f)
    {
        if (instance == null || clip == null) return;
        instance.sfxSource.PlayOneShot(clip, instance.sfxVolume * volumeMul);
    }

    public static void SetMusicVolume(float v)
    {
        if (instance == null) return;
        instance.musicVolume = Mathf.Clamp01(v);
        instance.musicSource.volume = instance.musicVolume;
    }

    public static void SetAmbienceVolume(float v)
    {
        if (instance == null) return;
        instance.ambienceVolume = Mathf.Clamp01(v);
        instance.ambienceSource.volume = instance.ambienceVolume;
    }

    public static void SetSFXVolume(float v)
    {
        if (instance == null) return;
        instance.sfxVolume = Mathf.Clamp01(v);
        instance.sfxSource.volume = instance.sfxVolume;
    }
}
