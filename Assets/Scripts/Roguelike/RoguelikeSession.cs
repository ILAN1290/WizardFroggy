using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class RoguelikeMerchantTextures
{
    public Texture2D panelBackground;
    public Texture2D closeButton;
    public Texture2D itemSlotBackground;
    public Texture2D itemSlotSelectedBackground;
    public Texture2D itemSlotSoldBackground;
    public Texture2D itemDescriptionBackground;
    public Texture2D itemBuyButton;
    public Texture2D currencyIcon;
    public Texture2D upgradeRowBackground;
    public Texture2D upgradePlusButton;
    public Texture2D upgradeMinusButton;
    public Texture2D upgradeLevelFilled;
    public Texture2D upgradeLevelEmpty;
}

/// <summary>
/// Controls the lobby, selected floor, current room and transitions between generated rooms.
/// Room graph generation and room decoration live in their own files.
/// </summary>
public class RoguelikeSession : MonoBehaviour
{
    public static RoguelikeSession Instance { get; private set; }

    [Header("Lobby")]
    [SerializeField] private string lobbyEnvironmentName = "Library Environment";
    [SerializeField] private Vector2 lobbyPlayerPosition = new Vector2(0f, -3f);
    [SerializeField] private AudioClip itemPickupSound;

    [Header("Merchant UI Textures")]
    [SerializeField] private RoguelikeMerchantTextures merchantTextures =
        new RoguelikeMerchantTextures();

    public RoguelikeMerchantTextures MerchantTextures => merchantTextures;

    private PlayerController playerController;
    private PlayerHealth playerHealth;
    private PlayerMana playerMana;
    private Rigidbody2D playerBody;
    private GameObject lobbyEnvironment;
    private GameObject dungeonRoot;
    private Transform roomContentRoot;
    private Transform environmentRoot;
    private Transform enemiesRoot;
    private Transform pickupsRoot;
    private Transform projectilesRoot;
    private Transform effectsRoot;
    private RoomGenerator roomGenerator;
    private RoguelikeFloorData currentFloor;
    private RoguelikeRoomData currentRoom;
    private int currentRoomIndex;
    private int enemiesRemaining;
    private bool inLobby = true;
    private bool shopOpen;
    private bool deathScreenShown;
    private RoguelikeMerchant activeMerchant;
    private readonly List<WorldPlaceholder> worldPlaceholders = new List<WorldPlaceholder>();
    private GUIStyle minimapMarkerStyle;
    private GUIStyle mushroomCountStyle;
    private Item[] lootItems;

    private static readonly float[] FrameSourceX = new float[4];
    private static readonly float[] FrameSourceY = new float[4];
    private static readonly float[] FrameDestinationX = new float[4];
    private static readonly float[] FrameDestinationY = new float[4];

    private sealed class WorldPlaceholder
    {
        public Transform Target;
        public string Label;
    }
    private string hint = "Выберите один из трёх этажей";
    private float hintUntil;

    public bool InLobby => inLobby;
    public bool IsCurrentRoomCleared => currentRoom != null && currentRoom.IsCleared;
    public int CurrentFloor => currentFloor == null ? 0 : currentFloor.FloorNumber;
    public int EnemiesRemaining => enemiesRemaining;
    public static Transform ProjectileRoot =>
        Instance != null && !Instance.inLobby ? Instance.projectilesRoot : null;
    public RoguelikeRoomType CurrentRoomType =>
        currentRoom == null ? RoguelikeRoomType.Combat : currentRoom.Type;

    private void Awake()
    {
        Time.timeScale = 1f;
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        PlayerRunData.EnsureInstance();
    }

    private void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerHealth = GetComponent<PlayerHealth>();
        playerMana = GetComponent<PlayerMana>();
        playerBody = GetComponent<Rigidbody2D>();

        if (playerController != null)
        {
            playerController.canShoot = true;
            playerController.canParry = true;
            playerController.canBeInvincible = true;
        }

