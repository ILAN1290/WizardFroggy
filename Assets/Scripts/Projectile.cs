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
        // парирование не уничтожает снаряд — его развернёт Reflect()
        if (other.CompareTag("Parry")) return;

        if (projType == ProjectileType.EnemyProjectile)
        {
            // вражеский кинжал пробивает всё, кроме игрока
            if (!other.CompareTag("Player")) return;

            AudioManager.instance.PlaySoundFXClip(stubbingSound, transform, 0.5f);
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null) playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // снаряд игрока — поведение как раньше
        if (other.CompareTag("Enemy"))
        {
            Boss boss = other.GetComponentInParent<Boss>();
            if (boss != null)
            {
                boss.TakeDamage(damage);
                Debug.Log(boss.CurrentHealth);
            }
        }
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