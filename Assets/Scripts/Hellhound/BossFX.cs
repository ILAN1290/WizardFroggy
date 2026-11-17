using UnityEngine;

// Процедурная графика: линии, кольца, спирали, лучи + спрайты клеток и искр.
public static class BossFx
{
    private static Material lineMaterial;
    private static Material spriteMaterial;
    private static Sprite cellFillSprite;
    private static Sprite cellOutlineSprite;
    private static Sprite dotSprite;

    private static Shader FxShader
    {
        get
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            return sh;
        }
    }

    private static Material LineMat
    {
        get
        {
            if (lineMaterial == null) lineMaterial = new Material(FxShader);
            return lineMaterial;
        }
    }

    public static Material SpriteMaterial
    {
        get
        {
            if (spriteMaterial == null) spriteMaterial = new Material(FxShader);
            return spriteMaterial;
        }
    }

    // ---------- линии ----------
    public static LineRenderer CreateLine(Color color, float width, bool worldSpace = true, int sortingOrder = 990)
    {
        GameObject go = new GameObject("BossFxLine");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = LineMat;
        lr.useWorldSpace = worldSpace;
        lr.widthMultiplier = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.sortingOrder = sortingOrder;
        lr.positionCount = 0;
        return lr;
    }

    public static void SetAlpha(LineRenderer lr, float a)
    {
        if (lr == null) return;
        Color c = lr.startColor; c.a = a;
        lr.startColor = c; lr.endColor = c;
    }

    public static void SetCircle(LineRenderer lr, Vector2 center, float radius, int segments = 48)
    {
        if (lr == null) return;
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            lr.SetPosition(i, lr.useWorldSpace ? center + p : (Vector3)p);
        }
    }

    public static void SetSpiral(LineRenderer lr, float radius, float turns, int segments = 120)
    {
        if (lr == null) return;
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float k = i / (float)segments;
            float a = k * turns * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * k);
        }
    }

    public static void SetBeam(LineRenderer lr, Vector2 from, Vector2 to)
    {
        if (lr == null) return;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
    }

    public static Vector2 Rotate(Vector2 v, float degrees)
    {
        return Quaternion.Euler(0f, 0f, degrees) * v;
    }

    // ---------- спрайты (ровно 1 юнит при масштабе 1) ----------
    public static Sprite GetCellSprite(bool outline)
    {
        if (outline)
        {
            if (cellOutlineSprite == null) cellOutlineSprite = MakeCell(true);
            return cellOutlineSprite;
        }
        if (cellFillSprite == null) cellFillSprite = MakeCell(false);
        return cellFillSprite;
    }

    public static Sprite GetDotSprite()
    {
        if (dotSprite == null) dotSprite = MakeDot();
        return dotSprite;
    }

    private static Sprite shadowSprite;

    public static Sprite GetShadowSprite()
    {
        if (shadowSprite == null) shadowSprite = MakeShadow();
        return shadowSprite;
    }

    private static Sprite MakeShadow()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 c = new Vector2((size - 1) / 2f, (size - 1) / 2f);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f);
                float a = d < 0.6f ? 1f : Mathf.Clamp01(1f - (d - 0.6f) / 0.4f);
                a = Mathf.SmoothStep(0f, 1f, a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite MakeCell(bool outline)
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x, size - 1 - x) / (size * 0.5f);
                float dy = Mathf.Min(y, size - 1 - y) / (size * 0.5f);
                float d = Mathf.Min(dx, dy);
                float a = outline
                    ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.18f) / 0.16f)
                    : 0.18f + 0.82f * Mathf.Clamp01(d * 2.4f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite MakeDot()
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 c = new Vector2((size - 1) / 2f, (size - 1) / 2f);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}