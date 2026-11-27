using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public class TowerDefenceTowerStats
{
    [Min(1)] public int mushroomCost = 1;
    [Min(0.1f)] public float damage = 5f;
    [Min(0.05f)] public float attackInterval = 0.85f;
    [Min(0.25f)] public float range = 4.5f;
    [Min(0.1f)] public float projectileSpeed = 5.4f;
    [Min(0.01f)] public float projectileSize = 0.14f;
    [Min(0f)] public float splashRadius;
}

[DisallowMultipleComponent]
public class TowerDefenceManager : MonoBehaviour
{
    public static TowerDefenceManager Instance { get; private set; }

    [Header("Спрайты призраков")]
    [SerializeField] private Sprite whiteGhostSprite;
    [SerializeField] private Sprite blueGhostSprite;
    [SerializeField] private Sprite redGhostSprite;
    [SerializeField] private Sprite blackGhostSprite;

    [Header("Спрайты башен")]
    [SerializeField] private Sprite machineGunTowerSprite;
    [SerializeField] private Sprite standardTowerSprite;
    [SerializeField] private Sprite cannonTowerSprite;
    [SerializeField] private Sprite projectileSprite;

    [Header("Спрайты сцены и интерфейса")]
    [SerializeField] private Sprite grandpaSprite;
    [SerializeField] private Sprite roadTileSprite;
    [SerializeField] private Sprite mushroomSprite;
    [SerializeField] private Sprite instructionBookSprite;
    [SerializeField] private Sprite upgradePlusButtonSprite;
    [SerializeField] private Sprite sellMinusButtonSprite;
    [SerializeField] private Sprite buildButtonSprite;
    [SerializeField] private Sprite timerBackgroundSprite;
    [SerializeField] private Sprite waveCountBackgroundSprite;
    [SerializeField] private Sprite grandpaHpBackgroundSprite;
    [SerializeField] private Sprite sellTowerIconSprite;
    [SerializeField] private Sprite upgradeTowerIconSprite;
    [SerializeField, Range(0.1f, 1f)] private float mushroomToGhostHeight = 0.5f;
    [SerializeField] private TMP_FontAsset uiFontAsset;

    [Header("Здоровье дедушки")]
    [SerializeField, Min(1f)] private float grandpaMaximumHealth = 300f;
    [SerializeField, Min(0f)] private float grandpaPotionHealAmount = 15f;

    [Header("Параметры призраков")]
    [SerializeField, Min(1f)] private float whiteGhostHealth = 40f;
    [SerializeField, Min(0.05f)] private float whiteGhostSpeed = 0.72f;
    [SerializeField, Min(0.1f)] private float whiteGhostSize = 0.72f;
    [SerializeField, Min(1f)] private float blueGhostHealth = 40f;
    [SerializeField, Min(1f)] private float blueGhostSpeedMultiplier = 2f;
    [SerializeField, Min(0.1f)] private float blueGhostSize = 0.88f;
    [SerializeField, Min(1f)] private float redGhostHealth = 120f;
    [SerializeField, Min(0.1f)] private float redGhostSpeedMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float redGhostSize = 1.05f;
    [SerializeField, Min(1f)] private float blackGhostHealth = 800f;
    [SerializeField, Min(0.1f)] private float blackGhostSpeedMultiplier = 0.5f;
    [SerializeField, Min(0.1f)] private float blackGhostSize = 1.35f;

    [Header("Параметры башен")]
    [SerializeField] private TowerDefenceTowerStats machineGunStats = new TowerDefenceTowerStats
    {
        mushroomCost = 2, damage = 2.2f, attackInterval = 0.28f,
        range = 1.575f, projectileSpeed = 7f, projectileSize = 0.105f
    };
    [SerializeField] private TowerDefenceTowerStats standardStats = new TowerDefenceTowerStats
    {
        mushroomCost = 1, damage = 5f, attackInterval = 0.88f,
        range = 2.325f, projectileSpeed = 5.4f, projectileSize = 0.14f
    };
    [SerializeField] private TowerDefenceTowerStats cannonStats = new TowerDefenceTowerStats
    {
        mushroomCost = 4, damage = 18f, attackInterval = 2.45f,
        range = 1.375f, projectileSpeed = 4.8f, projectileSize = 0.19f,
        splashRadius = 0.625f
    };
    [SerializeField, Min(1)] private int maximumTowerLevel = 4;
    [SerializeField, Min(0f)] private float upgradeDamagePerLevel = 0.25f;
    [SerializeField, Min(0f)] private float upgradeRangePerLevel = 0.11f;

    [Header("Волны и подготовка")]
    [SerializeField, Min(0f)] private float firstWaveDelay = 30f;
    [SerializeField, Min(0f)] private float betweenWaveDelay = 15f;
    [SerializeField] private string[] grandpaPhrases =
    {
        "Ну что, внучок, держим оборону! Я ещё не старый!",
        "Вот раньше привидения были воспитаннее...",
        "Только картошку поставил, а тут опять гости!",
        "Не пускайте их ко мне, у меня спина!",
        "Шапку наденьте! Сквозняк же!",
        "Я вообще-то рассчитывал на спокойную пенсию."
    };

    private readonly List<TowerDefenceGhost> activeGhosts =
        new List<TowerDefenceGhost>();
    private readonly List<TowerDefenceTower> towers =
        new List<TowerDefenceTower>();
    private readonly HashSet<Vector3Int> roadCells =
        new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> occupiedCells =
        new HashSet<Vector3Int>();

    private TowerDefenceWaveSpawner waveSpawner;
    private int totalSpawnedGhosts;
    private int completedWaves;
    private bool waveLoopRunning;
    private Vector3[] routeA;
    private Vector3[] routeB;
    private Vector3 spawnPosition;
    private Transform grandpaTransform;
    private float grandpaHealth;
    private int mushrooms;
    private int nextRouteIndex;
    private int currentWave;
    private float countdown;
    private bool countdownActive;
    private bool instructionsOpen;
    private bool gameStarted;
    private bool gameOver;
    private bool victory;
    private string grandpaPhrase = "";
    private float phraseUntil;
    private float speedPotionMultiplier = 1f;
    private float speedPotionUntil;
    private float strengthPotionMultiplier = 1f;
    private float strengthPotionUntil;
    private string statusMessage = "";
    private float statusUntil;
    private TowerDefenceTower selectedTower;
    private Vector3Int pendingBuildCell;
    private bool buildMenuOpen;
    private GameObject rangePreview;
    private Sprite runtimeSquareSprite;
    private Texture2D runtimeSquareTexture;
    private Material rangeMaterial;
    private Sprite runtimeSellIcon;
    private Sprite runtimeUpgradeIcon;
    private Texture2D runtimeSellIconTexture;
    private Texture2D runtimeUpgradeIconTexture;
    private bool wavesFinished;

