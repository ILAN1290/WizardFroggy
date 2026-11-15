using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlotUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI countText;
    public Image selectionHighlight;

    private int index;
    private InventoryUI owner;

    public void Setup(int idx, InventoryUI ui)
    {
        index = idx;
        owner = ui;
    }

    public void UpdateView(ItemStack stack, bool selected)
    {
        if (stack.IsEmpty)
        {
            icon.gameObject.SetActive(false);
            countText.gameObject.SetActive(false);
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

    public void OnClick()
    {
        owner.OnSlotClicked(index);
    }
}