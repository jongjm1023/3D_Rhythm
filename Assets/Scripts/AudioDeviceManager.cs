using UnityEngine;

public class AudioDeviceManager : MonoBehaviour
{
    void Start()
    {
        // 오디오 장치 변경 감지 이벤트 연결
        AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
    }

    void OnAudioConfigurationChanged(bool deviceWasChanged)
    {
        if (deviceWasChanged)
        {
            // 장치가 바뀌었을 때 오디오 시스템 리셋 (잠시 소리 끊김 발생할 수 있음)
            AudioSettings.Reset(AudioSettings.GetConfiguration());
            Debug.Log("오디오 장치가 변경되어 설정을 리셋했습니다.");
        }
    }

    void OnDestroy()
    {
        // 이벤트 연결 해제 (메모리 누수 방지)
        AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
    }
}