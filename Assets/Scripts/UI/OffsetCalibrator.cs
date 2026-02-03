using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class OffsetCalibrator : MonoBehaviour
{
    public enum CalibrationType { Audio, Judgement }
    private CalibrationType currentType;
    
    private bool isCalibrating = false;
    private float bpm = 80f; // 120 / 1.5 = 80 BPM
    private float beatInterval;
    private double startTime;
    private List<double> tapDiffs = new List<double>();
    
    private int requiredTaps = 6;
    private int currentTaps = 0;
    private int nextSoundBeat = 0;
    
    private VisualElement overlay;
    private Label instructionLabel;
    private Label progressLabel;
    private VisualElement beatIndicator;
    
    private MainMenuController controller;

    public bool IsCalibrating => isCalibrating;
    
    public void Initialize(MainMenuController ctrl, VisualElement overlayRoot)
    {
        controller = ctrl;
        overlay = overlayRoot;
        instructionLabel = overlay.Q<Label>("CalibrationInstruction");
        progressLabel = overlay.Q<Label>("CalibrationProgress");
        beatIndicator = overlay.Q<VisualElement>("BeatIndicator");
        
        beatInterval = 60f / bpm;
    }

    public void StartCalibration(CalibrationType type)
    {
        currentType = type;
        isCalibrating = true;
        currentTaps = 0;
        nextSoundBeat = 0;
        tapDiffs.Clear();
        
        overlay.RemoveFromClassList("hidden");
        UpdateUI();
        
        // Stop background music to hear metronome clearly
        if (SongManager.Instance != null) SongManager.Instance.StopMusic();
        
        // Start metronome
        startTime = AudioSettings.dspTime + 0.5; // 0.5s delay
    }

    private void Update()
    {
        if (!isCalibrating) return;

        double currentTime = AudioSettings.dspTime;
        double elapsedTime = currentTime - startTime;
        
        if (elapsedTime < 0) return;

        // 1. Precise Sound Schedule (Metronome Tick)
        // We only want sound for Audio Calibration
        if (currentType == CalibrationType.Audio)
        {
            double nextBeatTime = startTime + nextSoundBeat * beatInterval;
            if (currentTime >= nextBeatTime - 0.05) // Schedule slightly ahead
            {
                if (SongManager.Instance != null) SongManager.Instance.PlayHitSFX();
                nextSoundBeat++;
            }
        }

        // 2. Visual Beat Indicator (Flash)
        // Used for both, but more critical for Judgement Calibration in "visual" mode
        double cycleT = elapsedTime % beatInterval;
        if (cycleT < 0.1) // Flash for 0.1s
        {
            beatIndicator.AddToClassList("flash");
        }
        else
        {
            beatIndicator.RemoveFromClassList("flash");
        }
    }

    public void OnTap()
    {
        if (!isCalibrating) return;
        
        double currentTime = AudioSettings.dspTime;
        double elapsedTime = currentTime - startTime;
        if (elapsedTime < -0.2) return; // Haven't started yet

        // Calculate offset to the NEAREST beat
        double nearestBeatIndex = System.Math.Round(elapsedTime / beatInterval);
        double nearestBeatTime = startTime + nearestBeatIndex * beatInterval;
        double diff = currentTime - nearestBeatTime;

        // Record every tap immediately
        tapDiffs.Add(diff);
        Debug.Log($"Calibration Tap {currentTaps + 1}: Diff {diff * 1000:F1}ms");
        
        currentTaps++;
        UpdateUI();
        
        if (currentTaps >= requiredTaps)
        {
            FinishCalibration();
        }
    }

    private void FinishCalibration()
    {
        isCalibrating = false;
        overlay.AddToClassList("hidden");
        
        // Resume background music
        if (SongManager.Instance != null) SongManager.Instance.PlayMenuMusic();
        
        if (tapDiffs.Count == 0) return;

        double sum = 0;
        foreach (var d in tapDiffs) sum += d;
        double averageDiff = sum / tapDiffs.Count;

        if (currentType == CalibrationType.Audio)
        {
            // Audio Offset (ms)
            // If averageDiff is positive (tap late), we need to spawn notes LATER (+ms)
            int offsetMs = Mathf.RoundToInt((float)(averageDiff * 1000));
            controller.ApplyAudioOffsetCalibration(offsetMs);
        }
        else
        {
            // Judgement Offset (ms)
            // If averageDiff is positive (tap late), we need to move the judgement line CLOSER (-ms in time terms, which GameManager translates to distance)
            int offsetMs = Mathf.RoundToInt((float)(averageDiff * 1000));
            controller.ApplyJudgementOffsetCalibration(offsetMs);
        }
        
        if (SongManager.Instance != null) SongManager.Instance.PlayUIClickSFX();
    }

    public void Cancel()
    {
        isCalibrating = false;
        overlay.AddToClassList("hidden");
        
        // Resume background music
        if (SongManager.Instance != null) SongManager.Instance.PlayMenuMusic();
        
        if (SongManager.Instance != null) SongManager.Instance.PlayUIClickSFX();
    }

    private void UpdateUI()
    {
        instructionLabel.text = currentType == CalibrationType.Audio 
            ? "Listen to the beat and tap (Space/Click)!" 
            : "Watch the flash and tap (Space/Click)!";
        
        progressLabel.text = $"Progress: {currentTaps} / {requiredTaps}";
    }
}
