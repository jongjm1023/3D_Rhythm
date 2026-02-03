using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SongData
{
    public string title;
    public string artist;
    public int bpm;
    public int level; // New
    public Texture2D albumCover;
    public AudioClip musicInfo; 
    public TextAsset beatmap;

    // Runtime/Saved Stats
    public int maxScore;
    public int maxCombo;

    public string GetUniqueId() // Helper for saving
    {
        return $"{title}_{artist}";
    }
}

public class SongManager : MonoBehaviour
{
    public static SongManager Instance { get; private set; }

    [Header("Library")]
    public List<SongData> songLibrary = new List<SongData>();

    public SongData SelectedSong { get; private set; }
    public float NoteSpeed { get; set; } = 10f; // Global Speed Setting

    [Header("Audio")]
    [SerializeField] private AudioClip menuBGM;
    [SerializeField] private AudioClip uiClickSFX; // New
    [SerializeField] private AudioClip hitSFX;     // New
    
    private AudioSource musicSource;
    private AudioSource sfxSource; // New dedicated source for SFX

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Setup AudioSource for Music
            musicSource = gameObject.GetComponent<AudioSource>();
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;

            // Setup AudioSource for SFX
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.priority = 0; // Highest priority for hit sounds
            sfxSource.spatialBlend = 0; // 2D Sound
            sfxSource.bypassEffects = true;
            sfxSource.bypassListenerEffects = true;
            sfxSource.bypassReverbZones = true;

            // Load Settings
            NoteSpeed = PlayerPrefs.GetFloat("NoteSpeed", 10f);
            
            // Volume Settings
            BGMVolume = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
            SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
            UpdateAudioVolumes();

            LoadSongStats(); // Load on Startup
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public float BGMVolume { get; private set; } = 1.0f;
    public float SFXVolume { get; private set; } = 1.0f;

    public void SetBGMVolume(float volume)
    {
        BGMVolume = volume;
        UpdateAudioVolumes();
        PlayerPrefs.SetFloat("BGMVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = volume;
        UpdateAudioVolumes();
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
    }

    private void UpdateAudioVolumes()
    {
        if (musicSource != null) musicSource.volume = BGMVolume;
        if (sfxSource != null) sfxSource.volume = SFXVolume;
    }

    public void SaveSongStats(SongData data)
    {
        if (data == null) return;
        string id = data.GetUniqueId();
        PlayerPrefs.SetInt($"{id}_Score", data.maxScore);
        PlayerPrefs.SetInt($"{id}_Combo", data.maxCombo);
        PlayerPrefs.Save();
        Debug.Log($"Saved Stats for {id}: Score {data.maxScore}, Combo {data.maxCombo}");
    }

    public void LoadSongStats()
    {
        foreach (var song in songLibrary)
        {
            string id = song.GetUniqueId();
            if (PlayerPrefs.HasKey($"{id}_Score"))
            {
                song.maxScore = PlayerPrefs.GetInt($"{id}_Score");
                song.maxCombo = PlayerPrefs.GetInt($"{id}_Combo");
            }
        }
        Debug.Log("Loaded Song Stats.");
    }

    public void SelectSong(SongData song)
    {
        SelectedSong = song;
        Debug.Log($"Song Selected: {song.title} by {song.artist}");
    }

    public void PlayMenuMusic()
    {
        if (menuBGM == null || musicSource == null) return;
        if (musicSource.clip == menuBGM && musicSource.isPlaying) return;

        musicSource.Stop();
        musicSource.clip = menuBGM;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayPreview(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    public void PlayGame()
    {
        if (SelectedSong != null)
        {
            StopMusic(); // Stop menu/preview music before gameplay
            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
        }
        else
        {
            Debug.LogWarning("SongManager: No song selected to play!");
        }
    }

    // SFX Methods
    public void PlayUIClickSFX()
    {
        if (sfxSource != null && uiClickSFX != null)
        {
            sfxSource.PlayOneShot(uiClickSFX);
        }
    }

    public void PlayHitSFX()
    {
        if (sfxSource != null && hitSFX != null)
        {
            sfxSource.PlayOneShot(hitSFX);
        }
    }
}
