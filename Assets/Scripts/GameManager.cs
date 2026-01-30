using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Score { get; private set; }

    [Header("Rhythm Settings")]
    [SerializeField] private TouchBar touchBar;
    
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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        HandleRhythmInput();
    }

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

    public void OnNoteMiss()
    {
        Debug.Log("Note Missed (Pass)");
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

        // Map keys to lanes: A=0, S=1, D=2, F=3
        if (UnityEngine.InputSystem.Keyboard.current.aKey.wasPressedThisFrame) TryHitNote(0);
        if (UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame) TryHitNote(1);
        if (UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame) TryHitNote(2);
        if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame) TryHitNote(3);
    }

    private void TryHitNote(int laneIndex)
    {
        int currentFloor = touchBar.CurrentFloorIndex;
        if (currentFloor == -1) return; // No floor selected

        // Find closest note in this lane AND on this floor
        Note closestNote = null;
        float minZCalls = float.MaxValue;

        // Clean nulls first
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i] == null) activeNotes.RemoveAt(i);
        }

        foreach (Note note in activeNotes)
        {
            if (note.IsHit) continue;
            if (note.LaneIndex != laneIndex) continue;
            if (note.FloorIndex != currentFloor) continue;

            // Calculate distance relative to TouchBar's Z position
            // Note moves from +Z towards -Z.
            float noteZ = note.transform.position.z;
            float barZ = touchBar.transform.position.z;
            float dist = Mathf.Abs(noteZ - barZ);

            if (dist < minZCalls)
            {
                minZCalls = dist;
                closestNote = note;
            }
        }

        if (closestNote != null)
        {
            JudgeHit(closestNote, minZCalls);
        }
    }

    private void JudgeHit(Note note, float distance)
    {
        // Accuracy windows (in units, assuming speed ~10)
        // Perfect: < 1.0
        // Great: < 2.0
        // Good: < 3.0
        // Bad: > 3.0 (Miss or just bad)
        
        string judgement = "";
        int scoreAdd = 0;

        if (distance <= 0.2f)
        {
            judgement = "PERFECT";
            scoreAdd = 500;
        }
        else if (distance <= 0.4f)
        {
            judgement = "GREAT";
            scoreAdd = 300;
        }
        else if (distance <= 0.7f)
        {
            judgement = "GOOD";
            scoreAdd = 100;
        }
        else if (distance <= 1.2f)
        {
            judgement = "BAD";
            scoreAdd = 50;
        }
        else
        {
            judgement = "MISS";
            scoreAdd = 0;
        }
        
        if (scoreAdd > 0)
        {
            Debug.Log($"Hit! {judgement} (Dist: {distance:F2})");
            Score += scoreAdd;
            note.IsHit = true;
            UnregisterNote(note);
            Destroy(note.gameObject);
        }
    }
}
