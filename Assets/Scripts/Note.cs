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
}
