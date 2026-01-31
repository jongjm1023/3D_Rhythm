using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _settingsOverlay;
    private Label _offsetValueLabel;
    private Label _judgementOffsetValueLabel;

    // PlayerPrefs Keys
    private const string PREF_VOLUME = "MasterVolume";
    private const string PREF_OFFSET = "AudioOffset";
    private const string PREF_JUDGEMENT_OFFSET = "JudgementOffset";

    private void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null)
        {
            Debug.LogError("MainMenuController: No UIDocument found!");
            return;
        }

        var root = _uiDocument.rootVisualElement;

        // Query Elements
        var startButton = root.Q<Button>("StartButton");
        var settingsButton = root.Q<Button>("SettingsButton");
        var exitButton = root.Q<Button>("ExitButton");
        var closeSettingsButton = root.Q<Button>("CloseSettingsButton");
        
        _settingsOverlay = root.Q<VisualElement>("SettingsOverlay");
        
        var volumeSlider = root.Q<Slider>("VolumeSlider");
        var offsetSlider = root.Q<SliderInt>("OffsetSlider");
        _offsetValueLabel = root.Q<Label>("OffsetValueLabel");

        var judgementOffsetSlider = root.Q<Slider>("JudgementOffsetSlider");
        _judgementOffsetValueLabel = root.Q<Label>("JudgementOffsetValueLabel");

        // Helper to safely register events
        if (startButton != null) startButton.clicked += OnStartClicked;
        if (settingsButton != null) settingsButton.clicked += OnSettingsClicked;
        if (exitButton != null) exitButton.clicked += OnExitClicked;
        if (closeSettingsButton != null) closeSettingsButton.clicked += OnCloseSettingsClicked;

        // Initialize Settings
        float currentVolume = PlayerPrefs.GetFloat(PREF_VOLUME, 1.0f);
        int currentOffset = PlayerPrefs.GetInt(PREF_OFFSET, 0);
        float currentJudgementOffset = PlayerPrefs.GetFloat(PREF_JUDGEMENT_OFFSET, 0f);

        if (volumeSlider != null)
        {
            volumeSlider.value = currentVolume;
            volumeSlider.RegisterValueChangedCallback(evt => OnVolumeChanged(evt.newValue));
        }
        
        // Apply initial volume
        AudioListener.volume = currentVolume;

        if (offsetSlider != null)
        {
            offsetSlider.value = currentOffset;
            UpdateOffsetLabel(currentOffset);
            offsetSlider.RegisterValueChangedCallback(evt => OnOffsetChanged(evt.newValue));
        }

        if (judgementOffsetSlider != null)
        {
            judgementOffsetSlider.value = currentJudgementOffset;
            UpdateJudgementOffsetLabel(currentJudgementOffset);
            judgementOffsetSlider.RegisterValueChangedCallback(evt => OnJudgementOffsetChanged(evt.newValue));
        }
    }

    private void OnStartClicked()
    {
        Debug.Log("Start Button Clicked. Loading 'Songs' scene...");
        SceneManager.LoadScene("Songs");
    }

    private void OnSettingsClicked()
    {
        if (_settingsOverlay != null)
        {
            _settingsOverlay.RemoveFromClassList("hidden");
        }
    }

    private void OnCloseSettingsClicked()
    {
        if (_settingsOverlay != null)
        {
            _settingsOverlay.AddToClassList("hidden");
        }
    }

    private void OnExitClicked()
    {
        Debug.Log("Exit Button Clicked. Quitting application...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnVolumeChanged(float newValue)
    {
        AudioListener.volume = newValue;
        PlayerPrefs.SetFloat(PREF_VOLUME, newValue);
        PlayerPrefs.Save();
    }

    private void OnOffsetChanged(int newValue)
    {
        // Update Label
        UpdateOffsetLabel(newValue);
        
        // Save Offset
        PlayerPrefs.SetInt(PREF_OFFSET, newValue);
        PlayerPrefs.Save();
        
        // NOTE: The actual game logic (NoteSpawner) needs to read this PlayerPrefs key
    }

    private void OnJudgementOffsetChanged(float newValue)
    {
        UpdateJudgementOffsetLabel(newValue);
        PlayerPrefs.SetFloat(PREF_JUDGEMENT_OFFSET, newValue);
        PlayerPrefs.Save();
    }

    private void UpdateOffsetLabel(int value)
    {
        if (_offsetValueLabel != null)
        {
            _offsetValueLabel.text = $"{value} ms";
        }
    }

    private void UpdateJudgementOffsetLabel(float value)
    {
        if (_judgementOffsetValueLabel != null)
        {
            // F2 for 2 decimal places
            _judgementOffsetValueLabel.text = $"{value:F2}"; 
        }
    }
}
