using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Поведение босса: волны, фазы по HP, анимации ярости. Головы включаются галками в компоненте Boss.
public class BossBehaviourManager : MonoBehaviour
{
    [System.Serializable]
    public class Phase
    {
        public string name = "Фаза";
        public float hpFrom = 0f;
        [Range(0f, 1f)] public float oldAttackChance = 0.5f;
        [Range(1, 3)] public int headsAtOnce = 2;
        public float pauseBetweenWaves = 1.5f;
        public float waveTimeout = 26f;
    }

    [SerializeField] private Boss boss;
    [SerializeField] private List<Phase> phases = new List<Phase>();
    [SerializeField] private bool autoStart;

    [Header("Анимации ярости")]
    [SerializeField] private Animator additionalEffectsAnimator;
    [SerializeField] private bool driveRageAnimation = true;

    public UnityEvent onWaveStart;
    public UnityEvent onWaveEnd;

    private Coroutine loop;
    private readonly int[] lastAttack = new int[3] { -1, -1, -1 };
    private int previousMask = -1;
    private readonly Phase fallback = new Phase();

    private void Reset()
    {
        phases = new List<Phase>
        {
            new Phase { name = "Обычная", hpFrom = 70f, oldAttackChance = 0.6f,  headsAtOnce = 2, pauseBetweenWaves = 1.8f, waveTimeout = 26f },
            new Phase { name = "Средняя", hpFrom = 40f, oldAttackChance = 0.4f,  headsAtOnce = 2, pauseBetweenWaves = 1.4f, waveTimeout = 26f },
            new Phase { name = "Злая",    hpFrom = 15f, oldAttackChance = 0.25f, headsAtOnce = 2, pauseBetweenWaves = 1.1f, waveTimeout = 26f },
            new Phase { name = "Ярость",  hpFrom = 0f,  oldAttackChance = 0.15f, headsAtOnce = 3, pauseBetweenWaves = 0.8f, waveTimeout = 26f }
        };
    }

    private void Start()
    {
        if (boss == null) boss = Boss.Instance;
        if (autoStart) StartBattle();
    }

    private void Update()
    {
        if (boss == null) return;

        // снял галку прямо в Play — голова гаснет сразу, не доигрывая атаку
        if (!boss.mopsAttacksEnabled && boss.mops != null && boss.mops.isRunning) boss.mops.Stop();
        if (!boss.chihAttacksEnabled && boss.chih != null && boss.chih.isRunning) boss.chih.Stop();
        if (!boss.bulAttacksEnabled && boss.bul != null && boss.bul.isRunning) boss.bul.Stop();

        if (driveRageAnimation) RageAnimation();
    }

    public void RageAnimation()
    {
        if (additionalEffectsAnimator == null || boss == null) return;

        if (boss.mops != null && boss.mops.isRunning && !additionalEffectsAnimator.GetBool("Mops Wind"))
            additionalEffectsAnimator.SetBool("Mops Wind", true);
        if (boss.chih != null && boss.chih.isRunning && !additionalEffectsAnimator.GetBool("Chih Fire"))
            additionalEffectsAnimator.SetBool("Chih Fire", true);
        if (boss.bul != null && boss.bul.isRunning && !additionalEffectsAnimator.GetBool("Bul Attack"))
            additionalEffectsAnimator.SetBool("Bul Attack", true);
    }

    [ContextMenu("Тест — Запустить бой")]
    public void StartBattle()
    {
        if (loop != null) return;
        if (boss == null) boss = Boss.Instance;
        if (boss == null) { Debug.LogWarning("BossBehaviourManager: не назначен Boss"); return; }
        loop = StartCoroutine(BattleLoop());
    }

    [ContextMenu("Тест — Остановить всё")]
    public void StopBattle()
    {
        StopAllCoroutines();
        loop = null;
        if (boss == null) boss = Boss.Instance;
        if (boss == null) return;
        if (boss.mops != null) boss.mops.Stop();
        if (boss.chih != null) boss.chih.Stop();
        if (boss.bul != null) boss.bul.Stop();
    }

    private IEnumerator BattleLoop()
    {
        while (boss.CurrentHealth > 0f)
        {
            List<BossAttack> heads = PickHeads(CurrentPhase().headsAtOnce);

            if (heads.Count == 0)
            {
                // все галки сняты — ждём, ничего не запускаем
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            Phase phase = CurrentPhase();
            for (int i = 0; i < heads.Count; i++)
                heads[i].Cast(PickAttack(heads[i], phase));

            onWaveStart?.Invoke();
            yield return new WaitForSeconds(1f);

            float t = 0f;
            while (t < phase.waveTimeout && AnyRunning(heads))
            {
                t += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < heads.Count; i++)
                if (heads[i].isRunning) heads[i].Stop();

            onWaveEnd?.Invoke();
            yield return new WaitForSeconds(phase.pauseBetweenWaves);
        }
        loop = null;
    }

    private Phase CurrentPhase()
    {
        float hp = boss.CurrentHealth;
        Phase best = null;
        for (int i = 0; i < phases.Count; i++)
        {
            Phase p = phases[i];
            if (p == null) continue;
            if (hp >= p.hpFrom && (best == null || p.hpFrom > best.hpFrom)) best = p;
        }
        return best ?? fallback;
    }

    private List<BossAttack> PickHeads(int count)
    {
        List<BossAttack> all = new List<BossAttack>();
        if (boss.mops != null && boss.mopsAttacksEnabled) all.Add(boss.mops);
        if (boss.chih != null && boss.chihAttacksEnabled) all.Add(boss.chih);
        if (boss.bul != null && boss.bulAttacksEnabled) all.Add(boss.bul);

        List<BossAttack> picked = new List<BossAttack>();
        if (all.Count == 0) return picked;
        count = Mathf.Clamp(count, 1, all.Count);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            picked.Clear();
            List<BossAttack> pool = new List<BossAttack>(all);
            for (int i = 0; i < count; i++)
            {
                int k = Random.Range(0, pool.Count);
                picked.Add(pool[k]);
                pool.RemoveAt(k);
            }
            int mask = 0;
            for (int i = 0; i < picked.Count; i++) mask |= 1 << HeadIndex(picked[i]);
            if (mask != previousMask || all.Count <= count) { previousMask = mask; break; }
        }
        return picked;
    }

    private int PickAttack(BossAttack head, Phase phase)
    {
        int i = HeadIndex(head);
        int idx;
        int guard = 0;
        do
        {
            idx = Random.value < phase.oldAttackChance ? 0 : Random.Range(1, BossAttack.AttackCount);
            guard++;
        }
        while (idx == lastAttack[i] && guard < 6);
        lastAttack[i] = idx;
        return idx;
    }

    private int HeadIndex(BossAttack a)
    {
        if (a == boss.mops) return 0;
        if (a == boss.chih) return 1;
        return 2;
    }

    private bool AnyRunning(List<BossAttack> heads)
    {
        for (int i = 0; i < heads.Count; i++)
            if (heads[i] != null && heads[i].isRunning) return true;
        return false;
    }
}