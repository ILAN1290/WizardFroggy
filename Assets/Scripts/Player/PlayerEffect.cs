using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    public float DamageMultiplier { get; private set; } = 1f;
    public float SpeedMultiplier { get; private set; } = 1f;
    public bool IsInvincible { get; private set; }
    public bool IsInfiniteMana { get; private set; }

    private struct CooldownInfo
    {
        public float until;
        public float total;
    }

    private readonly Dictionary<Item, CooldownInfo> cooldowns = new Dictionary<Item, CooldownInfo>();
    private readonly Dictionary<string, Coroutine> active = new Dictionary<string, Coroutine>();

    public bool IsOnCooldown(Item item)
        => cooldowns.TryGetValue(item, out var c) && Time.time < c.until;

    public float GetCooldownLeft(Item item)
        => cooldowns.TryGetValue(item, out var c) ? Mathf.Max(0f, c.until - Time.time) : 0f;

    // 1 → 0 по мере отката — готовое значение для заливки
    public float GetCooldownFraction(Item item)
        => cooldowns.TryGetValue(item, out var c) && c.total > 0f
            ? Mathf.Clamp01((c.until - Time.time) / c.total) : 0f;

    public void StartCooldown(Item item, float seconds)
        => cooldowns[item] = new CooldownInfo { until = Time.time + seconds, total = seconds };

    public void Strength(float multiplier, float duration)
        => Run("strength", duration, () => DamageMultiplier = multiplier, () => DamageMultiplier = 1f);

    public void Speed(float multiplier, float duration)
        => Run("speed", duration, () => SpeedMultiplier = multiplier, () => SpeedMultiplier = 1f);

    public void Invincibility(float duration)
        => Run("invincibility", duration, () => IsInvincible = true, () => IsInvincible = false);

    public void InfiniteMana(float duration)
        => Run("infiniteMana", duration, () => IsInfiniteMana = true, () => IsInfiniteMana = false);

    private void Run(string id, float duration, Action on, Action off)
    {
        if (active.TryGetValue(id, out Coroutine old) && old != null)
            StopCoroutine(old);
        active[id] = StartCoroutine(Routine(id, duration, on, off));
    }

    private IEnumerator Routine(string id, float duration, Action on, Action off)
    {
        on();
        yield return new WaitForSeconds(duration);
        off();
        active.Remove(id);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        active.Clear();
        DamageMultiplier = 1f;
        SpeedMultiplier = 1f;
        IsInvincible = false;
        IsInfiniteMana = false;
    }
}