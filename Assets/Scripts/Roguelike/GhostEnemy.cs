using UnityEngine;

public class GhostEnemy : MonoBehaviour, IDamageable
{
    private RoguelikeSession session;
    private Transform player;
    private Rigidbody2D body;
    private PlayerHealth playerHealth;
    private float health;
    private float damage;
    private bool melee;
    private float nextAttackTime;
    private bool defeated;

    public void Initialize(RoguelikeSession owner, int floor, bool isMelee)
    {
        session = owner;
        melee = isMelee;
        health = 22f + floor * 8f;
        damage = 8f + floor * 2.5f;
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (defeated || player == null) return;

        Vector2 toPlayer = (Vector2)player.position - body.position;
        float distance = toPlayer.magnitude;
        float moveSpeed = melee ? 2.4f : 1.7f;

        if (melee)
        {
            if (distance > 1.15f)
                body.MovePosition(body.position + toPlayer.normalized * moveSpeed * Time.deltaTime);
            else if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + 1.15f;
                if (playerHealth != null) playerHealth.TakeDamage(damage);
            }
        }
        else
        {
            if (distance > 4.2f)
                body.MovePosition(body.position + toPlayer.normalized * moveSpeed * Time.deltaTime);
            else if (distance < 2.7f)
                body.MovePosition(body.position - toPlayer.normalized * moveSpeed * Time.deltaTime);
            else if (distance < 8.5f && Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + 1.8f;
                Shoot(toPlayer.normalized);
            }
        }
    }

    private void Shoot(Vector2 direction)
    {
        GameObject bolt = new GameObject("Призрачный снаряд");
        bolt.transform.position = transform.position;
        SpriteRenderer renderer = bolt.AddComponent<SpriteRenderer>();
        renderer.sprite = RoomGenerator.CircleSprite;
        renderer.color = new Color(0.72f, 0.56f, 1f);
        renderer.sortingOrder = 5;

        CircleCollider2D collider = bolt.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        Rigidbody2D projectileBody = bolt.AddComponent<Rigidbody2D>();
        projectileBody.bodyType = RigidbodyType2D.Kinematic;
        projectileBody.gravityScale = 0f;
        bolt.AddComponent<RoguelikeProjectile>().Initialize(direction, 5.4f, damage);
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
