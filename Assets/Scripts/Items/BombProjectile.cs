using System.Collections.Generic;
using UnityEngine;

public class BombProjectile : MonoBehaviour
{
    [Header("Взрыв")]
    [Min(0.1f)] public float fuse = 2f;
    [Min(0.1f)] public float radius = 2.5f;
    public float damage = 50f;
    [Min(0f)] public float ghostDamage = 100f;
    public LayerMask hitMask = ~0;

    [Header("Размер")]
    [SerializeField, Min(1f)] private float sizeInPixels = 70f;

    [Header("Урон по себе")]
    public bool damagePlayer = true;
    [Range(0f, 1f)] public float selfDamageFactor = 0.5f;

    [Header("Эффекты")]
    public bool useProceduralVfx = true;
    public Color explosionColor = new Color(1f, 0.5f, 0.15f);
    public GameObject explosionVfxPrefab; // необязательно, если есть свой арт
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float soundVolume = 1f;
    public bool pulse = true;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private CircleCollider2D circleCollider;
    private float timer;
    private bool exploded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();

        FitToScreenSize();
        timer = fuse;
    }

    private void FitToScreenSize()
    {
        if (sr == null || sr.sprite == null)
        {
            Debug.LogWarning("BombProjectile: у бомбы отсутствует SpriteRenderer или Sprite.");
            return;
        }

        // Keep the original collider's world-space radius while shrinking only
        // the oversized source sprite to the requested screen-space dimensions.
        float originalWorldColliderRadius = circleCollider != null
            ? circleCollider.radius * Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y))
            : 0f;

        Camera camera = Camera.main;
        float pixelsPerWorldUnit = camera != null && camera.orthographic &&
            camera.pixelHeight > 0
                ? camera.pixelHeight /
                    Mathf.Max(0.01f, camera.orthographicSize * 2f)
                : Mathf.Max(1f, Screen.height) / 12f;

        float worldSize = sizeInPixels / Mathf.Max(1f, pixelsPerWorldUnit);
        Vector2 spriteSize = sr.sprite.bounds.size;
        transform.localScale = new Vector3(
            worldSize / Mathf.Max(0.001f, spriteSize.x),
            worldSize / Mathf.Max(0.001f, spriteSize.y),
            transform.localScale.z);

        if (circleCollider != null)
        {
            float newWorldScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y));
            if (newWorldScale > 0.0001f)
                circleCollider.radius = originalWorldColliderRadius / newWorldScale;
        }
    }

    public void Launch(Vector2 direction, float force)
    {
        if (rb != null)
            rb.AddForce(direction.normalized * force, ForceMode2D.Impulse);
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (pulse && sr != null)
        {
            float t = Mathf.Clamp01(1f - timer / fuse);
            // Keep the rendered footprint at exactly sizeInPixels; only tint pulses.
            sr.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.35f), t);
        }

        if (timer <= 0f) Explode();
    }

    public void Explode()
    {
        if (exploded) return;
        exploded = true;

        Vector2 center = transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, hitMask);
        HashSet<GameObject> damaged = new HashSet<GameObject>();

        foreach (Collider2D hit in hits)
        {
            GameObject target = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (target == gameObject || !damaged.Add(target)) continue;

            if (target.CompareTag("Enemy"))
            {
                IDamageable damageable = target.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    GhostEnemy ghost = damageable as GhostEnemy;
                    damageable.TakeDamage(ghost != null ? ghostDamage : damage);
                }
                else
                {
                    Boss boss = target.GetComponentInParent<Boss>();
                    if (boss != null) boss.TakeDamage(damage);
                }
            }
            else if (damagePlayer && target.CompareTag("Player"))
            {
                PlayerHealth hp = target.GetComponentInParent<PlayerHealth>();
                if (hp != null) hp.TakeDamage(damage * selfDamageFactor);
            }
        }

        if (useProceduralVfx)
            ExplosionEffect.Spawn(center, radius, explosionColor);

        if (explosionVfxPrefab != null)
            Instantiate(explosionVfxPrefab, center, Quaternion.identity);

        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, center, soundVolume);

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
