using UnityEngine;

public class RoguelikePickup : MonoBehaviour
{
    private Item item;
    private int count;
    private int currency;

    public void SetupItem(Item source, int amount)
    {
        item = source;
        count = Mathf.Max(1, amount);
    }

    public void SetupCurrency(int amount)
    {
        currency = Mathf.Max(1, amount);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (currency > 0)
        {
            PlayerRunData.EnsureInstance().AddMushrooms(currency);
            if (RoguelikeSession.Instance != null)
                RoguelikeSession.Instance.ShowHint("Получено грибочков: " + currency);
            Destroy(gameObject);
            return;
        }

        if (Inventory.Instance != null && item != null && Inventory.Instance.Add(item, count))
        {
            if (RoguelikeSession.Instance != null)
                RoguelikeSession.Instance.ShowHint("Получен предмет: " + item.itemName);
            Destroy(gameObject);
        }
        else if (RoguelikeSession.Instance != null)
        {
            RoguelikeSession.Instance.ShowHint("Инвентарь заполнен!");
        }
    }
}
