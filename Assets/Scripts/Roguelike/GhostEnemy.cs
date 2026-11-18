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

    private static Sprite softGlowSprite;

    private static Sprite SoftGlowSprite
    {
        get
        {
            if (softGlowSprite != null) return softGlowSprite;

            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 centre = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 offset = (new Vector2(x, y) - centre) / (size * 0.5f);
                    // Square falloff keeps the projectile glow visibly pixel-shaped.
                    float distance = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y));
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f) * 0.72f;
                    pixels[y * size + x] = new Color(0.36f, 0.76f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            softGlowSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            return softGlowSprite;
        }
    }

    private void Shoot(Vector2 direction)
    {
        GameObject bolt = new GameObject("Призрачный снаряд");
        bolt.transform.SetParent(transform.parent, true);
        bolt.transform.position = transform.position;

        SpriteRenderer renderer = bolt.AddComponent<SpriteRenderer>();
        renderer.sprite = RoomGenerator.WhiteSquareSprite;
        renderer.color = new Color(0.84f, 0.96f, 1f, 1f);
        renderer.sortingOrder = 7;
        bolt.transform.localScale = Vector3.one * 0.12f;

        GameObject glow = new GameObject("Мягкое голубое свечение");
        glow.transform.SetParent(bolt.transform, false);
        SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = SoftGlowSprite;
        glowRenderer.color = new Color(0.38f, 0.78f, 1f, 0.78f);
        glowRenderer.sortingOrder = 6;
        glow.transform.localScale = Vector3.one * 3f;

        ParticleSystem trail = bolt.AddComponent<ParticleSystem>();
        var main = trail.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 0.28f;
        main.startSpeed = 0.16f;
        main.startSize = 0.04f;
        main.startColor = new Color(0.55f, 0.86f, 1f, 0.92f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 36;

        var emission = trail.emission;
        emission.rateOverTime = 24f;

        var shape = trail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.018f;
        shape.rotation = new Vector3(0f, 0f,
            Vector2.SignedAngle(Vector2.up, -direction));

        var colorOverLifetime = trail.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.86f, 0.97f, 1f), 0f),
                new GradientColorKey(new Color(0.24f, 0.65f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.92f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer trailRenderer = trail.GetComponent<ParticleSystemRenderer>();
        if (trailRenderer != null) trailRenderer.sortingOrder = 5;

        BoxCollider2D collider = bolt.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.22f, 0.22f);
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