        if (InputManager.PlayerInput != null)
            InputManager.PlayerInput.SwitchCurrentActionMap("Player");

        lobbyEnvironment = GameObject.Find(lobbyEnvironmentName);
        if (lobbyEnvironment != null)
            lobbyEnvironment.SetActive(false);

        dungeonRoot = new GameObject("Generated Roguelike Room");
        roomContentRoot = new GameObject("Current Room").transform;
        roomContentRoot.SetParent(dungeonRoot.transform, false);
        CreateRoomFolders();
        dungeonRoot.SetActive(false);
        roomGenerator = GetComponent<RoomGenerator>();
        if (roomGenerator == null)
            roomGenerator = gameObject.AddComponent<RoomGenerator>();

        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 6f;
            Camera.main.transform.position = new Vector3(0f, 0f, -10f);
        }

        EnablePlayerBars();
        transform.position = Vector2.zero;
        // Roguelike starts directly in the first available floor. There is no lobby panel.
        StartFloor(1);
    }

    private void CreateRoomFolders()
    {
        environmentRoot = CreateRoomFolder("Environment");
        enemiesRoot = CreateRoomFolder("Enemies");
        pickupsRoot = CreateRoomFolder("Pickups");
        projectilesRoot = CreateRoomFolder("Projectiles");
        effectsRoot = CreateRoomFolder("Effects");
    }

    private Transform CreateRoomFolder(string folderName)
    {
        GameObject folder = new GameObject(folderName);
        folder.transform.SetParent(roomContentRoot, false);
        return folder.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void ClearRoomFolders()
    {
        ClearChildren(environmentRoot);
        ClearChildren(enemiesRoot);
        ClearChildren(pickupsRoot);
        ClearChildren(projectilesRoot);
        ClearChildren(effectsRoot);
    }

    private void EnablePlayerBars()
    {
        HealthBar[] healthBars = Resources.FindObjectsOfTypeAll<HealthBar>();
        foreach (HealthBar bar in healthBars)
            if (bar != null && bar.gameObject.scene.IsValid()) bar.gameObject.SetActive(true);

        ManaBar[] manaBars = Resources.FindObjectsOfTypeAll<ManaBar>();
        foreach (ManaBar bar in manaBars)
            if (bar != null && bar.gameObject.scene.IsValid()) bar.gameObject.SetActive(true);
    }

    public void StartFloor(int floorNumber)
    {
        if (!inLobby) return;

        PlayerRunData runData = PlayerRunData.EnsureInstance();
        if (!runData.CanStartFloor(floorNumber) && floorNumber != 1)
        {
            ShowHint(runData.RoguelikeCompleted
                ? "Все три этажа уже пройдены."
                : "Сначала завершите предыдущий этаж.");
            return;
        }

        currentFloor = RoguelikeGenerator.GenerateFloor(floorNumber);
        currentRoomIndex = currentFloor.StartRoomIndex;
        inLobby = false;
        shopOpen = false;

        if (lobbyEnvironment != null) lobbyEnvironment.SetActive(false);
        dungeonRoot.SetActive(true);
        ShowHint("Этаж " + floorNumber + ". Исследуйте комнаты и найдите выход.");
        GenerateCurrentRoom(Vector2.zero);
    }

    private void GenerateCurrentRoom(Vector2 spawnPosition)
    {
        if (currentFloor == null || roomGenerator == null) return;

        currentRoom = currentFloor.GetRoom(currentRoomIndex);
        if (currentRoom == null) return;
        currentRoom.IsVisited = true;

        worldPlaceholders.Clear();
        ClearRoomFolders();

        enemiesRemaining = 0;
        activeMerchant = null;
        shopOpen = false;

        int spawnedEnemies = roomGenerator.GenerateRoom(
            environmentRoot, enemiesRoot, effectsRoot, projectilesRoot,
            currentRoom, currentFloor.FloorNumber, currentFloor.Seed, this);
        enemiesRemaining = spawnedEnemies;

        if (currentRoom.Type == RoguelikeRoomType.Combat && spawnedEnemies == 0)
            currentRoom.IsCleared = true;
        if (currentRoom.Type == RoguelikeRoomType.Merchant)
            currentRoom.IsCleared = true;

        transform.position = spawnPosition;
        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;

        ShowHint(GetRoomLabel(currentRoom.Type) +
            (currentRoom.IsCleared ? " • пройдено" : " • зачистите комнату"));
    }

    public void TryMoveByDirection(int dx, int dy, Vector2 spawnPosition)
    {
        if (currentFloor == null || currentRoom == null) return;
        if (!currentRoom.IsCleared)
        {
            ShowHint("Сначала зачистите комнату!");
            return;
        }

        int destination = currentFloor.FindNeighbour(currentRoomIndex, dx, dy);
        if (destination < 0) return;
        TryEnterRoom(destination, spawnPosition);
    }

    public void MarkCurrentRoomCleared()
    {
        if (currentRoom != null)
            currentRoom.IsCleared = true;
    }

    public void TryEnterRoom(int destinationIndex, Vector2 spawnPosition)
    {
        if (inLobby || currentFloor == null) return;
        if (currentRoom != null && !currentRoom.IsCleared)
        {
            ShowHint("Сначала завершите комнату!");
            return;
        }

        RoguelikeRoomData destination = currentFloor.GetRoom(destinationIndex);
        if (destination == null) return;
        currentRoomIndex = destinationIndex;
        GenerateCurrentRoom(spawnPosition);
    }

    public void SetEnemyCount(int count)
    {
        enemiesRemaining = Mathf.Max(0, count);
    }

    public void TriggerMiniBossGroundSlam(Vector2 position, float damage)
    {
        if (roomGenerator != null)
            roomGenerator.StartGroundSlamWave(
                position, effectsRoot, transform, damage);
    }

    private Item[] GetLootItems()
    {
        if (lootItems == null)
            lootItems = Resources.LoadAll<Item>("RoguelikeLoot");
        return lootItems;
    }

    public void OnEnemyDefeated(Vector2 position, bool isMiniBoss = false)
    {
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);
        PlayerRunData.EnsureInstance();

        if (isMiniBoss)
        {
            SpawnChestLoot(position);
            ShowHint("Минибосс повержен! Заберите добычу.");
        }
        else
        {
            SpawnMushroomPickup(position, Random.Range(1, 4));

            Item[] loot = GetLootItems();
            if (loot.Length > 0 && Random.value < 0.38f)
            {
                Item item = loot[Random.Range(0, loot.Length)];
                SpawnItemPickup(item, 1, position + Random.insideUnitCircle * 0.55f);
            }
        }

        if (enemiesRemaining == 0 && currentRoom != null)
        {
            currentRoom.IsCleared = true;
            ShowHint(currentRoom.IsExit
                ? "Последняя комната зачищена. Выход открыт!"
                : "Комната зачищена. Выходы открыты!");
        }
    }

    public void OpenChest(Vector2 position)
    {
        SpawnChestLoot(position);
        if (currentRoom != null) currentRoom.IsCleared = true;
        ShowHint("Сундук открыт! Заберите добычу.");
    }

    private void SpawnChestLoot(Vector2 position)
    {
        Item[] loot = GetLootItems();
        if (loot.Length > 0)
        {
            int drops = Random.Range(1, 4);
            for (int i = 0; i < drops; i++)
                SpawnItemPickup(loot[Random.Range(0, loot.Length)], 1,
                    position + Random.insideUnitCircle * 1.25f);
        }

        SpawnMushroomPickup(position + new Vector2(0.6f, 0f), Random.Range(6, 13));
    }

    private void SpawnMushroomPickup(Vector2 position, int amount)
    {
        GameObject pickup = new GameObject("Грибочки x" + amount);
        if (pickupsRoot != null) pickup.transform.SetParent(pickupsRoot, true);
        SpriteRenderer renderer = pickup.AddComponent<SpriteRenderer>();
        renderer.sprite = roomGenerator != null && roomGenerator.MushroomSprite != null
            ? roomGenerator.MushroomSprite : RoomGenerator.CircleSprite;
        renderer.color = new Color(0.88f, 0.42f, 0.74f);
        // Keep loot behind all interior furniture sprites.
        renderer.sortingOrder = -5;

        float pickupRadius = 0.25f;
        // Keep the pickup at exactly 50x50 screen pixels, independent of sprite PPU.
        if (renderer.sprite != null)
        {
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                float pixelsPerWorldUnitY = camera.pixelHeight /
                    Mathf.Max(0.01f, camera.orthographicSize * 2f);
                float pixelsPerWorldUnitX = camera.pixelWidth /
                    Mathf.Max(0.01f, camera.orthographicSize * 2f * camera.aspect);
                float worldWidth = 50f / Mathf.Max(1f, pixelsPerWorldUnitX);
                float worldHeight = 50f / Mathf.Max(1f, pixelsPerWorldUnitY);
                Vector2 sourceSize = renderer.sprite.bounds.size;
                pickup.transform.localScale = new Vector3(
                    worldWidth / Mathf.Max(0.001f, sourceSize.x),
                    worldHeight / Mathf.Max(0.001f, sourceSize.y), 1f);
                pickupRadius = Mathf.Max(worldWidth, worldHeight) * 0.5f;
            }
        }

        position = FindAccessiblePickupPosition(position, pickupRadius);
        pickup.transform.position = position;
        if (roomGenerator != null)
            roomGenerator.ApplyPlaceholder(pickup, roomGenerator.MushroomSprite, "ГРИБОЧКИ", this);

        CircleCollider2D collider = pickup.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = pickupRadius;
        pickup.AddComponent<RoguelikePickup>().SetupCurrency(amount);
    }

    private void SpawnItemPickup(Item item, int count, Vector2 position)
    {
        if (item == null) return;

        Sprite itemSprite = item.icon != null ? item.icon :
            roomGenerator != null && roomGenerator.ItemSprite != null
                ? roomGenerator.ItemSprite : RoomGenerator.CircleSprite;

        Camera camera = Camera.main;
        float pixelsPerUnit = camera != null && camera.orthographic && camera.pixelHeight > 0
            ? camera.pixelHeight / Mathf.Max(0.01f, camera.orthographicSize * 2f)
            : 120f;
        float worldSize = 70f / Mathf.Max(1f, pixelsPerUnit);
        float pickupRadius = worldSize * 0.48f;
        position = FindAccessiblePickupPosition(position, pickupRadius);

        GameObject pickup = new GameObject("Предмет: " + item.itemName);
        pickup.transform.position = position;
        if (pickupsRoot != null) pickup.transform.SetParent(pickupsRoot, true);

        GameObject visual = new GameObject("Иконка предмета 70x70");
        visual.transform.SetParent(pickup.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = itemSprite;
        renderer.color = Color.white;
        // Item icons render beneath all interior furniture.
        renderer.sortingOrder = -5;

        Vector2 spriteSize = itemSprite.bounds.size;
        visual.transform.localScale = new Vector3(
            worldSize / Mathf.Max(0.001f, spriteSize.x),
            worldSize / Mathf.Max(0.001f, spriteSize.y), 1f);

        CircleCollider2D collider = pickup.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = pickupRadius;

        pickup.AddComponent<RoguelikePickup>().SetupItem(item, count, itemPickupSound);
    }

    private static Vector2 FindAccessiblePickupPosition(Vector2 preferredPosition,
        float pickupRadius)
    {
        Physics2D.SyncTransforms();
        if (IsPickupPositionFree(preferredPosition, pickupRadius))
            return preferredPosition;

        const int directions = 20;
        const int rings = 12;
        float step = Mathf.Max(0.18f, pickupRadius * 1.4f);

        for (int ring = 1; ring <= rings; ring++)
        {
            float distance = pickupRadius + 0.12f + ring * step;
            for (int direction = 0; direction < directions; direction++)
            {
                float angle = direction * (Mathf.PI * 2f / directions);
                Vector2 candidate = preferredPosition + new Vector2(
                    Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (IsPickupPositionFree(candidate, pickupRadius))
                    return candidate;
            }
        }

        // Keep a deterministic fallback rather than discarding the drop.
        return preferredPosition;
    }

    private static bool IsPickupPositionFree(Vector2 position, float pickupRadius)
    {
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(
            position, pickupRadius + 0.06f);

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D obstacle = overlaps[i];
            if (obstacle == null || !obstacle.enabled || obstacle.isTrigger)
                continue;

            // Generated invisible wall colliders and the solid shelf sections
            // use the Wall tag. Never place loot inside those blocking shapes.
            if (obstacle.CompareTag("Wall"))
                return false;
        }

        return true;
    }

    public void OpenShop(RoguelikeMerchant merchant)
    {
        if (merchant == null || deathScreenShown) return;
        activeMerchant = merchant;
        shopOpen = true;
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
        if (playerController != null)
            playerController.enabled = false;
        ShowHint("Торговец: выберите предмет или настройте три характеристики.");
    }

    public void CloseShop()
    {
        shopOpen = false;
        activeMerchant = null;
        if (!deathScreenShown && playerController != null)
            playerController.enabled = true;
    }

    public void ShowHint(string message)
    {
        hint = message;
        hintUntil = Time.time + 3.5f;
    }

    public void CompleteCurrentFloor()
    {
        if (inLobby || currentFloor == null || currentRoom == null ||
            !currentRoom.IsExit || !currentRoom.IsCleared)
        {
            ShowHint("Сначала зачистите последнюю комнату этажа.");
            return;
        }

        int completedFloor = currentFloor.FloorNumber;
        PlayerRunData runData = PlayerRunData.EnsureInstance();
        runData.CompleteFloor(completedFloor);

        if (completedFloor < 3)
        {
            currentFloor = RoguelikeGenerator.GenerateFloor(completedFloor + 1);
            currentRoomIndex = currentFloor.StartRoomIndex;
            inLobby = false;
            shopOpen = false;
            activeMerchant = null;
            if (lobbyEnvironment != null) lobbyEnvironment.SetActive(false);
            if (dungeonRoot != null) dungeonRoot.SetActive(true);
            ShowHint("Этаж " + (completedFloor + 1) + " открыт. Путь продолжается!");
            GenerateCurrentRoom(Vector2.zero);
            return;
        }

        // TowerDefence is intentionally supplied by the other developer.
        // Do not create a fake scene or attempt to load it until it is in Build Settings.
        if (Application.CanStreamedLevelBeLoaded("TowerDefence"))
        {
            SceneManager.LoadScene("TowerDefence");
        }
        else
        {
            ReturnToLobby();
            ShowHint("Все три этажа пройдены. Добавьте TowerDefence в Build Settings перед Boss.");
        }
    }

    public void RegisterWorldPlaceholder(Transform target, string label)
    {
        if (target == null || string.IsNullOrWhiteSpace(label)) return;

        for (int i = 0; i < worldPlaceholders.Count; i++)
        {
            if (worldPlaceholders[i].Target == target)
            {
                worldPlaceholders[i].Label = label;
                return;
            }
        }

        worldPlaceholders.Add(new WorldPlaceholder { Target = target, Label = label });
    }

    public void ReturnToLobby()
    {
        worldPlaceholders.Clear();
        shopOpen = false;
        activeMerchant = null;
        inLobby = true;
        currentFloor = null;
        currentRoom = null;
        enemiesRemaining = 0;

        if (dungeonRoot != null)
        {
            ClearRoomFolders();
            dungeonRoot.SetActive(false);
        }

        if (lobbyEnvironment != null) lobbyEnvironment.SetActive(true);
        transform.position = lobbyPlayerPosition;
        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;

        if (playerHealth != null)
            playerHealth.Healing(playerHealth.MaxHealth);
        if (playerMana != null)
            playerMana.RestoreMana(playerMana.MaxMana);

        if (PlayerRunData.Instance != null)
        {
            PlayerRunData.Instance.SaveHealth(playerHealth != null ? playerHealth.CurrentHealth : 100f);
            PlayerRunData.Instance.SaveMana(playerMana != null ? playerMana.CurrentMana : 100f);
        }

        ShowHint("Вы снова в библиотеке. Можно выбрать другой этаж.");
    }

    public bool HandlePlayerDeath()
    {
        if (deathScreenShown) return true;
        if (inLobby) return false;

        deathScreenShown = true;
        shopOpen = false;
        activeMerchant = null;

        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
        if (playerController != null)
            playerController.enabled = false;

        // Store a clean retry state before the scene unloads the player objects.
        if (playerMana != null)
            playerMana.RestoreMana(playerMana.MaxMana);
        if (PlayerRunData.Instance != null)
        {
            PlayerRunData.Instance.SaveHealth(
                playerHealth != null ? playerHealth.MaxHealth : PlayerRunData.Instance.MaximumHealth);
            PlayerRunData.Instance.SaveMana(playerMana != null ? playerMana.CurrentMana : 100f);
            PlayerPrefs.Save();
        }

        // Roguelike.unity already contains the configured DeathMenu canvas object.
        if (playerHealth != null)
            playerHealth.ShowDeathScreen();
        else
        {
            GameObject deathMenu = GameObject.Find("DeathMenu");
            if (deathMenu != null)
                deathMenu.SetActive(true);
        }

        Time.timeScale = 0f;
        return true;
    }

    private string GetRoomLabel(RoguelikeRoomType type)
    {
        switch (type)
        {
            case RoguelikeRoomType.Combat: return "Комната с призраками";
            case RoguelikeRoomType.Chest: return "Комната с сундуком";
            case RoguelikeRoomType.Merchant: return "Лавка торговца";
            default: return "Комната";
        }
    }

    private static Vector2 GetMiniMapRoomCentre(RoguelikeRoomData room,
        int floorHeight, float left, float top, float pitchX, float pitchY)
    {
        return new Vector2(left + room.X * pitchX,
            top + (floorHeight - 1 - room.Y) * pitchY);
    }

    private void DrawMiniMap()
    {
        if (currentFloor == null || currentRoom == null) return;

        float size = Mathf.Min(240f, Screen.width - 100f, Screen.height - 120f);
        size = Mathf.Max(150f, size);
        Rect frame = new Rect(Screen.width - size - 140f, 50f, size, size);
        if (roomGenerator != null && roomGenerator.MinimapFrameSprite != null)
            DrawSpriteFrame(roomGenerator.MinimapFrameSprite, frame);
        else
        {
            Color old = GUI.color;
            GUI.color = new Color(0.24f, 0.12f, 0.075f, 1f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Box(frame, GUIContent.none);
        }

        Rect map = new Rect(frame.x + 12f, frame.y + 12f,
            frame.width - 24f, frame.height - 24f);
        DrawMiniMapRect(map, new Color(0.16f, 0.075f, 0.045f, 1f));

        float left = map.x + 22f;
        float top = map.y + 22f;
        float pitchX = currentFloor.Width > 1 ? (map.width - 44f) / (currentFloor.Width - 1) : 0f;
        float pitchY = currentFloor.Height > 1 ? (map.height - 44f) / (currentFloor.Height - 1) : 0f;
        const float roomWidth = 24f;
        const float roomHeight = 24f;
        const float corridorThickness = 4f;

        // Corridors are visible once their source room has been explored.
        for (int i = 0; i < currentFloor.Rooms.Count; i++)
        {
            RoguelikeRoomData room = currentFloor.Rooms[i];
            if (room == null || !room.IsVisited) continue;
            Vector2 from = GetMiniMapRoomCentre(room, currentFloor.Height, left, top, pitchX, pitchY);
            for (int n = 0; n < room.Neighbours.Count; n++)
            {
                RoguelikeRoomData neighbour = currentFloor.GetRoom(room.Neighbours[n]);
                if (neighbour == null || (neighbour.IsVisited && room.Index > neighbour.Index)) continue;
                Vector2 to = GetMiniMapRoomCentre(neighbour, currentFloor.Height, left, top, pitchX, pitchY);
                if (!neighbour.IsVisited)
                {
                    Vector2 direction = new Vector2(neighbour.X - room.X,
                        -(neighbour.Y - room.Y)).normalized;
                    to = from + direction * (direction.x != 0f ? pitchX * 0.42f : pitchY * 0.42f);
                }
                Rect corridor = Mathf.Abs(to.x - from.x) >= Mathf.Abs(to.y - from.y)
                    ? new Rect(Mathf.Min(from.x, to.x), from.y - corridorThickness * 0.5f,
                        Mathf.Abs(to.x - from.x), corridorThickness)
                    : new Rect(from.x - corridorThickness * 0.5f, Mathf.Min(from.y, to.y),
                        corridorThickness, Mathf.Abs(to.y - from.y));
                DrawMiniMapRect(corridor, new Color(0.48f, 0.48f, 0.48f, 1f));
            }
        }

        for (int i = 0; i < currentFloor.Rooms.Count; i++)
        {
            RoguelikeRoomData room = currentFloor.Rooms[i];
            if (room == null) continue;
            // Special-room icons are only revealed after the player enters the room.
            if (!room.IsVisited) continue;

            Vector2 centre = GetMiniMapRoomCentre(room, currentFloor.Height, left, top, pitchX, pitchY);
            Rect roomRect = new Rect(centre.x - roomWidth * 0.5f,
                centre.y - roomHeight * 0.5f, roomWidth, roomHeight);
            Color roomColour = room.Index == currentRoomIndex
                ? new Color(0.9f, 0.9f, 0.82f, 1f)
                : room.IsCleared ? new Color(0.46f, 0.46f, 0.46f, 1f)
                : new Color(0.27f, 0.27f, 0.27f, 1f);
            DrawMiniMapRect(roomRect, roomColour);

            string marker = room.Type == RoguelikeRoomType.Chest ? "C"
                : room.Type == RoguelikeRoomType.Merchant ? "$"
                : room.Type == RoguelikeRoomType.Stairs ? "↓" : "";
            if (room.Index == currentRoomIndex && marker.Length == 0) marker = "●";
            if (!string.IsNullOrEmpty(marker))
            {
                Color old = GUI.color;
                GUI.color = room.Type == RoguelikeRoomType.Chest ? new Color(1f, 0.82f, 0.3f)
                    : room.Type == RoguelikeRoomType.Merchant ? new Color(0.75f, 0.9f, 1f)
                    : room.Type == RoguelikeRoomType.Stairs ? new Color(1f, 0.42f, 0.35f)
                    : Color.black;
                if (minimapMarkerStyle == null)
                {
                    minimapMarkerStyle = new GUIStyle(GUI.skin.label);
                    minimapMarkerStyle.alignment = TextAnchor.MiddleCenter;
                    minimapMarkerStyle.fontSize = 16;
                    minimapMarkerStyle.fontStyle = FontStyle.Bold;
                }
                GUI.Label(roomRect, marker, minimapMarkerStyle);
                GUI.color = old;
            }
        }

        // Reuse the mushroom sprite already assigned to roguelike pickups.
        Rect mushroomRect = new Rect(frame.x + 2f, frame.yMax + 5f, 75f, 75f);
        if (roomGenerator != null)
            DrawSpriteFit(roomGenerator.MushroomSprite, mushroomRect);

        if (mushroomCountStyle == null)
        {
            mushroomCountStyle = new GUIStyle(GUI.skin.label);
            mushroomCountStyle.alignment = TextAnchor.MiddleLeft;
            mushroomCountStyle.fontSize = 45;
            mushroomCountStyle.fontStyle = FontStyle.Bold;
            mushroomCountStyle.normal.textColor = Color.white;
        }

        Rect countRect = new Rect(mushroomRect.xMax + 4f, mushroomRect.y + 15f,
            150f, 55f);
        GUI.Label(countRect, PlayerRunData.EnsureInstance().Mushrooms.ToString(),
            mushroomCountStyle);
    }

    private static void DrawMiniMapRect(Rect rect, Color colour)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        Color old = GUI.color;
        GUI.color = colour;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static void DrawSpriteFrame(Sprite sprite, Rect destination)
    {
        if (sprite == null || sprite.texture == null)
        {
            GUI.Box(destination, GUIContent.none);
            return;
        }

        Texture2D texture = sprite.texture;
        Rect source = sprite.textureRect;
        float sourceBorderX = Mathf.Min(28f, source.width * 0.18f);
        float sourceBorderY = Mathf.Min(26f, source.height * 0.28f);
        float destinationBorderX = Mathf.Min(12f, destination.width * 0.12f);
        float destinationBorderY = Mathf.Min(12f, destination.height * 0.12f);

        FrameSourceX[0] = source.x;
        FrameSourceX[1] = source.x + sourceBorderX;
        FrameSourceX[2] = source.xMax - sourceBorderX;
        FrameSourceX[3] = source.xMax;
        FrameSourceY[0] = source.y;
        FrameSourceY[1] = source.y + sourceBorderY;
        FrameSourceY[2] = source.yMax - sourceBorderY;
        FrameSourceY[3] = source.yMax;
        FrameDestinationX[0] = destination.x;
        FrameDestinationX[1] = destination.x + destinationBorderX;
        FrameDestinationX[2] = destination.xMax - destinationBorderX;
        FrameDestinationX[3] = destination.xMax;
        FrameDestinationY[0] = destination.y;
        FrameDestinationY[1] = destination.y + destinationBorderY;
        FrameDestinationY[2] = destination.yMax - destinationBorderY;
        FrameDestinationY[3] = destination.yMax;

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                Rect target = Rect.MinMaxRect(FrameDestinationX[column], FrameDestinationY[row],
                    FrameDestinationX[column + 1], FrameDestinationY[row + 1]);
                Rect uv = new Rect(FrameSourceX[column] / texture.width,
                    FrameSourceY[row] / texture.height,
                    (FrameSourceX[column + 1] - FrameSourceX[column]) / texture.width,
                    (FrameSourceY[row + 1] - FrameSourceY[row]) / texture.height);
                GUI.DrawTextureWithTexCoords(target, texture, uv, true);
            }
        }
    }

    private static void DrawSpriteFit(Sprite sprite, Rect destination)
    {
        if (sprite == null || sprite.texture == null) return;

        Texture2D texture = sprite.texture;
        Rect source = sprite.textureRect;
        float sourceAspect = source.width / Mathf.Max(1f, source.height);
        float destinationAspect = destination.width / Mathf.Max(1f, destination.height);
        Rect target = destination;

        if (sourceAspect > destinationAspect)
        {
            float fittedHeight = destination.width / sourceAspect;
            target.y += (destination.height - fittedHeight) * 0.5f;
            target.height = fittedHeight;
        }
        else
        {
            float fittedWidth = destination.height * sourceAspect;
            target.x += (destination.width - fittedWidth) * 0.5f;
            target.width = fittedWidth;
        }

        Rect uv = new Rect(
            source.x / texture.width,
            source.y / texture.height,
            source.width / texture.width,
            source.height / texture.height);
        GUI.DrawTextureWithTexCoords(target, texture, uv, true);
    }

    private void OnGUI()
    {
        if (inLobby || deathScreenShown) return;

        // The map is static GUI output; skip its layout work for input events.
        if (Event.current.type == EventType.Repaint)
            DrawMiniMap();

        if (shopOpen && activeMerchant != null)
            activeMerchant.DrawShopGUI(this);
    }
}