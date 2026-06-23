using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TuttorialStep : MonoBehaviour
{
    [SerializeField] private TMP_Text _stepText;
    [SerializeField] private Image _stepImage;
    [SerializeField] private Animation _animation;
    [SerializeField] private Button _button;


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
        _animation.Play();
    }

    public void OnButtonClick()
    {
        _button.interactable = false;
        TutorialManager.instance.NextTutorialStep();    
    }
}   
