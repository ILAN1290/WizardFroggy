using UnityEngine;

/// <summary>
/// Lightweight projectile for roguelike ghosts with a short-lived square comet trail.
/// </summary>
public class RoguelikeProjectile : MonoBehaviour
{
    private static Sprite glowSprite;

    private Vector2 direction;
    private Color trailTint = Color.white;
    private float nextTrailTime;
    private float speed;
    private float damage;
    private float life;
    private Rigidbody2D body;
    private bool initialized;
    private bool reflected;
    private bool towerOwned;
    private float towerSplashRadius;

    public void Initialize(Vector2 targetDirection, float projectileSpeed, float projectileDamage)
    {
        Initialize(targetDirection, projectileSpeed, projectileDamage,
            new Color(0.72f, 0.92f, 1f, 1f));
    }

    public void Initialize(Vector2 targetDirection, float projectileSpeed,
        float projectileDamage, Color tint)
    {
        Initialize(targetDirection, projectileSpeed, projectileDamage, tint, false);
    }

    public void Initialize(Vector2 targetDirection, float projectileSpeed,
        float projectileDamage, Color tint, bool whiteCore)
    {
        direction = targetDirection.sqrMagnitude > 0.001f
            ? targetDirection.normalized : Vector2.up;
        speed = Mathf.Max(0f, projectileSpeed);
        damage = Mathf.Max(0f, projectileDamage);
        trailTint = tint;
        reflected = false;
        towerOwned = false;
        towerSplashRadius = 0f;
        nextTrailTime = Time.time;
        body = GetComponent<Rigidbody2D>();

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();
        if (renderer.sprite == null)
            renderer.sprite = RoomGenerator.WhiteSquareSprite;

        // Mini-boss bolts keep a white core while the glow and trail retain
        // the red tint, matching the bright comet-like projectile style.
        renderer.color = whiteCore ? Color.white : tint;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 8;

        CreateGlow(tint);
        initialized = true;
    }

    private void CreateGlow(Color tint)
    {
        GameObject glow = new GameObject("Свечение снаряда");
        glow.transform.SetParent(transform, false);

        SpriteRenderer renderer = glow.AddComponent<SpriteRenderer>();
        renderer.sprite = GlowSprite;
        renderer.color = new Color(tint.r, tint.g, tint.b, 0.42f);
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 7;
        glow.transform.localScale = Vector3.one * 2.6f;
    }

    /// <summary>
    /// Initializes the same comet projectile visual for a Tower Defence tower.
    /// Tower-owned bolts can only damage TowerDefenceGhost instances.
    /// </summary>
    public void InitializeTowerDefence(
        Vector2 targetDirection,
        float projectileSpeed,
        float projectileDamage,
        Color tint,
        float splashRadius)
    {
        Initialize(targetDirection, projectileSpeed, projectileDamage, tint);
        towerOwned = true;
        towerSplashRadius = Mathf.Max(0f, splashRadius);
    }

    /// <summary>
    /// Turns an incoming ghost bolt back toward the ghost it came from.
    /// </summary>
    public void Reflect(Vector2 reflectDirection, float force)
    {
        if (!initialized || reflected)
            return;

        if (reflectDirection.sqrMagnitude <= 0.0001f)
            reflectDirection = Vector2.up;

        direction = reflectDirection.normalized;
        reflected = true;

        // A reflected bolt travels faster than the original shot.
        speed = Mathf.Max(speed, 7.5f + Mathf.Clamp(force / 1000f, 0f, 2f));
        trailTint = new Color(0.72f, 1f, 0.78f, 1f);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.color = trailTint;

        Transform glow = transform.Find("Свечение снаряда");
        SpriteRenderer glowRenderer = glow != null
            ? glow.GetComponent<SpriteRenderer>() : null;
        if (glowRenderer != null)
            glowRenderer.color = new Color(trailTint.r, trailTint.g, trailTint.b, 0.48f);

        // Reset the short lifetime so the returned bolt has enough time
        // to travel back through the room.
        life = 0f;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        Vector2 nextPosition = (Vector2)transform.position +
            direction * speed * Time.fixedDeltaTime;

        if (body != null)
            body.MovePosition(nextPosition);
        else
            transform.position = nextPosition;

        life += Time.fixedDeltaTime;
        if (Time.time >= nextTrailTime)
        {
            EmitCometTrail();
            nextTrailTime = Time.time + 0.045f;
        }

        if (life >= 5f)
            Destroy(gameObject);
    }

