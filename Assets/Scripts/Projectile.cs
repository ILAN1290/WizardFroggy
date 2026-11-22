using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ProjectileType
{
    PlayerProjectile,
    EnemyProjectile
}

public class Projectile : MonoBehaviour
{
    Rigidbody2D rigidbody2d;
    public ProjectileType projType;
    [SerializeField] private float damage = 7f;
    [SerializeField] private AudioClip stubbingSound;

    [Header("Уничтожение")]
    [SerializeField] private bool destroyOffScreen = true;
    [SerializeField] private float screenMargin = 0.15f;   // запас за краем экрана (доли кадра)
    [SerializeField] private float spawnGrace = 1.5f;      // сколько живёт, если родился вне кадра
    [SerializeField] private float maxLife = 12f;          // страховка, 0 = без лимита

    private Camera cam;
    private float life;
    private bool enteredScreen;

    void Awake()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        cam = Camera.main;
    }

    public void SetDamage(float value)
    {
        damage = Mathf.Max(0f, value);
    }

    public void Launch(Vector2 direction, float force)
    {
        rigidbody2d.AddForce(direction * force);
    }

    private void Update()
    {
        life += Time.deltaTime;
        if (maxLife > 0f && life > maxLife) { Destroy(gameObject); return; }

        if (!destroyOffScreen) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        bool inside = vp.z > 0f
                      && vp.x > -screenMargin && vp.x < 1f + screenMargin
                      && vp.y > -screenMargin && vp.y < 1f + screenMargin;

        if (inside) enteredScreen = true;
        else if (enteredScreen || life > spawnGrace) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // Parrying is handled by Parry and must not consume the projectile.
        if (other.CompareTag("Parry")) return;

        if (projType == ProjectileType.EnemyProjectile)
        {
            // Enemy shots only affect the player; every other object is ignored.
            if (!other.CompareTag("Player")) return;

            if (AudioManager.instance != null)
                AudioManager.instance.PlaySoundFXClip(stubbingSound, transform, 0.5f);
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null) playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Player shots pass through all triggers, pickups, traps and enemy shots.
        // They are consumed only by an enemy or by an explicitly marked wall.
        GhostEnemy ghost = other.GetComponentInParent<GhostEnemy>();
        Boss boss = other.GetComponentInParent<Boss>();
        if (other.CompareTag("Enemy") || ghost != null || boss != null)
        {
            if (ghost != null)
                ghost.TakeDamage(damage);
            else if (boss != null)
            {
                boss.TakeDamage(damage);
                Debug.Log(boss.CurrentHealth);
            }

            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Wall") && !other.isTrigger)
            Destroy(gameObject);
    }

    public void Reflect(Vector2 reflectDirection, float force)
    {
        float angle = Mathf.Atan2(reflectDirection.y, reflectDirection.x) * Mathf.Rad2Deg - 90;
        transform.eulerAngles = new Vector3(0, 0, angle);
        rigidbody2d.linearVelocity = Vector2.zero;
        rigidbody2d.AddForce(reflectDirection * force);
    }
}