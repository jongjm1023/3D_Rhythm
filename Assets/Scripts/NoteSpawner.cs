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
        float randomX = xPositions[Random.Range(0, xPositions.Length)];
        float randomY = yPositions[Random.Range(0, yPositions.Length)];
        
        Vector3 spawnPos = new Vector3(randomX, randomY, spawnZ);

        GameObject noteObj = Instantiate(notePrefab, spawnPos, Quaternion.identity);
        Note noteScript = noteObj.GetComponent<Note>();
        
        if (noteScript != null)
        {
            noteScript.SetSpeed(noteSpeed);
        }
    }
}
