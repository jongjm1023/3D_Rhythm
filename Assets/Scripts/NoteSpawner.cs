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
    private double dspStartTime;
    private bool audioDeviceChanged = false;

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

        // Load Offset from Settings (Stored in ms, convert to seconds)
        // Default 0 if not set
        int offsetMs = PlayerPrefs.GetInt("AudioOffset", 0);
        noteSpawnOffset = offsetMs / 1000.0f;
        
        // Load Note Speed
        if (SongManager.Instance != null)
        {
            noteSpeed = SongManager.Instance.NoteSpeed;
        }
        else
        {
            noteSpeed = PlayerPrefs.GetFloat("NoteSpeed", 10f);
        }

        Debug.Log($"NoteSpawner: Loaded Audio Offset: {offsetMs}ms, Note Speed: {noteSpeed}");

        if (beatmapFile != null)
        {
            // Check for SongManager
            if (SongManager.Instance != null && SongManager.Instance.SelectedSong != null)
            {
                var selected = SongManager.Instance.SelectedSong;
                Debug.Log($"NoteSpawner: Loading Selected Song - {selected.title}");
                
                if (selected.beatmap != null)
                {
                    hitNotes = parser.Parse(selected.beatmap.text);
                }
                
                if (selected.musicInfo != null)
                {
                    musicClip = selected.musicInfo;
                }
            }
            else
            {
               hitNotes = parser.Parse(beatmapFile.text);
            }

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
        
        // Use DSP Time for precision (Audio Clock)
        // Add 0.5s delay to allow audio system to prepare
        dspStartTime = AudioSettings.dspTime + 0.5;
        mapStartTime = (float)dspStartTime; // For debug
        
        Debug.Log($"NoteSpawner: Game Starting at DSP {dspStartTime}");
        
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
            
            // Critical: PlayScheduled ensures synchronization
            audioSource.PlayScheduled(dspStartTime);
            
            Debug.Log($"[{gameObject.name}:{gameObject.GetInstanceID()}] NoteSpawner: Scheduled Play at {dspStartTime}");
            Debug.Log($"AudioListener Info - Pause: {AudioListener.pause}, Volume: {AudioListener.volume}");
        }
    }

    private void OnEnable()
    {
        AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
    }

    private void OnDisable()
    {
        AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
    }

    private void OnAudioConfigurationChanged(bool deviceWasChanged)
    {
        if (deviceWasChanged)
        {
            audioDeviceChanged = true;
            Debug.Log("NoteSpawner: Audio Device Change Detected!");
        }
    }

    private float debugTimer = 0f;

    private void Update()
    {
        if (audioDeviceChanged)
        {
            audioDeviceChanged = false;
            if (audioSource != null && isPlaying)
            {
                // Restart playback to adapt to new device (sometimes required)
                float time = audioSource.time;
                audioSource.Stop();
                audioSource.Play();
                audioSource.time = time;
                Debug.Log($"NoteSpawner: Restored Audio Playback at {time}s due to Device Change");
            }
        }

        if (!isPlaying) return;

        // Skip to First Note Debug
        if (UnityEngine.InputSystem.Keyboard.current != null && 
            UnityEngine.InputSystem.Keyboard.current.wKey.wasPressedThisFrame)
        {
            SkipToFirstNote();
        }

        // Precise Song Time using DSP Clock
        // This is decoupled from Frame Rate and Main Thread Jitter.
        float songTime = (float)(AudioSettings.dspTime - dspStartTime);

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

        // Check for Game End
        // If all notes are spawned AND no active notes in GameManager (optional, or just time)
        // Let's use Time: If songTime > lastNoteTime + 2s?
        if (hitNotes.Count > 0 && currentNoteIndex >= hitNotes.Count)
        {
            // All notes spawned.
            
            // Wait for all active notes to be destroyed/hit
            bool allNotesCleared = !GameManager.Instance.HasActiveNotes;
            
            // Failsafe: If notes are stuck for too long (e.g. 10s past last note), force end
            float lastNoteTime = hitNotes[hitNotes.Count - 1].time;
            bool timeOut = songTime > lastNoteTime + 20.0f;

            if (allNotesCleared || timeOut)
            {
                // Trigger End Game (One-shot check)
                if (isPlaying)
                {
                    isPlaying = false;
                    GameManager.Instance.EndGame();
                }
            }
        }
    }

    private void SkipToFirstNote()
    {
        if (hitNotes.Count == 0) return;

        // Calculate Target Time
        // We want to be 2.0 seconds BEFORE the first note spawns
        MapData.HitInfo firstNote = hitNotes[0];
        
        float approachTime = spawnZ / noteSpeed; 
        float firstSpawnTime = (firstNote.time + noteSpawnOffset) - approachTime;
        
        // Target: 2.0s before spawn
        float targetTime = firstSpawnTime - 2.0f;
        
        // Clamp to 0
        if (targetTime < 0) targetTime = 0f;

        // Don't skip if we are already past that time
        float currentSongTime = (audioSource != null && audioSource.isPlaying) ? audioSource.time : (Time.time - mapStartTime);
        if (currentSongTime >= targetTime) 
        {
            Debug.Log("NoteSpawner: Already past skip target time.");
            return;
        }

        Debug.Log($"NoteSpawner: Skipping from {currentSongTime:F2}s to {targetTime:F2}s (First Spawn: {firstSpawnTime:F2}s)");

        // Apply Skip with Precision
        if (audioSource != null)
        {
            audioSource.Stop(); // Stop to prepare fresh schedule
            audioSource.time = targetTime;
            
            // Schedule resume slightly in future to allow buffering/sync
            double resumeDSP = AudioSettings.dspTime + 0.05; // 50ms buffer
            audioSource.PlayScheduled(resumeDSP);
            
            // Re-align DSP Reference
            // SongTime at resumeDSP will be 'targetTime'
            // SongTime = CurrentDSP - dspStartTime
            // targetTime = resumeDSP - dspStartTime
            // dspStartTime = resumeDSP - targetTime
            dspStartTime = resumeDSP - targetTime;
             
            Debug.Log($"NoteSpawner: Skipped to {targetTime:F2}s, Resuming at DSP {resumeDSP:F4}");
        }
        else
        {
             // Fallback if no audio source
             dspStartTime = AudioSettings.dspTime - targetTime;
        }
        
        mapStartTime = (float)dspStartTime;
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

        // Match Visual Head to Timing
        // Visual Head is at (Pos - Length/2).
        // Standard logic puts Pos at Target at Time. Thus Head is Early.
        // Offset Z by +Length/2 adds travel distance, delaying it to sync Head with Time.
        float effectiveLength = (data.type == Note.NoteType.Normal) ? 1.0f : data.length;
        
        // CORRECTION: For Curved Notes, Transform is at Zero (Head). No Offset Needed.
        // For Straight Notes, Transform is at Center. Offset Needed.
        bool isCurved = (data.curvePoints != null && data.curvePoints.Count > 1);
        float zOffset = isCurved ? 0f : (effectiveLength * 0.5f);

        Vector3 spawnPos = new Vector3(targetX, targetY, spawnZ + zOffset);

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
            noteScript.Initialize(data.floor, data.lane, noteSpeed, data.type, data.length, data.curvePoints);
            GameManager.Instance.RegisterNote(noteScript);
        }
    }
}
