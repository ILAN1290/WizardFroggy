using UnityEngine;

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
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;
        health.TakeDamage(damage);
        Destroy(gameObject);
    }
}
