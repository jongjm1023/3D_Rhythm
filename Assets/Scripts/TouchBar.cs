using UnityEngine;

public class TouchBar : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float minHeight = -5f;
    [SerializeField] private float maxHeight = 5f;

    [Header("Floor Settings")]
    [SerializeField] private GameObject[] floorVisuals; // UI/TouchBar indicators
    [SerializeField] private GameObject[] stageFloors;  // Actual Stage Floors (1F, 2F, 3F)
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material selectedMaterial; // Transparent/Glowing material

    public int CurrentFloorIndex { get; private set; } = -1;

    private void Update()
    {
        HandleInput();
        UpdateFloorSelection();
    }

    private void HandleInput()
    {
        if (UnityEngine.InputSystem.Mouse.current == null) return;

        float mouseY = UnityEngine.InputSystem.Mouse.current.delta.y.ReadValue();
        
        Vector3 newPosition = transform.position;
        newPosition.y += mouseY * sensitivity * 0.1f; 
        newPosition.y = Mathf.Clamp(newPosition.y, minHeight, maxHeight);
        
        transform.position = newPosition;
    }

    private void UpdateFloorSelection()
    {
        // Define ranges based on NoteSpawner Y positions: 1, 4, 7
        // Midpoints: 2.5 and 5.5
        int newFloorIndex = -1;

        if (transform.position.y < 3f)
        {
            newFloorIndex = 0; // 1st Floor
        }
        else if (transform.position.y < 5.5f)
        {
            newFloorIndex = 1; // 2nd Floor
        }
        else
        {
            newFloorIndex = 2; // 3rd Floor
        }

        if (newFloorIndex != CurrentFloorIndex)
        {
            CurrentFloorIndex = newFloorIndex;
            UpdateVisuals();
        }
    }

    [Header("Color Settings")]
    // Make sure these match NoteSpawner settings in Inspector!
    [SerializeField] private Color[] floorColors; 

    private void Start()
    {
        // Default Fallback if empty
        if (floorColors == null || floorColors.Length == 0)
        {
            floorColors = new Color[]
            {
                new Color(150f/255f, 0f, 1f), 
                new Color(0f, 1f, 1f), 
                new Color(1f, 0f, 150f/255f)
            };
        }
    }

    private void UpdateVisuals()
    {
        if (floorVisuals == null) return;
        if (floorColors == null || floorColors.Length == 0) Start(); // Ensure Init

        if (stageFloors != null)
        {
            if(CurrentFloorIndex == 0)
            {
                if(stageFloors.Length > 0 && stageFloors[0]) stageFloors[0].SetActive(true);
                if(stageFloors.Length > 1 && stageFloors[1]) stageFloors[1].SetActive(false);
                if(stageFloors.Length > 2 && stageFloors[2]) stageFloors[2].SetActive(false);
            }
            else if(CurrentFloorIndex == 1)
            {
                if(stageFloors.Length > 0 && stageFloors[0]) stageFloors[0].SetActive(false);
                if(stageFloors.Length > 1 && stageFloors[1]) stageFloors[1].SetActive(true);
                if(stageFloors.Length > 2 && stageFloors[2]) stageFloors[2].SetActive(false);
            }
            else
            {
                if(stageFloors.Length > 0 && stageFloors[0]) stageFloors[0].SetActive(false);
                if(stageFloors.Length > 1 && stageFloors[1]) stageFloors[1].SetActive(false);
                if(stageFloors.Length > 2 && stageFloors[2]) stageFloors[2].SetActive(true);
            }
        }
        
        for (int i = 0; i < floorVisuals.Length; i++)
        {
            if (floorVisuals[i] == null) continue;

            Renderer[] renderers = floorVisuals[i].GetComponentsInChildren<Renderer>();
            
            // Use Configured Color
            Color baseColor = (i < floorColors.Length) ? floorColors[i] : Color.white;
            
            // Determine styles based on selection
            if (i == CurrentFloorIndex)
            {
                // Selected: 100% Alpha, Glow ON
                baseColor.a = 1.0f;
            }
            else
            {
                // Unselected: 25% Alpha, Glow OFF
                baseColor.a = 0.25f;
            }

            foreach (Renderer rend in renderers)
            {
                rend.material.color = baseColor;
                
                if (i == CurrentFloorIndex)
                {
                    rend.material.EnableKeyword("_EMISSION");
                    rend.material.SetColor("_EmissionColor", baseColor);
                }
                else
                {
                    rend.material.DisableKeyword("_EMISSION");
                }
            }
        }
    }
}
