using UnityEngine;
using System.Reflection;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public class TowerDefenceKeyPickup : MonoBehaviour
{
    [SerializeField] private Item keyItem;
    [SerializeField] private TowerDefenceSceneController sceneController;

    private bool collected;

    public void Initialize(Item item, TowerDefenceSceneController owner)
    {
        keyItem = item;
        sceneController = owner;

        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.48f;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    private static bool AddOneKeyToInventory(Inventory inventory, Item keyItem)
    {
        if (inventory == null || keyItem == null)
            return false;

        MethodInfo addWithAmount = inventory.GetType().GetMethod(
            "Add", new[] { typeof(Item), typeof(int) });
        if (addWithAmount != null)
        {
            object result = addWithAmount.Invoke(inventory, new object[] { keyItem, 1 });
            return !(result is bool) || (bool)result;
        }

        MethodInfo addSingle = inventory.GetType().GetMethod(
            "Add", new[] { typeof(Item) });
        if (addSingle != null)
        {
            object result = addSingle.Invoke(inventory, new object[] { keyItem });
            return !(result is bool) || (bool)result;
        }

        Debug.LogError("Tower Defence: Inventory.Add не найден.");
        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || keyItem == null || other == null)
            return;

        if (!other.CompareTag("Player") &&
            other.GetComponentInParent<PlayerController>() == null)
            return;

        Inventory inventory = Inventory.EnsureInstance();
        if (inventory == null)
            return;

        if (!AddOneKeyToInventory(inventory, keyItem))
        {
            if (sceneController != null)
                sceneController.ShowKeyPickupMessage("Не удалось положить ключ в инвентарь");
            return;
        }

        bool itemWasAdded = false;
        if (inventory.slots != null)
        {
            for (int i = 0; i < inventory.slots.Count; i++)
            {
                ItemStack stack = inventory.slots[i];
                if (stack != null && !stack.IsEmpty && stack.item == keyItem)
                {
                    itemWasAdded = true;
                    break;
                }
            }
        }

        if (!itemWasAdded)
        {
            if (sceneController != null)
                sceneController.ShowKeyPickupMessage("В инвентаре нет свободного места для ключа");
            return;
        }

        collected = true;
        if (sceneController != null)
            sceneController.OnTowerDefenceKeyCollected();
        Destroy(gameObject);
    }
}
