using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossHead { Mops, Chih, Bul }

// Ядро босса: здоровье + фазы анимаций + урон телом (бывшие EnemyHealth и Enemy),
// границы арены, кэш игрока, общие функции для голов.
public class Boss : MonoBehaviour
{
    public static Boss Instance;

    [Header("Здоровье")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Animator mopsAnimator;
    [SerializeField] private Animator chihAnimator;
    [SerializeField] private Animator bulAnimator;

    [Header("Урон телом")]
    [SerializeField] private float damageByBody = 10f;
    [SerializeField] private AudioClip clashSound;

    [Header("Головы")]
    public WindAttacks mops;
    public FireAttacks chih;
    public PortalAttacks bul;

    [Header("Арена (мировые координаты)")]
    [SerializeField] private Vector2 arenaCenter = Vector2.zero;
    [SerializeField] private Vector2 arenaSize = new Vector2(18f, 10f);

    [Header("Какие головы атакуют")]
    public bool mopsAttacksEnabled = true;
    public bool chihAttacksEnabled = true;
    public bool bulAttacksEnabled = true;

    private float currentHealth;
    private int invincibleLayer = -1;

    public Transform Player { get; private set; }
    public Rigidbody2D PlayerRb { get; private set; }
    public PlayerHealth PlayerHp { get; private set; }
    public Vector2 Center { get { return arenaCenter; } }
    public Vector2 ArenaSize { get { return arenaSize; } }
    public Vector2 PlayerPos { get { return PlayerRb != null ? PlayerRb.position : (Vector2)Player.position; } }
    public float CurrentHealth { get { return Mathf.Max(0f, currentHealth); } }

    private void Awake()
    {
        Instance = this;
        currentHealth = maxHealth;
        invincibleLayer = LayerMask.NameToLayer("Invincibility");
        CachePlayer();
        if (mops != null) mops.boss = this;
        if (chih != null) chih.boss = this;
        if (bul != null) bul.boss = this;

        // Keep the boss above floor fire, ground decals and other arena effects.
        bossRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        ApplyBossRenderOrder();
    }

    private void LateUpdate()
    {
        // Animators may write sprite sorting values during Update, so restore the
        // foreground order after animation has evaluated for this frame.
        ApplyBossRenderOrder();
    }

    private void ApplyBossRenderOrder()
    {
        if (bossRenderers == null) return;
        foreach (SpriteRenderer spriteRenderer in bossRenderers)
        {
            if (spriteRenderer == null) continue;
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = Mathf.Max(50, spriteRenderer.sortingOrder);
        }
    }

    // ---------- здоровье ----------
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Sfx(damageSound, 0.6f);
        HealthStagesAnimation(currentHealth);
        StartCoroutine(PainAnimation());
        if (currentHealth <= 0f) Die();
    }

    private IEnumerator PainAnimation()
    {
        if (mopsAnimator != null) mopsAnimator.SetBool("Damage", true);
        if (chihAnimator != null) chihAnimator.SetBool("Damage", true);
        if (bulAnimator != null) bulAnimator.SetBool("Damage", true);
        yield return new WaitForSeconds(0.3f);
        if (mopsAnimator != null) mopsAnimator.SetBool("Damage", false);
        if (chihAnimator != null) chihAnimator.SetBool("Damage", false);
        if (bulAnimator != null) bulAnimator.SetBool("Damage", false);
    }

    private void HealthStagesAnimation(float hp)
    {
        if (hp >= 40f && hp < 70f && mopsAnimator != null && !mopsAnimator.GetBool("70hp"))
            mopsAnimator.SetBool("70hp", true);
        else if (hp >= 15f && hp < 40f && bulAnimator != null && !bulAnimator.GetBool("40hp"))
            bulAnimator.SetBool("40hp", true);
        else if (hp < 15f && chihAnimator != null && !chihAnimator.GetBool("15hp"))
            chihAnimator.SetBool("15hp", true);
    }

    private void Die()
    {
        if (gameManager != null && !gameManager.isGoodEndingActive && !gameManager.isBadEndingActive)
        {
            gameManager.GameOver();
            gameManager.StartGoodEnding();
        }
    }

    // ---------- урон телом ----------
    private void OnCollisionEnter2D(Collision2D other)
    {
        PlayerHealth ph = other.gameObject.GetComponent<PlayerHealth>();
        if (ph != null && ph.gameObject.layer != invincibleLayer)
        {
            Sfx(clashSound, 0.5f);
            ph.TakeDamage(damageByBody);
        }
    }

