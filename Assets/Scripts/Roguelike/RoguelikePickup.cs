using UnityEngine;

public class RoguelikePickup : MonoBehaviour
{
    private Item item;
    private int count;
    private int currency;
    private AudioClip pickupSound;

    public void SetupItem(Item source, int amount, AudioClip sound = null)
    {
        item = source;
        count = Mathf.Max(1, amount);
        pickupSound = sound;
    }

    public void SetupCurrency(int amount, AudioClip sound = null)
    {
        currency = Mathf.Max(1, amount);
        pickupSound = sound;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (currency > 0)
        {
            PlayerRunData.EnsureInstance().AddMushrooms(currency);
            if (RoguelikeSession.Instance != null)
                RoguelikeSession.Instance.ShowHint("Получено грибочков: " + currency);
            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.55f);
            Destroy(gameObject);
            return;
        }

        if (Inventory.Instance != null && item != null && Inventory.Instance.Add(item, count))
        {
            if (RoguelikeSession.Instance != null)
                RoguelikeSession.Instance.ShowHint("Получен предмет: " + item.itemName);
            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.55f);
            Destroy(gameObject);
        }
        else if (RoguelikeSession.Instance != null)
        {
            RoguelikeSession.Instance.ShowHint("Инвентарь заполнен!");
        }
    }
}
