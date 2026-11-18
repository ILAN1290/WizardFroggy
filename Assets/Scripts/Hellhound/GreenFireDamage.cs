using UnityEngine;

/// <summary>
/// Deals contact damage while the player touches the green fire boundary.
/// The original scene referenced a missing MonoBehaviour script for this object.
/// </summary>
public class GreenFireDamage : MonoBehaviour
{
    [Header("Green Fire Damage")]
    [SerializeField] private float damage = 5f;
    [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;

    private float nextDamageTime;

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
        if (health == null || !health.CompareTag("Player")) return;

        nextDamageTime = Time.time + Mathf.Max(0.05f, damageInterval);
        health.TakeDamage(Mathf.Max(0f, damage));
    }
}
