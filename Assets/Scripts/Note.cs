using UnityEngine;

public class Note : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float destroyZ = -80f; // Position behind player to destroy note
    [SerializeField] private Material failMaterial;
    
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
    public bool IsCurved => isCurved;
    private System.Collections.Generic.List<Vector2Int> curvePoints;
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
                 hasMissed = true; // Prevents repeated triggers
                 
                 if (Type == NoteType.Long)
                 {
                     SetUnpressable();
                 }
                 
                 // Always unregister when a miss is confirmed
                 GameManager.Instance.UnregisterNote(this);
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
    
    public bool IsUnpressable { get; private set; } = false;
    public float HoldScoreTimer { get; set; } = 0f;

    public Vector3 GetCurveTargetPosition(float localZ)
    {
        if (!isCurved || curvePoints == null || curvePoints.Count < 2) return Vector3.zero;

        // Map localZ (0 to Length) to 0..1 interval
        float t = Mathf.Clamp01(localZ / Length);
        
        // Find the segment
        float scaledT = t * (curvePoints.Count - 1);
        int idx = Mathf.FloorToInt(scaledT);
        int nextIdx = Mathf.Min(idx + 1, curvePoints.Count - 1);
        float lerpT = scaledT - idx;

        Vector2Int p1 = curvePoints[idx];
        Vector2Int p2 = curvePoints[nextIdx];

        float spacingX = 2.5f;
        float spacingY = 2.75f;

        // X/Y Offset relative to Head (which is at transform.position)
        float x1 = (p1.x - LaneIndex) * spacingX;
        float x2 = (p2.x - LaneIndex) * spacingX;
        float targetLocalX = Mathf.Lerp(x1, x2, lerpT);

        float y1 = (p1.y - FloorIndex) * spacingY;
        float y2 = (p2.y - FloorIndex) * spacingY;
        float targetLocalY = Mathf.Lerp(y1, y2, lerpT);

        return new Vector3(targetLocalX, targetLocalY, localZ);
    }

    public void SetUnpressable()
    {
        if (IsUnpressable) return;
        IsUnpressable = true;
        IsHolding = false;
        
        // Apply fail material if assigned
        if (failMaterial != null)
        {
            if (noteRenderer != null) noteRenderer.material = failMaterial;
            
            LineRenderer lr = GetComponent<LineRenderer>();
            if (lr != null) lr.material = failMaterial;
        }

        // Disable Light component if it exists
        if (glowLight != null) glowLight.enabled = false;

        // Disable bottom glow visuals
        if (bottomGlow != null) bottomGlow.SetActive(false);
        
        Debug.Log($"Note at Lane {LaneIndex} is now unpressable with custom fail material.");
    }

    public void Initialize(int floorIndex, int laneIndex, float moveSpeed, NoteType type = NoteType.Normal, float durationSeconds = 1.0f, System.Collections.Generic.List<Vector2Int> curvePoints = null)
    {
        FloorIndex = floorIndex;
        LaneIndex = laneIndex;
        speed = moveSpeed;
        Type = type;
        this.curvePoints = curvePoints;
        
        if (Type == NoteType.Normal)
        {
            Length = 0.2f; 
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

                // 2. Setup Mesh (instead of LineRenderer)
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
                
                MeshRenderer mr = GetComponent<MeshRenderer>();
                if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
                
                // Ensure renderer is enabled for the mesh we're about to generate
                mr.enabled = true;

                // 3. Generate 3D Cuboid Mesh
                float noteWidth = transform.localScale.x;
                float noteHeight = 0.4f; // Fixed height for curved sliders
                mf.mesh = GenerateCurvedMesh(noteWidth, noteHeight);
                
                // Use the same material logic as before
                // Renderer component is already on the prefab usually
                
                // Do NOT scale Z
                transform.localScale = Vector3.one; 
            }
            else
            {
                // -- STRAIGHT LONG NOTE LOGIC --
                Vector3 scale = transform.localScale;
                scale.z = Length;
                transform.localScale = scale;
            }
        }
    }

    private Mesh GenerateCurvedMesh(float width, float height)
    {
        if (curvePoints == null || curvePoints.Count < 2) return null;

        Mesh mesh = new Mesh();
        mesh.name = "CurvedSliderMesh";

        System.Collections.Generic.List<Vector3> worldPoints = new System.Collections.Generic.List<Vector3>();
        float spacingX = 2.5f;
        float spacingY = 2.75f;

        for (int i = 0; i < curvePoints.Count; i++)
        {
            Vector2Int p = curvePoints[i];
            float offX = (p.x - LaneIndex) * spacingX;
            float offY = (p.y - FloorIndex) * spacingY;
            float t = (float)i / (curvePoints.Count - 1);
            float offZ = t * Length;
            worldPoints.Add(new Vector3(offX, offY, offZ));
        }

        int segments = worldPoints.Count - 1;
        int vertexCount = worldPoints.Count * 4;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[segments * 24 + 12]; // 4 faces * 2 tris * 3 indices * segments + 2 caps * 2 tris * 3 indices

        float hw = width * 0.5f;
        float hh = height * 0.5f;

        for (int i = 0; i < worldPoints.Count; i++)
        {
            Vector3 forward;
            if (i < worldPoints.Count - 1)
            {
                forward = (worldPoints[i + 1] - worldPoints[i]).normalized;
            }
            else
            {
                forward = (worldPoints[i] - worldPoints[i - 1]).normalized;
            }

            // Simple Right/Up calculation for rhythm game curve (mostly Z-forward)
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3 up = Vector3.Cross(forward, right).normalized;

            // 4 vertices for the rectangle cross-section
            vertices[i * 4 + 0] = worldPoints[i] + (right * -hw) + (up * hh);  // Top Left
            vertices[i * 4 + 1] = worldPoints[i] + (right * hw) + (up * hh);   // Top Right
            vertices[i * 4 + 2] = worldPoints[i] + (right * hw) + (up * -hh);  // Bottom Right
            vertices[i * 4 + 3] = worldPoints[i] + (right * -hw) + (up * -hh); // Bottom Left
        }

        int triIdx = 0;
        for (int i = 0; i < segments; i++)
        {
            int curr = i * 4;
            int next = (i + 1) * 4;

            // Top Face
            triangles[triIdx++] = curr + 0; triangles[triIdx++] = next + 0; triangles[triIdx++] = next + 1;
            triangles[triIdx++] = curr + 0; triangles[triIdx++] = next + 1; triangles[triIdx++] = curr + 1;

            // Right Face
            triangles[triIdx++] = curr + 1; triangles[triIdx++] = next + 1; triangles[triIdx++] = next + 2;
            triangles[triIdx++] = curr + 1; triangles[triIdx++] = next + 2; triangles[triIdx++] = curr + 2;

            // Bottom Face
            triangles[triIdx++] = curr + 2; triangles[triIdx++] = next + 2; triangles[triIdx++] = next + 3;
            triangles[triIdx++] = curr + 2; triangles[triIdx++] = next + 3; triangles[triIdx++] = curr + 3;

            // Left Face
            triangles[triIdx++] = curr + 3; triangles[triIdx++] = next + 3; triangles[triIdx++] = next + 0;
            triangles[triIdx++] = curr + 3; triangles[triIdx++] = next + 0; triangles[triIdx++] = curr + 0;
        }

        // Start Cap
        triangles[triIdx++] = 0; triangles[triIdx++] = 1; triangles[triIdx++] = 2;
        triangles[triIdx++] = 0; triangles[triIdx++] = 2; triangles[triIdx++] = 3;

        // End Cap
        int last = (worldPoints.Count - 1) * 4;
        triangles[triIdx++] = last + 0; triangles[triIdx++] = last + 2; triangles[triIdx++] = last + 1;
        triangles[triIdx++] = last + 0; triangles[triIdx++] = last + 3; triangles[triIdx++] = last + 2;

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
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
