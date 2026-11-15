using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class Item : ScriptableObject {
    public string itemName;
    public Sprite icon;
    public int maxStack = 64;

    public virtual void Use() {
        Debug.Log($"Использован предмет: {itemName}");
        // Здесь будет логика использования
    }
}