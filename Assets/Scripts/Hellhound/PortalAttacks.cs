using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Точка портала для атаки 0: позиция + отражение по горизонтали
[System.Serializable]
public class PortalSpot
{
    public Vector3 position;
    public bool flipX;
}

// Bul: 0 — анимированный портал слева/справа,
//      1 — три случайных портала + веерные залпы,
//      2 — одиночный портал на орбите + спираль.
public class PortalAttacks : BossAttack
{
    [Header("Атака 0 — анимированный портал (Animator)")]
    [SerializeField] private GameObject portal;
    public SpriteRenderer portalSpriteRenderer;
    [SerializeField] private bool restartAnimationOnOpen = true;
    [SerializeField] private bool autoFlipByPosition = true;   // левый зеркалится, правый нет
    [SerializeField] private PortalSpot[] portalSpots;
    [SerializeField] private Vector3[] portalPositions;      // старый список, фолбэк
    [SerializeField] private AudioClip portalSound;
    [SerializeField] private float shootInterval = 0.3f;
    [SerializeField] private float shooterDuration = 20f;
    [SerializeField] private float singleKnifeSpeed = 11f;
    [SerializeField] private AudioClip knifeFlySound;

    [Header("Атаки 1 и 2 — одиночный спрайт портала")]
    [SerializeField] private Sprite orbitPortalSprite;       // просто спрайт, объекты создаются кодом
    [SerializeField] private GameObject orbitPortal;         // или свой сценовый объект, можно пусто
    [SerializeField] private float orbitPortalSize = 1.6f;
    [SerializeField] private float orbitPortalSpin = 180f;   // град/с, вращение кодом
    [SerializeField] private Color orbitPortalTint = Color.white;
    [SerializeField] private int orbitPortalSortingOrder = 15;
    [SerializeField] private Transform orbitCenter;          // пусто = центр арены

