using UnityEngine;

public class Note : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float destroyZ = -10f; // Position behind player to destroy note

    private void Update()
    {
        // Move towards the player (assuming player is at Z=0 and notes spawn at positive Z)
        transform.Translate(Vector3.back * speed * Time.deltaTime);

        if (transform.position.z < destroyZ)
        {
            // Missed the note
            Debug.Log("Missed Note!");
            GameManager.Instance.OnNoteMiss();
            Destroy(gameObject);
        }
    }
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    public int FloorIndex { get; private set; }
    public int LaneIndex { get; private set; }
    public bool IsHit { get; set; } = false; // Prevent double hitting

    public void Initialize(int floorIndex, int laneIndex, float moveSpeed)
    {
        FloorIndex = floorIndex;
        LaneIndex = laneIndex;
        speed = moveSpeed;
    }

    public void SetColor(Color color)
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = color;
            // Enable emission for "Neon" look
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", color); 
        }
    }
}
