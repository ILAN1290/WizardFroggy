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
    private bool passThroughUpperWall;

    void Awake()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        if (projType == ProjectileType.PlayerProjectile)
            ReducePlayerProjectileHitbox();
    }

    private void ReducePlayerProjectileHitbox()
    {
        // The original prefab collider is oversized compared with the visible bolt.
        // Clamp its world-space dimensions so a near miss with a shelf does not
        // consume the projectile.
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D hitbox = colliders[i];
            if (hitbox == null) continue;

            Vector3 scale = hitbox.transform.lossyScale;
            float scaleX = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float scaleY = Mathf.Max(0.001f, Mathf.Abs(scale.y));

            BoxCollider2D box = hitbox as BoxCollider2D;
            if (box != null)
            {
                box.size = new Vector2(
                    Mathf.Min(box.size.x, 0.20f / scaleX),
                    Mathf.Min(box.size.y, 0.20f / scaleY));
                continue;
            }

            CircleCollider2D circle = hitbox as CircleCollider2D;
            if (circle != null)
            {
                float scaleMax = Mathf.Max(scaleX, scaleY);
                circle.radius = Mathf.Min(circle.radius, 0.11f / scaleMax);
                continue;
            }

            CapsuleCollider2D capsule = hitbox as CapsuleCollider2D;
            if (capsule != null)
            {
                capsule.size = new Vector2(
                    Mathf.Min(capsule.size.x, 0.20f / scaleX),
                    Mathf.Min(capsule.size.y, 0.20f / scaleY));
            }
        }

        if (rigidbody2d != null)
            rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void SetDamage(float value)
    {
        damage = Mathf.Max(0f, value);
    }

    public void Launch(Vector2 direction, float force)
    {
        // Player shots are consumed only by walls or enemies, not by a timer
        // or by leaving the camera viewport.
        if (projType == ProjectileType.PlayerProjectile)
        {
            destroyOffScreen = false;
            maxLife = 0f;
        }

        Vector2 shotDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized : Vector2.up;
        passThroughUpperWall = projType == ProjectileType.PlayerProjectile &&
            shotDirection.y <= 0.05f;
        rigidbody2d.AddForce(shotDirection * force);
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
        {
            // The upper boundary overlaps the player's launch area. Sideways
            // and downward shots should pass through it instead of being consumed.
            if (projType == ProjectileType.PlayerProjectile &&
                passThroughUpperWall &&
                other.name.StartsWith("Верхняя стена"))
                return;

            Destroy(gameObject);
        }
    }

    public void Reflect(Vector2 reflectDirection, float force)
    {
        float angle = Mathf.Atan2(reflectDirection.y, reflectDirection.x) * Mathf.Rad2Deg - 90;
        transform.eulerAngles = new Vector3(0, 0, angle);
        rigidbody2d.linearVelocity = Vector2.zero;
        rigidbody2d.AddForce(reflectDirection * force);
    }
}