    [Header("Снаряды")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float spawnOffset = 0.3f;
    [SerializeField] private float knifeLifetime = 0f;

    [Header("Атака 1 — Три портала + веер")]
    [SerializeField] private float volleyDuration = 14f;
    [SerializeField] private int portalCount = 3;
    [SerializeField] private float volleyTelegraph = 0.9f;
    [SerializeField] private float volleyPause = 1.2f;
    [SerializeField] private int fanBullets = 5;
    [SerializeField] private float fanSpread = 45f;
    [SerializeField] private float volleyKnifeSpeed = 9f;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip volleySound;

    [Header("Линии прицела")]
    [SerializeField] private float aimLineLength = 16f;
    [SerializeField] private float aimLineWidth = 0.035f;
    [Range(0f, 1f)] [SerializeField] private float aimLineAlpha = 0.16f;
    [Range(0f, 1f)] [SerializeField] private float mainLineAlpha = 0.28f;
    [SerializeField] private Color aimLineColor = new Color(1f, 0.55f, 0.95f, 1f);

    [Header("Атака 2 — Облёт + спираль")]
    [SerializeField] private float spiralDuration = 14f;
    [SerializeField] private float orbitRadius = 4.5f;
    [SerializeField] private float orbitSpeed = 70f;
    [SerializeField] private float spiralShootInterval = 0.22f;
    [SerializeField] private int arms = 2;
    [SerializeField] private float spiralStep = 27f;
    [SerializeField] private float spiralKnifeSpeed = 7f;
    [SerializeField] private AudioClip shotSound;

    private readonly List<GameObject> bullets = new List<GameObject>();

    private Vector3 animatedBaseScale = Vector3.one;
    private Animator portalAnimator;

    private GameObject runtimeOrbitPortal;
    private SpriteRenderer runtimeOrbitSr;
    private Vector3 orbitBaseScale = Vector3.one;

    private Vector2 Center
    {
        get { return orbitCenter != null ? (Vector2)orbitCenter.position : boss.Center; }
    }

    private void Awake()
    {
        if (portal != null)
        {
            animatedBaseScale = portal.transform.localScale;
            portalAnimator = portal.GetComponent<Animator>();
        }
        if (orbitPortal != null) orbitBaseScale = orbitPortal.transform.localScale;
    }

    protected override IEnumerator Attack(int index)
    {
        bullets.RemoveAll(b => b == null);
        switch (index)
        {
            case 0: yield return Single(); break;
            case 1: yield return Volley(); break;
            default: yield return Carousel(); break;
        }
    }

    protected override void Cleanup()
    {
        HideAnimatedPortal();
        HideOrbitPortal();
    }

    protected override void OnForceStop()
    {
        for (int i = 0; i < bullets.Count; i++)
            if (bullets[i] != null) Destroy(bullets[i]);
        bullets.Clear();
    }

    // ---------- портал атаки 0 (анимация на Animator) ----------
    private Transform UseAnimatedPortal(out SpriteRenderer sr)
    {
        sr = null;
        if (portal == null) return null;

        sr = portalSpriteRenderer != null ? portalSpriteRenderer : portal.GetComponent<SpriteRenderer>();
        portal.transform.localScale = animatedBaseScale;
        portal.transform.rotation = Quaternion.identity;
        if (sr != null) { sr.enabled = true; sr.flipX = false; }

        if (restartAnimationOnOpen && portalAnimator != null
            && portalAnimator.isActiveAndEnabled && portalAnimator.runtimeAnimatorController != null)
        {
            portalAnimator.Play(portalAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash, 0, 0f);
        }
        return portal.transform;
    }

    private void HideAnimatedPortal()
    {
        if (portalSpriteRenderer != null)
        {
            portalSpriteRenderer.enabled = false;
            portalSpriteRenderer.flipX = false;
        }
        if (portal != null)
        {
            portal.transform.localScale = animatedBaseScale;
            portal.transform.rotation = Quaternion.identity;
        }
    }

    private void ApplySpot(Transform pt, SpriteRenderer sr, PortalSpot spot)
    {
        pt.position = spot.position;
        pt.rotation = Quaternion.identity;
        pt.localScale = animatedBaseScale;

        bool flip = autoFlipByPosition ? spot.position.x < Center.x : spot.flipX;
        if (sr != null) { sr.enabled = true; sr.flipX = flip; }
    }

    // ---------- спрайт-портал для атак 1 и 2 ----------
    private GameObject MakePortalInstance(Vector2 pos)
    {
        GameObject go = null;

        if (orbitPortal != null)
        {
            go = Instantiate(orbitPortal, pos, Quaternion.identity);
            go.SetActive(true);
            SpriteRenderer s0 = go.GetComponent<SpriteRenderer>();
            if (s0 != null) { s0.enabled = true; s0.color = orbitPortalTint; }
        }
        else if (orbitPortalSprite != null)
        {
            go = new GameObject("VolleyPortal");
            go.transform.position = pos;
            SpriteRenderer s0 = go.AddComponent<SpriteRenderer>();
            s0.sprite = orbitPortalSprite;
            s0.sharedMaterial = BossFx.SpriteMaterial;
            s0.sortingOrder = orbitPortalSortingOrder;
            s0.color = orbitPortalTint;
            float unit = Mathf.Max(0.01f, Mathf.Max(orbitPortalSprite.bounds.size.x, orbitPortalSprite.bounds.size.y));
            go.transform.localScale = Vector3.one * (orbitPortalSize / unit);
        }

        if (go != null) Track(go);
        return go;
    }

    private Transform UseOrbitPortal(out SpriteRenderer sr)
    {
        if (orbitPortal != null)
        {
            sr = orbitPortal.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.enabled = true; sr.color = orbitPortalTint; }
            orbitPortal.transform.localScale = orbitBaseScale;
            orbitPortal.transform.rotation = Quaternion.identity;
            return orbitPortal.transform;
        }

        if (runtimeOrbitPortal == null && orbitPortalSprite != null)
        {
            runtimeOrbitPortal = MakePortalInstance(Center);
            runtimeOrbitSr = runtimeOrbitPortal != null ? runtimeOrbitPortal.GetComponent<SpriteRenderer>() : null;
            if (runtimeOrbitPortal != null) orbitBaseScale = runtimeOrbitPortal.transform.localScale;
        }

        sr = runtimeOrbitSr;
        if (sr != null) { sr.enabled = true; sr.color = orbitPortalTint; }
        if (runtimeOrbitPortal != null)
        {
            runtimeOrbitPortal.transform.localScale = orbitBaseScale;
            runtimeOrbitPortal.transform.rotation = Quaternion.identity;
            return runtimeOrbitPortal.transform;
        }
        return null;
    }

