using System.Collections.Generic;
using UnityEngine;

public class RoguelikeProjectile : MonoBehaviour
{
    private sealed class TrailParticle
    {
        public GameObject Object;
        public SpriteRenderer Renderer;
        public Vector2 Velocity;
        public Vector3 StartScale;
        public Color Color;
        public float Age;
        public float Lifetime;
    }

    private static Sprite glowSprite;

    private readonly List<TrailParticle> trailParticles = new List<TrailParticle>();
    private Vector2 direction;
    private float speed;
    private float damage;
    private float life;
    private float trailTimer;
    private Color projectileColor = new Color(0.72f, 0.92f, 1f, 1f);
    private Rigidbody2D body;
    private SpriteRenderer mainRenderer;
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
        speed = projectileSpeed;
        damage = projectileDamage;
        projectileColor = tint;
        body = GetComponent<Rigidbody2D>();
        mainRenderer = GetComponent<SpriteRenderer>();

        if (mainRenderer == null)
            mainRenderer = gameObject.AddComponent<SpriteRenderer>();
        if (mainRenderer.sprite == null)
            mainRenderer.sprite = RoomGenerator.WhiteSquareSprite;

        mainRenderer.color = projectileColor;
        mainRenderer.sortingLayerName = "Default";
        mainRenderer.sortingOrder = 8;

        CreateGlow();
        initialized = true;
    }

    private void CreateGlow()
    {
        GameObject glow = new GameObject("Свечение снаряда");
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = Vector3.zero;

        SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = GlowSprite;
        glowRenderer.color = new Color(projectileColor.r, projectileColor.g,
            projectileColor.b, 0.42f);
        glowRenderer.sortingLayerName = "Default";
        glowRenderer.sortingOrder = 7;
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
        trailTimer -= Time.fixedDeltaTime;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.045f;
            SpawnTrailParticle();
        }

        if (life >= 5f)
            Destroy(gameObject);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = trailParticles.Count - 1; i >= 0; i--)
        {
            TrailParticle particle = trailParticles[i];
            if (particle.Object == null)
            {
                trailParticles.RemoveAt(i);
                continue;
            }

            particle.Age += deltaTime;
            float progress = Mathf.Clamp01(particle.Age / particle.Lifetime);
            particle.Object.transform.position += (Vector3)(particle.Velocity * deltaTime);
            particle.Object.transform.localScale = particle.StartScale * (1f - progress * 0.75f);

            Color faded = particle.Color;
            faded.a *= 1f - progress;
            particle.Renderer.color = faded;

            if (particle.Age >= particle.Lifetime)
            {
                Destroy(particle.Object);
                trailParticles.RemoveAt(i);
            }
        }
    }

    private void SpawnTrailParticle()
    {
        GameObject square = new GameObject("Полупрозрачный квадратный след");
        square.transform.SetParent(transform.parent, true);

        Vector2 side = new Vector2(-direction.y, direction.x);
        float sideOffset = Random.Range(-0.12f, 0.12f);
        square.transform.position = transform.position -
            (Vector3)(direction * Random.Range(0.08f, 0.18f)) +
            (Vector3)(side * sideOffset);

        SpriteRenderer renderer = square.AddComponent<SpriteRenderer>();
        renderer.sprite = RoomGenerator.WhiteSquareSprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 6;

        Color varied = Color.Lerp(projectileColor, Color.white,
            Random.Range(0.08f, 0.32f));
        varied.a = Random.Range(0.42f, 0.72f);
        renderer.color = varied;

        float size = Random.Range(0.055f, 0.105f);
        Vector3 scale = Vector3.one * size;
        square.transform.localScale = scale;

        TrailParticle particle = new TrailParticle
        {
            Object = square,
            Renderer = renderer,
            StartScale = scale,
            Color = varied,
            Velocity = -direction * Random.Range(0.45f, 1.1f) +
                side * Random.Range(-0.65f, 0.65f),
            Lifetime = Random.Range(0.22f, 0.42f)
        };
        trailParticles.Add(particle);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;

        health.TakeDamage(damage);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < trailParticles.Count; i++)
            if (trailParticles[i].Object != null)
                Destroy(trailParticles[i].Object);
        trailParticles.Clear();
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
