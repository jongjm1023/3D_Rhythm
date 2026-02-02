using UnityEngine;

public class Note : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float destroyZ = -80f; // Position behind player to destroy note
    
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

    private bool isCurved = false;
    private bool hasMissed = false;

    private void Update()
    {
        // Move towards the player (assuming player is at Z=0 and notes spawn at positive Z)
        transform.Translate(Vector3.back * speed * Time.deltaTime);

        if (GameManager.Instance != null && !IsHit && !hasMissed && !IsHolding)
        {
            // Early Miss Check: Has the HEAD passed the Bad Threshold?
            float limit = GameManager.Instance.GetTouchBarZ() + GameManager.Instance.BadThreshold;
            if (HeadZ < limit)
            {
                 Debug.Log("Early Miss Triggered!");
                 GameManager.Instance.OnNoteMiss();
                 GameManager.Instance.UnregisterNote(this);
                 hasMissed = true;
            }
        }

        // Visual Consumption for Long Notes
        if (IsHolding && Type == NoteType.Long && GameManager.Instance != null)
        {
            // If we are already fully consumed (invisible), just update data but let it move
            if (noteRenderer != null && !noteRenderer.enabled)
            {
                 if (!isCurved) Length = 0f;
                 return; 
            }

            // Disable Bottom Glow (High Emission/Light) IMMEDIATELY during hold
            if (bottomGlow != null && bottomGlow.activeSelf)
            {
                bottomGlow.SetActive(false);
            }

            // If Curved, we do NOT shrink visually (Too complex for LineRenderer clip).
            // We just let it slide through the bar.
            // Logic relies on constant Length and moving Position to determine Tail Z.
            if (isCurved)
            {
                return;
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
                // ... (Same logic for straight notes)
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

        // Check for destruction (Miss)
        // User requested to judge based on TAIL position.
        // TailZ is now unified via property
        float tailZ = TailZ;
        
        if (tailZ < destroyZ)
        {
            // Fully off-screen. Destroy.
            // Only trigger miss if it wasn't already triggered (Early Miss) or Hit.
            if (!IsHit && !hasMissed)
            {
                Debug.Log("Missed Note (Destruction Fallback)!");
                GameManager.Instance.OnNoteMiss();
                GameManager.Instance.UnregisterNote(this);
            }
            
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

    public void Initialize(int floorIndex, int laneIndex, float moveSpeed, NoteType type = NoteType.Normal, float durationSeconds = 1.0f, System.Collections.Generic.List<Vector2Int> curvePoints = null)
    {
        FloorIndex = floorIndex;
        LaneIndex = laneIndex;
        speed = moveSpeed;
        Type = type;
        
        if (Type == NoteType.Normal)
        {
            Length = 1.0f; 
        }
        else
        {
            // Long Note: Physical Length depends on Duration and Speed
            float physicalLength = durationSeconds * speed;
            Length = physicalLength;

            // Check if we have curve data (Flexible Slider) with actual movement (at least 2 points)
            if (curvePoints != null && curvePoints.Count > 1)
            {
                isCurved = true;
                Debug.Log($"[Curved Slider] Initialized. Length: {Length:F2}, Points: {curvePoints.Count}");

                // -- CURVED SLIDER LOGIC --
                
                // 1. Hide default visuals
                if (GetComponent<Renderer>()) GetComponent<Renderer>().enabled = false;
                if (bottomGlow != null) bottomGlow.SetActive(false); // Disable glow for curve (or handle custom glow later)

                // 2. Setup LineRenderer
                LineRenderer lr = GetComponent<LineRenderer>();
                if (lr == null) lr = gameObject.AddComponent<LineRenderer>();
                
                lr.useWorldSpace = false; // Move with parent
                
                // Match width to the Note's physical width (X scale)
                float noteWidth = transform.localScale.x;
                lr.startWidth = noteWidth;
                lr.endWidth = noteWidth;
                
                // Use the same material as the Note Mesh to ensure visibility and matching style (Glow/Color)
                if (GetComponent<Renderer>() != null)
                {
                    lr.material = GetComponent<Renderer>().material;
                }
                else
                {
                    // Fallback if no renderer (unlikely)
                    lr.material = new Material(Shader.Find("Standard")); 
                }
                
                // Material already has color. Set Vertex Color to White to avoid multiplying tint.
                lr.startColor = Color.white;
                lr.endColor = Color.white;

                // 3. Generate Points
                int count = curvePoints.Count;
                lr.positionCount = count;
                
                float spacingX = 2.5f;
                float spacingY = 2.75f;
                
                for (int i = 0; i < count; i++)
                {
                    Vector2Int p = curvePoints[i];
                    
                    // X/Y Offset relative to Head
                    float offX = (p.x - LaneIndex) * spacingX;
                    float offY = (p.y - FloorIndex) * spacingY;
                    
                    // Z Position: Interpolate from 0 (Head) to Length (Tail)
                    // Assuming points are uniformly distributed in time/Z
                    float t = (float)i / (count - 1);
                    float offZ = t * Length; // Positive Z extends upwards/backwards relative to movement direction?
                    // Note moves -Z. Head is at 0. Tail is at +Length (Trailing behind).
                    
                    lr.SetPosition(i, new Vector3(offX, offY, offZ));
                }
                
                // Do NOT scale Z
                transform.localScale = Vector3.one; 
            }
            else
            {
                // -- STRAIT LONG NOTE LOGIC --
                Vector3 scale = transform.localScale;
                scale.z = Length;
                transform.localScale = scale;
            }
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

    // Unified Properties for Position Logic
    public float HeadZ => isCurved ? transform.position.z : transform.position.z - (Length * 0.5f);
    public float TailZ => isCurved ? transform.position.z + Length : transform.position.z + (Length * 0.5f);
}
