using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class GameplayUIController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private Label _scoreLabel;
    private Label _comboValueLabel;
    private VisualElement _comboContainer;
    private Label _judgmentLabel;

    private Coroutine _judgmentHideCoroutine;
    private Coroutine _comboPopCoroutine;

    private void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) return;

        var root = _uiDocument.rootVisualElement;

        _scoreLabel = root.Q<Label>("ScoreValue");
        _comboValueLabel = root.Q<Label>("ComboValue");
        _comboContainer = root.Q("ComboContainer");
        _judgmentLabel = root.Q<Label>("JudgmentValue");

        // Initial state
        if (_comboContainer != null) _comboContainer.style.opacity = 0;
        if (_judgmentLabel != null) _judgmentLabel.text = "";
    }

    public void UpdateScore(int score)
    {
        if (_scoreLabel != null)
        {
            _scoreLabel.text = score.ToString("D8");
        }
    }

    public void UpdateCombo(int combo)
    {
        if (_comboValueLabel == null || _comboContainer == null) return;

        _comboValueLabel.text = combo.ToString();
        
        if (combo > 0)
        {
            _comboContainer.style.opacity = 1;
            
            // Trigger Pop Animation
            if (_comboPopCoroutine != null) StopCoroutine(_comboPopCoroutine);
            _comboPopCoroutine = StartCoroutine(AnimatePop(_comboValueLabel));
        }
        else
        {
            _comboContainer.style.opacity = 0;
        }
    }

    public void ShowJudgment(string judgment)
    {
        if (_judgmentLabel == null) return;

        _judgmentLabel.text = judgment.ToUpper();
        
        // Remove existing classes
        _judgmentLabel.RemoveFromClassList("perfect");
        _judgmentLabel.RemoveFromClassList("great");
        _judgmentLabel.RemoveFromClassList("good");
        _judgmentLabel.RemoveFromClassList("bad");
        _judgmentLabel.RemoveFromClassList("miss");

        // Add new class based on text
        string className = judgment.ToLower();
        _judgmentLabel.AddToClassList(className);

        // Animation logic
        if (_judgmentHideCoroutine != null) StopCoroutine(_judgmentHideCoroutine);
        _judgmentHideCoroutine = StartCoroutine(HandleJudgmentLifecycle());
    }

    private IEnumerator HandleJudgmentLifecycle()
    {
        // Simple scale and fade out effect
        _judgmentLabel.style.scale = new StyleScale(new Vector2(1.2f, 1.2f));
        _judgmentLabel.style.opacity = 1;

        float elapsed = 0;
        float duration = 0.1f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _judgmentLabel.style.scale = new StyleScale(new Vector2(1.0f, 1.0f));

        yield return new WaitForSeconds(0.5f);

        // Fade out
        elapsed = 0;
        duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _judgmentLabel.style.opacity = 1.0f - (elapsed / duration);
            yield return null;
        }
        _judgmentLabel.style.opacity = 0;
    }

    private IEnumerator AnimatePop(VisualElement element)
    {
        element.style.scale = new StyleScale(new Vector2(1.3f, 1.3f));
        yield return new WaitForSeconds(0.05f);
        element.style.scale = new StyleScale(new Vector2(1.0f, 1.0f));
    }
}
