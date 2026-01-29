using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Score { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnNoteHit()
    {
        Score += 100;
        Debug.Log($"Note Hit! Score: {Score}");
    }

    public void OnNoteMiss()
    {
        Debug.Log("Note Missed");
        // Reset combo or decrease health here
    }
}
