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
    private readonly float[] yPositions = { 1.75f, 4.25f, 6.75f };

    private List<MapData.HitInfo> hitNotes = new List<MapData.HitInfo>();
    private int currentNoteIndex = 0;
    private bool isPlaying = false;
    private float mapStartTime;

    private BeatmapParser parser;

    private void Start()
    {
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
            
            // Calculate Spawn Time
            // We want the note to arrive at Z=0 exactly at noteData.time
            // Distance = spawnZ - 0 = spawnZ
            // Speed = Distance / ApproachTime (if ApproachTime is fixed)
            // Or if Speed is fixed, ApproachTime = Distance / Speed.
            // Let's stick to fixed Speed from Inspector (noteSpeed)
            // Then ApproachTime = spawnZ / noteSpeed.
            
            float approachTime = spawnZ / noteSpeed; 
            
            // Apply Offset: If Offset is positive, we want to delay the spawn (Spawn later).
            // HitTime is fixed by music.
            // Only SpawnTime changes.
            // To delay note (arrive later), we reduce spawned lead time? No.
            // If I want the note to arrive at T=10, but user feels it is early (arrives at T=9.9).
            // That means the note spawned too early. We should spawn it later.
            // SpawnTime = (noteData.time + offset) - approachTime.
            // If offset is +0.1, TargetHitTime becomes 10.1. SpawnTime becomes later.
            // Effectively delaying the note.
            
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
            switch (data.floor)
            {
                case 0: // 1F
                    noteColor = new Color(150f/255f, 0f, 1f); 
                    break;
                case 1: // 2F
                    noteColor = new Color(0f, 1f, 1f);
                    break;
                case 2: // 3F
                    noteColor = new Color(1f, 0f, 150f/255f);
                    break;
            }
            noteScript.SetColor(noteColor);

            // Initialize Data and Register
            noteScript.Initialize(data.floor, data.lane, noteSpeed, data.type, data.length);
            GameManager.Instance.RegisterNote(noteScript);
        }
    }
}
