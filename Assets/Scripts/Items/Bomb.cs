using UnityEngine;

[CreateAssetMenu(fileName = "Bomb", menuName = "Inventory/Items/Bomb")]
public class Bomb : Item
{
    [Header("Bomb")]
    [Min(0f)] public float cooldown = 5f;
    public BombProjectile bombPrefab;
    public float throwForce = 8f;
    [Tooltip("Насколько далеко от центра игрока появляется бомба")]
    public float spawnOffset = 0.6f;

    public override bool Use(GameObject user)
    {
        PlayerEffects effects = user.GetComponent<PlayerEffects>();
        if (effects == null)
        {
            Debug.LogError($"На {user.name} нет PlayerEffects!");
            return false;
        }

        if (effects.IsOnCooldown(this))
        {
            Debug.Log($"{itemName}: кулдаун ещё {effects.GetCooldownLeft(this):F1} сек");
            return false;
        }

        if (bombPrefab == null)
        {
            Debug.LogError($"{itemName}: не назначен bombPrefab!");
            return false;
        }

        PlayerController pc = user.GetComponent<PlayerController>();
        Vector2 dir = pc != null ? pc.FacingDirection : Vector2.up;
        Vector2 spawnPos = (Vector2)user.transform.position + dir * spawnOffset;

        BombProjectile bomb = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
        bomb.Launch(dir, throwForce);

        effects.StartCooldown(this, cooldown);
        return true;
    }
}