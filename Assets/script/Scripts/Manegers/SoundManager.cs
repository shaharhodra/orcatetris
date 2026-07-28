using UnityEngine;

// SoundManager – Singleton for global SFX control
// Uses the generic Singleton<T> base class already in the project.
public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource audioSource;

    [Header("UI / Shapes")] 
    [SerializeField] private AudioClip clickShapeClip;      // לחיצה על צורה
    [SerializeField] private AudioClip placeShapeClip;      // הנחה של צורה על הלוח

    [Header("Combos")]
    [SerializeField] private AudioClip combo1Clip;          // מחיקה של שורה/עמודה אחת
    [SerializeField] private AudioClip combo2Clip;          // מחיקה של 2
    [SerializeField] private AudioClip combo3Clip;          // מחיקה של 3
    [SerializeField] private AudioClip combo4Clip;          // מחיקה של 4
    [SerializeField] private AudioClip combo5PlusClip;      // מחיקה של 5+

    [Header("Line Clear")]
    [SerializeField] private AudioClip lineClearClip;       // פירוק שורה
    [SerializeField] private AudioClip lineClearPossibleClip; // יש אפשרות להוריד שורה (לפני שהיא נמחקת בפועל)
    [SerializeField] private AudioClip boardClearedClip;    // סיום/ניקוי מלא של הגריד

    [Header("Game Flow")]
    [SerializeField] private AudioClip gameStartClip;       // תחילת משחק
    [SerializeField] private AudioClip levelCompleteClip;   // סיום שלב בהצלחה
    [SerializeField] private AudioClip gameOverClip;        // הפסד
    [SerializeField] private AudioClip nextLevelClip;       // מעבר לשלב הבא

    [Header("Target Symbols")]
    [SerializeField] private AudioClip symbolGrowClip;          // סמל גדל (scale up)
    [SerializeField] private AudioClip symbolReachedTargetClip; // סמל מגיע למטרה

    [Header("Coins")]
    [SerializeField] private AudioClip coinsClip;           // מטבעות עולות

    [Header("Popups")]
    [SerializeField] private AudioClip amazingPopupClip;    // פופאפ מדהים

    [Header("Buttons")]
    [SerializeField] private AudioClip buttonClickClip;     // לחיצה על כפתור (הגדרות, ניווט וכו')

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    // ===== Public API =====

    public void PlayClickShape()
    {
        PlayClip(clickShapeClip);
    }

    public void PlayPlaceShape()
    {
        PlayClip(placeShapeClip);
    }

    public void PlayCombo(int clearedLines)
    {
        if (clearedLines <= 0)
            return;

        if (clearedLines == 1)
        {
            PlayClip(combo1Clip);
        }
        else if (clearedLines == 2)
        {
            PlayClip(combo2Clip);
        }
        else if (clearedLines == 3)
        {
            PlayClip(combo3Clip);
        }
        else if (clearedLines == 4)
        {
            PlayClip(combo4Clip);
        }
        else
        {
            PlayClip(combo5PlusClip);
        }

       // Debug.Log($"Playing combo sound for {clearedLines} lines cleared.");
    }

    public void PlayLineClear()
    {
        PlayClip(lineClearClip);
    }

    public void PlayLineClearPossible()
    {
        PlayClip(lineClearPossibleClip);
    }

    public void PlayBoardCleared()
    {
        PlayClip(boardClearedClip);
    }

    public void PlayGameStart()
    {
        PlayClip(gameStartClip);
    }

    public void PlayLevelComplete()
    {
        PlayClip(levelCompleteClip);
    }

    public void PlayGameOver()
    {
        PlayClip(gameOverClip);
    }

    public void PlayNextLevel()
    {
        PlayClip(nextLevelClip);
    }

    public float SymbolGrowClipLength => symbolGrowClip != null ? symbolGrowClip.length : 0f;
    public float SymbolReachedTargetClipLength => symbolReachedTargetClip != null ? symbolReachedTargetClip.length : 0f;

    public void PlaySymbolGrow(float pitch = 1f)
    {
        if (symbolGrowClip == null || audioSource == null)
            return;

        float prevPitch = audioSource.pitch;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(symbolGrowClip);
        audioSource.pitch = prevPitch;
    }

    public void PlaySymbolReachedTarget(float pitch = 1f)
    {
        if (symbolReachedTargetClip == null || audioSource == null)
            return;

        float prevPitch = audioSource.pitch;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(symbolReachedTargetClip);
        audioSource.pitch = prevPitch;
    }

    public void PlayCoins()
    {
        PlayClip(coinsClip);
    }

    public void PlayAmazingPopup()
    {
        PlayClip(amazingPopupClip);
    }

    public void PlayButtonClick()
    {
        PlayClip(buttonClickClip);
    }

    // גנרי – אם תרצה להשמיע קליפ ספציפי מבחוץ
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        PlayClip(clip, volume);
    }
}
