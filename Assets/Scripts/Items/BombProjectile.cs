using System.Collections.Generic;
using UnityEngine;

public class BombProjectile : MonoBehaviour
{
    [Header("Взрыв")]
    [Min(0.1f)] public float fuse = 2f;
    [Min(0.1f)] public float radius = 2.5f;
    public float damage = 50f;
    public LayerMask hitMask = ~0;

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
    private Vector3 baseScale;
    private float timer;
    private bool exploded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        timer = fuse;
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
            float k = 1f + 0.15f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 30f, t));
            transform.localScale = baseScale * k;
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
                Boss boss = target.GetComponentInParent<Boss>();
                if (boss != null) boss.TakeDamage(damage);
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