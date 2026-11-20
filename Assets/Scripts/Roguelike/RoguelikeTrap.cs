using UnityEngine;

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
