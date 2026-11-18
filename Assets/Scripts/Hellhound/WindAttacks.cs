using System.Collections;
using UnityEngine;

// Mops: 0 — порывы, 1 — торнадо (тянет, без урона), 2 — ударная волна (стан + отброс ОТ эпицентра).
public class WindAttacks : BossAttack
{
    [Header("Атака 0 — Порывы")]
    public GameObject wind;
    [SerializeField] private float windForce = 5500f;
    [SerializeField, Range(0f, 0.1f)] private float playerForceScale = 0.01f;
    [SerializeField] private float windDuration = 20f;
    [SerializeField] private float directionChangeInterval = 3f;
    [SerializeField] private float rechargeTime = 1f;
    [SerializeField] private AudioClip windSound;

    [Header("Атака 1 — Торнадо")]
    [SerializeField] private GameObject tornadoVisual;      // свой спрайт, можно пусто
    [SerializeField] private bool placeholderIfEmpty = true; // заглушка, пока спрайта нет
    [SerializeField] private float visualSpin = 240f;
    [SerializeField] private Color placeholderColor = new Color(0.7f, 0.92f, 1f, 0.7f);
    [SerializeField] private float vortexDuration = 14f;
    [SerializeField] private float vortexRadius = 5f;
    [SerializeField] private float pullForce = 5500f;
    [SerializeField] private float swirlFactor = 0.35f;
    [SerializeField] private float falloffPower = 0.75f;
    [SerializeField] private float deadZone = 0.2f;
    [SerializeField] private float vortexMoveSpeed = 2.5f;

    [Header("Атака 2 — Ударная волна")]
    [SerializeField] private Transform waveSource;
    [SerializeField] private int waveCount = 4;
    [SerializeField] private float waveTelegraph = 0.9f;
    [SerializeField] private float waveSpeed = 8f;
    [SerializeField] private float waveMaxRadius = 14f;
    [SerializeField] private float waveThickness = 0.7f;
    [SerializeField] private float wavePause = 1.1f;
    [SerializeField] private AudioClip waveSound;
    [SerializeField] private bool hitFlash = true;
    [SerializeField] private bool respectInvincibility = true; // дэш уклоняется от волны

    [Header("Отброс и стан")]
    [SerializeField] private float minKnockback = 1.5f;
    [SerializeField] private float maxKnockback = 6f;
    [SerializeField] private float knockbackTime = 0.22f;
    [SerializeField] private float minStun = 0.35f;
    [SerializeField] private float maxStun = 1.1f;
    [SerializeField] private bool clampToArena = true;
    [SerializeField] private float arenaMargin = 0.6f;
    [SerializeField] private bool freezeController = true;   // гасим PlayerController на время стана
    [SerializeField] private bool switchActionMap = false;
    [SerializeField] private string stunActionMap = "Cutscene";

    private bool mapSwitched;
    private readonly Vector2[] windDirections = { Vector2.down, Vector2.left, Vector2.right, Vector2.up };
    private int currentDirectionIndex;
    private Animator windAnimator;

    private Coroutine stunRoutine;
    private bool stunned;
    private string savedActionMap = "Player";
    private PlayerController disabledController;

    private void Awake()
    {
        currentDirectionIndex = Random.Range(0, windDirections.Length);
        if (wind != null) windAnimator = wind.GetComponent<Animator>();
    }

    protected override IEnumerator Attack(int index)
    {
        switch (index)
        {
            case 0: yield return Gust(); break;
            case 1: yield return Vortex(); break;
            default: yield return Shockwave(); break;
        }
    }

    protected override void Cleanup()
    {
        if (stunRoutine != null) { StopCoroutine(stunRoutine); stunRoutine = null; }
        EndStun();
        if (tornadoVisual != null) tornadoVisual.SetActive(false);
    }

