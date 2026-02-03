using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _settingsOverlay;
    private Label _offsetValueLabel;
    private Label _judgementOffsetValueLabel;
    private Label _speedValueLabel; // New
    private OffsetCalibrator _calibrator;
    private Slider _judgementOffsetSlider;
    private SliderInt _audioOffsetSlider;

    // PlayerPrefs Keys
    private const string PREF_VOLUME = "MasterVolume";
    private const string PREF_BGM_VOLUME = "BGMVolume";
    private const string PREF_SFX_VOLUME = "SFXVolume";
    private const string PREF_OFFSET = "AudioOffset";
    private const string PREF_JUDGEMENT_OFFSET = "JudgementOffset";
    private const string PREF_NOTE_SPEED = "NoteSpeed"; // New

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
        var calibrateAudioButton = root.Q<Button>("CalibrateAudioButton");
        var calibrateJudgementButton = root.Q<Button>("CalibrateJudgementButton");
        var cancelCalibrationButton = root.Q<Button>("CancelCalibrationButton");
        
        _settingsOverlay = root.Q<VisualElement>("SettingsOverlay");
        var calibrationOverlay = root.Q<VisualElement>("CalibrationOverlay");
        
        _calibrator = gameObject.GetComponent<OffsetCalibrator>();
        if (_calibrator == null) _calibrator = gameObject.AddComponent<OffsetCalibrator>();
        _calibrator.Initialize(this, calibrationOverlay);
        
        var volumeSlider = root.Q<Slider>("VolumeSlider");
        var bgmSlider = root.Q<Slider>("BgmVolumeSlider");
        var sfxSlider = root.Q<Slider>("SfxVolumeSlider");
        _audioOffsetSlider = root.Q<SliderInt>("OffsetSlider");
        _offsetValueLabel = root.Q<Label>("OffsetValueLabel");

        _judgementOffsetSlider = root.Q<Slider>("JudgementOffsetSlider");
        _judgementOffsetValueLabel = root.Q<Label>("JudgementOffsetValueLabel");

        var speedSlider = root.Q<Slider>("SpeedSlider");
        _speedValueLabel = root.Q<Label>("SpeedValueLabel");

        // Helper to safely register events
        if (startButton != null) startButton.clicked += OnStartClicked;
        if (settingsButton != null) settingsButton.clicked += OnSettingsClicked;
        if (exitButton != null) exitButton.clicked += OnExitClicked;
        if (closeSettingsButton != null) closeSettingsButton.clicked += OnCloseSettingsClicked;
        
        if (calibrateAudioButton != null) calibrateAudioButton.clicked += () => _calibrator.StartCalibration(OffsetCalibrator.CalibrationType.Audio);
        if (calibrateJudgementButton != null) calibrateJudgementButton.clicked += () => _calibrator.StartCalibration(OffsetCalibrator.CalibrationType.Judgement);
        if (cancelCalibrationButton != null) cancelCalibrationButton.clicked += () => _calibrator.Cancel();

        // Initialize Settings
        float currentVolume = PlayerPrefs.GetFloat(PREF_VOLUME, 1.0f);
        float currentBgmVolume = PlayerPrefs.GetFloat(PREF_BGM_VOLUME, 1.0f);
        float currentSfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 1.0f);
        int currentOffset = PlayerPrefs.GetInt(PREF_OFFSET, 0);
        float currentJudgementOffset = PlayerPrefs.GetFloat(PREF_JUDGEMENT_OFFSET, 0f);
        float currentSpeed = PlayerPrefs.GetFloat(PREF_NOTE_SPEED, 10f); // Default 10

        if (volumeSlider != null)
        {
            volumeSlider.value = currentVolume;
            volumeSlider.RegisterValueChangedCallback(evt => OnVolumeChanged(evt.newValue));
        }

        if (bgmSlider != null)
        {
            bgmSlider.value = currentBgmVolume;
            bgmSlider.RegisterValueChangedCallback(evt => OnBgmVolumeChanged(evt.newValue));
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = currentSfxVolume;
            sfxSlider.RegisterValueChangedCallback(evt => OnSfxVolumeChanged(evt.newValue));
        }
        
        // Apply initial volumes
        AudioListener.volume = currentVolume;
        if (SongManager.Instance != null)
        {
            SongManager.Instance.SetBGMVolume(currentBgmVolume);
            SongManager.Instance.SetSFXVolume(currentSfxVolume);
        }

        if (_audioOffsetSlider != null)
        {
            _audioOffsetSlider.value = currentOffset;
            UpdateOffsetLabel(currentOffset);
            _audioOffsetSlider.RegisterValueChangedCallback(evt => OnOffsetChanged(evt.newValue));
        }

        if (_judgementOffsetSlider != null)
        {
            _judgementOffsetSlider.value = currentJudgementOffset;
            UpdateJudgementOffsetLabel(currentJudgementOffset);
            _judgementOffsetSlider.RegisterValueChangedCallback(evt => OnJudgementOffsetChanged(evt.newValue));
        }

        if (speedSlider != null)
        {
            speedSlider.value = currentSpeed;
            UpdateSpeedLabel(currentSpeed);
            speedSlider.RegisterValueChangedCallback(evt => OnSpeedChanged(evt.newValue));
        }
    }

    private void Update()
    {
        // Handle Taps during calibration
        if (_calibrator != null && _calibrator.IsCalibrating)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            
            if ((kb != null && kb.spaceKey.wasPressedThisFrame) || 
                (mouse != null && mouse.leftButton.wasPressedThisFrame))
            {
                _calibrator.OnTap();
            }
        }
    }

    private void Start()
    {
        // Play Menu BGM
        if (SongManager.Instance != null)
        {
            SongManager.Instance.PlayMenuMusic();
        }
    }

    private void OnStartClicked()
    {
        if (SongManager.Instance != null) SongManager.Instance.PlayUIClickSFX();
        Debug.Log("Start Button Clicked. Loading 'Songs' scene...");
        SceneManager.LoadScene("Songs");
    }

    private void OnSettingsClicked()
    {
        if (SongManager.Instance != null) SongManager.Instance.PlayUIClickSFX();
        if (_settingsOverlay != null)
        {
            _settingsOverlay.RemoveFromClassList("hidden");
        }
    }

    private void OnCloseSettingsClicked()
    {
        if (SongManager.Instance != null) SongManager.Instance.PlayUIClickSFX();
        if (_settingsOverlay != null)
        {
            _settingsOverlay.AddToClassList("hidden");
        }
    }

    private void OnExitClicked()
    {
        if (SongManager.Instance != null) SongManager.Instance.PlayUIClickSFX();
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

    private void OnBgmVolumeChanged(float newValue)
    {
        if (SongManager.Instance != null) SongManager.Instance.SetBGMVolume(newValue);
    }

    private void OnSfxVolumeChanged(float newValue)
    {
        if (SongManager.Instance != null) SongManager.Instance.SetSFXVolume(newValue);
    }

    private void OnOffsetChanged(int newValue)
    {
        UpdateOffsetLabel(newValue);
        PlayerPrefs.SetInt(PREF_OFFSET, newValue);
        PlayerPrefs.Save();
    }

    private void OnJudgementOffsetChanged(float newValue)
    {
        UpdateJudgementOffsetLabel(newValue);
        PlayerPrefs.SetFloat(PREF_JUDGEMENT_OFFSET, newValue);
        PlayerPrefs.Save();
    }

    private void OnSpeedChanged(float newValue)
    {
        UpdateSpeedLabel(newValue);
        PlayerPrefs.SetFloat(PREF_NOTE_SPEED, newValue);
        PlayerPrefs.Save();
        
        if (SongManager.Instance != null)
        {
            SongManager.Instance.NoteSpeed = newValue;
        }
    }

    public void ApplyAudioOffsetCalibration(int offsetMs)
    {
        if (_audioOffsetSlider != null) _audioOffsetSlider.value = offsetMs;
        OnOffsetChanged(offsetMs);
    }

    public void ApplyJudgementOffsetCalibration(float distanceOffset)
    {
        if (_judgementOffsetSlider != null) _judgementOffsetSlider.value = distanceOffset;
        OnJudgementOffsetChanged(distanceOffset);
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
            _judgementOffsetValueLabel.text = $"{value:F2}"; 
        }
    }

    private void UpdateSpeedLabel(float value)
    {
        if (_speedValueLabel != null)
        {
            float multiplier = value / 10.0f;
            _speedValueLabel.text = $"x{multiplier:F1}"; 
        }
    }
}
