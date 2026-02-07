using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum PopUpCondition
{
    None,
    OnGameStart,
    OnWin,
    OnLose,
    Custom
}

public class PopUpService : MonoBehaviour
{
    [SerializeField] private Image _overlay;
    [SerializeField] private RectTransform _popUpRect;
    [SerializeField] private PopUpCondition condition = PopUpCondition.None; 

    public bool IsActive;
    [SerializeField] public const float popUPDuretion= 1f;
    [SerializeField] public const float TWEEN_DURATION = 0.5f;
    public void SetOverlayActiveState (bool isActive)
    {
        _overlay.gameObject.SetActive(true);
        _overlay.DOFade(isActive ? 0.75f : 0.0f, TWEEN_DURATION).SetEase(Ease.OutSine).OnComplete(() =>
        {
            if (!isActive)
                _overlay.gameObject.SetActive(false);
        });
    }

    public void ShowPopUp (bool isActive)
    {
        _popUpRect.gameObject.SetActive(true);
        _popUpRect.DOScale(isActive ? 1.0f : 0.0f, TWEEN_DURATION).SetEase(Ease.OutSine).OnComplete(() =>
       {
           if (!isActive)
               _popUpRect.gameObject.SetActive(false);
       });
    }

    public async UniTask ShowOpenPopUpSequence ()
    {
        SetOverlayActiveState(false);
        await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
        ShowPopUp(false);
    }

    public async UniTask DoSomething ()
    {
        await UniTask.WaitUntil(() => IsActive);

    }

    public void OnXButtonClicked ()
    {
        ShowPopUp(false);
        SetOverlayActiveState(false);
    }

    // מפעיל את רצף הפתיחה-סגירה רק אם הטריגר שהתקבל מתאים ל-condition שמוגדר באינספקטור
    public void RunIfConditionMet(PopUpCondition trigger)
    {
        if (trigger == condition)
        {
            RunPopupSequenceAsync().Forget();
        }
    }

    public async UniTask RunPopupSequenceAsync ()
    {
        IsActive = true;
        SetOverlayActiveState(true);
        ShowPopUp(true);

        await UniTask.Delay(TimeSpan.FromSeconds(popUPDuretion));

        ShowPopUp(false);
        SetOverlayActiveState(false);

        await UniTask.Delay(TimeSpan.FromSeconds(popUPDuretion));

        IsActive = false;
    }
     
}

