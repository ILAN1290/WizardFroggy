using UnityEngine;

/// <summary>
/// Damages the player when their collider overlaps one of the three visible
/// boundary-fire strips. The CompositeCollider2D is intentionally ignored:
/// its bounds cover the whole enclosure, not just the fire itself.
/// </summary>
public class GreenFireDamage : MonoBehaviour
{
    [Header("Green Fire Damage")]
    [SerializeField] private float damage = 5f;
    [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;

    private BoxCollider2D[] damageZones;
    private GameObject playerObject;
    private float nextDamageTime;

    private void Awake()
    {
        // Each child BoxCollider2D represents one visible fire strip.
        // Do not use the parent's CompositeCollider2D as its bounds span the arena.
        damageZones = GetComponentsInChildren<BoxCollider2D>(true);
    }

    private void FixedUpdate()
    {
        if (Time.time < nextDamageTime) return;

        if (playerObject == null)
            playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null) return;

        PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
        if (health == null)
            health = playerObject.GetComponentInChildren<PlayerHealth>();
        if (health == null || damageZones == null || damageZones.Length == 0) return;

        Collider2D[] playerColliders = playerObject.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < damageZones.Length; i++)
        {
            BoxCollider2D zone = damageZones[i];
            if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy) continue;

            Bounds fireBounds = GetWorldBounds(zone);
            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D player = playerColliders[j];
                if (player == null || player == zone || !player.enabled ||
                    !player.gameObject.activeInHierarchy) continue;

                if (Overlaps2D(fireBounds, player.bounds))
                {
                    ApplyDamage(health);
                    return;
                }
            }
        }
    }

    private static Bounds GetWorldBounds(BoxCollider2D box)
    {
        // Calculate the rectangle from the child transform and BoxCollider data.
        // This still works when the box is marked as being composed into a parent.
        Vector3 scale = box.transform.lossyScale;
        Vector2 size = new Vector2(
            Mathf.Abs(box.size.x * scale.x),
            Mathf.Abs(box.size.y * scale.y));

        float radians = box.transform.eulerAngles.z * Mathf.Deg2Rad;
        float cosine = Mathf.Abs(Mathf.Cos(radians));
        float sine = Mathf.Abs(Mathf.Sin(radians));
        Vector2 extents = new Vector2(
            (size.x * cosine + size.y * sine) * 0.5f,
            (size.x * sine + size.y * cosine) * 0.5f);

        Vector3 center = box.transform.TransformPoint(box.offset);
        return new Bounds(center, new Vector3(extents.x * 2f, extents.y * 2f, 0.1f));
    }

    private static bool Overlaps2D(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x && a.max.x >= b.min.x &&
               a.min.y <= b.max.y && a.max.y >= b.min.y;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (other == null || Time.time < nextDamageTime) return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;
        ApplyDamage(health);
    }

    private void ApplyDamage(PlayerHealth health)
    {
        if (health == null || Time.time < nextDamageTime) return;

        nextDamageTime = Time.time + Mathf.Max(0.05f, damageInterval);
        health.TakeDamage(Mathf.Max(0f, damage));
    }
}
