using UnityEngine;

public class TouchBar : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float minHeight = -5f;
    [SerializeField] private float maxHeight = 5f;

    [Header("Floor Settings")]
    [SerializeField] private GameObject[] floorVisuals; // Assign 1F, 2F, 3F objects here
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material selectedMaterial; // Transparent/Glowing material

    private int currentFloorIndex = -1;

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

        if (transform.position.y < 3.5f)
        {
            newFloorIndex = 0; // 1st Floor
        }
        else if (transform.position.y < 6.5f)
        {
            newFloorIndex = 1; // 2nd Floor
        }
        else
        {
            newFloorIndex = 2; // 3rd Floor
        }

        if (newFloorIndex != currentFloorIndex)
        {
            currentFloorIndex = newFloorIndex;
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        if (floorVisuals == null) return;

        for (int i = 0; i < floorVisuals.Length; i++)
        {
            if (floorVisuals[i] == null) continue;

            Renderer rend = floorVisuals[i].GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = (i == currentFloorIndex) ? selectedMaterial : defaultMaterial;
            }
        }
    }
}