    // ================= 0: порывы =================
    private IEnumerator Gust()
    {
        float end = Time.time + windDuration;
        while (Time.time < end)
        {
            yield return Blow(end);
            float wait = rechargeTime;
            while (wait > 0f && Time.time < end)
            {
                wait -= Time.deltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator Blow(float end)
    {
        int newIndex;
        do { newIndex = Random.Range(0, windDirections.Length); }
        while (newIndex == currentDirectionIndex);
        currentDirectionIndex = newIndex;

        SetWindAnimation(currentDirectionIndex);
        boss.Sfx(windSound, 0.1f, directionChangeInterval);

        Vector2 dir = windDirections[currentDirectionIndex];
        float t = 0f;
        while (t < directionChangeInterval && Time.time < end)
        {
            if (boss.Player != null)
            {
                PlayerController controller = boss.Player.GetComponent<PlayerController>();
                Vector2 force = dir * windForce * playerForceScale;

                if (controller != null)
                    controller.AddExternalForce(force);
                else if (boss.PlayerRb != null)
                    boss.PlayerRb.AddForce(force);
            }
            t += Time.deltaTime;
            yield return null;
        }
        SetWindAnimation(currentDirectionIndex);
    }

    private void SetWindAnimation(int index)
    {
        if (windAnimator == null) return;
        switch (index)
        {
            case 0: windAnimator.SetTrigger("Down"); break;
            case 1: windAnimator.SetTrigger("Left"); break;
            case 2: windAnimator.SetTrigger("Right"); break;
            case 3: windAnimator.SetTrigger("Up"); break;
        }
    }

    // ================= 1: торнадо =================
    private IEnumerator Vortex()
    {
        Vector2 pos = boss.RandomPoint(2f);
        Vector2 target = boss.RandomPoint(2f);
        Rigidbody2D rb = boss.PlayerRb;

        bool usePlaceholder = tornadoVisual == null && placeholderIfEmpty;
        LineRenderer spiral = null, core = null, outer = null;

        if (usePlaceholder)
        {
            spiral = NewRing(placeholderColor, 0.16f, false);
            BossFx.SetSpiral(spiral, vortexRadius * 0.55f, 3f);
            core = NewRing(new Color(1f, 1f, 1f, 0.5f), 0.1f);
            outer = NewRing(new Color(placeholderColor.r, placeholderColor.g, placeholderColor.b, 0.18f), 0.06f);
        }
        else if (tornadoVisual != null)
        {
            tornadoVisual.transform.position = pos;
            tornadoVisual.SetActive(true);
        }

        float end = Time.time + vortexDuration;
        float t = 0f;

        while (Time.time < end)
        {
            t += Time.deltaTime;
            pos = Vector2.MoveTowards(pos, target, vortexMoveSpeed * Time.deltaTime);
            if ((pos - target).sqrMagnitude < 0.05f) target = boss.RandomPoint(2f);

            float pulse = 1f + 0.08f * Mathf.Sin(t * 8f);

            if (usePlaceholder)
            {
                spiral.transform.position = pos;
                spiral.transform.Rotate(0f, 0f, visualSpin * Time.deltaTime);
                spiral.transform.localScale = new Vector3(pulse, pulse, 1f);
                BossFx.SetCircle(core, pos, 0.45f * pulse);
                BossFx.SetCircle(outer, pos, vortexRadius);
            }
            else if (tornadoVisual != null)
            {
                tornadoVisual.transform.position = pos;
                if (visualSpin != 0f) tornadoVisual.transform.Rotate(0f, 0f, visualSpin * Time.deltaTime);
            }

            if (rb != null)
            {
                Vector2 toCore = pos - rb.position;
                float d = toCore.magnitude;
                if (d < vortexRadius && d > deadZone)
                {
                    float k = Mathf.Pow(Mathf.Clamp01(1f - d / vortexRadius), falloffPower);
                    Vector2 n = toCore / d;
                    rb.AddForce(n * pullForce * k * Time.deltaTime);
                    if (swirlFactor > 0f)
                        rb.AddForce(new Vector2(-n.y, n.x) * pullForce * swirlFactor * k * Time.deltaTime);
                }
            }
            yield return null;
        }

        if (tornadoVisual != null) tornadoVisual.SetActive(false);
    }

    // ================= 2: ударная волна =================
    private IEnumerator Shockwave()
    {
        LineRenderer warn = NewRing(new Color(0.6f, 0.85f, 1f, 0.5f), 0.08f);
        LineRenderer wave = NewRing(new Color(0.8f, 0.95f, 1f, 0.9f), 0.35f);
        BossFx.SetAlpha(wave, 0f);

        for (int i = 0; i < Mathf.Max(1, waveCount); i++)
        {
            Vector2 c = waveSource != null ? (Vector2)waveSource.position : boss.Center;

            float t = 0f;
            while (t < waveTelegraph)
            {
                t += Time.deltaTime;
                float k = t / waveTelegraph;
                BossFx.SetCircle(warn, c, Mathf.Lerp(0.6f, 1.6f, k));
                BossFx.SetAlpha(warn, 0.25f + 0.5f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)));
                yield return null;
            }
            BossFx.SetAlpha(warn, 0f);
            boss.Sfx(waveSound, 0.4f);

            float r = 0.6f;
            bool hit = false;
            while (r < waveMaxRadius)
            {
                r += waveSpeed * Time.deltaTime;
                BossFx.SetCircle(wave, c, r);
                BossFx.SetAlpha(wave, Mathf.Clamp01(1f - r / waveMaxRadius));

                float d = Vector2.Distance(boss.PlayerPos, c);
                if (!hit && Mathf.Abs(d - r) <= waveThickness)
                {
                    hit = true;
                    if (!respectInvincibility || boss.CanDamage())
                    {
                        float near = Mathf.Clamp01(1f - d / Mathf.Max(0.01f, waveMaxRadius));
                        float dist = Mathf.Lerp(minKnockback, maxKnockback, near);
                        float stun = Mathf.Lerp(minStun, maxStun, near);
                        if (hitFlash) boss.Boom(boss.PlayerPos, 0.9f, new Color(0.8f, 0.93f, 1f));
                        ApplyStun(c, dist, stun);
                    }
                }
                yield return null;
            }

            BossFx.SetAlpha(wave, 0f);
            yield return new WaitForSeconds(wavePause);
        }

        float guard = 0f;
        while (stunned && guard < 3f)
        {
            guard += Time.deltaTime;
            yield return null;
        }
    }

    // ---------- стан + отброс ОТ эпицентра ----------
    private void ApplyStun(Vector2 epicenter, float distance, float time)
    {
        if (stunRoutine != null) StopCoroutine(stunRoutine);
        stunRoutine = StartCoroutine(StunRoutine(epicenter, distance, time));
    }

    private IEnumerator StunRoutine(Vector2 epicenter, float distance, float time)
    {
        Rigidbody2D rb = boss.PlayerRb;
        if (rb == null) yield break;

        stunned = true;

        if (switchActionMap && !mapSwitched)
        {
            savedActionMap = InputManager.PlayerInput.currentActionMap != null
                ? InputManager.PlayerInput.currentActionMap.name
                : "Player";
            InputManager.PlayerInput.SwitchCurrentActionMap(stunActionMap);
            mapSwitched = true;
        }

        if (freezeController && boss.Player != null)
        {
            PlayerController pc = boss.Player.GetComponent<PlayerController>();
            if (pc != null && pc.enabled)
            {
                pc.enabled = false;
                disabledController = pc;
            }
        }

        Vector2 start = rb.position;
        Vector2 delta = start - epicenter;
        Vector2 dir = delta.sqrMagnitude > 0.0001f
            ? delta.normalized
            : BossFx.Rotate(Vector2.up, Random.Range(0f, 360f));

        Vector2 desired = start + dir * distance;
        Vector2 targetPos = clampToArena ? boss.ClampToArena(desired, arenaMargin) : desired;
        if (Vector2.Dot(targetPos - start, dir) < 0f) targetPos = start;

        float moveTime = Mathf.Max(0.05f, knockbackTime);
        float t = 0f;
        while (t < moveTime)
        {
            rb.linearVelocity = Vector2.zero;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / moveTime), 3f);
            rb.MovePosition(Vector2.Lerp(start, targetPos, k));
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(targetPos);
        yield return new WaitForFixedUpdate();

        float rest = Mathf.Max(0f, time - moveTime);
        float w = 0f;
        while (w < rest)
        {
            rb.linearVelocity = Vector2.zero;
            w += Time.deltaTime;
            yield return null;
        }

        EndStun();
    }

    private void EndStun()
    {
        stunRoutine = null;

        if (disabledController != null)
        {
            disabledController.enabled = true;
            disabledController = null;
        }

        if (mapSwitched)
        {
            mapSwitched = false;
            InputManager.PlayerInput.SwitchCurrentActionMap(string.IsNullOrEmpty(savedActionMap) ? "Player" : savedActionMap);
        }

        stunned = false;
    }
}