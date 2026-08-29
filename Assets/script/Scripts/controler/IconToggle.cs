using UnityEngine;
using UnityEngine.UI;

// Swaps the sprite on a target Image based on a Toggle's state, so the icon
// stays visible and switches (e.g. sound-on / sound-off) instead of the
// default Toggle behavior of just showing/hiding its Graphic.
[RequireComponent(typeof(Toggle))]
public class IconToggle : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite onSprite;
    [SerializeField] private Sprite offSprite;

    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
    }

    private void OnEnable()
    {
        toggle.onValueChanged.AddListener(SetIcon);
        SetIcon(toggle.isOn);
    }

    private void OnDisable()
    {
        toggle.onValueChanged.RemoveListener(SetIcon);
    }

    private void SetIcon(bool isOn)
    {
        if (targetImage == null) return;
        targetImage.sprite = isOn ? onSprite : offSprite;
    }
}
