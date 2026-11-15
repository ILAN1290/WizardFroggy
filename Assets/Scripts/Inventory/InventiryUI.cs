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

    [Header("Test")]
    [SerializeField] private Item testItem;

    private SlotUI[] invSlots;
    private SlotUI[] hotSlots;
    private int selectedHotbar = 0;
    private int hotbarSlotCount = 4;

    private ItemStack held = new ItemStack();
    
    private GameObject cursorItem;
    private Image cursorIcon;
    private TextMeshProUGUI cursorCount;
    private RectTransform cursorRectTransform;

    private void Start()
    {
        Inventory.Instance.Init(21);
        
        if (testItem != null)
        {
            Inventory.Instance.Add(testItem, 5);
            Inventory.Instance.Add(testItem, 12);
        }
        
        BuildSlots(inventoryRoot, 16, out invSlots, slotPrefab, 5);
        BuildSlots(hotbarRoot, hotbarSlotCount, out hotSlots, hotbarSlotPrefab, 0);

        if (cursorItemPrefab != null && mainCanvas != null)
        {
            cursorItem = Instantiate(cursorItemPrefab, mainCanvas.transform);
            cursorItem.SetActive(false);
            cursorIcon = cursorItem.GetComponent<Image>();
            cursorCount = cursorItem.GetComponentInChildren<TextMeshProUGUI>();
            cursorRectTransform = cursorItem.GetComponent<RectTransform>();
            
            // ВАЖНО: отключаем raycast на курсоре, чтобы клики проходили сквозь него
            if (cursorIcon != null)
                cursorIcon.raycastTarget = false;
            if (cursorCount != null)
                cursorCount.raycastTarget = false;
        }
        else
        {
            Debug.LogError("cursorItemPrefab или mainCanvas не назначены!");
        }

        Inventory.Instance.OnChanged += Refresh;
        Refresh();
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
        {
            int realIndex = 5 + i;
            invSlots[i].UpdateView(Inventory.Instance.GetSlot(realIndex), false);
        }

        for (int i = 0; i < hotSlots.Length; i++)
        {
            hotSlots[i].UpdateView(Inventory.Instance.GetSlot(i), i == selectedHotbar);
        }

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
            else
            {
                cursorCount.gameObject.SetActive(false);
            }
        }
    }

    public void OnSlotClicked(int index)
    {
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
                int canAdd = slot.item.maxStack - slot.count;
                int toAdd = Mathf.Min(canAdd, held.count);
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
        var stack = Inventory.Instance.GetSlot(slotIndex);
        if (!stack.IsEmpty)
        {
            stack.item.Use();
            Inventory.Instance.RemoveAt(slotIndex, 1);
        }
    }

    // Возврат предмета в инвентарь
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
        // Позиционирование курсора
        if (cursorItem != null && !held.IsEmpty && cursorRectTransform != null && mainCanvas != null)
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 localPoint;
            
            RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, 
                mousePos, 
                mainCanvas.worldCamera, 
                out localPoint
            );
            
            cursorRectTransform.anchoredPosition = localPoint;
        }

        for (int i = 0; i < hotbarSlotCount; i++)
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedHotbar = i;
                Refresh();
                UseItem(i);
            }
        }

        // Правый клик - отменить перенос
        if (UnityEngine.Input.GetMouseButtonDown(1) && !held.IsEmpty)
        {
            ReturnHeldItem();
        }
    }
}