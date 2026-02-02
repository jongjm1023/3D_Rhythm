using UnityEngine;

public class Note : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float destroyZ = -10f; // Position behind player to destroy note
    
    private GameObject bottomGlow;
    private Light glowLight; // Cached reference

    private Renderer noteRenderer;

    private void Start()
    {
        InitializeBottomGlow();
        noteRenderer = GetComponent<Renderer>();
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
        // Parent (Note) is 1 unit high, bottom is at -0.5.
        // Glow is 0.15 high, half height is 0.075.
        // To sit exactly below without overlap: -0.5 - 0.075 = -0.575.
        // Adding a tiny buffer (-0.576f) to ensure no Z-fighting at the seam.
        bottomGlow.transform.localPosition = new Vector3(0, -0.576f, 0);
        bottomGlow.transform.localRotation = Quaternion.identity;
        
        // Scale to be thin
        bottomGlow.transform.localScale = new Vector3(1.0f, 0.15f, 1.0f); 
    }

    private void Update()
    {
        // Move towards the player (assuming player is at Z=0 and notes spawn at positive Z)
        transform.Translate(Vector3.back * speed * Time.deltaTime);

        // Visual Consumption for Long Notes
        if (IsHolding && Type == NoteType.Long && GameManager.Instance != null)
        {
            // If we are already fully consumed (invisible), just update data but let it move
            if (noteRenderer != null && !noteRenderer.enabled)
            {
                 // Ensure length stays 0
                 Length = 0f;
                 return; 
            }

            // Disable Bottom Glow (High Emission/Light) IMMEDIATELY during hold
            if (bottomGlow != null && bottomGlow.activeSelf)
            {
                bottomGlow.SetActive(false);
            }

            float barZ = GameManager.Instance.GetTouchBarZ();
            
            // Calculate Current Physical Tail Z (based on current clamped state)
            // Tail = Center + HalfScale
            float halfScale = transform.localScale.z * 0.5f;
            float currentHeadZ = transform.position.z - halfScale;
            float currentTailZ = transform.position.z + halfScale;

            // Check if Head is past bar
            if (currentHeadZ < barZ)
            {
                // Logic: How much "Tail" is left above the bar?
                // Visual Length = TailZ - BarZ
                float newLength = currentTailZ - barZ;

                if (newLength <= 0f)
                {
                    // Fully Consumed
                    newLength = 0f;
                    Length = 0f;
                    
                    // Hide Visuals
                    if (noteRenderer != null) noteRenderer.enabled = false;
                    if (bottomGlow != null) bottomGlow.SetActive(false);

                    // Set Scale to 0 one last time? Or keep 0.
                    transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, 0f);
                    
                    // DO NOT CLAMP POSITION. Let the zero-length note continue moving down in next frames.
                    // This ensures the "Tail" (which is now just the point object) moves away from the bar,
                    // allowing GameManager to judge 'late release'.
                }
                else
                {
                    // Update Visuals: Anchor Head to Bar
                    float newZ = barZ + (newLength * 0.5f);

                    transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, newLength);
                    transform.position = new Vector3(transform.position.x, transform.position.y, newZ);
                    Length = newLength; 
                }
            }
        }

        if (transform.position.z < destroyZ)
        {
            // Missed the note
            Debug.Log("Missed Note!");
            GameManager.Instance.OnNoteMiss();
            // Note: OnNoteMiss may modify activeNotes list.
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

    public void Initialize(int floorIndex, int laneIndex, float moveSpeed, NoteType type = NoteType.Normal, float durationSeconds = 1.0f)
    {
        FloorIndex = floorIndex;
        LaneIndex = laneIndex;
        speed = moveSpeed;
        Type = type;
        
        if (Type == NoteType.Normal)
        {
            // Normal Note has fixed visual size (default cube scale 1)
            // Logic must match visual size for accurate judgement (Head/Tail calculation)
            Length = 1.0f; 
        }
        else
        {
            // Long Note: Physical Length depends on Duration and Speed
            float physicalLength = durationSeconds * speed;
            Length = physicalLength;

            // Scale visuals for Long Note
            // Z Scale represents Physical Length on track
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
            if (glowLight == null) glowLight = bottomGlow.GetComponent<Light>();
            if (glowLight == null) glowLight = bottomGlow.AddComponent<Light>();
            
            // Light should use the glow color
            glowLight.color = glowColor;
            glowLight.type = LightType.Point;
            glowLight.range = 3.0f;
            glowLight.intensity = 2.0f;
        }
    }
}
