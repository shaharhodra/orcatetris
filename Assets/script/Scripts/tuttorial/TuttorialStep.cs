using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class TuttorialStep : MonoBehaviour
{
    [SerializeField] private TMP_Text _stepText;
    [SerializeField] private Image _stepImage;
    [SerializeField] private Animation _animation;
    [SerializeField] private Button _button;

    private Tween _fallbackTween;

    private void OnEnable()
    {
        if (TutorialManager.instance != null)
            TutorialManager.instance.OnTutorialStepShown += HandleTutorialStepShown;

        if (TutorialManager.instance != null)
            TutorialManager.instance.ShowTutorial();
    }

    private void OnDisable()
    {
        _fallbackTween?.Kill();
        _fallbackTween = null;

        if (TutorialManager.instance != null)
            TutorialManager.instance.OnTutorialStepShown -= HandleTutorialStepShown;
    }

    private void HandleTutorialStepShown(TutorialScripableObject.TutorialData data)
    {
        if (data == null)
            return;

        SetStep(data.description, data.icon);
        PlayAnimation();
    }


    public void SetStep(string text, Sprite image = null)
    {
        _button.interactable = true;    
        _stepText.text = text;
        if (image != null)
        { 
            _stepImage.sprite = image;
            _stepImage.gameObject.SetActive(true);
        }   
        else
            _stepImage.gameObject.SetActive(false);
    }

    public void PlayAnimation()
    {
        _fallbackTween?.Kill();
        _fallbackTween = null;

        if (_animation != null && Time.timeScale > 0f)
        {
            _animation.Play();
            return;
        }

        var t = transform;
        t.localScale = Vector3.one;
        _fallbackTween = t.DOPunchScale(new Vector3(0.08f, 0.08f, 0f), 0.35f, 8, 0.9f)
            .SetUpdate(true);
    }

    public void OnButtonClick()
    {
        Debug.Log("Button clicked");
		_button.interactable = false;
        TutorialManager.instance.NextTutorialStep();    
    }
}   
