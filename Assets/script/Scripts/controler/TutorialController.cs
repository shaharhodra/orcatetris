using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
	[SerializeField] private List<TuttorialStep> tutorialSteps;
	[SerializeField] private Image _overlay;

	void Start()
    {
		TutorialManager.instance.OnTutorialStepShown += HandleOnTutorialStepShown;
		TutorialManager.instance.OnTutorialCompleted += HandleOnTutorialCompleted;		
		TutorialManager.instance.ShowTutorial();
	}

    void OnDestroy()
    {
        TutorialManager.instance.OnTutorialStepShown -= HandleOnTutorialStepShown;
		TutorialManager.instance.OnTutorialCompleted -= HandleOnTutorialCompleted;

	}

	private void HandleOnTutorialStepShown(TutorialScripableObject.TutorialData tutorialData)
    {
		_overlay.gameObject.SetActive(false);
		_overlay.DOFade(0.0f, 0.0f);

		for (int i = 0; i < tutorialSteps.Count; i++)
		{
			var step = tutorialSteps[i];
			if (step.StepIndex == tutorialData.Index)
			{
				_overlay.gameObject.SetActive(true);
				_overlay.DOFade(0.5f, 0.5f).SetEase(Ease.OutSine);
				step.gameObject.SetActive(true);
				step.SetStep(tutorialData.description, tutorialData.icon);
				//step.PlayAnimation();	

			}
			else
			{
				step.gameObject.SetActive(false);
			}
		}
	}

	private void HandleOnTutorialCompleted()
	{
		_overlay.DOFade(0.0f, 0.5f).SetEase(Ease.OutSine).OnComplete(() =>
		{
			_overlay.gameObject.SetActive(false);
		});
		foreach (var step in tutorialSteps)
		{
			step.gameObject.SetActive(false);
		}
	}

}
