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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSongStats(); // Load on Startup
        }
        else
        {
            Destroy(gameObject);
        }
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

    public void PlayGame()
    {
        if (SelectedSong != null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
        }
        else
        {
            Debug.LogWarning("SongManager: No song selected to play!");
        }
    }
}