    private void HideOrbitPortal()
    {
        if (runtimeOrbitSr != null) runtimeOrbitSr.enabled = false;
        if (orbitPortal != null)
        {
            SpriteRenderer sr = orbitPortal.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            orbitPortal.transform.localScale = orbitBaseScale;
            orbitPortal.transform.rotation = Quaternion.identity;
        }
    }

    // ---------- выстрел ----------
    private void Shoot(Vector2 pos, Vector2 dir, float speed)
    {
        if (projectilePrefab == null) return;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
        pos += dir * spawnOffset;

        GameObject go = Instantiate(projectilePrefab, pos, Quaternion.FromToRotation(Vector3.down, dir));
        bullets.Add(go);

        Projectile p = go.GetComponent<Projectile>();
        if (p != null) p.projType = ProjectileType.EnemyProjectile;

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = dir * Mathf.Max(0.1f, speed);
            rb.angularVelocity = 0f;
        }

        if (knifeLifetime > 0f) Destroy(go, knifeLifetime);
    }

    private static Color A(Color c, float a) { c.a = a; return c; }

    private float FanOffset(int b, int perFan)
    {
        return perFan == 1 ? 0f : Mathf.Lerp(-fanSpread * 0.5f, fanSpread * 0.5f, b / (float)(perFan - 1));
    }

    private PortalSpot[] ResolveSpots()
    {
        if (portalSpots != null && portalSpots.Length > 0) return portalSpots;

        if (portalPositions != null && portalPositions.Length > 0)
        {
            PortalSpot[] arr = new PortalSpot[portalPositions.Length];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = new PortalSpot { position = portalPositions[i], flipX = portalPositions[i].x > 0f };
            return arr;
        }

        Debug.LogWarning("PortalAttacks: список точек пуст — беру края арены. Заполни Portal Spots.");
        Vector3 left = boss.ClampToArena(new Vector2(boss.Center.x - 999f, boss.Center.y), 1.2f);
        Vector3 right = boss.ClampToArena(new Vector2(boss.Center.x + 999f, boss.Center.y), 1.2f);
        return new[]
        {
            new PortalSpot { position = left, flipX = true },
            new PortalSpot { position = right, flipX = false }
        };
    }

    // ================= 0: слева / справа =================
    private IEnumerator Single()
    {
        if (projectilePrefab == null) { Debug.LogWarning("PortalAttacks: не назначен Projectile Prefab"); yield break; }

        SpriteRenderer sr;
        Transform pt = UseAnimatedPortal(out sr);
        if (pt == null) { Debug.LogWarning("PortalAttacks: не назначен Portal для атаки 0"); yield break; }

        PortalSpot[] spots = ResolveSpots();

        if (portalSound != null && AudioManager.instance != null)
            AudioManager.instance.PlayLoopSound(portalSound, transform, 0.3f, shooterDuration);

        int index = Random.Range(0, spots.Length);
        ApplySpot(pt, sr, spots[index]);

        float end = Time.time + shooterDuration;
        float swapAt = Time.time + shooterDuration * 0.5f;
        float cd = 0f;

        while (Time.time < end)
        {
            if (Time.time >= swapAt && spots.Length > 1)
            {
                int next;
                do { next = Random.Range(0, spots.Length); } while (next == index);
                index = next;
                ApplySpot(pt, sr, spots[index]);
                swapAt = float.MaxValue;
            }

            cd -= Time.deltaTime;
            if (cd <= 0f)
            {
                cd = Mathf.Max(0.05f, shootInterval);
                Vector3 sp = pt.position;
                float angle = sp.x > Center.x ? Random.Range(-93f, 0f) : Random.Range(0f, 93f);
                Shoot(sp, BossFx.Rotate(Vector2.down, angle), singleKnifeSpeed);
                boss.Sfx(knifeFlySound, 0.1f);
            }
            yield return null;
        }

        HideAnimatedPortal();
    }

    // ================= 1: три портала + веер =================
    private IEnumerator Volley()
    {
        if (projectilePrefab == null) { Debug.LogWarning("PortalAttacks: не назначен Projectile Prefab"); yield break; }
        if (orbitPortalSprite == null && orbitPortal == null)
        {
            Debug.LogWarning("PortalAttacks: назначь Orbit Portal Sprite (или Orbit Portal)");
            yield break;
        }

        int count = Mathf.Max(1, portalCount);
        int perFan = Mathf.Max(1, fanBullets);
        float tele = Mathf.Max(0.05f, volleyTelegraph);
        float end = Time.time + volleyDuration;

        while (Time.time < end)
        {
            Vector2[] points = new Vector2[count];
            Vector2[] dirs = new Vector2[count];
            GameObject[] visuals = new GameObject[count];
            Vector3[] baseScales = new Vector3[count];
            LineRenderer[] mains = new LineRenderer[count];
            LineRenderer[][] fans = new LineRenderer[count][];
            Vector2 lockPos = boss.PlayerPos;

            for (int i = 0; i < count; i++)
            {
                points[i] = boss.RandomEdgePoint(1.2f);
                Vector2 delta = lockPos - points[i];
                dirs[i] = delta.sqrMagnitude > 0.01f ? delta.normalized : Vector2.down;

                visuals[i] = MakePortalInstance(points[i]);
                if (visuals[i] != null)
                {
                    baseScales[i] = visuals[i].transform.localScale;
                    visuals[i].transform.localScale = baseScales[i] * 0.2f;
                }

                mains[i] = NewRing(A(aimLineColor, 0f), aimLineWidth);
                fans[i] = new LineRenderer[perFan];
                for (int b = 0; b < perFan; b++)
                    fans[i][b] = NewRing(A(aimLineColor, 0f), aimLineWidth);
            }
            boss.Sfx(openSound, 0.3f);

            float t = 0f;
            while (t < tele)
            {
                float dt = Time.deltaTime;
                t += dt;
                float k = Mathf.Clamp01(t / tele);
                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f));

                for (int i = 0; i < count; i++)
                {
                    if (visuals[i] != null)
                    {
                        visuals[i].transform.localScale = baseScales[i] * Mathf.Lerp(0.2f, 1f, grow);
                        if (orbitPortalSpin != 0f)
                            visuals[i].transform.Rotate(0f, 0f, orbitPortalSpin * dt);
                    }

                    Vector2 o = points[i] + dirs[i] * spawnOffset;
                    BossFx.SetBeam(mains[i], o, o + dirs[i] * aimLineLength);
                    BossFx.SetAlpha(mains[i], mainLineAlpha * k);

                    for (int b = 0; b < perFan; b++)
                    {
                        Vector2 d = BossFx.Rotate(dirs[i], FanOffset(b, perFan));
                        Vector2 s = points[i] + d * spawnOffset;
                        BossFx.SetBeam(fans[i][b], s, s + d * aimLineLength);
                        BossFx.SetAlpha(fans[i][b], aimLineAlpha * k);
                    }
                }
                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                for (int b = 0; b < perFan; b++)
                {
                    Shoot(points[i], BossFx.Rotate(dirs[i], FanOffset(b, perFan)), volleyKnifeSpeed);
                    Drop(fans[i][b].gameObject);
                }
                Drop(mains[i].gameObject);
                if (visuals[i] != null) Drop(visuals[i]);
            }
            boss.Sfx(volleySound, 0.25f);

            yield return new WaitForSeconds(volleyPause);
        }
    }

    // ================= 2: облёт + спираль =================
    private IEnumerator Carousel()
    {
        if (projectilePrefab == null) { Debug.LogWarning("PortalAttacks: не назначен Projectile Prefab"); yield break; }

        SpriteRenderer sr;
        Transform pt = UseOrbitPortal(out sr);
        if (pt == null) { Debug.LogWarning("PortalAttacks: назначь Orbit Portal Sprite или Orbit Portal"); yield break; }

        int armCount = Mathf.Max(1, arms);
        float orbit = Random.Range(0f, 360f);
        float spin = 0f;
        float cd = 0f;
        float end = Time.time + spiralDuration;

        while (Time.time < end)
        {
            float dt = Time.deltaTime;
            orbit += orbitSpeed * dt;
            pt.position = Center + BossFx.Rotate(Vector2.right, orbit) * orbitRadius;
            if (orbitPortalSpin != 0f) pt.Rotate(0f, 0f, orbitPortalSpin * dt);

            cd -= dt;
            if (cd <= 0f)
            {
                cd = Mathf.Max(0.05f, spiralShootInterval);
                spin += spiralStep;
                boss.Sfx(shotSound, 0.12f);

                Vector2 p = pt.position;
                for (int k = 0; k < armCount; k++)
                    Shoot(p, BossFx.Rotate(Vector2.right, spin + orbit + k * (360f / armCount)), spiralKnifeSpeed);
            }
            yield return null;
        }

        HideOrbitPortal();
    }
}