using UnityEngine;

/// <summary>
/// Applies damage when a player collider overlaps one of the green boundary strips.
/// Uses both trigger callbacks and an explicit overlap check so the composite
/// boundary collider remains reliable with the player's multi-collider setup.
/// </summary>
public class GreenFireDamage : MonoBehaviour
{
    [Header("Green Fire Damage")]
    [SerializeField] private float damage = 5f;
    [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;

    private Collider2D[] fireColliders;
    private GameObject playerObject;
    private float nextDamageTime;

    private void Awake()
    {
        fireColliders = GetComponentsInChildren<Collider2D>(true);
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
        if (health == null) return;

        Collider2D[] playerColliders = playerObject.GetComponentsInChildren<Collider2D>(true);
        if (fireColliders == null || playerColliders.Length == 0) return;

        for (int i = 0; i < fireColliders.Length; i++)
        {
            Collider2D fire = fireColliders[i];
            if (fire == null || !fire.enabled || !fire.gameObject.activeInHierarchy) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D player = playerColliders[j];
                if (player == null || player == fire || !player.enabled ||
                    !player.gameObject.activeInHierarchy) continue;

                if (fire.Distance(player).isOverlapped)
                {
                    ApplyDamage(health);
                    return;
                }
            }
        }
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
