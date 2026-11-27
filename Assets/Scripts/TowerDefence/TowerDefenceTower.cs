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
    private SpriteRenderer spriteRenderer;
    private Vector2 baseSpriteSize;
    private int lastScreenHeight;
    private float lastOrthoSize;

    public TowerDefenceTowerType Type => type;
    public int Level => level;
    public int TotalInvested => totalInvested;
    public float CurrentRange => stats != null
        ? stats.range + (level - 1) * manager.UpgradeRangePerLevel
        : 0f;
    public float CurrentDamage => stats != null
        ? stats.damage * (1f + (level - 1) * manager.UpgradeDamagePerLevel)
        : 0f;
    public float AttacksPerSecond => stats != null
        ? 1f / Mathf.Max(0.05f, stats.attackInterval)
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

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = GetTint(type);
        spriteRenderer.sortingLayerName = "Default";
        spriteRenderer.sortingOrder = 30;
        baseSpriteSize = sprite != null ? sprite.bounds.size : Vector2.one;
        ApplyPixelSize();

        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        collider.size = baseSpriteSize;
        collider.isTrigger = true;

        attackTimer = Random.Range(0f, Mathf.Max(0.05f, stats.attackInterval));
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        if (lastScreenHeight != camera.pixelHeight ||
            !Mathf.Approximately(lastOrthoSize, camera.orthographicSize))
            ApplyPixelSize();
    }

    private void ApplyPixelSize()
    {
        Camera camera = Camera.main;
        if (camera == null || spriteRenderer == null ||
            baseSpriteSize.x <= 0f || baseSpriteSize.y <= 0f)
            return;

        float pixelsPerWorldUnit = camera.pixelHeight /
            Mathf.Max(0.01f, camera.orthographicSize * 2f);
        float targetWidth = 40f / pixelsPerWorldUnit;
        float targetHeight = 70f / pixelsPerWorldUnit;

        transform.localScale = new Vector3(
            targetWidth / baseSpriteSize.x,
            targetHeight / baseSpriteSize.y,
            1f);

        lastScreenHeight = camera.pixelHeight;
        lastOrthoSize = camera.orthographicSize;
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

        manager.PlayTowerShot(type);
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
