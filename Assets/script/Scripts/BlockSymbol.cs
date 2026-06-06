using UnityEngine;
using DG.Tweening;

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

    [Header("Entry Animation")]
    [SerializeField] private bool animateEntry = false;
    [SerializeField] private float entryDuration = 0.6f;
    [SerializeField] private Ease entryEase = Ease.OutBack;
    [SerializeField] private float entryDistance = 8f;
    [SerializeField] private float entryDelay = 0f;

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
        iconInstance.transform.localScale = Vector3.one * iconScale;

        var sr = iconInstance.AddComponent<SpriteRenderer>();
        sr.sprite = symbolSprite;
        sr.sortingOrder = sortingOrder;
        
        if (animateEntry)
        {
            AnimateIconEntry();
        }
        else
        {
            iconInstance.transform.localPosition = Vector3.zero;
        }
        
        Debug.Log($"[BlockSymbol] ✓ Created icon: sprite={symbolSprite?.name}, localPos={iconInstance.transform.localPosition}, worldPos={iconInstance.transform.position}, scale={iconScale}, order={sortingOrder}");
    }

    private void AnimateIconEntry()
    {
        // Choose random side to enter from
        Vector3 entryDirection = GetRandomEntryDirection();
        Vector3 startPosition = entryDirection * entryDistance;
        Vector3 targetPosition = Vector3.zero;
        
        iconInstance.transform.localPosition = startPosition;
        
        // Animate to target position
        iconInstance.transform.DOLocalMove(targetPosition, entryDuration)
            .SetDelay(entryDelay)
            .SetEase(entryEase);
    }

    private Vector3 GetRandomEntryDirection()
    {
        // Randomly choose one of 4 sides (left, right, top, bottom)
        int side = UnityEngine.Random.Range(0, 4);
        
        switch (side)
        {
            case 0: return Vector3.left;   // From left
            case 1: return Vector3.right;  // From right
            case 2: return Vector3.up;     // From top
            case 3: return Vector3.down;   // From bottom
            default: return Vector3.left;
        }
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
