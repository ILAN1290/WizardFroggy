using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public int maxStack = 64;

    // true = предмет сработал, списывай со стака
    public virtual bool Use(GameObject user)
    {
        Debug.Log($"Использован предмет: {itemName}");
        return false;
    }
}