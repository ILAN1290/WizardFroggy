using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

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
    [SerializeField] private Sprite bossGhostSprite;

    [Header("Звуки Tower Defence")]
    [SerializeField] private AudioClip waveStartSound;
    [SerializeField] private AudioClip waveEndSound;
    [SerializeField] private AudioClip ghostHitSound;
    [SerializeField] private AudioClip towerShotSound;
    [SerializeField] private AudioClip cannonImpactSound;
    [SerializeField] private AudioClip purchaseSound;
    [SerializeField] private AudioClip sellSound;
    [SerializeField] private AudioClip upgradeSound;
    [Header("Обучение и окружение")]
    [SerializeField] private AudioClip tutorialOpenSound;
    [SerializeField] private AudioClip tutorialCloseSound;
    [SerializeField] private AudioClip ambientSound;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.18f;
    [SerializeField] private AudioClip dialogueVoiceClip;

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
    [SerializeField] private Sprite instructionButtonSprite;
    [SerializeField] private Sprite upgradePlusButtonSprite;
    [SerializeField] private Sprite sellMinusButtonSprite;
    [SerializeField] private Sprite buildButtonSprite;
    [SerializeField] private Sprite timerBackgroundSprite;
    [SerializeField] private Sprite statusBackgroundSprite;
    [SerializeField] private Sprite waveCountBackgroundSprite;
    [SerializeField] private Sprite grandpaHpBackgroundSprite;
    [SerializeField] private Sprite sellTowerIconSprite;
    [SerializeField] private Sprite upgradeTowerIconSprite;
    [SerializeField] private Sprite skipWaveIconSprite;
    [SerializeField, Range(0.1f, 1f)] private float mushroomToGhostHeight = 0.5f;
    [SerializeField, Min(0)] private int startingMushrooms = 5;
    [SerializeField] private Item[] waveRewardItems = new Item[0];
    [SerializeField, Range(0f, 1f)] private float waveRewardDropChance = 0.7f;
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
    [SerializeField, Min(1f)] private float blackGhostHealth = 180f;
    [SerializeField, Min(0.1f)] private float blackGhostSpeedMultiplier = 0.8f;
    [SerializeField, Min(0.1f)] private float blackGhostSize = 1.15f;
    [SerializeField, Min(1f)] private float bossGhostHealth = 10000f;
    [SerializeField, Min(0.05f)] private float bossGhostSpeed = 0.33f;
    [SerializeField, Min(0.1f)] private float bossGhostSize = 2.2f;

    [Header("Параметры башен")]
    [SerializeField] private TowerDefenceTowerStats machineGunStats = new TowerDefenceTowerStats
    {
        mushroomCost = 2, damage = 2.2f, attackInterval = 0.28f,
        range = 2.325f, projectileSpeed = 7f, projectileSize = 0.105f
    };
    [SerializeField] private TowerDefenceTowerStats standardStats = new TowerDefenceTowerStats
    {
        mushroomCost = 1, damage = 5f, attackInterval = 0.88f,
        range = 1.575f, projectileSpeed = 5.4f, projectileSize = 0.14f
    };
    [SerializeField] private TowerDefenceTowerStats cannonStats = new TowerDefenceTowerStats
    {
        mushroomCost = 4, damage = 18f, attackInterval = 1.5f,
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
        "НЕ ШУМИТЕ, ПРОКАЗНИКИ!",
        "Шапки! ШАПКИ надевайте!",
        "Расшумелись...",
        "Оххх, спинааа...",
        "Ла-ла-ла-лаААААА",
        "Ой, куда ж делся... Куда же он...",
        "Ой, ой-ой-ой!",
        "Непорядок...",
        "Иже иси на небеси...",
        "Ох помру я так",
        "Ломайте, ломайте! Анархия - мать порядка!"
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
    private AudioSource soundSource;
    private AudioSource ambientSource;
    private Canvas grandpaHealthCanvas;
    private Image grandpaHealthFill;
    private TMP_Text grandpaHealthLabel;
    private readonly Dictionary<Vector3Int, SpriteRenderer> buildCellHighlights =
        new Dictionary<Vector3Int, SpriteRenderer>();
    private Sprite runtimeSkipWaveIcon;
    private Texture2D runtimeSkipWaveIconTexture;

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
        mushrooms = startingMushrooms;
        waveSpawner = GetComponent<TowerDefenceWaveSpawner>();
        soundSource = GetComponent<AudioSource>();
        if (soundSource == null)
            soundSource = gameObject.AddComponent<AudioSource>();
        soundSource.playOnAwake = false;
        soundSource.loop = false;
        soundSource.spatialBlend = 0f;

        if (ambientSound != null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.clip = ambientSound;
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.spatialBlend = 0f;
            ambientSource.volume = Mathf.Clamp01(ambientVolume);
            ambientSource.Play();
        }

        if (sellTowerIconSprite == null)
            runtimeSellIcon = CreatePixelActionIcon(true, out runtimeSellIconTexture);
        else
            runtimeSellIcon = sellTowerIconSprite;
        if (upgradeTowerIconSprite == null)
            runtimeUpgradeIcon = CreatePixelActionIcon(false, out runtimeUpgradeIconTexture);
        else
            runtimeUpgradeIcon = upgradeTowerIconSprite;
        if (skipWaveIconSprite == null)
            runtimeSkipWaveIcon = CreateSkipWaveIcon(out runtimeSkipWaveIconTexture);
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
        if (runtimeSkipWaveIcon != null)
            Destroy(runtimeSkipWaveIcon);
        if (runtimeSkipWaveIconTexture != null)
            Destroy(runtimeSkipWaveIconTexture);
        if (grandpaHealthCanvas != null)
            Destroy(grandpaHealthCanvas.gameObject);
        ClearBuildCellHighlights();
    }

    private void Update()
    {
        UpdateGrandpaHealthWorldCanvas();
        if (gameOver || victory)
        {
            ClearBuildCellHighlights();
            return;
        }
        if (gameStarted && !instructionsOpen)
            UpdateBuildCellHighlights();

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
        EnsureGrandpaHealthWorldCanvas();
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
        PlaySound(tutorialOpenSound, 0.55f);
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
            int expectedGhosts = waveSpawner.GetEnemyCountForWave(wave) +
                (wave == 15 ? 1 : 0);
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
            PlaySound(waveStartSound, 0.55f);

            int spawnedBeforeWave = totalSpawnedGhosts;
            yield return StartCoroutine(waveSpawner.SpawnWave(wave, this));

            if (gameOver)
            {
                waveLoopRunning = false;
                yield break;
            }

            if (wave == 15)
                SpawnGhost(TowerDefenceGhostType.Boss);

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

            if (!gameOver)
                PlaySound(waveEndSound, 0.5f);

            if (gameOver)
            {
                waveLoopRunning = false;
                yield break;
            }

            completedWaves = wave;
            CreateWaveRewardDrop(wave);

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
        // Бесплатная стартовая башня остаётся, а игрок начинает с 5 грибочками.
        Vector3Int cell = new Vector3Int(5, -1, 0);
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
            case TowerDefenceGhostType.Boss:
                health = bossGhostHealth;
                speed = bossGhostSpeed;
                size = bossGhostSize;
                sprite = bossGhostSprite != null ? bossGhostSprite :
                    (blackGhostSprite != null ? blackGhostSprite : whiteGhostSprite);
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
        int mushroomReward = ghost != null &&
            ghost.Type == TowerDefenceGhostType.Boss ? 3 : 1;
        CreateMushroomDrop(position, mushroomReward);
        ShowStatus(mushroomReward > 1
            ? "Золотой босс повержен! Выпало 3 грибочка!"
            : "Выпал грибочек — подбери его!");
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

    private void CreateMushroomDrop(Vector3 position, int amount)
    {
        if (mushroomSprite == null)
            return;

        amount = Mathf.Max(1, amount);
        float mushroomHeight = mushroomSprite.bounds.size.y;
        float whiteGhostHeight = 0.72f * Mathf.Max(0.1f, whiteGhostSize);
        float targetMushroomHeight = whiteGhostHeight * mushroomToGhostHeight;
        float mushroomScale = mushroomHeight > 0.001f
            ? targetMushroomHeight / mushroomHeight
            : targetMushroomHeight;

        // Bosses visibly scatter three separate mushrooms rather than a single
        // pickup that only increments the balance by three.
        for (int i = 0; i < amount; i++)
        {
            Vector2 scatter = amount == 1
                ? Vector2.zero
                : new Vector2((i - 1) * 0.28f, (i % 2) * 0.12f);

            GameObject drop = new GameObject("Выпавший грибочек");
            drop.transform.position = position +
                new Vector3(scatter.x, 0.12f + scatter.y, 0f);
            drop.transform.localScale = Vector3.one * mushroomScale;

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
                .Initialize(this, 20f, 1);
        }
    }

    private void CreateWaveRewardDrop(int waveNumber)
    {
        if (waveRewardItems == null || waveRewardItems.Length == 0 ||
            Random.value > waveRewardDropChance)
            return;

        List<Item> availableItems = new List<Item>();
        for (int i = 0; i < waveRewardItems.Length; i++)
            if (waveRewardItems[i] != null && waveRewardItems[i].icon != null)
                availableItems.Add(waveRewardItems[i]);

        if (availableItems.Count == 0)
            return;

        Item reward = availableItems[Random.Range(0, availableItems.Count)];
        Vector2 dropPosition = new Vector2(0f, -2.5f) +
            Random.insideUnitCircle * 0.55f;

        GameObject pickup = new GameObject("Награда волны " + waveNumber + " • " + reward.itemName);
        pickup.transform.position = new Vector3(dropPosition.x, dropPosition.y, -0.05f);

        GameObject visual = new GameObject("Иконка награды");
        visual.transform.SetParent(pickup.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = reward.icon;
        renderer.color = Color.white;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 36;

        Vector2 spriteSize = reward.icon.bounds.size;
        float targetSize = 0.72f;
        visual.transform.localScale = new Vector3(
            targetSize / Mathf.Max(0.001f, spriteSize.x),
            targetSize / Mathf.Max(0.001f, spriteSize.y),
            1f);

        CircleCollider2D trigger = pickup.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.38f;

        pickup.AddComponent<RoguelikePickup>().SetupItem(reward, 1);
    }

    public void CollectMushroom(int amount = 1)
    {
        amount = Mathf.Max(1, amount);
        mushrooms += amount;
        ShowStatus("+" + amount + (amount == 1 ? " грибочек" : " грибочка"));
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
        if (splashRadius > 0f)
        {
            float effectRadius = Mathf.Clamp(splashRadius * 0.65f, 0.28f, 0.55f);
            ExplosionEffect.Spawn(
                impactPosition,
                effectRadius,
                new Color(1f, 0.65f, 0.25f, 0.95f));
            PlaySound(cannonImpactSound, 0.22f);
        }

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

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null || soundSource == null)
            return;
        soundSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public void PlayGhostHitSound()
    {
        // Sound of a hit intentionally disabled per user request.
    }

    public void PlayTowerShot(TowerDefenceTowerType type)
    {
        PlaySound(towerShotSound,
            type == TowerDefenceTowerType.Cannon ? 0.18f : 0.09f);
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

        if (countdownActive && GetSkipWaveButtonRect().Contains(guiPosition))
            return;

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
        PlaySound(purchaseSound, 0.55f);
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
        PlaySound(upgradeSound, 0.6f);
        ShowStatus("Башня улучшена до " + selectedTower.Level + " уровня");
    }

    private void SellSelectedTower()
    {
        if (selectedTower == null)
            return;

        TowerDefenceTower tower = selectedTower;
        int refund = tower.GetSellRefund();
        mushrooms += refund;
        PlaySound(sellSound, 0.55f);

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
        line.sortingOrder = 10;
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
            return new Rect(20f, 20f, 320f, 148f);

        Vector3 worldPosition = new Vector3(
            pendingBuildCell.x + 0.5f,
            pendingBuildCell.y + 0.5f,
            0f);
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);

        const float width = 320f;
        const float height = 148f;
        float x = Mathf.Clamp(screenPosition.x - width * 0.5f,
            8f, Mathf.Max(8f, Screen.width - width - 8f));
        float y = Screen.height - screenPosition.y - height - 12f;

        if (y < 8f)
            y = Screen.height - screenPosition.y + 22f;

        y = Mathf.Clamp(y, 8f, Mathf.Max(8f, Screen.height - height - 8f));
        return new Rect(x, y, width, height);
    }

    private Rect GetTowerPanelRect()
    {
        Rect row = GetBuildPanelRect();
        return new Rect(row.x, row.y - 36f, row.width, row.height + 70f);
    }

    private void OnGUI()
    {
        // Keep the health overlay behind inventory/build overlays.
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
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontSize = 18;
            statusStyle.fontStyle = FontStyle.Bold;
            statusStyle.alignment = TextAnchor.MiddleCenter;
            statusStyle.normal.textColor = Color.white;
            float statusY = Mathf.Clamp(154f, 8f, Mathf.Max(8f, Screen.height - 50f));
            Rect statusRect = new Rect(
                Screen.width * 0.5f - 220f, statusY, 440f, 42f);
            DrawSprite(statusRect, statusBackgroundSprite);
            GUI.Label(statusRect, statusMessage, statusStyle);
        }

        if (gameOver || victory)
            DrawEndScreen();
    }

    private void DrawHud()
    {
        GUIStyle heading = new GUIStyle(GUI.skin.label);
        heading.fontSize = 26;
        heading.fontStyle = FontStyle.Bold;
        heading.normal.textColor = Color.white;


        Rect moneyPanel = new Rect(Screen.width - 285f, 18f, 265f, 64f);
        GUI.Box(moneyPanel, GUIContent.none);
        DrawSprite(
            new Rect(moneyPanel.x + 12f, moneyPanel.y + 7f, 50f, 50f),
            mushroomSprite);
        GUI.Label(new Rect(moneyPanel.x + 72f, moneyPanel.y + 10f, 175f, 44f),
            mushrooms.ToString(), heading);

        int totalWaves = waveSpawner != null ? waveSpawner.TotalWaves : 15;
        Rect wavePanel = new Rect(Screen.width * 0.5f - 135f, 18f, 270f, 58f);
        DrawSprite(wavePanel, waveCountBackgroundSprite);
        string waveText = wavesFinished
            ? "Волны: " + completedWaves + " / " + totalWaves
            : !gameStarted ? "Подготовка" :
                "Волна: " + currentWave + " / " + totalWaves;
        GUI.Label(new Rect(wavePanel.x + 8f, wavePanel.y + 5f, wavePanel.width - 16f, 38f),
            waveText, heading);

        if (countdownActive)
        {
            // Пропорции фонового спрайта длинные, поэтому панель не растягиваем по высоте.
            Rect timerPanel = new Rect(Screen.width * 0.5f - 165f, 84f, 330f, 62f);
            DrawSprite(timerPanel, timerBackgroundSprite);

            GUIStyle timerCaption = new GUIStyle(GUI.skin.label);
            timerCaption.fontSize = 15;
            timerCaption.fontStyle = FontStyle.Bold;
            timerCaption.alignment = TextAnchor.MiddleCenter;
            timerCaption.normal.textColor = Color.white;
            GUI.Label(new Rect(timerPanel.x + 10f, timerPanel.y + 3f, timerPanel.width - 20f, 19f),
                "ОСТАЛОСЬ ВРЕМЕНИ",
                timerCaption);

            GUIStyle timerNumber = new GUIStyle(heading);
            timerNumber.fontSize = 28;
            timerNumber.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(timerPanel.x + 10f, timerPanel.y + 21f, timerPanel.width - 20f, 36f),
                Mathf.CeilToInt(Mathf.Max(0f, countdown)).ToString() + " сек.", timerNumber);
            DrawSkipWaveButton(timerPanel);
        }
    }

    private void UpdateBuildCellHighlights()
    {
        const int starterX = 5;
        const int starterY = -1;
        HashSet<Vector3Int> desired = new HashSet<Vector3Int>();
        desired.Add(new Vector3Int(starterX, starterY, 0));

        // Use the same rule as CanPlaceTowerAt: only free cells within the
        // road-side build band are marked. The starter tower cell is included.
        for (int y = -5; y <= 0; y++)
        {
            for (int x = -8; x <= 7; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (cell.x == starterX && cell.y == starterY)
                    continue;
                if (CanPlaceTowerAt(cell))
                    desired.Add(cell);
            }
        }

        List<Vector3Int> stale = new List<Vector3Int>();
        foreach (KeyValuePair<Vector3Int, SpriteRenderer> pair in buildCellHighlights)
            if (!desired.Contains(pair.Key) || pair.Value == null)
                stale.Add(pair.Key);
        for (int i = 0; i < stale.Count; i++)
        {
            if (buildCellHighlights.TryGetValue(stale[i], out SpriteRenderer oldRenderer) &&
                oldRenderer != null)
                Destroy(oldRenderer.gameObject);
            buildCellHighlights.Remove(stale[i]);
        }

        Sprite square = GetRuntimeSquareSprite();
        Vector2 spriteSize = square != null ? square.bounds.size : Vector2.one;
        foreach (Vector3Int cell in desired)
        {
            if (buildCellHighlights.ContainsKey(cell) || square == null)
                continue;
            GameObject marker = new GameObject("Buildable Cell • " + cell.x + "_" + cell.y);
            marker.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0.05f);
            marker.transform.localScale = new Vector3(
                1f / Mathf.Max(0.001f, spriteSize.x),
                1f / Mathf.Max(0.001f, spriteSize.y), 1f);
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(0.08f, 0.08f, 0.08f, 0.68f);
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 28;
            buildCellHighlights[cell] = renderer;
        }
    }

    private void ClearBuildCellHighlights()
    {
        foreach (SpriteRenderer renderer in buildCellHighlights.Values)
            if (renderer != null)
                Destroy(renderer.gameObject);
        buildCellHighlights.Clear();
    }

    private void EnsureGrandpaHealthWorldCanvas()
    {
        if (grandpaTransform == null || grandpaHealthCanvas != null)
            return;

        GameObject root = new GameObject(
            "Grandpa Health Bar • World Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(204f, 26f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        grandpaHealthCanvas = root.GetComponent<Canvas>();
        grandpaHealthCanvas.renderMode = RenderMode.WorldSpace;
        grandpaHealthCanvas.worldCamera = Camera.main;
        grandpaHealthCanvas.overrideSorting = true;
        grandpaHealthCanvas.sortingLayerName = "Default";
        grandpaHealthCanvas.sortingOrder = -100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;
        scaler.dynamicPixelsPerUnit = 10f;

        GameObject trackObject = new GameObject(
            "Health Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackObject.transform.SetParent(root.transform, false);
        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.anchorMin = Vector2.zero;
        trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = Vector2.zero;
        trackRect.offsetMax = Vector2.zero;
        Image track = trackObject.GetComponent<Image>();
        track.sprite = grandpaHpBackgroundSprite;
        track.color = grandpaHpBackgroundSprite != null
            ? Color.white
            : new Color(0.1f, 0.07f, 0.06f, 0.98f);
        track.raycastTarget = false;

        GameObject fillObject = new GameObject(
            "Health Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(root.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
        grandpaHealthFill = fillObject.GetComponent<Image>();
        grandpaHealthFill.color = new Color(0.29f, 0.78f, 0.31f, 1f);
        grandpaHealthFill.type = Image.Type.Filled;
        grandpaHealthFill.fillMethod = Image.FillMethod.Horizontal;
        grandpaHealthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        grandpaHealthFill.raycastTarget = false;

        GameObject labelObject = new GameObject(
            "Health Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        grandpaHealthLabel = labelObject.GetComponent<TextMeshProUGUI>();
        grandpaHealthLabel.text = "";
        grandpaHealthLabel.alignment = TextAlignmentOptions.Center;
        grandpaHealthLabel.fontSize = 14f;
        grandpaHealthLabel.fontStyle = FontStyles.Bold;
        grandpaHealthLabel.color = Color.white;
        if (uiFontAsset != null)
            grandpaHealthLabel.font = uiFontAsset;
        grandpaHealthLabel.raycastTarget = false;

        UpdateGrandpaHealthWorldCanvas();
    }

    private void UpdateGrandpaHealthWorldCanvas()
    {
        if (grandpaTransform == null)
            return;
        if (grandpaHealthCanvas == null)
        {
            EnsureGrandpaHealthWorldCanvas();
            if (grandpaHealthCanvas == null)
                return;
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            grandpaHealthCanvas.worldCamera = camera;
            if (camera.orthographic && camera.pixelHeight > 0)
            {
                float pixelsPerWorldUnit = camera.pixelHeight /
                    Mathf.Max(0.01f, camera.orthographicSize * 2f);
                grandpaHealthCanvas.transform.localScale =
                    Vector3.one / Mathf.Max(1f, pixelsPerWorldUnit);
                grandpaHealthCanvas.transform.rotation = camera.transform.rotation;
            }
        }

        grandpaHealthCanvas.transform.position =
            grandpaTransform.position + Vector3.down * 0.68f;
        float ratio = Mathf.Clamp01(grandpaHealth / Mathf.Max(1f, grandpaMaximumHealth));
        if (grandpaHealthFill != null)
        {
            grandpaHealthFill.fillAmount = ratio;
            grandpaHealthFill.color = ratio > 0.55f
                ? new Color(0.29f, 0.78f, 0.31f, 1f)
                : ratio > 0.25f
                    ? new Color(0.95f, 0.68f, 0.2f, 1f)
                    : new Color(0.88f, 0.24f, 0.19f, 1f);
        }
        if (grandpaHealthLabel != null)
            grandpaHealthLabel.text =
                Mathf.CeilToInt(grandpaHealth) + " / " +
                Mathf.CeilToInt(grandpaMaximumHealth);
    }

    private void DrawSkipWaveButton(Rect timerPanel)
    {
        Rect rect = GetSkipWaveButtonRect(timerPanel);
        DrawSprite(rect, buildButtonSprite);
        Sprite icon = skipWaveIconSprite != null ? skipWaveIconSprite : runtimeSkipWaveIcon;
        if (icon != null)
            DrawSprite(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f), icon);

        GUIStyle invisibleButton = new GUIStyle(GUI.skin.button)
        {
            normal = { background = null },
            hover = { background = null },
            active = { background = null },
            focused = { background = null },
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };
        if (GUI.Button(rect, GUIContent.none, invisibleButton))
            countdown = 0f;
    }

    private Rect GetSkipWaveButtonRect()
    {
        Rect panel = new Rect(Screen.width * 0.5f - 165f, 84f, 330f, 62f);
        return GetSkipWaveButtonRect(panel);
    }

    private static Rect GetSkipWaveButtonRect(Rect panel)
    {
        return new Rect(panel.xMax + 10f, panel.y + 3f, 56f, 56f);
    }

    private Sprite CreateSkipWaveIcon(out Texture2D texture)
    {
        const int size = 32;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Tower Defence • Skip Wave";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        Color32 ink = new Color32(255, 242, 210, 255);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);

        // Две пиксельные стрелки вправо — значок ускоренной перемотки.
        for (int step = 0; step <= 10; step++)
        {
            int top = 16 - step;
            int bottom = 16 + step;
            pixels[top * size + 4 + step] = ink;
            pixels[bottom * size + 4 + step] = ink;
            pixels[top * size + 17 + step] = ink;
            pixels[bottom * size + 17 + step] = ink;
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), size);
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

        Rect closeRect = new Rect(
            page.x + 100f, page.yMax - 96f, page.width - 200f, 58f);
        DrawSprite(closeRect, instructionButtonSprite);

        GUIStyle button = new GUIStyle(GUI.skin.button);
        button.fontSize = 19;
        button.fontStyle = FontStyle.Bold;
        button.normal.background = null;
        button.hover.background = null;
        button.active.background = null;
        button.focused.background = null;
        button.normal.textColor = button.hover.textColor = button.active.textColor = button.focused.textColor = Color.white;
        if (GUI.Button(closeRect, "Понятно, начинаем подготовку", button))
        {
            PlaySound(tutorialCloseSound, 0.55f);
            instructionsOpen = false;
        }
    }

    private void DrawBuildMenu()
    {
        Rect row = GetBuildPanelRect();
        const float buttonSize = 88f;
        const float gap = 16f;

        DrawBuildButton(
            new Rect(row.x + 6f, row.y, buttonSize, buttonSize),
            TowerDefenceTowerType.Standard, "Обычная", "О", standardStats.mushroomCost);
        DrawBuildButton(
            new Rect(row.x + buttonSize + gap + 6f, row.y, buttonSize, buttonSize),
            TowerDefenceTowerType.MachineGun, "Пулемёт", "П", machineGunStats.mushroomCost);
        DrawBuildButton(
            new Rect(row.x + (buttonSize + gap) * 2f + 6f, row.y, buttonSize, buttonSize),
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

        GUIStyle symbolStyle = new GUIStyle(GUI.skin.label);
        symbolStyle.fontSize = 34;
        symbolStyle.fontStyle = FontStyle.Bold;
        symbolStyle.alignment = TextAnchor.MiddleCenter;
        symbolStyle.normal.textColor = canAfford ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        GUI.Label(rect, symbol, symbolStyle);

        GUIStyle priceStyle = new GUIStyle(GUI.skin.label);
        priceStyle.fontSize = 18;
        priceStyle.alignment = TextAnchor.MiddleCenter;
        priceStyle.wordWrap = true;
        priceStyle.normal.textColor = canAfford ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
        GUI.Label(new Rect(rect.x - 8f, rect.yMax + 3f, rect.width + 16f, 42f),
            label + "\n" + cost + " гриб.", priceStyle);

        GUIStyle hitArea = new GUIStyle(GUI.skin.button)
        {
            normal = { background = null },
            hover = { background = null },
            active = { background = null },
            focused = { background = null },
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };

        GUI.enabled = canAfford;
        bool clicked = GUI.Button(rect, GUIContent.none, hitArea);
        GUI.enabled = true;

        if (clicked)
            TryBuyTower(type);
    }

    private void DrawSelectedTowerPanel()
    {
        if (selectedTower == null)
            return;

        Rect row = GetTowerPanelRect();
        const float buttonSize = 82f;
        const float gap = 16f;

        Rect sellRect = new Rect(row.x + 6f, row.y, buttonSize, buttonSize);
        Rect upgradeRect = new Rect(row.x + buttonSize + gap + 6f, row.y, buttonSize, buttonSize);
        Rect statsRect = new Rect(row.x + (buttonSize + gap) * 2f + 6f, row.y, buttonSize, buttonSize);

        DrawSprite(sellRect, buildButtonSprite);
        DrawSprite(upgradeRect, buildButtonSprite);
        DrawSprite(statsRect, buildButtonSprite);

        float currentDamage = selectedTower.CurrentDamage;
        float attacksPerSecond = selectedTower.AttacksPerSecond;

        GUIStyle info = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
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
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        priceStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(sellRect.x - 8f, sellRect.yMax + 3f, sellRect.width + 16f, 40f),
            "Продать\n+" + selectedTower.GetSellRefund(), priceStyle);

        int upgradeCost = selectedTower.GetNextUpgradeCost();
        bool canUpgrade = selectedTower.CanUpgrade && mushrooms >= upgradeCost;
        GUI.Label(new Rect(upgradeRect.x - 8f, upgradeRect.yMax + 3f, upgradeRect.width + 16f, 40f),
            selectedTower.CanUpgrade ? "Улучшить\n" + upgradeCost : "Макс. уровень",
            priceStyle);

        GUIStyle hitArea = new GUIStyle(GUI.skin.button)
        {
            normal = { background = null },
            hover = { background = null },
            active = { background = null },
            focused = { background = null },
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };

        bool sellClicked = GUI.Button(sellRect, GUIContent.none, hitArea);
        GUI.enabled = canUpgrade;
        bool upgradeClicked = GUI.Button(upgradeRect, GUIContent.none, hitArea);
        GUI.enabled = true;

        // Draw icons after the invisible hit buttons so the button skin cannot hide them.
        Rect sellIconRect = new Rect(sellRect.x + 16f, sellRect.y + 16f, 50f, 50f);
        Rect upgradeIconRect = new Rect(upgradeRect.x + 16f, upgradeRect.y + 16f, 50f, 50f);
        GUI.color = Color.white;
        DrawSprite(sellIconRect, runtimeSellIcon);
        DrawSprite(upgradeIconRect, runtimeUpgradeIcon);

        if (sellClicked)
            SellSelectedTower();
        else if (upgradeClicked)
            TryUpgradeSelectedTower();

        GUIStyle towerNameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        towerNameStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(row.x, row.y - 30f, row.width, 24f),
            selectedTower != null
                ? selectedTower.GetDisplayName() + " • ур. " + selectedTower.Level
                : string.Empty,
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

        const float width = 204f;
        const float height = 26f;
        Rect outer = new Rect(
            screen.x - width * 0.5f,
            Screen.height - screen.y - height * 0.5f,
            width,
            height);

        // Один непрерывный трек без центрального разрыва и картинки поверх HP.
        GUI.color = new Color(0.12f, 0.08f, 0.07f, 0.96f);
        GUI.DrawTexture(outer, Texture2D.whiteTexture);

        Rect inner = new Rect(outer.x + 3f, outer.y + 3f,
            outer.width - 6f, outer.height - 6f);
        GUI.color = new Color(0.18f, 0.15f, 0.14f, 1f);
        GUI.DrawTexture(inner, Texture2D.whiteTexture);

        float progress = Mathf.Clamp01(
            grandpaHealth / Mathf.Max(1f, grandpaMaximumHealth));
        Color fillColor = progress > 0.55f
            ? new Color(0.29f, 0.78f, 0.31f, 1f)
            : progress > 0.25f
                ? new Color(0.95f, 0.68f, 0.2f, 1f)
                : new Color(0.88f, 0.24f, 0.19f, 1f);

        Rect fill = new Rect(inner.x, inner.y,
            inner.width * progress, inner.height);
        GUI.color = fillColor;
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle hpStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        hpStyle.normal.textColor = Color.white;
        GUI.Label(outer,
            Mathf.CeilToInt(grandpaHealth) + " / " +
            Mathf.CeilToInt(grandpaMaximumHealth),
            hpStyle);
    }

    private Sprite CreatePixelActionIcon(bool trash, out Texture2D texture)
    {
        // Geometry matches the save menu's upload-arrow and delete icons.
        const int size = 32;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = trash ? "Save UI • Delete" : "Save UI • Save";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 ink = new Color32(255, 239, 207, 255);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        if (!trash)
        {
            for (int y = 18; y < 29; y++)
                for (int x = 13; x <= 18; x++)
                    pixels[y * size + x] = ink;

            for (int row = 0; row < 14; row++)
            {
                int y = 5 + row;
                int halfWidth = 2 + row;
                for (int x = 16 - halfWidth; x <= 16 + halfWidth; x++)
                    pixels[y * size + x] = ink;
            }
        }
        else
        {
            for (int x = 6; x <= 25; x++)
                pixels[25 * size + x] = ink;
            for (int x = 11; x <= 20; x++)
                pixels[27 * size + x] = ink;
            for (int y = 27; y <= 29; y++)
                for (int x = 13; x <= 18; x++)
                    pixels[y * size + x] = ink;

            for (int y = 7; y <= 23; y++)
            {
                int inset = (23 - y) / 7;
                for (int x = 9 + inset; x <= 22 - inset; x++)
                    pixels[y * size + x] = ink;
            }

            Color32 cut = new Color32(70, 48, 39, 255);
            for (int y = 10; y <= 19; y++)
            {
                pixels[y * size + 13] = cut;
                pixels[y * size + 18] = cut;
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
