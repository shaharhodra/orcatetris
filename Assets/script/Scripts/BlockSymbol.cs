using UnityEngine;

public class BlockSymbol : MonoBehaviour
{
    [SerializeField] private ColectionTypes symbolType;
    [SerializeField] private Sprite symbolSprite;

    public ColectionTypes SymbolType => symbolType;
    public bool HasSymbol => symbolSprite != null;

    [Header("Icon Settings")]
    [SerializeField] private int sortingOrder = 10;
    [SerializeField] private float iconScale = 0.4f;

    private GameObject iconInstance;

    private void Start()
    {
        if (symbolSprite != null && iconInstance == null)
            CreateIcon();
    }

    private void CreateIcon()
    {
        iconInstance = new GameObject("SymbolIcon");
        iconInstance.transform.SetParent(transform, false);
        iconInstance.transform.localPosition = Vector3.zero;
        iconInstance.transform.localScale = Vector3.one * iconScale;

        var sr = iconInstance.AddComponent<SpriteRenderer>();
        sr.sprite = symbolSprite;
        sr.sortingOrder = sortingOrder;
    }

    public GameObject DetachIcon()
    {
        if (iconInstance == null)
            return null;

        var icon = iconInstance;
        iconInstance = null;
        return icon;
    }
}
