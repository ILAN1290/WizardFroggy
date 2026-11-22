using UnityEngine;

/// <summary>
/// Lightweight projectile for roguelike ghosts.
/// The glow sprite is shared; no temporary trail GameObjects are spawned.
/// </summary>
public class RoguelikeProjectile : MonoBehaviour
{
    private static Sprite glowSprite;

    private Vector2 direction;
    private float speed;
    private float damage;
    private float life;
    private Rigidbody2D body;
    private bool initialized;

    public void Initialize(Vector2 targetDirection, float projectileSpeed, float projectileDamage)
    {
        Initialize(targetDirection, projectileSpeed, projectileDamage,
            new Color(0.72f, 0.92f, 1f, 1f));
    }

    public void Initialize(Vector2 targetDirection, float projectileSpeed,
        float projectileDamage, Color tint)
    {
        direction = targetDirection.sqrMagnitude > 0.001f
            ? targetDirection.normalized : Vector2.up;
        speed = Mathf.Max(0f, projectileSpeed);
        damage = Mathf.Max(0f, projectileDamage);
        body = GetComponent<Rigidbody2D>();

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();
        if (renderer.sprite == null)
            renderer.sprite = RoomGenerator.WhiteSquareSprite;

        renderer.color = tint;
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
        if (life >= 5f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

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
