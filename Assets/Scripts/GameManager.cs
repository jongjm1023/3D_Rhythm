using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Score { get; private set; }

    [Header("Rhythm Settings")]
    [SerializeField] private TouchBar touchBar;
    [SerializeField] private TMP_Text judgementText; // Supports TextMeshPro
    [Tooltip("Adjust hit timing. Positive: Late Hit (Closer to Player), Negative: Early Hit (Further)")]
    [SerializeField] private float judgementOffset = 0f;
    
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

            if (!keyHeld)
            {

                // Tail = Center + ScaleZ/2. (Tail is at larger Z, arrives later).
                
                float tailZ = note.transform.position.z + (note.Length * 0.5f);
                float barZ = touchBar.transform.position.z;
                
                // Calculate distance relative to Adjusted Hit Line
                float targetZ = barZ + judgementOffset; 
                float dist = Mathf.Abs(tailZ - targetZ);

                JudgeHit(note, dist, true); // true = isTail
            }
        }
    }

    private void TryHitNote(int laneIndex)
    {
        int currentFloor = touchBar.CurrentFloorIndex;
        if (currentFloor == -1) return; // No floor selected

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
            if (note.LaneIndex != laneIndex) continue;
            if (note.FloorIndex != currentFloor) continue;

            // User requested to judge based on smallest Z (furthest note)
            // Note moves +Z -> -Z. Smallest Z means it's the "oldest" note on screen.
            float noteZ = note.transform.position.z;
            
            if (noteZ < minZ)
            {
                minZ = noteZ;
                closestNote = note;
            }
        }

        if (closestNote != null)
        {
            // Now calculate key distance for judgement on this specific note
            float headZ = closestNote.transform.position.z - (closestNote.Length * 0.5f);
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

        if (distance <= 0.5f)
        {
            judgement = "PERFECT";
            scoreAdd = 500;
        }
        else if (distance <= 0.8f)
        {
            judgement = "GREAT";
            scoreAdd = 300;
        }
        else if (distance <= 1.1f)
        {
            judgement = "GOOD";
            scoreAdd = 100;
        }
        else if (distance <= 1.4f)
        {
            judgement = "BAD";
            scoreAdd = 50;
        }
        else
        {
            judgement = "MISS";
            scoreAdd = 0;
        }

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
                    // Missed the head = Miss the whole note
                    Debug.Log("Long Note Missed Head");
                    ShowJudgementUI("MISS");
                    DestroyNote(note);
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
}
