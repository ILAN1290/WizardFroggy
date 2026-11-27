using UnityEngine;

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || keyItem == null || other == null)
            return;

        if (!other.CompareTag("Player") &&
            other.GetComponentInParent<PlayerController>() == null)
            return;

        Inventory inventory = Inventory.EnsureInstance();
        if (inventory == null || !inventory.Add(keyItem, 1))
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
