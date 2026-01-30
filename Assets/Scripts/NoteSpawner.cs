using UnityEngine;

public class NoteSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject notePrefab;

    [Header("Settings")]
    [SerializeField] private float spawnInterval = 1.0f;
    [SerializeField] private float spawnZ = 50f;
    [SerializeField] private float noteSpeed = 10f;

    // Specific spawn positions requested by user
    private readonly float[] xPositions = { -3.75f, -1.25f, 1.25f, 3.75f };
    private readonly float[] yPositions = { 1.75f, 4.25f, 6.75f };

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            SpawnNote();
            timer = 0f;
        }
    }

    private void SpawnNote()
    {
        if (notePrefab == null) return;

        // Select random X and Y from the predefined arrays
        int xIndex = Random.Range(0, xPositions.Length);
        int yIndex = Random.Range(0, yPositions.Length);

        float randomX = xPositions[xIndex];
        float randomY = yPositions[yIndex];
        
        Vector3 spawnPos = new Vector3(randomX, randomY, spawnZ);

        GameObject noteObj = Instantiate(notePrefab, spawnPos, Quaternion.identity);
        Note noteScript = noteObj.GetComponent<Note>();
        
        if (noteScript != null)
        {
            noteScript.SetSpeed(noteSpeed);

            // Set Color based on Floor (Y Index)
            // yPositions = { 1.75f, 4.25f, 6.75f } -> Index 0, 1, 2
            // 0 (1F): Electric Purple (150, 0, 255)
            // 1 (2F): Cyan (0, 255, 255)
            // 2 (3F): Hot Pink (255, 0, 150)

            Color noteColor = Color.white;
            switch (yIndex)
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
            noteScript.Initialize(yIndex, xIndex, noteSpeed);
            GameManager.Instance.RegisterNote(noteScript);
        }
    }
}