    private void EmitCometTrail()
    {
        Sprite squareSprite = RoomGenerator.WhiteSquareSprite;
        if (squareSprite == null) return;

        int count = Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            GameObject square = new GameObject("Частица кометного следа");
            Transform parent = transform.parent;
            if (parent != null) square.transform.SetParent(parent, true);

            square.transform.position = transform.position -
                (Vector3)(direction * Random.Range(0.05f, 0.13f)) +
                (Vector3)(Random.insideUnitCircle * 0.045f);
            square.transform.rotation = Quaternion.Euler(
                0f, 0f, Random.Range(0f, 360f));

            SpriteRenderer renderer = square.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            float brightness = Random.Range(0.78f, 1.18f);
            renderer.color = new Color(
                Mathf.Clamp01(trailTint.r * brightness + 0.05f),
                Mathf.Clamp01(trailTint.g * brightness + 0.05f),
                Mathf.Clamp01(trailTint.b * brightness + 0.05f),
                Random.Range(0.28f, 0.52f));
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 7;

            float size = Random.Range(0.035f, 0.075f);
            square.transform.localScale = Vector3.one * size;

            RoguelikeCometParticle particle =
                square.AddComponent<RoguelikeCometParticle>();
            Vector2 drift = -direction * Random.Range(0.3f, 0.9f) +
                Random.insideUnitCircle * 0.7f;
            particle.Initialize(renderer.color, drift, Random.Range(0.28f, 0.48f));
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // After reflection, ghost projectiles become player-owned:
        // they can damage ghosts but must never hurt the player again.
        if (reflected)
        {
            GhostEnemy ghost = other.GetComponentInParent<GhostEnemy>();
            if (ghost == null) return;

            ghost.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Tower bolts reuse the roguelike comet effect, but they never
        // collide with or damage the player and only affect TDS ghosts.
        if (towerOwned)
        {
            TowerDefenceGhost towerGhost =
                other.GetComponentInParent<TowerDefenceGhost>();
            if (towerGhost != null)
            {
                TowerDefenceManager manager = TowerDefenceManager.Instance;
                if (manager != null)
                    manager.ApplyTowerProjectileDamage(
                        towerGhost, transform.position, damage, towerSplashRadius);
                else
                    towerGhost.TakeDamage(damage);

                Destroy(gameObject);
            }
            return;
        }

        // Let the Parry component handle its own child trigger.
        Parry parryHitbox = other.GetComponent<Parry>();
        if (parryHitbox != null)
        {
            if (parryHitbox.IsActive)
                Reflect((Vector2)transform.position - (Vector2)parryHitbox.transform.position, 1000f);
            return;
        }

        // The player body collider may enter on the same physics step as
        // the parry trigger. Check the active hitbox here as a fallback.
        PlayerController playerController = other.GetComponentInParent<PlayerController>();
        if (playerController != null)
        {
            Parry activeParry = playerController.GetComponentInChildren<Parry>(true);
            if (activeParry != null && activeParry.IsActive)
            {
                Reflect((Vector2)transform.position - (Vector2)activeParry.transform.position, 1000f);
                return;
            }
        }

        // The Parry tag is also used by the regular Projectile component.
        if (other.CompareTag("Parry"))
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;

        health.TakeDamage(damage);
        Destroy(gameObject);
    }

    private static Sprite GlowSprite
    {
        get
        {
            if (glowSprite != null) return glowSprite;

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
                    float distance = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y));
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f) * 0.7f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            return glowSprite;
        }
    }
}

/// <summary>
/// Fades and disperses one small square from a ghost projectile's comet trail.
/// </summary>
public sealed class RoguelikeCometParticle : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color startColor;
    private Vector2 drift;
    private float lifetime;
    private float age;
    private float startSize;

    public void Initialize(Color tint, Vector2 velocity, float duration)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        startColor = tint;
        drift = velocity;
        lifetime = Mathf.Max(0.05f, duration);
        startSize = transform.localScale.x;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / lifetime);
        transform.position += (Vector3)(drift * Time.deltaTime);
        transform.Rotate(0f, 0f, 90f * Time.deltaTime);

        if (spriteRenderer != null)
        {
            Color color = startColor;
            color.a *= 1f - progress;
            spriteRenderer.color = color;
        }

        float size = startSize * Mathf.Lerp(1f, 0.12f, progress);
        transform.localScale = Vector3.one * size;

        if (age >= lifetime)
            Destroy(gameObject);
    }
}
