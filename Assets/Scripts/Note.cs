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

    public enum NoteType { Normal, Long }
    
    public NoteType Type { get; private set; }
    public float Length { get; private set; } = 1.0f;
    public bool IsHolding { get; set; } = false; // For Long Note logic

    public int FloorIndex { get; private set; }
    public int LaneIndex { get; private set; }
    public bool IsHit { get; set; } = false; // Prevent double hitting

    public void Initialize(int floorIndex, int laneIndex, float moveSpeed, NoteType type = NoteType.Normal, float length = 1.0f)
    {
        FloorIndex = floorIndex;
        LaneIndex = laneIndex;
        speed = moveSpeed;
        Type = type;
        Length = length;

        if (Type == NoteType.Long)
        {
            // Scale visuals for Long Note
            // Assuming default scale Z is 1 and length 1 corresponds to 1 unit.
            Vector3 scale = transform.localScale;
            scale.z = Length;
            transform.localScale = scale;
        }
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
