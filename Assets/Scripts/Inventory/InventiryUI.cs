using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("Refs")]
    public GameObject slotPrefab;
    public GameObject hotbarSlotPrefab;
    public Transform inventoryRoot;
    public Transform hotbarRoot;
    public GameObject cursorItemPrefab;
    public Canvas mainCanvas;

    [Header("Player")]
    [SerializeField] private GameObject player;

    [Header("Test")]
    [SerializeField] private Item testItem;

    public PlayerEffects Effects { get; private set; }

    private SlotUI[] invSlots;
    private SlotUI[] hotSlots;
    private int selectedHotbar = 0;
    private int inventorySlotCount = 16;
    private int hotbarSlotCount = 4;
    private ItemStack held = new ItemStack();
    private GameObject cursorItem;
    private Image cursorIcon;
    private TextMeshProUGUI cursorCount;
    private RectTransform cursorRectTransform;
    private bool hotbarVisible = true;

    [Header("Прозрачность инвентаря при перекрытии игроком")]
    [SerializeField, Range(0.1f, 1f)] private float overlapAlpha = 0.38f;
    private CanvasGroup inventoryVisibilityGroup;
    private CanvasGroup inventoryOcclusionGroup;
    private CanvasGroup hotbarCanvasGroup;
    private float hotbarBaseAlpha = 1f;
    private SpriteRenderer playerRenderer;
    private Camera cachedWorldCamera;
    private readonly Vector3[] playerWorldCorners = new Vector3[4];
    private readonly Vector3[] inventoryWorldCorners = new Vector3[4];
    private readonly Vector3[] hotbarWorldCorners = new Vector3[4];

    private void Awake()
    {
        EnsureCanvasOnTop();
    }

    private void EnsureCanvasOnTop()
    {
        if (mainCanvas == null) return;

        // Keep the inventory and cursor above every world sprite and other canvas.
        mainCanvas.overrideSorting = true;
        mainCanvas.sortingOrder = short.MaxValue;
    }

    private void Start()
    {
        cachedWorldCamera = Camera.main;
        EnsureCanvasOnTop();
        Inventory.Instance.Init(hotbarSlotCount + inventorySlotCount);

        if (player == null)
        {
            var fx = GetComponent<PlayerEffects>();
            if (fx != null) player = fx.gameObject;
            else Debug.LogError("InventoryUI: назначь player или добавь PlayerEffects на игрока!");
        }

        if (player != null)
        {
            Effects = player.GetComponent<PlayerEffects>();
            if (Effects == null)
                Debug.LogError("InventoryUI: на player нет компонента PlayerEffects!");

            playerRenderer = player.GetComponent<SpriteRenderer>();
            if (playerRenderer == null)
                playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);
        }

        InitializeInventoryCanvasGroups();

        if (testItem != null)
        {
            Inventory.Instance.Add(testItem, 5);
            Inventory.Instance.Add(testItem, 12);
        }

        BuildSlots(inventoryRoot, inventorySlotCount, out invSlots, slotPrefab, hotbarSlotCount);
        BuildSlots(hotbarRoot, hotbarSlotCount, out hotSlots, hotbarSlotPrefab, 0);

        if (cursorItemPrefab != null && mainCanvas != null)
        {
            cursorItem = Instantiate(cursorItemPrefab, mainCanvas.transform);
            cursorItem.transform.SetAsLastSibling();
            cursorItem.SetActive(false);
            cursorIcon = cursorItem.GetComponent<Image>();
            cursorCount = cursorItem.GetComponentInChildren<TextMeshProUGUI>();
            cursorRectTransform = cursorItem.GetComponent<RectTransform>();
            if (cursorIcon != null) cursorIcon.raycastTarget = false;
            if (cursorCount != null) cursorCount.raycastTarget = false;
        }
        else Debug.LogError("cursorItemPrefab или mainCanvas не назначены!");

        Inventory.Instance.OnChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnChanged -= Refresh;
    }

    private void BuildSlots(Transform root, int count, out SlotUI[] arr, GameObject prefab, int startIndex)
    {
        arr = new SlotUI[count];
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab, root);
            arr[i] = go.GetComponent<SlotUI>();
            arr[i].Setup(startIndex + i, this);
        }
    }

    private void Refresh()
    {
        for (int i = 0; i < invSlots.Length; i++)
            invSlots[i].UpdateView(Inventory.Instance.GetSlot(hotbarSlotCount + i), false);
        for (int i = 0; i < hotSlots.Length; i++)
            hotSlots[i].UpdateView(Inventory.Instance.GetSlot(i), i == selectedHotbar);
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        if (cursorItem == null) return;
        if (held.IsEmpty)
        {
            cursorItem.SetActive(false);
        }
        else
        {
            cursorItem.SetActive(true);
            cursorIcon.sprite = held.item.icon;
            if (held.count > 1)
            {
                cursorCount.gameObject.SetActive(true);
                cursorCount.text = held.count.ToString();
            }
            else cursorCount.gameObject.SetActive(false);
        }
    }

    public void OnSlotClicked(int index)
    {
        if (!ControlGate.HasControl) return;

        var slot = Inventory.Instance.GetSlot(index);
        if (held.IsEmpty)
        {
            if (!slot.IsEmpty)
            {
                held.item = slot.item;
                held.count = slot.count;
                slot.Clear();
            }
        }
        else
        {
            if (slot.IsEmpty)
            {
                slot.item = held.item;
                slot.count = held.count;
                held.Clear();
            }
            else if (slot.item == held.item && slot.count < slot.item.maxStack)
            {
                int toAdd = Mathf.Min(slot.item.maxStack - slot.count, held.count);
                slot.count += toAdd;
                held.count -= toAdd;
                if (held.count <= 0) held.Clear();
            }
            else
            {
                var tmpItem = slot.item;
                var tmpCount = slot.count;
                slot.item = held.item;
                slot.count = held.count;
                held.item = tmpItem;
                held.count = tmpCount;
            }
        }
        Inventory.Instance.NotifyChanged();
    }

    public void UseItem(int slotIndex)
    {
        if (!ControlGate.HasControl) return;

        var stack = Inventory.Instance.GetSlot(slotIndex);
        if (stack.IsEmpty) return;

        if (player == null)
        {
            Debug.LogError("InventoryUI: player не назначен!");
            return;
        }

        if (stack.item.Use(player))
            Inventory.Instance.RemoveAt(slotIndex, 1);
    }

    public void ReturnHeldItem()
    {
        if (!held.IsEmpty)
        {
            Inventory.Instance.Add(held.item, held.count);
            held.Clear();
            Refresh();
        }
    }

    private void InitializeInventoryCanvasGroups()
    {
        if (inventoryRoot != null)
        {
            // This original CanvasGroup belongs to InventoryToggle. Never change
            // its alpha: it is zero while the inventory panel is closed.
            inventoryVisibilityGroup = inventoryRoot.GetComponent<CanvasGroup>();
            if (inventoryVisibilityGroup == null)
                inventoryVisibilityGroup = inventoryRoot.gameObject.AddComponent<CanvasGroup>();

            RectTransform sourceRect = inventoryRoot as RectTransform;
            if (sourceRect != null)
            {
                Transform originalParent = inventoryRoot.parent;
                int originalSibling = inventoryRoot.GetSiblingIndex();

                GameObject wrapper = new GameObject(
                    "Inventory Occlusion Group",
                    typeof(RectTransform), typeof(CanvasGroup));
                RectTransform wrapperRect = wrapper.GetComponent<RectTransform>();
                wrapperRect.SetParent(originalParent, false);
                wrapperRect.anchorMin = sourceRect.anchorMin;
                wrapperRect.anchorMax = sourceRect.anchorMax;
                wrapperRect.anchoredPosition = sourceRect.anchoredPosition;
                wrapperRect.sizeDelta = sourceRect.sizeDelta;
                wrapperRect.pivot = sourceRect.pivot;
                wrapperRect.localRotation = sourceRect.localRotation;
                wrapperRect.localScale = sourceRect.localScale;
                wrapperRect.SetSiblingIndex(originalSibling);

                sourceRect.SetParent(wrapperRect, false);
                sourceRect.anchorMin = Vector2.zero;
                sourceRect.anchorMax = Vector2.one;
                sourceRect.offsetMin = Vector2.zero;
                sourceRect.offsetMax = Vector2.zero;
                sourceRect.localRotation = Quaternion.identity;
                sourceRect.localScale = Vector3.one;

                inventoryOcclusionGroup = wrapper.GetComponent<CanvasGroup>();
                inventoryOcclusionGroup.alpha = 1f;
            }
        }

        if (hotbarRoot != null)
        {
            hotbarCanvasGroup = hotbarRoot.GetComponent<CanvasGroup>();
            if (hotbarCanvasGroup == null)
                hotbarCanvasGroup = hotbarRoot.gameObject.AddComponent<CanvasGroup>();
            hotbarBaseAlpha = hotbarCanvasGroup.alpha;
        }
    }

    private void UpdateInventoryOcclusion()
    {
        // Inventory and hotbar are faded independently.
        bool inventoryOpen = inventoryVisibilityGroup != null &&
            inventoryVisibilityGroup.alpha > 0.01f &&
            inventoryVisibilityGroup.blocksRaycasts &&
            inventoryVisibilityGroup.interactable;

        if (player == null ||
            (inventoryOcclusionGroup == null && hotbarCanvasGroup == null))
            return;

        if (cachedWorldCamera == null) cachedWorldCamera = Camera.main;
        Camera worldCamera = cachedWorldCamera;
        if (worldCamera == null) return;

        Bounds worldBounds = playerRenderer != null
            ? playerRenderer.bounds
            : new Bounds(player.transform.position, new Vector3(0.5f, 0.8f, 0.1f));

        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        playerWorldCorners[0] = new Vector3(min.x, min.y, worldBounds.center.z);
        playerWorldCorners[1] = new Vector3(min.x, max.y, worldBounds.center.z);
        playerWorldCorners[2] = new Vector3(max.x, min.y, worldBounds.center.z);
        playerWorldCorners[3] = new Vector3(max.x, max.y, worldBounds.center.z);

        Vector2 screenMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 screenMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < playerWorldCorners.Length; i++)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(playerWorldCorners[i]);
            screenMin = Vector2.Min(screenMin, screen);
            screenMax = Vector2.Max(screenMax, screen);
        }

        Rect playerScreenBounds = Rect.MinMaxRect(
            screenMin.x, screenMin.y, screenMax.x, screenMax.y);

        Camera uiCamera = mainCanvas != null &&
            mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? mainCanvas.worldCamera : null;
        bool overlapsInventory = OverlapsUiRect(
            inventoryRoot, playerScreenBounds, uiCamera, inventoryWorldCorners);
        bool overlapsHotbar = OverlapsUiRect(
            hotbarRoot, playerScreenBounds, uiCamera, hotbarWorldCorners);

        float inventoryAlpha = inventoryOpen && overlapsInventory ? overlapAlpha : 1f;
        if (inventoryOcclusionGroup != null &&
            Mathf.Abs(inventoryOcclusionGroup.alpha - inventoryAlpha) > 0.001f)
            inventoryOcclusionGroup.alpha = inventoryAlpha;

        float hotbarAlpha = hotbarBaseAlpha * (overlapsHotbar ? overlapAlpha : 1f);
        if (hotbarCanvasGroup != null &&
            Mathf.Abs(hotbarCanvasGroup.alpha - hotbarAlpha) > 0.001f)
            hotbarCanvasGroup.alpha = hotbarAlpha;
    }

    private static bool OverlapsUiRect(
        Transform root, Rect screenBounds, Camera uiCamera, Vector3[] corners)
    {
        RectTransform rectTransform = root as RectTransform;
        if (rectTransform == null || !root.gameObject.activeInHierarchy)
            return false;

        rectTransform.GetWorldCorners(corners);

        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            minimum = Vector2.Min(minimum, screen);
            maximum = Vector2.Max(maximum, screen);
        }

        Rect uiBounds = Rect.MinMaxRect(
            minimum.x, minimum.y, maximum.x, maximum.y);
        return screenBounds.Overlaps(uiBounds, true);
    }

    private void Update()
    {
        UpdateInventoryOcclusion();
        bool control = ControlGate.HasControl;

        if (control != hotbarVisible)
        {
            hotbarVisible = control;
            hotbarRoot.gameObject.SetActive(control);
        }

        if (cursorItem != null && !held.IsEmpty && cursorRectTransform != null && mainCanvas != null)
        {
            RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, Input.mousePosition, mainCanvas.worldCamera, out Vector2 localPoint);
            cursorRectTransform.anchoredPosition = localPoint;
        }

        if (!control) return;

        for (int i = 0; i < hotbarSlotCount; i++)
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedHotbar = i;
                Refresh();
            }
        }

        if (UnityEngine.Input.GetKeyDown(KeyCode.F))
        {
            UseItem(selectedHotbar);
        }

        if (UnityEngine.Input.GetMouseButtonDown(1) && !held.IsEmpty)
            ReturnHeldItem();
    }
}