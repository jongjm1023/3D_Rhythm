using UnityEngine;

public class Note : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float destroyZ = -10f; // Position behind player to destroy note
    
    private GameObject bottomGlow;

    private void Start()
    {
        InitializeBottomGlow();
    }

    private void InitializeBottomGlow()
    {
        if (bottomGlow != null) return;

        // Create a Cube to act as the bottom face glow
        bottomGlow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bottomGlow.transform.SetParent(this.transform);
        
        // Remove Collider
        Destroy(bottomGlow.GetComponent<Collider>());
        
        // Position at the bottom face
        // Parent pivot is likely center (0,0,0). Bottom face is at y = -0.5.
        bottomGlow.transform.localPosition = new Vector3(0, -0.5f, 0);
        bottomGlow.transform.localRotation = Quaternion.identity;
        
        // Scale to be thin and slightly wider? 
        // User asked for "Bottom face", so match X/Z size (1,1). Y thickness small (0.05).
        // Since parent might be scaled, local scale 1, 0.05, 1 works relative to parent.
        // Wait, if parent Y scale changes, this thickness changes too. 
        // Assuming Note Y scale is usually 1.
        bottomGlow.transform.localScale = new Vector3(1.0f, 0.15f, 1.0f); 
    }

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

    public void SetColor(Color noteColor, Color glowColor)
    {
        InitializeBottomGlow(); // Ensure bottom glow exists before setting its color

        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = noteColor;
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", noteColor); 
        }

        if (bottomGlow != null)
        {
            Renderer glowRend = bottomGlow.GetComponent<Renderer>();
            if (glowRend != null)
            {
                // Apply Configured Glow Color
                glowRend.material.color = glowColor;
                glowRend.material.EnableKeyword("_EMISSION");
                
                // For emission, use high intensity with the configured color
                glowRend.material.SetColor("_EmissionColor", glowColor * 10.0f);
            }

            // Add Real-time Light for effect
            Light glowLight = bottomGlow.GetComponent<Light>();
            if (glowLight == null) glowLight = bottomGlow.AddComponent<Light>();
            
            // Light should use the glow color
            glowLight.color = glowColor;
            glowLight.type = LightType.Point;
            glowLight.range = 3.0f;
            glowLight.intensity = 2.0f;
        }
    }
}
