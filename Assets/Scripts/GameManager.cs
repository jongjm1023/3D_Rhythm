using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Score { get; private set; }
    public int Combo { get; private set; }

    [Header("Rhythm Settings")]
    [SerializeField] private TouchBar touchBar;
    
    [Header("UI Toolkit (New)")]
    [SerializeField] private GameplayUIController gameplayUI;

    [Header("Legacy UI (Deprecated)")]
    [SerializeField] private TMP_Text judgementText; 
    [SerializeField] private TMP_Text comboText; 
    [SerializeField] private TMP_Text scoreText; 

    [Tooltip("Adjust hit timing. Positive: Late Hit (Closer to Player), Negative: Early Hit (Further)")]
    [SerializeField] private float judgementOffset = 0f;
    [Header("Result UI")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultScoreText;
    [SerializeField] private TMP_Text resultMaxComboText;
    [SerializeField] private TMP_Text resultAccuracyText;
    [SerializeField] private TMP_Text resultDetailsText; // P/G/G/B/M counts

    // Stats
    private int maxCombo;
    private int perfectCount;
    private int greatCount;
    private int goodCount;
    private int badCount;
    private int missCount;
    private int totalNotes; // For accuracy calculation

    private Coroutine activeJudgementCoroutine;
    
    // Track active notes
    private System.Collections.Generic.List<Note> activeNotes = new System.Collections.Generic.List<Note>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
            // Set Background Color to Dark Navy
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0f, 0f, 30f/255f);
            }

            // Load Judgement Offset
            judgementOffset = PlayerPrefs.GetFloat("JudgementOffset", 0f);
            Debug.Log($"GameManager: Loaded Judgement Offset: {judgementOffset}");

            // Auto-discover Gameplay UI if not assigned
            if (gameplayUI == null)
            {
                gameplayUI = FindObjectOfType<GameplayUIController>();
                if (gameplayUI != null) Debug.Log("GameManager: Auto-connected GameplayUIController.");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Debugging Input
        // if (UnityEngine.InputSystem.Keyboard.current.aKey.wasPressedThisFrame) Debug.Log("GameManager: A key pressed");
        
        if (touchBar == null)
        {
             Debug.LogError("GameManager: TouchBar reference is MISSING in Inspector!");
             return;
        }

        // Developer Shortcut: R to End Game immediately
        if (UnityEngine.InputSystem.Keyboard.current != null && 
            UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
        {
            EndGame();
        }

        HandleRhythmInput();
    }

    // ... (rest of file) ...



    public void RegisterNote(Note note)
    {
        if (!activeNotes.Contains(note))
        {
            activeNotes.Add(note);
        }
    }

    public void UnregisterNote(Note note)
    {
        if (activeNotes.Contains(note))
        {
            activeNotes.Remove(note);
        }
    }

    public bool HasActiveNotes => activeNotes.Count > 0;

    public void OnNoteMiss()
    {
        Debug.Log("Note Missed (Pass)");
        ShowJudgementUI("MISS");
        
        missCount++;
        totalNotes++;

        // Reset Combo
        Combo = 0;
        UpdateComboUI();

        // Find the note that passed and remove it from list? 
        // Note.cs calls this before Destroy, so we should allow it to be removed via OnDestroy or explicit call.
        // Actually, Note.cs destroys itself, so we should clean up nulls or remove in OnDestroy.
        // For now, simple cleanup:
        activeNotes.RemoveAll(n => n == null);
    }

    private void HandleRhythmInput()
    {
        if (touchBar == null) return;
        if (UnityEngine.InputSystem.Keyboard.current == null) return;

        // Check for Hold Notes (Keys being held)
        CheckHoldNotes();

        // Check for New Hits (Keys pressed this frame)
        // Map keys to lanes: A=0, S=1, D=2, F=3
        if (UnityEngine.InputSystem.Keyboard.current.aKey.wasPressedThisFrame) TryHitNote(0);
        if (UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame) TryHitNote(1);
        if (UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame) TryHitNote(2);
        if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame) TryHitNote(3);
    }

    private void CheckHoldNotes()
    {
        // Iterate backwards to allow removal
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            Note note = activeNotes[i];
            if (note == null) continue;
            if (!note.IsHolding) continue;

            // Which key corresponds to this note's lane?
            bool keyHeld = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            switch (note.LaneIndex)
            {
                case 0: keyHeld = kb.aKey.isPressed; break;
                case 1: keyHeld = kb.sKey.isPressed; break;
                case 2: keyHeld = kb.dKey.isPressed; break;
                case 3: keyHeld = kb.fKey.isPressed; break;
            }

            if (keyHeld)
            {
                // Check if the tail has already passed the line (Late Over-hold)
                float tailZ = note.TailZ;
                float barZ = touchBar.transform.position.z;
                float targetZ = barZ + judgementOffset;

                // Threshold: If tail is past the "Bad" window (same logic as Normal note miss)
                if (tailZ < targetZ - (1.4f * speedMultiplier))
                {
                    Debug.Log("Long Note Over-held! Forcing Late Miss.");
                    JudgeHit(note, Mathf.Abs(tailZ - targetZ), true); // This will trigger MISS
                    continue; // Note is destroyed in JudgeHit
                }

                // Curved Slider Y-Matching Check
                if (note.IsCurved)
                {
                    // localZ is the distance from the head back to the bar
                    float localZAtBar = targetZ - note.HeadZ; 
                    
                    Vector3 targetLocalPos = note.GetCurveTargetPosition(localZAtBar);
                    float targetWorldY = note.transform.position.y + targetLocalPos.y;
                    
                    float yDiff = Mathf.Abs(targetWorldY - touchBar.transform.position.y);
                    
                    // Threshold: 1.25f (roughly half the distance between floors, which is 2.75f)
                    if (yDiff > 2.5f)
                    {
                        Debug.Log($"[Curved Slider] Y-Mismatch! Target: {targetWorldY:F2}, Current: {touchBar.transform.position.y:F2}, Diff: {yDiff:F2}. Forcing Miss.");
                        ShowJudgementUI("MISS");
                        note.SetUnpressable();
                        UnregisterNote(note); 
                        continue;
                    }
                }

                // Continuous scoring logic (0.5s ticks)
                note.HoldScoreTimer += Time.deltaTime;
                if (note.HoldScoreTimer >= 0.5f)
                {
                    note.HoldScoreTimer -= 0.5f;
                    
                    // Add small score and increment combo
                    Score += 50; 
                    Combo++;
                    if (Combo > maxCombo) maxCombo = Combo;
                    
                    UpdateComboUI();
                    UpdateScoreUI();
                }
            }
            else
            {
                // Key released -> Judge Release (Tail)
                float tailZ = note.TailZ;
                float barZ = touchBar.transform.position.z;
                float targetZ = barZ + judgementOffset; 
                float dist = Mathf.Abs(tailZ - targetZ);

                JudgeHit(note, dist, true); // true = isTail
            }
        }
    }

    private void TryHitNote(int laneIndex)
    {
        if (touchBar == null) return;
        
        int currentFloor = touchBar.CurrentFloorIndex;
        // Debug.Log($"TryHitNote Lane {laneIndex}, Floor {currentFloor}");
        
        if (currentFloor == -1) 
        {
             // Debug.LogWarning("TryHitNote: No floor selected (Index -1)");
             return; 
        }

        // Find the note with the SMALLEST Z coordinate (furthest along the track)
        Note closestNote = null;
        float minZ = float.MaxValue;

        // Clean nulls first
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i] == null) activeNotes.RemoveAt(i);
        }

        foreach (Note note in activeNotes)
        {
            if (note.IsHit) continue; 
            if (note.IsHolding) continue; // Already holding this one
            if (note.IsUnpressable) continue; // Skip notes that were missed at head
            if (note.LaneIndex != laneIndex) continue;
            if (note.FloorIndex != currentFloor) continue;

            // Zombie Note Check: Ignore notes that have already passed the "Bad" window
            // They are technically "Missed" but not destroyed yet.
            // HeadZ decreases as it moves. TargetZ is fixed (~0).
            // If HeadZ < TargetZ - Window, it's irrelevant.
            float hZ = note.HeadZ;
            float tZ = touchBar.transform.position.z + judgementOffset;
            float rawDiff = hZ - tZ;
            
            // Bad Window = 1.4 * Multiplier.
            float threshold = -1.4f * speedMultiplier;

            if (rawDiff < threshold)
            {
                // Note is too far past the line. Ignore it.
                continue;
            }

            // User requested to judge based on smallest Z (furthest note)
            // Note moves +Z -> -Z. Smallest Z means it's the "oldest" note on screen.
            // MUST use HeadZ to be consistent across Straight (Center-based) and Curved (Head-based) notes.
            float noteZ = note.HeadZ;
            
            if (noteZ < minZ)
            {
                minZ = noteZ;
                closestNote = note;
            }
        }

        if (closestNote != null)
        {
            // Now calculate key distance for judgement on this specific note
            float headZ = closestNote.HeadZ;
            float barZ = touchBar.transform.position.z;
            
            float targetZ = barZ + judgementOffset;
            float dist = Mathf.Abs(headZ - targetZ);

            JudgeHit(closestNote, dist, false);
        }
    }

    private void JudgeHit(Note note, float distance, bool isTail)
    {
        // Accuracy windows
        
        string judgement = "";
        int scoreAdd = 0;

        // Base Thresholds: Perfect=0.5, Great=0.8, Good=1.1, Bad=1.4 (at Speed 10)
        // Scaled by speedMultiplier (Cached in Start)
        if (distance <= 0.5f * speedMultiplier)
        {
            judgement = "PERFECT";
            scoreAdd = 500;
            Combo++;
            perfectCount++;
        }
        else if (distance <= 0.8f * speedMultiplier)
        {
            judgement = "GREAT";
            scoreAdd = 300;
            Combo++;
            greatCount++;
        }
        else if (distance <= 1.1f * speedMultiplier)
        {
            judgement = "GOOD";
            scoreAdd = 100;
            Combo++;
            goodCount++;
        }
        else if (distance <= 1.4f * speedMultiplier)
        {
            judgement = "BAD";
            scoreAdd = 50;
            Combo = 0;
            badCount++;
        }
        else
        {
            judgement = "MISS";
            scoreAdd = 0;
            Combo = 0;
            missCount++;
        }

        totalNotes++;
        if (Combo > maxCombo) maxCombo = Combo;

        // Apply Bonus: +20 Score per 10 Combo
        // 10-19: +20, 20-29: +40, etc.
        if (scoreAdd > 0 && Combo >= 10)
        {
            int bonus = (Combo / 10) * 20;
            scoreAdd += bonus;
        }

        UpdateComboUI();


        // Logic branching based on Note Type and Hit Type
        if (note.Type == Note.NoteType.Long)
        {
            if (!isTail)
            {
                // Head Hit
                if (scoreAdd > 0)
                {
                    // Good hit on head -> Start Holding
                    note.IsHolding = true;
                    // note.IsHit = true; // Don't mark as hit yet, or we skip it? 
                    // Actually TryHitNote skips "IsHolding" notes. So we set IsHolding = true.
                    // We might want to give some partial score or just feedback?
                    Debug.Log($"Long Note Start! {judgement}");
                    // Create immediate feedback but don't destroy
                    ShowJudgementUI(judgement);
                }
                else
                {
                    // Missed the head = Note becomes unpressable but continues moving
                    Debug.Log("Long Note Missed Head - Setting Unpressable");
                    ShowJudgementUI("MISS");
                    note.SetUnpressable();
                    UnregisterNote(note); // No more tracking for this note
                }
                return; // Don't destroy yet
            }
            else
            {
                // Tail Hit (Release)
                if (scoreAdd > 0)
                {
                    Debug.Log($"Long Note Finish! {judgement}");
                    ShowJudgementUI(judgement);
                    Score += scoreAdd; // Add score on completion
                    UpdateScoreUI();
                    DestroyNote(note);
                }
                else
                {
                    // Released too early/late (Bad)
                    Debug.Log("Long Note Released Badly");
                    ShowJudgementUI("MISS");
                    DestroyNote(note);
                }
            }
        }
        else
        {
            // Normal Note
            if (scoreAdd > 0)
            {
                Debug.Log($"Hit! {judgement} (Dist: {distance:F2})");
                Score += scoreAdd;
                UpdateScoreUI();
                ShowJudgementUI(judgement);
                DestroyNote(note);
            }
            else
            {
                Debug.Log($"Miss! (Dist: {distance:F2})");
                ShowJudgementUI("MISS");
            }
        }
    }

    private void DestroyNote(Note note)
    {
        note.IsHit = true;
        UnregisterNote(note);
        Destroy(note.gameObject);
    }

    private void ShowJudgementUI(string text)
    {
        // New UI
        if (gameplayUI != null)
        {
            gameplayUI.ShowJudgment(text);
        }

        // Legacy Fallback
        if (judgementText != null)
        {
            judgementText.text = text;
            if (activeJudgementCoroutine != null) StopCoroutine(activeJudgementCoroutine);
            activeJudgementCoroutine = StartCoroutine(ResetJudgementText());
        }
    }
    private System.Collections.IEnumerator ResetJudgementText()
    {
        yield return new WaitForSeconds(0.5f);
        if (judgementText != null) judgementText.text = "";
    }

    private void UpdateComboUI()
    {
        // New UI
        if (gameplayUI != null)
        {
            gameplayUI.UpdateCombo(Combo);
        }

        // Legacy Fallback
        if (comboText != null)
        {
            if (Combo > 0)
                comboText.text = $"COMBO {Combo}";
            else
                comboText.text = ""; 
        }
    }

    private void UpdateScoreUI()
    {
        // New UI
        if (gameplayUI != null)
        {
            gameplayUI.UpdateScore(Score);
        }

        // Legacy Fallback
        if (scoreText != null)
        {
            scoreText.text = $"SCORE: {Score}";
        }
    }

    [SerializeField] private Button returnButton; // Drag Button here

    public float GetTouchBarZ()
    {
        return touchBar != null ? touchBar.transform.position.z : 0f;
    }

    // ... (existing code)

    // Speed Multiplier
    private float speedMultiplier = 1.0f;
    
    // Threshold for "Early Miss" (Past the Bad Window)
    public float BadThreshold => -1.4f * (speedMultiplier > 0 ? speedMultiplier : 1.0f);

    private void Start()
    {
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(OnReturnToMenu);
            returnButton.gameObject.SetActive(false); // Hide initially
        }

        // Cache Speed Multiplier
        float currentSpeed = 10f;
        if (SongManager.Instance != null) currentSpeed = SongManager.Instance.NoteSpeed;
        else currentSpeed = PlayerPrefs.GetFloat("NoteSpeed", 10f);

        speedMultiplier = currentSpeed / 10.0f;
        Debug.Log($"GameManager: Speed Multiplier set to {speedMultiplier:F2} (Speed {currentSpeed})");
    }



    public void OnReturnToMenu()
    {
        Time.timeScale = 1.0f; // Reset time just in case
        UnityEngine.SceneManagement.SceneManager.LoadScene("Songs");
    }

    public void EndGame()
    {
        Debug.Log("Game Over!");
        
        // Hide In-Game UI
        if (gameplayUI != null) gameplayUI.gameObject.SetActive(false);
        if (scoreText != null) scoreText.gameObject.SetActive(false);
        if (comboText != null) comboText.gameObject.SetActive(false);
        if (judgementText != null) judgementText.gameObject.SetActive(false);

        if (resultPanel != null) resultPanel.SetActive(true);
        if (returnButton != null) returnButton.gameObject.SetActive(true); // Show Button

        // Accuracy Calculation (Weighted)
        // Perfect=100%, Great=80%, Good=50%, Bad=20%, Miss=0%
        float totalWeight = totalNotes * 100f;
        float currentWeight = (perfectCount * 100f) + (greatCount * 80f) + (goodCount * 50f) + (badCount * 20f);
        
        float accuracy = 0f;
        if (totalNotes > 0) accuracy = (currentWeight / totalWeight) * 100f;

        if (resultScoreText != null) resultScoreText.text = $"Score: {Score}";
        if (resultMaxComboText != null) resultMaxComboText.text = $"Max Combo: {maxCombo}";
        if (resultAccuracyText != null) resultAccuracyText.text = $"Accuracy: {accuracy:F2}%";
        
        if (resultDetailsText != null)
        {
            resultDetailsText.text = $"PERFECT: {perfectCount}\n" +
                                     $"GREAT: {greatCount}\n" +
                                     $"GOOD: {goodCount}\n" +
                                     $"BAD: {badCount}\n" +
                                     $"MISS: {missCount}";
        }
        
        // Save Stats to SongManager
        // Ensure SongManager exists and has a selected song
        if (SongManager.Instance != null && SongManager.Instance.SelectedSong != null)
        {
            SongData currentSong = SongManager.Instance.SelectedSong;
            bool changed = false;

            if (Score > currentSong.maxScore)
            {
                currentSong.maxScore = Score;
                changed = true;
                Debug.Log("New Best Score!");
            }
            
            if (maxCombo > currentSong.maxCombo)
            {
                currentSong.maxCombo = maxCombo;
                changed = true;
                Debug.Log("New Max Combo!");
            }

            if (changed)
            {
                SongManager.Instance.SaveSongStats(currentSong);
            }
        }
    }
}