    // ---------- игрок ----------
    public void CachePlayer()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;
        Player = go.transform;
        PlayerRb = go.GetComponent<Rigidbody2D>();
        PlayerHp = go.GetComponent<PlayerHealth>();
        invincibleLayer = LayerMask.NameToLayer("Invincibility");
    }

    public BossAttack Head(BossHead head)
    {
        switch (head)
        {
            case BossHead.Mops: return mops;
            case BossHead.Chih: return chih;
            default: return bul;
        }
    }

    public bool CanDamage()
    {
        return Player != null && PlayerHp != null && Player.gameObject.layer != invincibleLayer;
    }

    public void Hit(float damage) { if (CanDamage()) PlayerHp.TakeDamage(damage); }
    public void Burn(float damage, float time, string color = "Red") { if (CanDamage()) PlayerHp.StartBurning(damage, time, color); }

    // ---------- физика ----------
    public void Push(Vector2 dir, float accel) // юнит/с^2
    {
        if (PlayerRb != null) PlayerRb.AddForce(dir * accel * PlayerRb.mass * Time.deltaTime);
    }

    public void Impulse(Vector2 dir, float speed) // юнит/с
    {
        if (PlayerRb != null) PlayerRb.AddForce(dir * speed * PlayerRb.mass, ForceMode2D.Impulse);
    }

    // ---------- звук / взрыв ----------
    public void Sfx(AudioClip clip, float volume = 0.3f, float duration = 0f)
    {
        if (clip == null || AudioManager.instance == null) return;
        if (duration > 0f) AudioManager.instance.PlaySoundFXClip(clip, transform, volume, duration);
        else AudioManager.instance.PlaySoundFXClip(clip, transform, volume);
    }

    public void Boom(Vector2 pos, float radius, Color color)
    {
        ExplosionEffect.Spawn(pos, radius, color);
    }

    // ---------- точки арены ----------
    public Vector2 RandomPoint(float margin = 1f)
    {
        Vector2 h = Half(margin);
        return arenaCenter + new Vector2(Random.Range(-h.x, h.x), Random.Range(-h.y, h.y));
    }

    public Vector2 RandomEdgePoint(float margin = 1f)
    {
        Vector2 h = Half(margin);
        switch (Random.Range(0, 4))
        {
            case 0: return arenaCenter + new Vector2(Random.Range(-h.x, h.x), h.y);
            case 1: return arenaCenter + new Vector2(Random.Range(-h.x, h.x), -h.y);
            case 2: return arenaCenter + new Vector2(h.x, Random.Range(-h.y, h.y));
            default: return arenaCenter + new Vector2(-h.x, Random.Range(-h.y, h.y));
        }
    }
    public Vector2 ClampToArena(Vector2 p, float margin = 0.5f)
    {
        Vector2 h = Half(margin);
        return new Vector2(
            Mathf.Clamp(p.x, arenaCenter.x - h.x, arenaCenter.x + h.x),
            Mathf.Clamp(p.y, arenaCenter.y - h.y, arenaCenter.y + h.y));
    }

    private Vector2 Half(float margin)
    {
        Vector2 h = arenaSize * 0.5f - Vector2.one * margin;
        return new Vector2(Mathf.Max(h.x, 0.1f), Mathf.Max(h.y, 0.1f));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(arenaCenter, new Vector3(arenaSize.x, arenaSize.y, 0f));
    }
}

// База для трёх голов.
public abstract class BossAttack : MonoBehaviour
{
    public const int AttackCount = 3;

    public Boss boss;
    public GameObject fireAboveHead;
    [SerializeField, Min(0.1f)] private float attackDuration = 20f;

    [HideInInspector] public bool isRunning;
    [HideInInspector] public int currentAttack = -1;

    protected readonly List<GameObject> fx = new List<GameObject>();

    // для UnityEvent'ов туториала (без параметров)
    public void CastOld() { Cast(0); }

    public void Cast(int index)
    {
        if (isRunning) return;
        if (boss == null) boss = Boss.Instance;
        if (boss == null) { Debug.LogWarning(name + ": не назначен Boss"); return; }
        boss.CachePlayer();
        if (boss.Player == null) { Debug.LogWarning(name + ": игрок с тегом Player не найден"); return; }

        currentAttack = Mathf.Clamp(index, 0, AttackCount - 1);
        isRunning = true;
        if (fireAboveHead != null) fireAboveHead.SetActive(true);
        StartCoroutine(Wrap(currentAttack));
    }

    public void Stop()
    {
        StopAllCoroutines();
        Finish();
    }

    private IEnumerator Wrap(int index)
    {
        // All three boss heads share one fixed attack window. The attack logic may
        // finish early, but its controller remains active until the common deadline.
        bool bodyFinished = false;
        Coroutine body = StartCoroutine(RunAttack(index, () => bodyFinished = true));
        yield return new WaitForSeconds(Mathf.Max(0.1f, attackDuration));

        if (!bodyFinished && body != null)
            StopCoroutine(body);
        Finish();
    }

    private IEnumerator RunAttack(int index, System.Action onFinished)
    {
        yield return Attack(index);
        onFinished?.Invoke();
    }

    private void Finish()
    {
        OnForceStop();
        isRunning = false;
        currentAttack = -1;
        Cleanup();
        if (fireAboveHead != null) fireAboveHead.SetActive(false);
        for (int i = 0; i < fx.Count; i++) if (fx[i] != null) Destroy(fx[i]);
        fx.Clear();
    }

    protected abstract IEnumerator Attack(int index);
    protected virtual void Cleanup() { }
    protected virtual void OnForceStop() { }

    protected LineRenderer NewRing(Color color, float width = 0.12f, bool worldSpace = true)
    {
        LineRenderer lr = BossFx.CreateLine(color, width, worldSpace);
        fx.Add(lr.gameObject);
        return lr;
    }

    protected void Track(GameObject go) { if (go != null) fx.Add(go); }

    protected void Drop(GameObject go)
    {
        if (go != null) { fx.Remove(go); Destroy(go); }
    }

    protected virtual void OnDisable() { if (isRunning) Stop(); }
}