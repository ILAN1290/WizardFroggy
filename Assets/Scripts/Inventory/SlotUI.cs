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

        if (stack.IsEmpty)
        {
            icon.gameObject.SetActive(false);
            countText.gameObject.SetActive(false);
            if (cooldownFill != null)
                cooldownFill.gameObject.SetActive(false);
        }
        else
        {
            icon.gameObject.SetActive(true);
            icon.sprite = stack.item.icon;
            if (stack.count > 1)
            {
                countText.gameObject.SetActive(true);
                countText.text = stack.count.ToString();
            }
            else countText.gameObject.SetActive(false);
        }
        selectionHighlight.gameObject.SetActive(selected);
    }

    private void Update()
    {
        PlayerEffects effects = owner != null ? owner.Effects : null;
        if (effects == null)
        {
            if (!warnedNoEffects)
            {
                Debug.LogWarning($"SlotUI ({name}): InventoryUI не отдал PlayerEffects!");
                warnedNoEffects = true;
            }
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
        owner.OnSlotClicked(index);
    }
}