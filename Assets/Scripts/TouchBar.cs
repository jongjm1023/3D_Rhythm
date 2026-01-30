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

    private void UpdateVisuals()
    {
        if (floorVisuals == null) return;

        // Define specific colors for consistency with Notes
        // 0: Electric Purple, 1: Cyan, 2: Hot Pink
        Color[] floorColors = new Color[]
        {
            new Color(150f/255f, 0f, 1f),     // 1F
            new Color(0f, 1f, 1f),            // 2F
            new Color(1f, 0f, 150f/255f)      // 3F
        };

        if(CurrentFloorIndex == 1){
            stageFloors[0].SetActive(true);
            stageFloors[1].SetActive(false);
        }
        else if(CurrentFloorIndex == 2){
            stageFloors[0].SetActive(false);
            stageFloors[1].SetActive(true);
        }
        else{
            stageFloors[0].SetActive(false);
            stageFloors[1].SetActive(false);
        }
        
        for (int i = 0; i < floorVisuals.Length; i++)
        {
            if (floorVisuals[i] == null) continue;

            // Updated: Apply to all child renderers as requested
            Renderer[] renderers = floorVisuals[i].GetComponentsInChildren<Renderer>();
            
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
