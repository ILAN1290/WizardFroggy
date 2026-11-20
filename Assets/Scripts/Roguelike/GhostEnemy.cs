using System.Collections;
using UnityEngine;

public class GhostEnemy : MonoBehaviour, IDamageable
{
    private RoguelikeSession session;
    private Transform player;
    private PlayerHealth playerHealth;
    private GhostAI ai;
    private float health;
    private float damage;
    private bool melee;
    private bool miniBoss;
    private float nextAttackTime;
    private float nextRadialShotTime;
    private bool defeated;
    private Sprite[] attackSprites;

    public void Initialize(RoguelikeSession owner, int floor, bool isMelee,
        bool isMiniBoss = false, Sprite[] attackAnimationSprites = null)
    {
        session = owner;
        melee = isMelee;
        miniBoss = isMiniBoss;
        health = isMiniBoss ? 125f + floor * 25f : 22f + floor * 8f;
        damage = isMiniBoss ? 16f + floor * 3f : 8f + floor * 2.5f;
        attackSprites = attackAnimationSprites != null
            ? (Sprite[])attackAnimationSprites.Clone()
            : new Sprite[0];

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;

        ai = GetComponent<GhostAI>();
        if (ai == null) ai = gameObject.AddComponent<GhostAI>();
        ai.Initialize(player, melee, miniBoss);

        nextRadialShotTime = Time.time + 2.5f;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (defeated || player == null || ai == null) return;

        if (miniBoss && Time.time >= nextRadialShotTime)
        {
            nextRadialShotTime = Time.time + 4.5f;
            ShootRadialBurst();
        }

        if (!ai.CanAttack || Time.time < nextAttackTime) return;

        Vector2 direction = ai.DirectionToPlayer;
        if (melee)
        {
            nextAttackTime = Time.time + (miniBoss ? 0.95f : 1.15f);
            StartCoroutine(PlayAttackAnimation(direction));
            if (playerHealth != null) playerHealth.TakeDamage(damage);
        }
        else
        {
            nextAttackTime = Time.time + 1.8f;
            StartCoroutine(PlayAttackAnimation(direction));
            Shoot(direction);
        }
    }

    private IEnumerator PlayAttackAnimation(Vector2 direction)
    {
        if (attackSprites == null || attackSprites.Length == 0) yield break;

        GameObject effect = new GameObject("Анимация атаки призрака");
        effect.transform.SetParent(transform.parent, true);
        effect.transform.position = transform.position + (Vector3)(direction * 0.78f);
        effect.transform.rotation = Quaternion.Euler(0f, 0f,
            Vector2.SignedAngle(Vector2.right, direction));

        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 80;
        renderer.enabled = true;

        const float frameTime = 0.11f;
        float targetHeight = 1.35f * (miniBoss ? 1.2f : 1f);

        for (int i = 0; i < attackSprites.Length; i++)
        {
            Sprite frame = attackSprites[i];
            if (frame == null) continue;

            renderer.sprite = frame;
            renderer.color = miniBoss
                ? new Color(1f, 0.36f, 0.4f, 1f)
                : new Color(1f, 1f, 1f, 1f);

            float sourceHeight = Mathf.Max(0.001f, frame.bounds.size.y);
            effect.transform.localScale = Vector3.one * (targetHeight / sourceHeight);

            yield return new WaitForSeconds(frameTime);
        }

        Destroy(effect);
    }

    private void ShootRadialBurst()
    {
        const int projectileCount = 8;
        Color redTint = new Color(1f, 0.17f, 0.22f, 1f);

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = i * (360f / projectileCount);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            GameObject bolt = CreateProjectile("Красный снаряд мини-босса",
                transform.position, redTint, 0.17f);
            BoxCollider2D hitbox = bolt.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = Vector2.one * 0.8f;

            Rigidbody2D projectileBody = bolt.AddComponent<Rigidbody2D>();
            projectileBody.bodyType = RigidbodyType2D.Kinematic;
            projectileBody.gravityScale = 0f;

            bolt.AddComponent<RoguelikeProjectile>().Initialize(
                direction, 4.6f, damage * 0.65f, redTint);
        }
    }

    private void Shoot(Vector2 direction)
    {
        Color blueTint = new Color(0.68f, 0.9f, 1f, 1f);
        GameObject bolt = CreateProjectile("Призрачный снаряд", transform.position,
            blueTint, 0.14f);

        BoxCollider2D collider = bolt.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.22f, 0.22f);

        Rigidbody2D projectileBody = bolt.AddComponent<Rigidbody2D>();
        projectileBody.bodyType = RigidbodyType2D.Kinematic;
        projectileBody.gravityScale = 0f;

        bolt.AddComponent<RoguelikeProjectile>().Initialize(direction, 5.4f,
            damage, blueTint);
    }

    private GameObject CreateProjectile(string objectName, Vector2 position,
        Color tint, float size)
    {
        GameObject projectile = new GameObject(objectName);
        projectile.transform.SetParent(transform.parent, true);
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * size;

        SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
        renderer.sprite = RoomGenerator.WhiteSquareSprite;
        renderer.color = tint;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 8;
        return projectile;
    }

    public void TakeDamage(float value)
    {
        if (defeated) return;
        health -= value;
        if (health <= 0f)
        {
            defeated = true;
            if (session != null) session.OnEnemyDefeated(transform.position);
            Destroy(gameObject);
        }
    }
}
