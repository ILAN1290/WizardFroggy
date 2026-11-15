using UnityEngine;

public class InventoryToggle : MonoBehaviour
{
    public CanvasGroup inventoryPanel;
    public InventoryUI inventoryUI; // ДОБАВЬ ЭТО ПОЛЕ

    private bool isOpen = false;

    private void Update()
    {
        if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
        {
            isOpen = !isOpen;

            inventoryPanel.alpha = isOpen ? 1 : 0;
            inventoryPanel.interactable = isOpen;
            inventoryPanel.blocksRaycasts = isOpen;

            // Если закрываем инвентарь - возвращаем предмет
            if (!isOpen && inventoryUI != null)
            {
                inventoryUI.ReturnHeldItem();
            }
        }
    }
}