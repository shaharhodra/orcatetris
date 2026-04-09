using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
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

    [Header("Game Over Score")]
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _maxScoreText;
    [SerializeField] private Button _restartButton;
    [SerializeField] private float scoreCountDuration = 1.5f;
    [SerializeField] private float elementTweenDuration = 0.35f;

    public bool IsActive;
    [SerializeField] public const float popUPDuretion= 1f;
    [SerializeField] public const float TWEEN_DURATION = 0.5f;

    private Tween scoreCountTween;
    private Sequence elementsSequence;

    // אירוע שנשלח כשהשחקן סוגר את הפופאפ ידנית (בלחיצת כפתור)
    public event Action OnPopupDismissed;
    public void SetOverlayActiveState (bool isActive)
    {
        _overlay.gameObject.SetActive(true);
        _overlay.DOFade(isActive ? 0.75f : 0.0f, TWEEN_DURATION).SetEase(Ease.OutSine).OnComplete(() =>
        {
            if (!isActive)
                _overlay.gameObject.SetActive(false);
        });
    }
    public void SetOverlayActiveStateForLoss (bool isActive)
    {
        _overlay.gameObject.SetActive(true);
        _overlay.DOFade(isActive ? 1.0f : 0.0f, TWEEN_DURATION).SetEase(Ease.OutSine).OnComplete(() =>
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
    public async UniTask ShowClosePopUpSequence ()
    {
        ShowPopUp(false);
        await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
        SetOverlayActiveState(false);
    }

    public async UniTask DoSomething ()
    {
        await UniTask.WaitUntil(() => IsActive);

    }

    public void OnXButtonClicked ()
    {
        scoreCountTween?.Kill();
        elementsSequence?.Kill();

        var closeSeq = DOTween.Sequence();

        if (_restartButton != null)
            closeSeq.Append(_restartButton.transform.DOScale(0f, elementTweenDuration).SetEase(Ease.InBack));

        if (_maxScoreText != null)
            closeSeq.Append(_maxScoreText.rectTransform.DOScale(0f, elementTweenDuration).SetEase(Ease.InBack));

        if (_scoreText != null)
            closeSeq.Append(_scoreText.rectTransform.DOScale(0f, elementTweenDuration).SetEase(Ease.InBack));

        closeSeq.AppendCallback(() =>
        {
            ShowPopUp(false);
            SetOverlayActiveState(false);
            IsActive = false;
            OnPopupDismissed?.Invoke();
        });
    }

    // מפעיל את רצף הפתיחה-סגירה רק אם הטריגר שהתקבל מתאים ל-condition שמוגדר באינספקטור
    public void RunIfConditionMet(PopUpCondition trigger)
    {
        if (trigger == condition)
        {
            RunPopupSequenceAsync().Forget();
        }
    }

    // פותח את הפופאפ ומשאיר אותו פתוח עד שהשחקן לוחץ כפתור
    public void RunIfConditionMetAndStay(PopUpCondition trigger)
    {
        if (trigger == condition)
        {
            ShowAndStay();
        }
    }

    public void ShowAndStay()
    {
        IsActive = true;
        SetOverlayActiveStateForLoss(true);
        UpdateScoreTexts();
        ShowPopUp(true);
    }

    private void UpdateScoreTexts()
    {
        int finalScore = ScoreManager.instance != null ? ScoreManager.instance.Score : 0;
        int maxScore = ScoreManager.instance != null ? ScoreManager.instance.MaxScore : 0;

        // כפתור חסום עד סיום הספירה
        if (_restartButton != null)
            _restartButton.interactable = false;

        // איפוס scale לפני האנימציה
        if (_scoreText != null)
            _scoreText.rectTransform.localScale = Vector3.zero;
        if (_maxScoreText != null)
            _maxScoreText.rectTransform.localScale = Vector3.zero;
        if (_restartButton != null)
            _restartButton.transform.localScale = Vector3.zero;

        // הצגת ערכים
        if (_maxScoreText != null)
            _maxScoreText.text = maxScore.ToString();
        if (_scoreText != null)
            _scoreText.text = "0";

        // רצף אנימציות: score -> max score -> ספירה -> כפתור
        scoreCountTween?.Kill();
        elementsSequence?.Kill();

        elementsSequence = DOTween.Sequence();

        // 1. Score label מופיע
        if (_scoreText != null)
            elementsSequence.Append(_scoreText.rectTransform.DOScale(1f, elementTweenDuration).SetEase(Ease.OutBack));

        // 2. ספירת נקודות מ-0 עד הניקוד
        if (_scoreText != null && finalScore > 0)
        {
            int displayed = 0;
            elementsSequence.Append(
                DOTween.To(() => displayed, x =>
                {
                    displayed = x;
                    _scoreText.text = displayed.ToString();
                }, finalScore, scoreCountDuration)
                .SetEase(Ease.OutCubic)
            );
        }

        // 3. Max Score מופיע
        if (_maxScoreText != null)
            elementsSequence.Append(_maxScoreText.rectTransform.DOScale(1f, elementTweenDuration).SetEase(Ease.OutBack));

        // 4. כפתור התחל מחדש מופיע ונהפך ללחיץ
        if (_restartButton != null)
        {
            elementsSequence.Append(_restartButton.transform.DOScale(1f, elementTweenDuration).SetEase(Ease.OutBack));
            elementsSequence.AppendCallback(() => _restartButton.interactable = true);
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