    public event System.Action AllWavesDefeated;

    public bool IsGameSessionRunning =>
        (gameStarted || instructionsOpen) && !gameOver && !victory;
    public bool IsGameOver => gameOver || victory;
    public float TowerSpeedMultiplier =>
        Time.time < speedPotionUntil ? speedPotionMultiplier : 1f;
    public float TowerDamageMultiplier =>
        Time.time < strengthPotionUntil ? strengthPotionMultiplier : 1f;
    public int MaximumTowerLevel => Mathf.Max(1, maximumTowerLevel);
    public float UpgradeDamagePerLevel => upgradeDamagePerLevel;
    public float UpgradeRangePerLevel => upgradeRangePerLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        grandpaHealth = grandpaMaximumHealth;
        mushrooms = 0;
        waveSpawner = GetComponent<TowerDefenceWaveSpawner>();
        if (sellTowerIconSprite == null)
            runtimeSellIcon = CreatePixelActionIcon(true, out runtimeSellIconTexture);
        else
            runtimeSellIcon = sellTowerIconSprite;
        if (upgradeTowerIconSprite == null)
            runtimeUpgradeIcon = CreatePixelActionIcon(false, out runtimeUpgradeIconTexture);
        else
            runtimeUpgradeIcon = upgradeTowerIconSprite;
        DisablePlayerCombat();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (runtimeSquareSprite != null)
            Destroy(runtimeSquareSprite);
        if (runtimeSquareTexture != null)
            Destroy(runtimeSquareTexture);
        if (rangeMaterial != null)
            Destroy(rangeMaterial);
        if (runtimeSellIcon != null && runtimeSellIcon != sellTowerIconSprite)
            Destroy(runtimeSellIcon);
        if (runtimeUpgradeIcon != null && runtimeUpgradeIcon != upgradeTowerIconSprite)
            Destroy(runtimeUpgradeIcon);
        if (runtimeSellIconTexture != null)
            Destroy(runtimeSellIconTexture);
        if (runtimeUpgradeIconTexture != null)
            Destroy(runtimeUpgradeIconTexture);
    }

    private void Update()
    {
        if (gameOver || victory)
            return;

        if (Time.time >= speedPotionUntil)
            speedPotionMultiplier = 1f;
        if (Time.time >= strengthPotionUntil)
            strengthPotionMultiplier = 1f;

        if (gameStarted && !instructionsOpen && !gameOver && !victory)
            HandleWorldClick();
    }

    public void ConfigureRoutes(
        Vector3[] firstRoute,
        Vector3[] secondRoute,
        HashSet<Vector3Int> generatedRoadCells,
        Vector3 spawn)
    {
        routeA = firstRoute != null ? (Vector3[])firstRoute.Clone() : null;
        routeB = secondRoute != null ? (Vector3[])secondRoute.Clone() : null;
        spawnPosition = spawn;

        roadCells.Clear();
        if (generatedRoadCells != null)
        {
            foreach (Vector3Int cell in generatedRoadCells)
                roadCells.Add(cell);
        }
    }

    public void SetGrandpa(Transform grandpa)
    {
        grandpaTransform = grandpa;
        if (grandpaTransform != null && grandpaSprite != null)
        {
            SpriteRenderer renderer = grandpaTransform.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = grandpaSprite;
        }
    }

    public void BeginAfterIntro()
    {
        if (gameStarted || instructionsOpen || waveLoopRunning)
            return;

        if (waveSpawner == null)
            waveSpawner = GetComponent<TowerDefenceWaveSpawner>();
        if (waveSpawner == null)
            waveSpawner = gameObject.AddComponent<TowerDefenceWaveSpawner>();

        if (waveSpawner == null || waveSpawner.TotalWaves <= 0)
        {
            ReportWaveSetupFailure("Не найден спавнер или в нём не настроены волны.");
            return;
        }

        if (routeA == null || routeB == null ||
            routeA.Length < 2 || routeB.Length < 2)
        {
            ReportWaveSetupFailure("Не настроены маршруты призраков. Запуск волн отменён.");
            return;
        }

        totalSpawnedGhosts = 0;
        completedWaves = 0;
        StartCoroutine(ShowInstructionsAndRun());
    }

    private IEnumerator ShowInstructionsAndRun()
    {
        if (waveLoopRunning)
            yield break;

        waveLoopRunning = true;

        if (waveSpawner == null || waveSpawner.TotalWaves <= 0)
        {
            ReportWaveSetupFailure("Спавнер не содержит ни одной волны.");
            waveLoopRunning = false;
            yield break;
        }

        instructionsOpen = true;
        yield return new WaitUntil(() => !instructionsOpen || gameOver);

        if (gameOver)
        {
            waveLoopRunning = false;
            yield break;
        }

        gameStarted = true;
        CreateStarterTower();

        int totalWaves = waveSpawner.TotalWaves;
        countdown = Mathf.Max(0f, firstWaveDelay);
        countdownActive = true;

        for (int wave = 1; wave <= totalWaves; wave++)
        {
            int expectedGhosts = waveSpawner.GetEnemyCountForWave(wave);
            if (expectedGhosts <= 0)
            {
                ReportWaveSetupFailure(
                    "Волна " + wave + " пуста. Победа не засчитывается.");
                waveLoopRunning = false;
                yield break;
            }

            while (countdown > 0f && !gameOver)
            {
                countdown -= Time.deltaTime;
                yield return null;
            }

            if (gameOver)
            {
                waveLoopRunning = false;
                yield break;
            }

            currentWave = wave;
            ShowGrandpaPhrase();
            countdownActive = false;

            int spawnedBeforeWave = totalSpawnedGhosts;
            yield return StartCoroutine(waveSpawner.SpawnWave(wave, this));

            if (gameOver)
            {
                waveLoopRunning = false;
                yield break;
            }

            int actuallySpawned = totalSpawnedGhosts - spawnedBeforeWave;
            if (actuallySpawned != expectedGhosts)
            {
                ReportWaveSetupFailure(
                    "Волна " + wave + ": создано призраков " +
                    actuallySpawned + " из " + expectedGhosts +
                    ". Проверь маршруты и настройки спавнера.");
                waveLoopRunning = false;
                yield break;
            }

            while (activeGhosts.Count > 0 && !gameOver)
                yield return null;

            if (gameOver)
            {
                waveLoopRunning = false;
                yield break;
            }

            completedWaves = wave;

            if (wave < totalWaves)
            {
                countdown = Mathf.Max(0f, betweenWaveDelay);
                countdownActive = true;

                while (countdown > 0f && !gameOver)
                {
                    countdown -= Time.deltaTime;
                    yield return null;
                }

                if (gameOver)
                {
                    waveLoopRunning = false;
                    yield break;
                }

                countdownActive = false;
            }
        }

        if (!gameOver && completedWaves == totalWaves)
        {
            wavesFinished = true;
            gameStarted = false;
            countdownActive = false;
            statusMessage = "Все " + totalWaves + " волн отбиты! Дедушка ждёт тебя.";
            statusUntil = float.MaxValue;
            AllWavesDefeated?.Invoke();
        }

        waveLoopRunning = false;
    }

    private void ReportWaveSetupFailure(string message)
    {
        gameStarted = false;
        instructionsOpen = false;
        countdownActive = false;
        statusMessage = message;
        statusUntil = float.MaxValue;
        Debug.LogError("Tower Defence: " + message, this);
    }

    private void CreateStarterTower()
    {
        // Бесплатная стартовая башня нужна, потому что личные атаки игрока
        // в этом режиме выключены, а баланс грибов начинается строго с нуля.
        Vector3Int cell = new Vector3Int(5, 1, 0);
        if (occupiedCells.Contains(cell))
            return;

        occupiedCells.Add(cell);
        CreateTowerAtCell(cell, TowerDefenceTowerType.Standard, true);
        ShowStatus("Поставлена бесплатная стартовая башня");
    }

    private void DisablePlayerCombat()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        PlayerController controller = playerObject.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.canShoot = false;
            controller.canParry = false;
            controller.canBeInvincible = false;
        }

        Parry[] parries = playerObject.GetComponentsInChildren<Parry>(true);
        for (int i = 0; i < parries.Length; i++)
            if (parries[i] != null)
                parries[i].enabled = false;
    }

    public void SpawnGhost(TowerDefenceGhostType type)
    {
        if (gameOver || routeA == null || routeB == null ||
            routeA.Length < 2 || routeB.Length < 2)
            return;

        int branch = nextRouteIndex % 2;
        nextRouteIndex++;
        Vector3[] route = branch == 0 ? routeA : routeB;

        float health;
        float speed;
        float size;
        Sprite sprite;

        switch (type)
        {
            case TowerDefenceGhostType.Blue:
                health = blueGhostHealth;
                speed = whiteGhostSpeed * blueGhostSpeedMultiplier;
                size = blueGhostSize;
                sprite = blueGhostSprite != null ? blueGhostSprite : whiteGhostSprite;
                break;
            case TowerDefenceGhostType.Red:
                health = redGhostHealth;
                speed = whiteGhostSpeed * redGhostSpeedMultiplier;
                size = redGhostSize;
                sprite = redGhostSprite != null ? redGhostSprite : whiteGhostSprite;
                break;
            case TowerDefenceGhostType.Black:
                health = blackGhostHealth;
                speed = whiteGhostSpeed * blackGhostSpeedMultiplier;
                size = blackGhostSize;
                sprite = blackGhostSprite != null ? blackGhostSprite : whiteGhostSprite;
                break;
            default:
                health = whiteGhostHealth;
                speed = whiteGhostSpeed;
                size = whiteGhostSize;
                sprite = whiteGhostSprite;
                break;
        }

        GameObject ghostObject = new GameObject("TDS Ghost • " + type);
        ghostObject.transform.position = route.Length > 0 ? route[0] : spawnPosition;
        ghostObject.AddComponent<Rigidbody2D>();
        ghostObject.AddComponent<CircleCollider2D>();
        TowerDefenceGhost ghost = ghostObject.AddComponent<TowerDefenceGhost>();
        ghost.Initialize(this, type, route, sprite, health, speed, size);
        activeGhosts.Add(ghost);
        totalSpawnedGhosts++;
    }

    public void OnGhostDefeated(TowerDefenceGhost ghost, Vector3 position)
    {
        activeGhosts.Remove(ghost);
        CreateMushroomDrop(position);
        ShowStatus("Выпал грибочек — подбери его!");
    }

    public void OnGhostReachedGrandpa(TowerDefenceGhost ghost, float remainingHealth)
    {
        activeGhosts.Remove(ghost);
        float damage = Mathf.Max(0f, remainingHealth);
        grandpaHealth = Mathf.Max(0f, grandpaHealth - damage);
        ShowStatus("Призрак добрался до деда: -" + Mathf.CeilToInt(damage) + " HP");

        if (grandpaHealth <= 0f)
        {
            gameOver = true;
            statusMessage = "Дедушка пал... Оборона прорвана";
            statusUntil = float.MaxValue;
        }
    }

    private void CreateMushroomDrop(Vector3 position)
    {
        if (mushroomSprite == null)
            return;

        GameObject drop = new GameObject("Выпавший грибочек");
        drop.transform.position = position + Vector3.up * 0.12f;
        float mushroomHeight = mushroomSprite.bounds.size.y;
        float whiteGhostHeight = 0.72f * Mathf.Max(0.1f, whiteGhostSize);
        float targetMushroomHeight = whiteGhostHeight * mushroomToGhostHeight;
        drop.transform.localScale = Vector3.one *
            (mushroomHeight > 0.001f
                ? targetMushroomHeight / mushroomHeight
                : targetMushroomHeight);

        SpriteRenderer renderer = drop.AddComponent<SpriteRenderer>();
        renderer.sprite = mushroomSprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 35;

        CircleCollider2D trigger = drop.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.24f;

        Rigidbody2D body = drop.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        drop.AddComponent<TowerDefenceMushroomPickup>()
            .Initialize(this, 20f);
    }

    public void CollectMushroom()
    {
        mushrooms++;
        ShowStatus("+1 грибочек");
    }

    public void NotifyKeyCollected()
    {
        ShowStatus("Получен ключ от библиотеки");
    }

    public void ShowStatusMessage(string message)
    {
        ShowStatus(message);
    }

    public void CompleteAfterDoor()
    {
        if (gameOver || victory || !wavesFinished)
            return;

        victory = true;
        statusMessage = "Этап пройден!";
        statusUntil = float.MaxValue;
        if (SaveGameSystem.Instance != null)
            SaveGameSystem.Instance.ShowTowerDefenceTransition();
    }

    public TowerDefenceGhost FindBestTarget(Vector3 towerPosition, float range)
    {
        TowerDefenceGhost best = null;
        float bestProgress = float.NegativeInfinity;
        float rangeSquared = range * range;

        for (int i = activeGhosts.Count - 1; i >= 0; i--)
        {
            TowerDefenceGhost ghost = activeGhosts[i];
            if (ghost == null || !ghost.IsAlive)
            {
                activeGhosts.RemoveAt(i);
                continue;
            }

            Vector2 offset = (Vector2)ghost.transform.position -
                (Vector2)towerPosition;
            if (offset.sqrMagnitude > rangeSquared)
                continue;

            if (ghost.PathProgress > bestProgress)
            {
                best = ghost;
                bestProgress = ghost.PathProgress;
            }
        }

        return best;
    }

    public void SpawnTowerProjectile(
        Vector3 position,
        Vector2 direction,
        float damage,
        float speed,
        float size,
        float splashRadius)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        GameObject bolt = new GameObject("Снаряд башни");
        bolt.transform.position = position;
        bolt.transform.localScale = Vector3.one * Mathf.Max(0.01f, size);

        SpriteRenderer renderer = bolt.AddComponent<SpriteRenderer>();
        renderer.sprite = projectileSprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 8;

        BoxCollider2D collider = bolt.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = Vector2.one;

        Rigidbody2D body = bolt.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        RoguelikeProjectile projectile = bolt.AddComponent<RoguelikeProjectile>();
        projectile.InitializeTowerDefence(
            direction, speed, damage,
            new Color(0.68f, 0.9f, 1f, 1f), splashRadius);
    }

    public void ApplyTowerProjectileDamage(
        TowerDefenceGhost directTarget,
        Vector2 impactPosition,
        float damage,
        float splashRadius)
    {
        if (splashRadius <= 0f)
        {
            if (directTarget != null)
                directTarget.TakeDamage(damage);
            return;
        }

        float radiusSquared = splashRadius * splashRadius;
        List<TowerDefenceGhost> targets = new List<TowerDefenceGhost>();
        for (int i = 0; i < activeGhosts.Count; i++)
        {
            TowerDefenceGhost ghost = activeGhosts[i];
            if (ghost == null || !ghost.IsAlive)
                continue;

            Vector2 offset = (Vector2)ghost.transform.position - impactPosition;
            if (offset.sqrMagnitude <= radiusSquared)
                targets.Add(ghost);
        }

        if (targets.Count == 0 && directTarget != null)
            targets.Add(directTarget);

        for (int i = 0; i < targets.Count; i++)
            if (targets[i] != null)
                targets[i].TakeDamage(damage);
    }

    public TowerDefenceTowerStats GetStats(TowerDefenceTowerType type)
    {
        switch (type)
        {
            case TowerDefenceTowerType.MachineGun: return machineGunStats;
            case TowerDefenceTowerType.Cannon: return cannonStats;
            default: return standardStats;
        }
    }

    public Sprite GetTowerSprite(TowerDefenceTowerType type)
    {
        switch (type)
        {
            case TowerDefenceTowerType.MachineGun: return machineGunTowerSprite;
            case TowerDefenceTowerType.Cannon: return cannonTowerSprite;
            default: return standardTowerSprite;
        }
    }

    public float GetRangeForType(TowerDefenceTowerType type, int level)
    {
        TowerDefenceTowerStats stats = GetStats(type);
        return stats.range + Mathf.Max(0, level - 1) * upgradeRangePerLevel;
    }

    public bool TryApplySpeedPotion(float multiplier, float duration)
    {
        if (!IsGameSessionRunning)
            return false;

        speedPotionMultiplier = Mathf.Max(1f, multiplier);
        speedPotionUntil = Time.time + Mathf.Max(0.1f, duration);
        ShowStatus("Зелье скорости: башни стреляют быстрее!");
        return true;
    }

    public bool TryApplyStrengthPotion(float multiplier, float duration)
    {
        if (!IsGameSessionRunning)
            return false;

        strengthPotionMultiplier = Mathf.Max(1f, multiplier);
        strengthPotionUntil = Time.time + Mathf.Max(0.1f, duration);
        ShowStatus("Зелье силы: башни наносят больше урона!");
        return true;
    }

    public bool TryHealGrandpa()
    {
        if (!IsGameSessionRunning || grandpaHealth >= grandpaMaximumHealth)
            return false;

        grandpaHealth = Mathf.Min(
            grandpaMaximumHealth, grandpaHealth + grandpaPotionHealAmount);
        ShowStatus("Дедушка восстановил " +
            Mathf.CeilToInt(grandpaPotionHealAmount) + " HP");
        return true;
    }

    private void ShowGrandpaPhrase()
    {
        if (grandpaPhrases == null || grandpaPhrases.Length == 0)
            return;

        string previous = grandpaPhrase;
        for (int i = 0; i < grandpaPhrases.Length; i++)
        {
            string candidate = grandpaPhrases[Random.Range(0, grandpaPhrases.Length)];
            if (!string.IsNullOrWhiteSpace(candidate) && candidate != previous)
            {
                grandpaPhrase = candidate;
                break;
            }
        }
        if (string.IsNullOrWhiteSpace(grandpaPhrase))
            grandpaPhrase = grandpaPhrases[0];

        phraseUntil = Time.time + 4.5f;
    }

    private void ShowStatus(string message)
    {
        statusMessage = message;
        statusUntil = Time.time + 2.2f;
    }

    private void HandleWorldClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();
        Vector2 guiPosition = new Vector2(
            mousePosition.x,
            Screen.height - mousePosition.y);

        if (buildMenuOpen && GetBuildPanelRect().Contains(guiPosition))
            return;
        if (selectedTower != null && GetTowerPanelRect().Contains(guiPosition))
            return;
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 world = camera.ScreenToWorldPoint(new Vector3(
            mousePosition.x, mousePosition.y,
            Mathf.Abs(camera.transform.position.z)));

        Collider2D hit = Physics2D.OverlapPoint(world);
        TowerDefenceTower tower = hit != null
            ? hit.GetComponentInParent<TowerDefenceTower>()
            : null;

        if (tower != null)
        {
            SelectTower(tower);
            buildMenuOpen = false;
            return;
        }

        Vector3Int cell = new Vector3Int(
            Mathf.FloorToInt(world.x),
            Mathf.FloorToInt(world.y),
            0);

        if (CanPlaceTowerAt(cell))
        {
            pendingBuildCell = cell;
            buildMenuOpen = true;
            selectedTower = null;
            ClearRangePreview();
            return;
        }

        buildMenuOpen = false;
        selectedTower = null;
        ClearRangePreview();
    }

    private bool CanPlaceTowerAt(Vector3Int cell)
    {
        // Башни можно ставить только внутри границ игровой площадки,
        // не за боковыми стенами и не за верхней/нижней перегородками.
        if (cell.x < -8 || cell.x > 7 || cell.y < -5 || cell.y > 0)
            return false;

        if (roadCells.Contains(cell) || occupiedCells.Contains(cell))
            return false;

        Vector2 centre = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        bool nearRoad = false;
        foreach (Vector3Int roadCell in roadCells)
        {
            Vector2 roadCentre = new Vector2(roadCell.x + 0.5f, roadCell.y + 0.5f);
            float distance = Vector2.Distance(centre, roadCentre);
            if (distance >= 1.25f && distance <= 2.4f)
            {
                nearRoad = true;
                break;
            }
        }
        if (!nearRoad)
            return false;

        for (int i = 0; i < towers.Count; i++)
        {
            TowerDefenceTower tower = towers[i];
            if (tower == null)
                continue;
            if (Vector2.Distance(centre, tower.transform.position) < 1.45f)
                return false;
        }

        return true;
    }

    private void SelectTower(TowerDefenceTower tower)
    {
        selectedTower = tower;
        if (tower != null)
        {
            pendingBuildCell = new Vector3Int(
                Mathf.FloorToInt(tower.transform.position.x),
                Mathf.FloorToInt(tower.transform.position.y),
                0);
        }
        buildMenuOpen = false;
        ShowRangePreview(tower);
    }

    private void CreateTowerAtCell(
        Vector3Int cell,
        TowerDefenceTowerType type,
        bool freeStarter)
    {
        Vector3 position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, -0.1f);
        GameObject towerObject = new GameObject("Башня • " + type);
        towerObject.transform.position = position;

        Sprite sprite = GetTowerSprite(type);
        if (sprite == null)
            sprite = GetRuntimeSquareSprite();

        towerObject.AddComponent<BoxCollider2D>();
        TowerDefenceTower tower = towerObject.AddComponent<TowerDefenceTower>();
        tower.Initialize(this, type, GetStats(type), sprite, freeStarter);
        towers.Add(tower);
    }

    private bool TryBuyTower(TowerDefenceTowerType type)
    {
        if (!CanPlaceTowerAt(pendingBuildCell))
        {
            ShowStatus("Здесь нельзя поставить башню");
            buildMenuOpen = false;
            return false;
        }

        TowerDefenceTowerStats stats = GetStats(type);
        if (mushrooms < stats.mushroomCost)
        {
            ShowStatus("Не хватает грибочков");
            return false;
        }

        mushrooms -= stats.mushroomCost;
        occupiedCells.Add(pendingBuildCell);
        CreateTowerAtCell(pendingBuildCell, type, false);
        buildMenuOpen = false;
        ShowStatus("Башня построена");
        return true;
    }

    private void TryUpgradeSelectedTower()
    {
        if (selectedTower == null || !selectedTower.CanUpgrade)
            return;

        int cost = selectedTower.GetNextUpgradeCost();
        if (mushrooms < cost)
        {
            ShowStatus("Не хватает грибочков для улучшения");
            return;
        }

        mushrooms -= cost;
        selectedTower.ApplyUpgrade(cost);
        ShowRangePreview(selectedTower);
        ShowStatus("Башня улучшена до " + selectedTower.Level + " уровня");
    }

    private void SellSelectedTower()
    {
        if (selectedTower == null)
            return;

        TowerDefenceTower tower = selectedTower;
        int refund = tower.GetSellRefund();
        mushrooms += refund;

        Vector3Int cell = new Vector3Int(
            Mathf.FloorToInt(tower.transform.position.x),
            Mathf.FloorToInt(tower.transform.position.y),
            0);
        occupiedCells.Remove(cell);
        towers.Remove(tower);
        selectedTower = null;
        ClearRangePreview();
        Destroy(tower.gameObject);
        ShowStatus("Башня продана: +" + refund);
    }

    private void ShowRangePreview(TowerDefenceTower tower)
    {
        ClearRangePreview();
        if (tower == null)
            return;

        rangePreview = new GameObject("Радиус башни • просмотр");
        rangePreview.transform.position = tower.transform.position;

        Color rangeColour = new Color(0.95f, 0.9f, 0.55f, 0.85f);
        if (tower.Type == TowerDefenceTowerType.MachineGun)
            rangeColour = new Color(0.3f, 0.72f, 1f, 0.85f);
        else if (tower.Type == TowerDefenceTowerType.Cannon)
            rangeColour = new Color(1f, 0.45f, 0.28f, 0.85f);

        CreateRangeRing(rangePreview.transform, tower.CurrentRange, rangeColour);
    }

    private void CreateRangeRing(Transform parent, float radius, Color colour)
    {
        GameObject ring = new GameObject("Радиус " + radius.ToString("F1"));
        ring.transform.SetParent(parent, false);

        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 65;
        line.startWidth = 0.035f;
        line.endWidth = 0.035f;
        line.numCapVertices = 2;
        line.sortingLayerName = "Default";
        line.sortingOrder = -9;
        line.startColor = colour;
        line.endColor = colour;
        line.material = GetRangeMaterial();

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i / 64f * Mathf.PI * 2f;
            Vector3 point = parent.position + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0.15f);
            line.SetPosition(i, point);
        }
    }

    private Material GetRangeMaterial()
    {
        if (rangeMaterial != null)
            return rangeMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        rangeMaterial = new Material(shader);
        return rangeMaterial;
    }

    private void ClearRangePreview()
    {
        if (rangePreview != null)
            Destroy(rangePreview);
        rangePreview = null;
    }

    private Sprite GetRuntimeSquareSprite()
    {
        if (runtimeSquareSprite != null)
            return runtimeSquareSprite;

        runtimeSquareTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        runtimeSquareTexture.filterMode = FilterMode.Point;
        runtimeSquareTexture.SetPixels(new[]
        {
            Color.white, Color.white, Color.white, Color.white
        });
        runtimeSquareTexture.Apply();
        runtimeSquareSprite = Sprite.Create(
            runtimeSquareTexture, new Rect(0, 0, 2, 2),
            new Vector2(0.5f, 0.5f), 1f);
        return runtimeSquareSprite;
    }

    private Rect GetBuildPanelRect()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return new Rect(20f, 20f, 188f, 84f);

        Vector3 worldPosition = new Vector3(
            pendingBuildCell.x + 0.5f,
            pendingBuildCell.y + 0.5f,
            0f);
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);

        const float width = 188f;
        const float height = 84f;
        float x = Mathf.Clamp(screenPosition.x - width * 0.5f,
            8f, Mathf.Max(8f, Screen.width - width - 8f));
        float y = Screen.height - screenPosition.y - height - 12f;

        // Если над клеткой мало места, переносим ряд под неё.
        if (y < 8f)
            y = Screen.height - screenPosition.y + 22f;

        y = Mathf.Clamp(y, 8f, Mathf.Max(8f, Screen.height - height - 8f));
        return new Rect(x, y, width, height);
    }

    private Rect GetTowerPanelRect()
    {
        Rect row = GetBuildPanelRect();
        // Захватываем заголовок и подписи под тремя кнопками, чтобы клик
        // по тексту не проходил сквозь интерфейс в игровое поле.
        return new Rect(row.x, row.y - 22f, row.width, row.height + 38f);
    }

    private void OnGUI()
    {
        if (!gameStarted && !instructionsOpen && string.IsNullOrEmpty(statusMessage))
            return;

        // IMGUI использует исходный TTF из того же SDF-шрифта TDA.
        // TMP-элементы сцены получают сам TMP_FontAsset в SceneBuilder.
        if (uiFontAsset != null && uiFontAsset.sourceFontFile != null)
            GUI.skin.font = uiFontAsset.sourceFontFile;

        DrawHud();

        if (instructionsOpen)
        {
            DrawInstructions();
            return;
        }

        if (buildMenuOpen)
            DrawBuildMenu();

        if (selectedTower != null)
            DrawSelectedTowerPanel();

        if (Time.time < phraseUntil && grandpaTransform != null)
            DrawGrandpaPhrase();

        if (Time.time < statusUntil && !string.IsNullOrEmpty(statusMessage))
        {
            GUIStyle statusStyle = new GUIStyle(GUI.skin.box);
            statusStyle.fontSize = 18;
            statusStyle.alignment = TextAnchor.MiddleCenter;
            float statusY = Mathf.Clamp(154f, 8f, Mathf.Max(8f, Screen.height - 50f));
            Rect statusRect = new Rect(
                Screen.width * 0.5f - 220f, statusY, 440f, 42f);
            GUI.Box(statusRect, statusMessage, statusStyle);
        }

        if (gameOver || victory)
            DrawEndScreen();
    }

    private void DrawHud()
    {
        GUIStyle heading = new GUIStyle(GUI.skin.label);
        heading.fontSize = 22;
        heading.fontStyle = FontStyle.Bold;
        heading.normal.textColor = Color.white;

        DrawGrandpaHealthBar();

        Rect moneyPanel = new Rect(Screen.width - 235f, 18f, 215f, 55f);
        GUI.Box(moneyPanel, GUIContent.none);
        GUI.Label(new Rect(moneyPanel.x + 14f, moneyPanel.y + 10f, 190f, 35f),
            "Грибочки: " + mushrooms, heading);

        int totalWaves = waveSpawner != null ? waveSpawner.TotalWaves : 15;
        Rect wavePanel = new Rect(Screen.width * 0.5f - 110f, 18f, 220f, 48f);
        DrawSprite(wavePanel, waveCountBackgroundSprite);
        string waveText = wavesFinished
            ? "Волны: " + completedWaves + " / " + totalWaves
            : !gameStarted ? "Подготовка" :
                "Волна: " + currentWave + " / " + totalWaves;
        GUI.Label(new Rect(wavePanel.x + 8f, wavePanel.y + 5f, wavePanel.width - 16f, 38f),
            waveText, heading);

        if (countdownActive)
        {
            Rect timerPanel = new Rect(Screen.width * 0.5f - 120f, 72f, 240f, 76f);
            DrawSprite(timerPanel, timerBackgroundSprite);

            GUIStyle timerCaption = new GUIStyle(GUI.skin.label);
            timerCaption.fontSize = 12;
            timerCaption.fontStyle = FontStyle.Bold;
            timerCaption.alignment = TextAnchor.MiddleCenter;
            timerCaption.normal.textColor = Color.white;
            GUI.Label(new Rect(timerPanel.x + 10f, timerPanel.y + 5f, timerPanel.width - 20f, 20f),
                "ОСТАЛОСЬ ВРЕМЕНИ",
                timerCaption);

            GUIStyle timerNumber = new GUIStyle(heading);
            timerNumber.fontSize = 25;
            timerNumber.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(timerPanel.x + 10f, timerPanel.y + 28f, timerPanel.width - 20f, 39f),
                Mathf.CeilToInt(Mathf.Max(0f, countdown)).ToString() + " сек.", timerNumber);
        }
    }

    private void DrawInstructions()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.64f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Тот же лист бумаги, который используется в Boss при чтении книг.
        Rect page = new Rect(
            Screen.width * 0.5f - 325f,
            Screen.height * 0.5f - 325f,
            650f, 650f);
        DrawSprite(page, instructionBookSprite);

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 25;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = new Color(0.25f, 0.2f, 0.12f, 1f);
        GUI.Label(new Rect(page.x + 76f, page.y + 62f, page.width - 152f, 44f),
            "КАК ЗАЩИЩАТЬ ДЕДА", title);

        GUIStyle text = new GUIStyle(GUI.skin.label);
        text.fontSize = 17;
        text.wordWrap = true;
        text.richText = true;
        text.normal.textColor = new Color(0.22f, 0.18f, 0.12f, 1f);
        GUI.Label(new Rect(page.x + 108f, page.y + 120f, page.width - 216f, 392f),
            "• Призраки идут по дорожке к деду. Если доберутся — отнимут у него здоровье.\n\n" +
            "• Нажми на свободную клетку рядом с дорожкой, чтобы построить башню.\n\n" +
            "• Обычная башня стоит 1 грибочек, пулемётная — 2, пушечная — 4.\n\n" +
            "• Нажми на башню, чтобы увидеть её радиус, продать или улучшить.\n\n" +
            "• Улучшение увеличивает урон и скорострельность. За каждого призрака выпадает грибочек.\n\n" +
            "• Всего 15 волн. Первая начнётся через 30 секунд; между волнами — 15 секунд.",
            text);

        GUIStyle button = new GUIStyle(GUI.skin.button);
        button.fontSize = 17;
        button.fontStyle = FontStyle.Bold;
        Rect closeRect = new Rect(page.x + 120f, page.yMax - 88f, page.width - 240f, 48f);
        if (GUI.Button(closeRect, "Понятно, начинаем подготовку", button))
            instructionsOpen = false;
    }

    private void DrawBuildMenu()
    {
        Rect row = GetBuildPanelRect();
        const float buttonSize = 52f;
        const float gap = 10f;

        DrawBuildButton(
            new Rect(row.x, row.y, buttonSize, buttonSize),
            TowerDefenceTowerType.Standard, "Обычная", "О", standardStats.mushroomCost);
        DrawBuildButton(
            new Rect(row.x + buttonSize + gap, row.y, buttonSize, buttonSize),
            TowerDefenceTowerType.MachineGun, "Пулемёт", "П", machineGunStats.mushroomCost);
        DrawBuildButton(
            new Rect(row.x + (buttonSize + gap) * 2f, row.y, buttonSize, buttonSize),
            TowerDefenceTowerType.Cannon, "Пушка", "К", cannonStats.mushroomCost);
    }

    private void DrawBuildButton(
        Rect rect,
        TowerDefenceTowerType type,
        string label,
        string symbol,
        int cost)
    {
        bool canAfford = mushrooms >= cost;
        GUI.color = canAfford ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.8f);
        DrawSprite(rect, buildButtonSprite);
        GUI.color = Color.white;

        GUIStyle hitArea = new GUIStyle(GUI.skin.button)
        {
            normal = { background = null },
            hover = { background = null },
            active = { background = null },
            focused = { background = null },
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };

        GUIStyle symbolStyle = new GUIStyle(GUI.skin.label);
        symbolStyle.fontSize = 22;
        symbolStyle.fontStyle = FontStyle.Bold;
        symbolStyle.alignment = TextAnchor.MiddleCenter;
        symbolStyle.normal.textColor = canAfford ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        GUI.Label(rect, symbol, symbolStyle);

        GUIStyle priceStyle = new GUIStyle(GUI.skin.label);
        priceStyle.fontSize = 12;
        priceStyle.alignment = TextAnchor.MiddleCenter;
        priceStyle.wordWrap = true;
        priceStyle.normal.textColor = canAfford ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
        GUI.Label(new Rect(rect.x - 5f, rect.yMax + 1f, rect.width + 10f, 28f),
            label + "\n" + cost + " гриб.", priceStyle);

        GUI.enabled = canAfford;
        if (GUI.Button(rect, GUIContent.none, hitArea))
            TryBuyTower(type);
        GUI.enabled = true;
    }

    private void DrawSelectedTowerPanel()
    {
        if (selectedTower == null)
            return;

        Rect row = GetTowerPanelRect();
        const float buttonSize = 52f;
        const float gap = 10f;

        Rect sellRect = new Rect(row.x, row.y, buttonSize, buttonSize);
        Rect upgradeRect = new Rect(row.x + buttonSize + gap, row.y, buttonSize, buttonSize);
        Rect statsRect = new Rect(row.x + (buttonSize + gap) * 2f, row.y, buttonSize, buttonSize);

        DrawSprite(sellRect, buildButtonSprite);
        DrawSprite(upgradeRect, buildButtonSprite);
        DrawSprite(statsRect, buildButtonSprite);
        DrawSprite(new Rect(sellRect.x + 13f, sellRect.y + 13f, 26f, 26f), runtimeSellIcon);
        DrawSprite(new Rect(upgradeRect.x + 13f, upgradeRect.y + 13f, 26f, 26f), runtimeUpgradeIcon);

        TowerDefenceTowerStats stats = GetStats(selectedTower.Type);
        float currentDamage = stats.damage *
            (1f + (selectedTower.Level - 1) * upgradeDamagePerLevel);
        float attacksPerSecond = 1f / Mathf.Max(0.05f, stats.attackInterval);

        GUIStyle info = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        info.normal.textColor = Color.white;
        GUI.Label(statsRect,
            "УРОН\n" + currentDamage.ToString("F1") +
            "\nСКОР. " + attacksPerSecond.ToString("F1") + "/с",
            info);

        GUIStyle priceStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        priceStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(sellRect.x - 6f, sellRect.yMax + 1f, sellRect.width + 12f, 30f),
            "Продать\n+" + selectedTower.GetSellRefund(), priceStyle);

        int upgradeCost = selectedTower.GetNextUpgradeCost();
        bool canUpgrade = selectedTower.CanUpgrade && mushrooms >= upgradeCost;
        GUI.Label(new Rect(upgradeRect.x - 6f, upgradeRect.yMax + 1f, upgradeRect.width + 12f, 30f),
            selectedTower.CanUpgrade ? "Улучшить\n" + upgradeCost : "Макс. уровень",
            priceStyle);

        // Третья кнопка показывает параметры башни прямо внутри квадрата.

        GUIStyle hitArea = new GUIStyle(GUI.skin.button)
        {
            normal = { background = null },
            hover = { background = null },
            active = { background = null },
            focused = { background = null },
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };

        if (GUI.Button(sellRect, GUIContent.none, hitArea))
            SellSelectedTower();

        GUI.enabled = canUpgrade;
        if (GUI.Button(upgradeRect, GUIContent.none, hitArea))
            TryUpgradeSelectedTower();
        GUI.enabled = true;

        // Third tile is an information tile, intentionally not a purchase action.
        GUIStyle towerNameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        towerNameStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(row.x, row.y - 21f, row.width, 18f),
            selectedTower.GetDisplayName() + " • ур. " + selectedTower.Level,
            towerNameStyle);
    }

    private void DrawGrandpaPhrase()
    {
        if (grandpaTransform == null || Camera.main == null)
            return;

        Vector3 screen = Camera.main.WorldToScreenPoint(
            grandpaTransform.position + Vector3.up * 1.2f);
        if (screen.z < 0f)
            return;

        Rect bubble = new Rect(
            screen.x - 170f,
            Screen.height - screen.y - 74f,
            340f, 62f);
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 16;
        style.wordWrap = true;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Box(bubble, grandpaPhrase, style);
    }

    private void DrawEndScreen()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 34;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = victory
            ? new Color(0.65f, 1f, 0.62f, 1f)
            : new Color(1f, 0.55f, 0.5f, 1f);
        GUI.Label(new Rect(Screen.width * 0.5f - 400f,
            Screen.height * 0.5f - 55f, 800f, 110f), statusMessage, style);
    }

    private void DrawGrandpaHealthBar()
    {
        if (grandpaTransform == null || Camera.main == null)
            return;

        Vector3 screen = Camera.main.WorldToScreenPoint(
            grandpaTransform.position + Vector3.down * 0.68f);
        if (screen.z < 0f)
            return;

        const float width = 126f;
        const float height = 18f;
        Rect bar = new Rect(screen.x - width * 0.5f,
            Screen.height - screen.y - height * 0.5f, width, height);

        DrawSprite(bar, grandpaHpBackgroundSprite);

        // На текстуре по центру нарисована мордочка лягушки. Заливаем
        // две части полоски по бокам, не закрывая саму картинку.
        float progress = Mathf.Clamp01(grandpaHealth / Mathf.Max(1f, grandpaMaximumHealth));
        float segmentWidth = (bar.width - 46f) * 0.5f;
        Rect leftSegment = new Rect(bar.x + 10f, bar.y + 6f, segmentWidth, 5f);
        Rect rightSegment = new Rect(bar.x + 36f + segmentWidth, bar.y + 6f, segmentWidth, 5f);

        GUI.color = new Color(0.16f, 0.12f, 0.12f, 0.9f);
        GUI.DrawTexture(leftSegment, Texture2D.whiteTexture);
        GUI.DrawTexture(rightSegment, Texture2D.whiteTexture);

        float totalFill = progress * segmentWidth * 2f;
        GUI.color = new Color(0.35f, 0.88f, 0.34f, 1f);
        GUI.DrawTexture(new Rect(leftSegment.x, leftSegment.y,
            Mathf.Min(segmentWidth, totalFill), leftSegment.height), Texture2D.whiteTexture);
        float rightFill = Mathf.Clamp(totalFill - segmentWidth, 0f, segmentWidth);
        GUI.DrawTexture(new Rect(rightSegment.x, rightSegment.y,
            rightFill, rightSegment.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle hpStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        hpStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(bar.x, bar.yMax + 1f, bar.width, 13f),
            "HP " + Mathf.CeilToInt(grandpaHealth) + " / " +
            Mathf.CeilToInt(grandpaMaximumHealth), hpStyle);
    }

    private Sprite CreatePixelActionIcon(bool trash, out Texture2D texture)
    {
        const int size = 32;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = trash ? "TDS Trash Icon" : "TDS Upgrade Arrow Icon";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 ink = new Color32(255, 239, 207, 255);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        if (trash)
        {
            for (int x = 7; x <= 24; x++)
                pixels[25 * size + x] = ink;
            for (int x = 10; x <= 21; x++)
                pixels[27 * size + x] = ink;
            for (int y = 8; y <= 24; y++)
            {
                for (int x = 10; x <= 21; x++)
                    pixels[y * size + x] = ink;
                pixels[y * size + 9] = ink;
                pixels[y * size + 22] = ink;
            }
            for (int x = 12; x <= 19; x++)
                pixels[6 * size + x] = ink;
            for (int y = 3; y <= 5; y++)
                for (int x = 14; x <= 17; x++)
                    pixels[y * size + x] = ink;
            for (int y = 11; y <= 21; y++)
            {
                pixels[y * size + 14] = new Color32(85, 58, 45, 255);
                pixels[y * size + 18] = new Color32(85, 58, 45, 255);
            }
        }
        else
        {
            // Стрелка вверх: стержень снизу, наконечник расширяется кверху.
            for (int y = 4; y <= 18; y++)
                for (int x = 14; x <= 17; x++)
                    pixels[y * size + x] = ink;

            for (int y = 14; y <= 28; y++)
            {
                int halfWidth = y - 14;
                for (int x = 16 - halfWidth; x <= 16 + halfWidth; x++)
                    pixels[y * size + x] = ink;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f);
    }

    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        Texture2D texture = sprite.texture;
        Rect source = sprite.textureRect;
        Rect uv = new Rect(
            source.x / texture.width,
            source.y / texture.height,
            source.width / texture.width,
            source.height / texture.height);
        GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
    }
}
