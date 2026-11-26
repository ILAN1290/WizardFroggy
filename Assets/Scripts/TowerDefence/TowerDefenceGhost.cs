using UnityEngine;

public enum TowerDefenceGhostType
{
    White,
    Blue,
    Red,
    Black
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class TowerDefenceGhost : MonoBehaviour
{
    private TowerDefenceManager manager;
    private Rigidbody2D body;
    private Vector3[] route;
    private int waypointIndex;
    private float currentHealth;
    private float moveSpeed;
    private float pathProgress;
    private bool finished;

    public TowerDefenceGhostType Type { get; private set; }
    public float CurrentHealth => Mathf.Max(0f, currentHealth);
    public float PathProgress => pathProgress;
    public bool IsAlive => !finished && currentHealth > 0f;

    public void Initialize(
        TowerDefenceManager owner,
        TowerDefenceGhostType type,
        Vector3[] selectedRoute,
        Sprite sprite,
        float health,
        float speed,
        float sizeMultiplier)
    {
        manager = owner;
        Type = type;
        route = selectedRoute;
        currentHealth = Mathf.Max(1f, health);
        moveSpeed = Mathf.Max(0.05f, speed);
        waypointIndex = 1;
        pathProgress = 0f;
        finished = false;

        transform.position = route != null && route.Length > 0
            ? route[0]
            : Vector3.zero;

        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        CircleCollider2D collider = GetComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.34f * Mathf.Max(0.75f, sizeMultiplier);
        transform.localScale = Vector3.one;

        // Visual is a child so the root collider stays at the correct size.
        Transform visual = transform.Find("Ghost Visual");
        if (visual == null)
        {
            GameObject visualObject = new GameObject("Ghost Visual");
            visualObject.transform.SetParent(transform, false);
            visual = visualObject.transform;
        }

        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = visual.gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.color = GetTint(type);
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 18;

        float sourceHeight = sprite != null
            ? Mathf.Max(0.001f, sprite.bounds.size.y)
            : 1f;
        float targetHeight = 0.72f * sizeMultiplier;
        visual.localScale = sprite != null
            ? Vector3.one * (targetHeight / sourceHeight)
            : Vector3.one * targetHeight;
    }

    private void FixedUpdate()
    {
        if (finished || route == null || route.Length == 0 || manager == null)
            return;

        if (waypointIndex >= route.Length)
        {
            ReachGrandpa();
            return;
        }

        Vector2 previous = body != null
            ? body.position
            : (Vector2)transform.position;
        Vector2 target = route[waypointIndex];
        Vector2 next = Vector2.MoveTowards(
            previous, target, moveSpeed * Time.fixedDeltaTime);

        if (body != null)
            body.MovePosition(next);
        else
            transform.position = next;

        pathProgress += Vector2.Distance(previous, next);

        if (Vector2.Distance(next, target) <= 0.025f)
        {
            if (body != null)
                body.position = target;
            else
                transform.position = target;

            waypointIndex++;
            if (waypointIndex >= route.Length)
                ReachGrandpa();
        }
    }

    public void TakeDamage(float amount)
    {
        if (finished || amount <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (finished)
            return;

        finished = true;
        if (manager != null)
            manager.OnGhostDefeated(this, transform.position);
        Destroy(gameObject);
    }

    private void ReachGrandpa()
    {
        if (finished)
            return;

        finished = true;
        if (manager != null)
            manager.OnGhostReachedGrandpa(this, CurrentHealth);
        Destroy(gameObject);
    }

    private static Color GetTint(TowerDefenceGhostType type)
    {
        switch (type)
        {
            case TowerDefenceGhostType.Blue:
                return new Color(0.48f, 0.72f, 1f, 1f);
            case TowerDefenceGhostType.Red:
                return new Color(1f, 0.36f, 0.36f, 1f);
            case TowerDefenceGhostType.Black:
                return new Color(0.12f, 0.12f, 0.16f, 1f);
            default:
                return Color.white;
        }
    }
}
