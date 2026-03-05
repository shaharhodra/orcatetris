using UnityEngine;

// SoundManager – Singleton for global SFX control
// Uses the generic Singleton<T> base class already in the project.
public class SoundManager : Singleton<SoundManager>
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;

    [Header("UI / Shapes")] 
    [SerializeField] private AudioClip clickShapeClip;      // לחיצה על צורה
    [SerializeField] private AudioClip placeShapeClip;      // הנחה של צורה על הלוח

    [Header("Combos")]
    [SerializeField] private AudioClip combo1Clip;          // מחיקה של שורה/עמודה אחת
    [SerializeField] private AudioClip combo2Clip;          // מחיקה של 2
    [SerializeField] private AudioClip combo3PlusClip;      // מחיקה של 3+

    // חשוב: לא להגדיר Awake כאן, כדי לא להסתיר את Awake שב-Singleton<T> שמגדיר את instance.
    private void Start()
    {
        // אם אין אודיו סורס משויך, ננסה לקחת מהאובייקט עצמו
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }
    }

    private void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, volume);
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
        else
        {
            PlayClip(combo3PlusClip);
        }
    }

    // גנרי – אם תרצה להשמיע קליפ ספציפי מבחוץ
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        PlayClip(clip, volume);
    }
}
