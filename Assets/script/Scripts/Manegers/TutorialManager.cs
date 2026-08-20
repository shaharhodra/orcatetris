using UnityEngine;
using System;

public class TutorialManager :Singleton<TutorialManager>
{
    public event Action OnTutorialCompleted;
    public event Action<TutorialScripableObject.TutorialData> OnTutorialStepShown;    

    [SerializeField] private TutorialScripableObject _tutorialData;

    public int Tutorialindex { get; private set; } = 0;
    public bool IsTutorialCompleted { get; private set; } = false;

    void OnEnable()
    {
        DidCompleteTutorial();
		GameManager.instance.OnDataLoaded += HandleOnDataLoadedEvent;


	}
    void OnDisable()
    {
        GameManager.instance.OnDataLoaded -= HandleOnDataLoadedEvent;
    }
    private void HandleOnDataLoadedEvent(LevelData levelData)
    {
        ShowTutorial();
	}   

	public void ShowTutorial()
    {
        if (!IsTutorialCompleted && Tutorialindex < _tutorialData.Tutorials.Count)
        {
            var currentTutorial = _tutorialData.Tutorials[Tutorialindex];
            // Display the tutorial information using currentTutorial.title, currentTutorial.description, etc.
            // You can use UI elements to show this information to the player.
            OnTutorialStepShown?.Invoke(currentTutorial);
        }
    }

    public void SetTutorialCompleted()
    {
        IsTutorialCompleted = true;
        PlayerPrefs.SetInt("DidCompleteTutorial", 1);
    }

    public void DidCompleteTutorial()
    {
        IsTutorialCompleted = PlayerPrefs.GetInt("DidCompleteTutorial")==1;

    }

    public void NextTutorialStep()
    {
        // Onboarding funnel: which step players stop at is the difference between "the tutorial is
        // too long" and "step 3 is confusing", and nothing distinguishes those today.
        AnalyticsManager.instance?.SendEvent(
            AnalyticsManager.AnalyticsEvent.TutorialStep.ToString(),
            AnalyticsManager.AnalyticsParam.Of("StepIndex", Tutorialindex));

        Tutorialindex++;
        if (Tutorialindex >= _tutorialData.Tutorials.Count)
        {
            SetTutorialCompleted();
            OnTutorialCompleted?.Invoke();
        }
        else
        {
            ShowTutorial();
        }
    }
}
