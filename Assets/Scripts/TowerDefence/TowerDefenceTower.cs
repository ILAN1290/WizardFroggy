using UnityEngine;

public enum TowerDefenceTowerType
{
    MachineGun,
    Standard,
    Cannon
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class TowerDefenceTower : MonoBehaviour
{
    private TowerDefenceManager manager;
    private TowerDefenceTowerType type;
    private TowerDefenceTowerStats stats;
    private float attackTimer;
    private int level = 1;
    private int totalInvested;

    public TowerDefenceTowerType Type => type;
    public int Level => level;
    public int TotalInvested => totalInvested;
    public float CurrentRange => stats != null
        ? stats.range + (level - 1) * manager.UpgradeRangePerLevel
        : 0f;

    public void Initialize(
        TowerDefenceManager owner,
        TowerDefenceTowerType towerType,
        TowerDefenceTowerStats towerStats,
        Sprite sprite,
        bool freeStarter)
    {
        manager = owner;
        type = towerType;
        stats = towerStats;
        level = 1;
        totalInvested = freeStarter ? 0 : Mathf.Max(0, stats.mushroomCost);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.color = GetTint(type);
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 30;

        transform.localScale = new Vector3(1.5f, 1f, 1f);
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.isTrigger = true;

        attackTimer = Random.Range(0f, Mathf.Max(0.05f, stats.attackInterval));
    }

    private void Update()
    {
        if (manager == null || stats == null ||
            !manager.IsGameSessionRunning || manager.IsGameOver)
            return;

        attackTimer += Time.deltaTime;
        float interval = stats.attackInterval /
            Mathf.Max(0.1f, manager.TowerSpeedMultiplier);
        if (attackTimer < interval)
            return;

        TowerDefenceGhost target = manager.FindBestTarget(
            transform.position, CurrentRange);
        if (target == null)
            return;

        attackTimer = 0f;
        FireAt(target);
    }

    private void FireAt(TowerDefenceGhost target)
    {
        if (target == null || manager == null)
            return;

        float damage = stats.damage *
            (1f + (level - 1) * manager.UpgradeDamagePerLevel) *
            manager.TowerDamageMultiplier;

        Vector2 direction = ((Vector2)target.transform.position -
            (Vector2)transform.position).normalized;

        manager.SpawnTowerProjectile(
            transform.position,
            direction,
            damage,
            stats.projectileSpeed,
            stats.projectileSize,
            stats.splashRadius);
    }

    public int GetNextUpgradeCost()
    {
        return stats == null ? 0 : stats.mushroomCost * level;
    }

    public bool CanUpgrade => level < manager.MaximumTowerLevel;

    public void ApplyUpgrade(int price)
    {
        if (!CanUpgrade)
            return;

        level++;
        totalInvested += Mathf.Max(0, price);
    }

    public int GetSellRefund()
    {
        return Mathf.Max(0, Mathf.FloorToInt(totalInvested * 0.5f));
    }

    public string GetDisplayName()
    {
        switch (type)
        {
            case TowerDefenceTowerType.MachineGun: return "Пулемётная башня";
            case TowerDefenceTowerType.Cannon: return "Пушечная башня";
            default: return "Обычная башня";
        }
    }

    private static Color GetTint(TowerDefenceTowerType towerType)
    {
        switch (towerType)
        {
            case TowerDefenceTowerType.MachineGun:
                return new Color(0.52f, 0.78f, 1f, 1f);
            case TowerDefenceTowerType.Cannon:
                return new Color(1f, 0.62f, 0.38f, 1f);
            default:
                return new Color(0.83f, 0.9f, 0.72f, 1f);
        }
    }
}
