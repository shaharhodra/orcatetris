using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PopUpService : MonoBehaviour
{
    [SerializeField] private Image _overlay;
    [SerializeField] private RectTransform _popUpRect;

    public bool IsActive;

    public const float TWEEN_DURATION = 0.5f;
   

   
    private void Start()
    {
        ShowOpenPopUpSequence().Forget();
    }

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
        _popUpRect.DOScale(isActive ? 5.0f : 0.0f, TWEEN_DURATION).SetEase(Ease.OutSine).OnComplete(() =>
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
}
