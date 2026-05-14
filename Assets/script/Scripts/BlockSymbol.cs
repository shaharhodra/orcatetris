using UnityEngine;

public class BlockSymbol : MonoBehaviour
{
    [SerializeField] private ColectionTypes symbolType;
    [SerializeField] private Sprite symbolSprite;

    public ColectionTypes SymbolType => symbolType;
    public bool HasSymbol => symbolSprite != null;

    /// <summary>
    /// Set the symbol type and sprite at runtime
    /// </summary>
    public void SetSymbolType(ColectionTypes newType, Sprite newSprite = null)
    {
        symbolType = newType;
        
        if (newSprite != null)
        {
            symbolSprite = newSprite;
        }
        else
        {
            // Try to get sprite from AdventureTargetUI
            var adventureUI = FindObjectOfType<AdventureTargetUI>();
            if (adventureUI != null)
            {
                // Get the sprite array from AdventureTargetUI using reflection
                var field = typeof(AdventureTargetUI).GetField("symbolSprites", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    var sprites = field.GetValue(adventureUI) as Sprite[];
                    if (sprites != null)
                    {
                        int typeIndex = (int)newType;
                        if (typeIndex >= 0 && typeIndex < sprites.Length)
                        {
                            symbolSprite = sprites[typeIndex];
                            Debug.Log($"[BlockSymbol] Set sprite for type {newType} from AdventureTargetUI");
                        }
                    }
                }
            }
        }
        
        // Recreate icon if it already exists
        if (iconInstance != null)
        {
            Destroy(iconInstance);
            iconInstance = null;
        }
        
        if (symbolSprite != null)
        {
            CreateIcon();
        }
    }

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
        Debug.Log($"[BlockSymbol] Creating icon for {symbolType} on block '{gameObject.name}' at position {transform.position}");
        
        iconInstance = new GameObject("SymbolIcon");
        iconInstance.transform.SetParent(transform, false);
        iconInstance.transform.localPosition = Vector3.zero;
        iconInstance.transform.localScale = Vector3.one * iconScale;

        var sr = iconInstance.AddComponent<SpriteRenderer>();
        sr.sprite = symbolSprite;
        sr.sortingOrder = sortingOrder;
        
        Debug.Log($"[BlockSymbol] ✓ Created icon: sprite={symbolSprite?.name}, localPos={iconInstance.transform.localPosition}, worldPos={iconInstance.transform.position}, scale={iconScale}, order={sortingOrder}");
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
