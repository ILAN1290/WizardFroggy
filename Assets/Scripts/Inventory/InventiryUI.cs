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
        }

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

    private void Update()
    {
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