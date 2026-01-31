using UnityEngine;
using System.Collections.Generic;

public class NoteSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject notePrefab;

    [Header("Map Settings")]
    [SerializeField] private TextAsset beatmapFile;
    [SerializeField] private AudioClip musicClip; // User can drag MP3 here
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float noteApproachTime = 2.0f; // Time for note to travel from spawn to hit line
    [SerializeField] private float spawnZ = 50f;
    [SerializeField] private float noteSpeed = 10f;
    [Tooltip("Positive: Delay Notes (Spawn Later), Negative: Advance Notes (Spawn Earlier)")]
    [SerializeField] private float noteSpawnOffset = 0f;

    // Specific spawn positions (Restored)
    private readonly float[] xPositions = { -3.75f, -1.25f, 1.25f, 3.75f };
    private readonly float[] yPositions = { 1.25f, 4f, 6.5f };

    private List<MapData.HitInfo> hitNotes = new List<MapData.HitInfo>();
    private int currentNoteIndex = 0;
    private bool isPlaying = false;
    private float mapStartTime;

    private BeatmapParser parser;

    [System.Serializable]
    public struct FloorColorSettings
    {
        public string label; // Just for inspector readability (e.g., "1st Floor")
        public Color noteColor;
        public Color glowColor;
    }

    [Header("Color Settings")]
    [SerializeField] private FloorColorSettings[] floorSettings; // Assign 3 elements in Inspector

    private void Start()
    {
        // Initialize default settings if empty (fallback)
        if (floorSettings == null || floorSettings.Length == 0)
        {
            floorSettings = new FloorColorSettings[3];
            // defaults
            floorSettings[0] = new FloorColorSettings { label = "1F", noteColor = new Color(150f/255f, 0, 1), glowColor = new Color(150f/255f, 0, 1) * 0.6f };
            floorSettings[1] = new FloorColorSettings { label = "2F", noteColor = Color.cyan, glowColor = Color.cyan * 0.6f };
            floorSettings[2] = new FloorColorSettings { label = "3F", noteColor = new Color(1, 0, 150f/255f), glowColor = new Color(1, 0, 150f/255f) * 0.6f };
        }
        
        parser = gameObject.AddComponent<BeatmapParser>();
        
        // Auto-get AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) 
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (beatmapFile != null)
        {
            hitNotes = parser.Parse(beatmapFile.text);
            StartGame();
        }
    }

    private void StartGame()
    {
        if (hitNotes.Count == 0) 
        {
            Debug.LogWarning("NoteSpawner: No notes found to play.");
            return;
        }
        
        // Sort by time just in case
        hitNotes.Sort((a, b) => a.time.CompareTo(b.time));

        isPlaying = true;
        mapStartTime = Time.time;
        Debug.Log("NoteSpawner: Game Started!");
        
        if (audioSource != null)
        {
            // Force settings to ensure audibility
            audioSource.spatialBlend = 0f; // 0 = 2D Sound (Hear everywhere), 1 = 3D Sound
            audioSource.volume = 1.0f;
            audioSource.loop = false;

            if (musicClip != null)
            {
                audioSource.clip = musicClip;
                Debug.Log($"NoteSpawner: Assigned Music Clip {musicClip.name} (Length: {musicClip.length:F2}s, Channels: {musicClip.channels}, Freq: {musicClip.frequency})");
                
                if (musicClip.loadState != AudioDataLoadState.Loaded)
                {
                    Debug.LogWarning($"NoteSpawner: Clip Load State is {musicClip.loadState}. Attempting to Load...");
                    musicClip.LoadAudioData();
                }
            }
            else
            {
                Debug.LogWarning("NoteSpawner: Music Clip is missing!");
            }
            
            audioSource.enabled = true; // Ensure component is enabled
            audioSource.mute = false;   // Ensure not muted
            audioSource.Play();
            
            Debug.Log($"[{gameObject.name}:{gameObject.GetInstanceID()}] NoteSpawner: Playing... IsPlaying: {audioSource.isPlaying}, Time: {audioSource.time}");
            Debug.Log($"AudioListener Info - Pause: {AudioListener.pause}, Volume: {AudioListener.volume}");
        }
    }

    private float debugTimer = 0f;

    private void Update()
    {
        if (!isPlaying) return;

        // Current Song Time
        // If AudioSource is playing, use its time for exact sync. Otherwise use Time.time offset.
        float songTime = (audioSource != null && audioSource.isPlaying) 
            ? audioSource.time 
            : (Time.time - mapStartTime);

        // Look-ahead for spawning
        // Note needs to spawn 'noteApproachTime' seconds BEFORE the hit time.
        // SpawnTime = HitTime - ApproachTime.
        // If (SongTime >= SpawnTime) -> Spawn!
        
        while (currentNoteIndex < hitNotes.Count)
        {
            MapData.HitInfo noteData = hitNotes[currentNoteIndex];
            float approachTime = spawnZ / noteSpeed; 
            float spawnTime = (noteData.time + noteSpawnOffset) - approachTime;

            if (songTime >= spawnTime)
            {
                SpawnSpecificNote(noteData);
                currentNoteIndex++;
            }
            else
            {
                break; // Next note is too far in future
            }
        }
    }

    private void SpawnSpecificNote(MapData.HitInfo data)
    {
        if (notePrefab == null) return;

        // Lane X Mapping (0,1,2,3)
        // xPositions array matches 0,1,2,3?
        // xPositions = { -3.75f, -1.25f, 1.25f, 3.75f }
        float targetX = 0f;
        if (data.lane >= 0 && data.lane < xPositions.Length)
            targetX = xPositions[data.lane];

        // Floor Y Mapping (0,1,2)
        // yPositions = { 1.75f, 4.25f, 6.75f }
        // MapData: 0=1F, 1=2F, 2=3F
        float targetY = 0f;
        if (data.floor >= 0 && data.floor < yPositions.Length)
            targetY = yPositions[data.floor];

        Vector3 spawnPos = new Vector3(targetX, targetY, spawnZ);

        GameObject noteObj = Instantiate(notePrefab, spawnPos, Quaternion.identity);
        Note noteScript = noteObj.GetComponent<Note>();
        
        if (noteScript != null)
        {
            Color noteColor = Color.white;
            Color glowColor = Color.white;

            // Use Configured Colors
            if (floorSettings != null && data.floor >= 0 && data.floor < floorSettings.Length)
            {
                noteColor = floorSettings[data.floor].noteColor;
                glowColor = floorSettings[data.floor].glowColor;
            }
            else
            {
                // Fallback hardcoded if settings fails
                switch (data.floor)
                {
                    case 0: noteColor = new Color(150f/255f, 0f, 1f); break;
                    case 1: noteColor = new Color(0f, 1f, 1f); break;
                    case 2: noteColor = new Color(1f, 0f, 150f/255f); break;
                }
                glowColor = noteColor * 0.6f;
            }

            noteScript.SetColor(noteColor, glowColor);

            // Initialize Data and Register
            noteScript.Initialize(data.floor, data.lane, noteSpeed, data.type, data.length);
            GameManager.Instance.RegisterNote(noteScript);
        }
    }
}
