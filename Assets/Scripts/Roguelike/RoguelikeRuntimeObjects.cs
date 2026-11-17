using UnityEngine;

public class RoguelikeDoor : MonoBehaviour
{
    private RoguelikeSession session;
    private int dx;
    private int dy;
    private Vector2 spawnPosition;

    public void Initialize(RoguelikeSession owner, int directionX, int directionY, Vector2 spawn)
    {
        session = owner;
        dx = directionX;
        dy = directionY;
        spawnPosition = spawn;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || session == null) return;
        session.TryMoveByDirection(dx, dy, spawnPosition);
    }
}

public class RoguelikeTrap : MonoBehaviour
{
    private float nextDamageTime;

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || Time.time < nextDamageTime) return;
        nextDamageTime = Time.time + 1f;
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null) health.TakeDamage(7f);
    }
}

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
            RoguelikeSession.Instance.ShowHint("Инвентарь заполнен!");
    }
}

public class RoguelikeChest : MonoBehaviour
{
    private RoguelikeSession session;
    private bool opened;

    public void Initialize(RoguelikeSession owner)
    {
        session = owner;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (opened || !other.CompareTag("Player") || !InteractionPressed()) return;
        opened = true;
        if (session != null) session.OpenChest(transform.position);
        Destroy(gameObject);
    }

    private bool InteractionPressed()
    {
        return InputManager.instance != null
            ? InputManager.instance.InteractionInput
            : UnityEngine.Input.GetKeyDown(KeyCode.E);
    }
}

public class RoguelikeExit : MonoBehaviour
{
    private RoguelikeSession session;

    public void Initialize(RoguelikeSession owner)
    {
        session = owner;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || session == null) return;
        bool pressed = InputManager.instance != null
            ? InputManager.instance.InteractionInput
            : UnityEngine.Input.GetKeyDown(KeyCode.E);
        if (pressed) session.ReturnToLobby();
    }
}

public class RoguelikeProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float damage;
    private float life;

    public void Initialize(Vector2 targetDirection, float projectileSpeed, float projectileDamage)
    {
        direction = targetDirection.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        life += Time.deltaTime;
        if (life > 5f) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null) health.TakeDamage(damage);
        Destroy(gameObject);
    }
}
