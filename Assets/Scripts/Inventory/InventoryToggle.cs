using UnityEngine;

public class InventoryToggle : MonoBehaviour
{
    public CanvasGroup inventoryPanel;
    public InventoryUI inventoryUI;

    private bool isOpen = false;

    private void Start()
    {
        SetOpen(false);
    }

    private void Update()
    {
        if (!ControlGate.HasControl)
        {
            if (isOpen) SetOpen(false);
            return;
        }

        if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
        {
            SetOpen(!isOpen);
        }
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        inventoryPanel.alpha = open ? 1 : 0;
        inventoryPanel.interactable = open;
        inventoryPanel.blocksRaycasts = open;

        if (!open && inventoryUI != null)
        {
            inventoryUI.ReturnHeldItem();
        }
    }
}