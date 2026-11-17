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
    private readonly Dictionary<string, GameObject> activeVfx = new Dictionary<string, GameObject>();

    public bool IsOnCooldown(Item item)
        => cooldowns.TryGetValue(item, out var c) && Time.time < c.until;

    public float GetCooldownLeft(Item item)
        => cooldowns.TryGetValue(item, out var c) ? Mathf.Max(0f, c.until - Time.time) : 0f;

    public float GetCooldownFraction(Item item)
        => cooldowns.TryGetValue(item, out var c) && c.total > 0f
            ? Mathf.Clamp01((c.until - Time.time) / c.total) : 0f;

    public void StartCooldown(Item item, float seconds)
        => cooldowns[item] = new CooldownInfo { until = Time.time + seconds, total = seconds };

    // Методы с VFX
    public void Strength(float multiplier, float duration, GameObject vfx)
        => Run("strength", duration, () => DamageMultiplier = multiplier, () => DamageMultiplier = 1f, vfx);

    public void Speed(float multiplier, float duration, GameObject vfx)
        => Run("speed", duration, () => SpeedMultiplier = multiplier, () => SpeedMultiplier = 1f, vfx);

    public void Invincibility(float duration, GameObject vfx)
        => Run("invincibility", duration, () => IsInvincible = true, () => IsInvincible = false, vfx);

    public void InfiniteMana(float duration, GameObject vfx)
        => Run("infiniteMana", duration, () => IsInfiniteMana = true, () => IsInfiniteMana = false, vfx);

    private void Run(string id, float duration, Action on, Action off, GameObject vfxPrefab)
    {
        // Сносим старую корутину, если бафф уже висит
        if (active.TryGetValue(id, out Coroutine old) && old != null)
        {
            StopCoroutine(old);
            active.Remove(id);
        }

        // Сносим старый визуал
        if (activeVfx.TryGetValue(id, out GameObject oldVfx) && oldVfx != null)
        {
            Destroy(oldVfx);
            activeVfx.Remove(id);
        }

        active[id] = StartCoroutine(Routine(id, duration, on, off, vfxPrefab));
    }

    private IEnumerator Routine(string id, float duration, Action on, Action off, GameObject vfxPrefab)
    {
        on();

        if (vfxPrefab != null)
        {
            GameObject spawnedVfx = Instantiate(vfxPrefab, transform.position, Quaternion.identity, transform);
            activeVfx[id] = spawnedVfx;
        }

        yield return new WaitForSeconds(duration);

        off();

        // Сносим визуал по истечении времени
        if (activeVfx.TryGetValue(id, out GameObject foundVfx) && foundVfx != null)
        {
            Destroy(foundVfx);
            activeVfx.Remove(id);
        }

        active.Remove(id);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        active.Clear();

        foreach (var kvp in activeVfx)
            if (kvp.Value != null) Destroy(kvp.Value);
        activeVfx.Clear();

        DamageMultiplier = 1f;
        SpeedMultiplier = 1f;
        IsInvincible = false;
        IsInfiniteMana = false;
    }
}