using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlotUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI countText;
    public Image selectionHighlight;

    [Header("Cooldown")]
    public Image cooldownFill;

    private int index;
    private InventoryUI owner;
    private ItemStack currentStack;
    private bool warnedNoEffects;

    private static readonly Color NormalColor = Color.white;
    private static readonly Color CooldownColor = new Color(0.4f, 0.4f, 0.4f);

    public void Setup(int idx, InventoryUI ui)
    {
        index = idx;
        owner = ui;
    }

    private void Start()
    {
        if (cooldownFill != null)
        {
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Vertical;
            cooldownFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            cooldownFill.fillAmount = 0f;
            cooldownFill.raycastTarget = false;
            cooldownFill.gameObject.SetActive(false);

            if (cooldownFill.sprite == null)
                Debug.LogWarning($"SlotUI ({name}): у CooldownFill нет спрайта!");
        }
        else
        {
            Debug.LogWarning($"SlotUI ({name}): поле cooldownFill пустое!");
        }
    }

    public void UpdateView(ItemStack stack, bool selected)
    {
        currentStack = stack;
        bool hasItem = stack != null && !stack.IsEmpty;

        if (icon != null)
        {
            icon.sprite = hasItem ? stack.item.icon : null;
            icon.color = NormalColor;
            bool hasIcon = hasItem && icon.sprite != null;
            icon.enabled = hasIcon;
            icon.gameObject.SetActive(hasIcon);
        }

        if (countText != null)
        {
            if (!hasItem)
            {
                countText.gameObject.SetActive(false);
            }
            else if (stack.count > 1)
            {
                countText.text = stack.count.ToString();
                countText.gameObject.SetActive(true);
            }
            else if (icon == null || icon.sprite == null)
            {
                // A missing sprite must not make the item disappear from the UI.
                countText.text = stack.item.itemName;
                countText.gameObject.SetActive(true);
            }
            else
            {
                countText.gameObject.SetActive(false);
            }
        }

        if (cooldownFill != null && !hasItem)
            cooldownFill.gameObject.SetActive(false);

        // Inventory SlotPrefab has no selection child; only use a highlight
        // that actually belongs to this instantiated slot.
        if (selectionHighlight != null &&
            selectionHighlight.transform.IsChildOf(transform))
            selectionHighlight.gameObject.SetActive(selected);
    }

    private void Update()
    {
        if (icon == null) return;

        PlayerEffects effects = owner != null ? owner.Effects : null;
        if (effects == null)
        {
            if (!warnedNoEffects)
            {
                Debug.LogWarning($"SlotUI ({name}): InventoryUI не отдал PlayerEffects!");
                warnedNoEffects = true;
            }

            icon.color = NormalColor;
            if (cooldownFill != null)
                cooldownFill.gameObject.SetActive(false);
            return;
        }

        float fill = 0f;
        if (currentStack != null && !currentStack.IsEmpty)
            fill = effects.GetCooldownFraction(currentStack.item);

        bool onCooldown = fill > 0f;
        icon.color = onCooldown ? CooldownColor : NormalColor;

        if (cooldownFill != null)
        {
            cooldownFill.gameObject.SetActive(onCooldown);
            if (onCooldown) cooldownFill.fillAmount = fill; // 1 → 0, стекает сверху вниз
        }
    }

    public void OnClick()
    {
        if (owner != null)
            owner.OnSlotClicked(index);
    }
}