using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

    [Header("Здоровье дедушки")]
    [SerializeField, Min(1f)] private float grandpaMaximumHealth = 300f;
    [SerializeField, Min(0f)] private float grandpaPotionHealAmount = 15f;

    [Header("Параметры призраков")]
    [SerializeField, Min(1f)] private float whiteGhostHealth = 10f;
    [SerializeField, Min(0.05f)] private float whiteGhostSpeed = 0.72f;
    [SerializeField, Min(0.1f)] private float whiteGhostSize = 0.72f;
    [SerializeField, Min(1f)] private float blueGhostHealth = 10f;
    [SerializeField, Min(1f)] private float blueGhostSpeedMultiplier = 2f;
    [SerializeField, Min(0.1f)] private float blueGhostSize = 0.88f;
    [SerializeField, Min(1f)] private float redGhostHealth = 30f;
    [SerializeField, Min(0.1f)] private float redGhostSpeedMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float redGhostSize = 1.05f;
    [SerializeField, Min(1f)] private float blackGhostHealth = 200f;
    [SerializeField, Min(0.1f)] private float blackGhostSpeedMultiplier = 0.5f;
    [SerializeField, Min(0.1f)] private float blackGhostSize = 1.35f;

    [Header("Параметры башен")]
    [SerializeField] private TowerDefenceTowerStats machineGunStats = new TowerDefenceTowerStats
    {
        mushroomCost = 2, damage = 2.2f, attackInterval = 0.28f,
        range = 3.15f, projectileSpeed = 7f, projectileSize = 0.105f
    };
    [SerializeField] private TowerDefenceTowerStats standardStats = new TowerDefenceTowerStats
    {
        mushroomCost = 1, damage = 5f, attackInterval = 0.88f,
        range = 4.65f, projectileSpeed = 5.4f, projectileSize = 0.14f
    };
    [SerializeField] private TowerDefenceTowerStats cannonStats = new TowerDefenceTowerStats
    {
        mushroomCost = 4, damage = 18f, attackInterval = 2.45f,
        range = 2.75f, projectileSpeed = 4.8f, projectileSize = 0.19f,
        splashRadius = 1.25f
    };
    [SerializeField, Min(1)] private int maximumTowerLevel = 4;
    [SerializeField, Min(0f)] private float upgradeDamagePerLevel = 0.25f;
    [SerializeField, Min(0f)] private float upgradeRangePerLevel = 0.22f;

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
        if (!gameStarted && !instructionsOpen)
            StartCoroutine(ShowInstructionsAndRun());
    }

    private IEnumerator ShowInstructionsAndRun()
    {
        instructionsOpen = true;
        yield return new WaitUntil(() => !instructionsOpen);

        gameStarted = true;
        CreateStarterTower();
        countdown = Mathf.Max(0f, firstWaveDelay);
        countdownActive = true;

        for (int wave = 1; wave <= (waveSpawner != null ? waveSpawner.TotalWaves : 10); wave++)
        {
            while (countdown > 0f && !gameOver)
            {
                countdown -= Time.deltaTime;
                yield return null;
            }

            if (gameOver)
                yield break;

            currentWave = wave;
            ShowGrandpaPhrase();
            countdownActive = false;

            if (waveSpawner != null)
                yield return StartCoroutine(waveSpawner.SpawnWave(wave, this));

            while (activeGhosts.Count > 0 && !gameOver)
                yield return null;

            if (gameOver)
                yield break;

            if (wave < (waveSpawner != null ? waveSpawner.TotalWaves : 10))
            {
                countdown = Mathf.Max(0f, betweenWaveDelay);
                countdownActive = true;
                while (countdown > 0f && !gameOver)
                {
                    countdown -= Time.deltaTime;
                    yield return null;
                }

                if (gameOver)
                    yield break;

                countdownActive = false;
            }
        }

        victory = !gameOver;
        statusMessage = "Все 10 волн отбиты! Дед жив — победа!";
        statusUntil = float.MaxValue;
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
        if (gameOver || routeA == null || routeB == null)
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
        drop.transform.position = position + Vector3.up * 0.22f;
        drop.transform.localScale = Vector3.one * 0.28f;

        SpriteRenderer renderer = drop.AddComponent<SpriteRenderer>();
        renderer.sprite = mushroomSprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 35;

        CircleCollider2D trigger = drop.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.7f;

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

        rangePreview = new GameObject("Радиусы башен • просмотр");
        rangePreview.transform.position = tower.transform.position;

        CreateRangeRing(rangePreview.transform,
            GetRangeForType(TowerDefenceTowerType.MachineGun, tower.Level),
            new Color(0.3f, 0.72f, 1f, 0.85f));
        CreateRangeRing(rangePreview.transform,
            GetRangeForType(TowerDefenceTowerType.Standard, tower.Level),
            new Color(0.95f, 0.9f, 0.55f, 0.85f));
        CreateRangeRing(rangePreview.transform,
            GetRangeForType(TowerDefenceTowerType.Cannon, tower.Level),
            new Color(1f, 0.45f, 0.28f, 0.85f));
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
        return new Rect(20f, Screen.height - 178f, 450f, 158f);
    }

    private Rect GetTowerPanelRect()
    {
        return new Rect(Screen.width - 330f, Screen.height - 188f, 310f, 168f);
    }

    private void OnGUI()
    {
        if (!gameStarted && !instructionsOpen)
            return;

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
            Rect statusRect = new Rect(
                Screen.width * 0.5f - 220f, 76f, 440f, 42f);
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

        Rect hpPanel = new Rect(18f, 18f, 280f, 86f);
        GUI.Box(hpPanel, GUIContent.none);
        GUI.Label(new Rect(32f, 24f, 250f, 28f),
            "Дед: " + Mathf.CeilToInt(grandpaHealth) + " / " +
            Mathf.CeilToInt(grandpaMaximumHealth), heading);

        Rect barBackground = new Rect(32f, 60f, 248f, 20f);
        GUI.color = new Color(0.18f, 0.12f, 0.12f, 1f);
        GUI.DrawTexture(barBackground, Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.82f, 0.36f, 1f);
        GUI.DrawTexture(new Rect(barBackground.x, barBackground.y,
            barBackground.width * Mathf.Clamp01(grandpaHealth /
                Mathf.Max(1f, grandpaMaximumHealth)), barBackground.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect moneyPanel = new Rect(Screen.width - 235f, 18f, 215f, 55f);
        GUI.Box(moneyPanel, GUIContent.none);
        GUI.Label(new Rect(moneyPanel.x + 14f, moneyPanel.y + 10f, 190f, 35f),
            "Грибочки: " + mushrooms, heading);

        string waveLabel;
        if (!gameStarted)
            waveLabel = "Подготовка";
        else if (countdownActive)
            waveLabel = (currentWave == 0 ? "Первая волна" : "Следующая волна") +
                " через " + Mathf.CeilToInt(Mathf.Max(0f, countdown)) + " сек.";
        else
            waveLabel = "Волна " + currentWave + " / " +
                (waveSpawner != null ? waveSpawner.TotalWaves : 10);

        Rect wavePanel = new Rect(Screen.width * 0.5f - 160f, 18f, 320f, 55f);
        GUI.Box(wavePanel, GUIContent.none);
        GUI.Label(new Rect(wavePanel.x + 12f, wavePanel.y + 10f,
            wavePanel.width - 24f, 34f), waveLabel, heading);
    }

    private void DrawInstructions()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect panel = new Rect(
            Screen.width * 0.5f - 450f,
            Screen.height * 0.5f - 270f,
            900f, 540f);
        GUI.Box(panel, GUIContent.none);

        Rect bookRect = new Rect(panel.x + 26f, panel.y + 90f, 300f, 265f);
        DrawSprite(bookRect, instructionBookSprite);

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 30;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = new Color(1f, 0.87f, 0.56f, 1f);
        GUI.Label(new Rect(panel.x + 340f, panel.y + 24f, 530f, 48f),
            "КАК ЗАЩИЩАТЬ ДЕДА", title);

        GUIStyle text = new GUIStyle(GUI.skin.label);
        text.fontSize = 19;
        text.wordWrap = true;
        text.richText = true;
        text.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 350f, panel.y + 90f, 510f, 285f),
            "• Призраки идут по дорожке к деду. Если доберутся — снимут ему HP, равное оставшемуся здоровью.\n\n" +
            "• Нажми на свободную клетку рядом с дорожкой, чтобы построить башню.\n\n" +
            "• Обычная башня стоит 1 грибочек, пулемётная — 2, пушечная — 4.\n\n" +
            "• Нажми на башню, чтобы увидеть радиусы, уровень, улучшить или продать её. Улучшения повышают урон и дальность.\n\n" +
            "• За каждого уничтоженного призрака выпадает грибочек. Зелья скорости и силы усиливают башни, зелье лечения лечит деда.\n\n" +
            "• Всего 10 волн. Первая начнётся через 30 секунд после закрытия книги; между волнами — 15 секунд.",
            text);

        GUIStyle button = new GUIStyle(GUI.skin.button);
        button.fontSize = 22;
        button.fontStyle = FontStyle.Bold;
        Rect closeRect = new Rect(panel.x + 350f, panel.yMax - 88f, 510f, 54f);
        if (GUI.Button(closeRect, "Понятно, начинаем подготовку", button))
            instructionsOpen = false;
    }

    private void DrawBuildMenu()
    {
        Rect panel = GetBuildPanelRect();
        GUI.Box(panel, GUIContent.none);

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 19;
        title.fontStyle = FontStyle.Bold;
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, 400f, 28f),
            "Выбери башню", title);

        DrawBuildButton(
            new Rect(panel.x + 12f, panel.y + 45f, 132f, 88f),
            TowerDefenceTowerType.Standard, "Обычная", standardStats.mushroomCost);
        DrawBuildButton(
            new Rect(panel.x + 154f, panel.y + 45f, 132f, 88f),
            TowerDefenceTowerType.MachineGun, "Пулемёт", machineGunStats.mushroomCost);
        DrawBuildButton(
            new Rect(panel.x + 296f, panel.y + 45f, 140f, 88f),
            TowerDefenceTowerType.Cannon, "Пушка", cannonStats.mushroomCost);
    }

    private void DrawBuildButton(
        Rect rect,
        TowerDefenceTowerType type,
        string label,
        int cost)
    {
        bool canAfford = mushrooms >= cost;
        GUI.enabled = canAfford;
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = 17;
        style.wordWrap = true;
        if (GUI.Button(rect, label + "\n" + cost + " гриб.", style))
            TryBuyTower(type);
        GUI.enabled = true;
    }

    private void DrawSelectedTowerPanel()
    {
        if (selectedTower == null)
            return;

        Rect panel = GetTowerPanelRect();
        GUI.Box(panel, GUIContent.none);

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 18;
        title.fontStyle = FontStyle.Bold;
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, 280f, 28f),
            selectedTower.GetDisplayName(), title);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 38f, 270f, 25f),
            "Уровень: " + selectedTower.Level + " / " + maximumTowerLevel);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 64f, 280f, 22f),
            "Радиусы: ПУЛ " + machineGunStats.range.ToString("F1") +
            " | ОБЫЧ " + standardStats.range.ToString("F1") +
            " | ПУШ " + cannonStats.range.ToString("F1"));

        Rect sellRect = new Rect(panel.x + 14f, panel.y + 100f, 62f, 54f);
        Rect upgradeRect = new Rect(panel.x + 90f, panel.y + 100f, 62f, 54f);
        GUIStyle iconButton = new GUIStyle(GUI.skin.button);
        iconButton.normal.background = null;
        iconButton.hover.background = null;
        iconButton.active.background = null;
        DrawSprite(sellRect, sellMinusButtonSprite);
        DrawSprite(upgradeRect, upgradePlusButtonSprite);

        if (GUI.Button(sellRect, GUIContent.none, iconButton))
            SellSelectedTower();

        if (selectedTower != null)
        {
            int upgradeCost = selectedTower.GetNextUpgradeCost();
            bool canUpgrade = selectedTower.CanUpgrade && mushrooms >= upgradeCost;
            GUI.enabled = canUpgrade;
            if (GUI.Button(upgradeRect, GUIContent.none, iconButton))
                TryUpgradeSelectedTower();
            GUI.enabled = true;

            string upgradeText = selectedTower.CanUpgrade
                ? "Улучшить за " + upgradeCost
                : "Максимальный уровень";
            GUI.Label(new Rect(panel.x + 165f, panel.y + 108f, 135f, 42f),
                upgradeText);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 154f, 280f, 20f),
                "Продать за " + selectedTower.GetSellRefund());
        }
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
