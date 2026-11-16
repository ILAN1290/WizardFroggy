using System.Collections;
using UnityEngine;

// Процедурный визуал взрыва: кольцо + вспышка, размер под радиус бомбы.
// Создаётся кодом, префаб и спрайты не нужны.
public class ExplosionEffect : MonoBehaviour
{
    private const float Duration = 0.35f;
    private static Sprite ringSprite;
    private static Sprite fillSprite;

    private SpriteRenderer ring;
    private SpriteRenderer fill;
    private float radius;

    public static void Spawn(Vector2 center, float radius, Color color)
    {
        GameObject go = new GameObject("ExplosionVfx");
        go.transform.position = center;
        ExplosionEffect fx = go.AddComponent<ExplosionEffect>();
        fx.radius = radius;
        fx.Build(color);
    }

    private void Build(Color color)
    {
        ring = MakeChild("Ring", GetSprite(true), color);
        fill = MakeChild("Fill", GetSprite(false), color);
        StartCoroutine(Animate());
    }

    private SpriteRenderer MakeChild(string n, Sprite s, Color c)
    {
        GameObject child = new GameObject(n);
        child.transform.SetParent(transform, false);
        SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = c;
        sr.sortingOrder = 999;
        return sr;
    }

    private IEnumerator Animate()
    {
        float target = radius * 2f; // спрайт = 1 юнит, масштаб = диаметр
        float t = 0f;
        while (t < Duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Duration);

            float rs = Mathf.Lerp(target * 0.2f, target, Mathf.Sqrt(k));
            ring.transform.localScale = Vector3.one * rs;
            ring.color = WithAlpha(ring.color, 1f - k);

            float fs = Mathf.Lerp(target * 0.3f, target * 0.8f, k);
            fill.transform.localScale = Vector3.one * fs;
            fill.color = WithAlpha(fill.color, Mathf.Clamp01(1f - k * 2f));

            yield return null;
        }
        Destroy(gameObject);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    private static Sprite GetSprite(bool ring)
    {
        if (ring)
        {
            if (ringSprite == null) ringSprite = MakeCircle(true);
            return ringSprite;
        }
        if (fillSprite == null) fillSprite = MakeCircle(false);
        return fillSprite;
    }

    private static Sprite MakeCircle(bool ring)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 c = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        float maxR = size / 2f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
            float a = ring
                ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.15f)
                : Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        // pixelsPerUnit = size => спрайт ровно 1 юнит в мире
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}