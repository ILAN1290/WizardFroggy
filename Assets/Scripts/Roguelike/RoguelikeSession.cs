using UnityEngine;

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

    private PlayerController playerController;
    private PlayerHealth playerHealth;
    private PlayerMana playerMana;
    private Rigidbody2D playerBody;
    private GameObject lobbyEnvironment;
    private GameObject dungeonRoot;
    private RoomGenerator roomGenerator;
    private RoguelikeFloorData currentFloor;
    private RoguelikeRoomData currentRoom;
    private int currentRoomIndex;
    private int enemiesRemaining;
    private bool inLobby = true;
    private bool shopOpen;
    private RoguelikeMerchant activeMerchant;
    private string hint = "Выберите один из трёх этажей";
    private float hintUntil;

    public bool InLobby => inLobby;
    public bool IsCurrentRoomCleared => currentRoom == null || currentRoom.IsCleared;
    public int CurrentFloor => currentFloor == null ? 0 : currentFloor.FloorNumber;
    public int EnemiesRemaining => enemiesRemaining;
    public RoguelikeRoomType CurrentRoomType =>
        currentRoom == null ? RoguelikeRoomType.Combat : currentRoom.Type;

    private void Awake()
    {
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
            lobbyEnvironment.SetActive(true);

        dungeonRoot = new GameObject("Generated Roguelike Room");
        dungeonRoot.SetActive(false);
        roomGenerator = gameObject.AddComponent<RoomGenerator>();

        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 6f;
            Camera.main.transform.position = new Vector3(0f, 0f, -10f);
        }

        EnablePlayerBars();
        transform.position = lobbyPlayerPosition;
        ShowHint("Добро пожаловать в библиотеку. Выберите этаж.");
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

        PlayerRunData.EnsureInstance();
        currentFloor = RoguelikeGenerator.GenerateFloor(floorNumber);
        currentRoomIndex = 0;
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

        for (int i = dungeonRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(dungeonRoot.transform.GetChild(i).gameObject);

        enemiesRemaining = 0;
        activeMerchant = null;
        shopOpen = false;

        int spawnedEnemies = roomGenerator.GenerateRoom(
            dungeonRoot.transform, currentRoom, currentFloor.FloorNumber, this);
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

    public void OnEnemyDefeated(Vector2 position)
    {
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);
        PlayerRunData.EnsureInstance();

        SpawnMushroomPickup(position, Random.Range(1, 4));

        Item[] loot = Resources.LoadAll<Item>("RoguelikeLoot");
        if (loot.Length > 0 && Random.value < 0.38f)
        {
            Item item = loot[Random.Range(0, loot.Length)];
            SpawnItemPickup(item, 1, position + Random.insideUnitCircle * 0.55f);
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
        Item[] loot = Resources.LoadAll<Item>("RoguelikeLoot");
        if (loot.Length > 0)
        {
            int drops = Random.Range(1, 4);
            for (int i = 0; i < drops; i++)
                SpawnItemPickup(loot[Random.Range(0, loot.Length)], 1,
                    position + Random.insideUnitCircle * 1.25f);
        }

        SpawnMushroomPickup(position + new Vector2(0.6f, 0f), Random.Range(6, 13));
        if (currentRoom != null) currentRoom.IsCleared = true;
        ShowHint("Сундук открыт! Заберите добычу.");
    }

    private void SpawnMushroomPickup(Vector2 position, int amount)
    {
        GameObject pickup = new GameObject("Грибочки x" + amount);
        pickup.transform.position = position;
        if (dungeonRoot != null) pickup.transform.SetParent(dungeonRoot.transform, true);
        SpriteRenderer renderer = pickup.AddComponent<SpriteRenderer>();
        renderer.sprite = RoomGenerator.CircleSprite;
        renderer.color = new Color(0.88f, 0.42f, 0.74f);
        renderer.sortingOrder = 5;

        CircleCollider2D collider = pickup.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        pickup.AddComponent<RoguelikePickup>().SetupCurrency(amount);
    }

    private void SpawnItemPickup(Item item, int count, Vector2 position)
    {
        if (item == null) return;
        GameObject pickup = new GameObject("Предмет: " + item.itemName);
        pickup.transform.position = position;
        if (dungeonRoot != null) pickup.transform.SetParent(dungeonRoot.transform, true);
        SpriteRenderer renderer = pickup.AddComponent<SpriteRenderer>();
        renderer.sprite = item.icon != null ? item.icon : RoomGenerator.CircleSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 5;

        CircleCollider2D collider = pickup.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        pickup.AddComponent<RoguelikePickup>().SetupItem(item, count);
    }

    public void OpenShop(RoguelikeMerchant merchant)
    {
        if (merchant == null) return;
        activeMerchant = merchant;
        shopOpen = true;
        ShowHint("Торговец: покупайте, продавайте и меняйте посох.");
    }

    public void CloseShop()
    {
        shopOpen = false;
        activeMerchant = null;
    }

    public void ShowHint(string message)
    {
        hint = message;
        hintUntil = Time.time + 3.5f;
    }

    public void ReturnToLobby()
    {
        shopOpen = false;
        activeMerchant = null;
        inLobby = true;
        currentFloor = null;
        currentRoom = null;
        enemiesRemaining = 0;

        if (dungeonRoot != null)
        {
            for (int i = dungeonRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(dungeonRoot.transform.GetChild(i).gameObject);
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
        if (inLobby) return false;
        ReturnToLobby();
        ShowHint("Путешествие прервано. Вы вернулись в приёмную.");
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

    private void OnGUI()
    {
        PlayerRunData data = PlayerRunData.EnsureInstance();
        GUI.skin.label.fontSize = 18;
        GUI.skin.button.fontSize = 17;

        if (inLobby)
        {
            Rect panel = new Rect(Screen.width * 0.5f - 260f,
                Screen.height * 0.5f - 175f, 520f, 350f);
            GUI.Box(panel, "БИБЛИОТЕКА • ПРИЁМНАЯ");
            GUI.Label(new Rect(panel.x + 30, panel.y + 48, 460, 42),
                "В глубине библиотеки открыты три этажа.");
            GUI.Label(new Rect(panel.x + 30, panel.y + 86, 460, 30),
                "Грибочки: " + data.Mushrooms + "     Посох: " +
                (data.EquippedStaff != null ? data.EquippedStaff.displayName : "нет"));

            if (GUI.Button(new Rect(panel.x + 35, panel.y + 132, 450, 48),
                "Этаж I • Забытые каталоги"))
                StartFloor(1);
            if (GUI.Button(new Rect(panel.x + 35, panel.y + 190, 450, 48),
                "Этаж II • Архив призраков"))
                StartFloor(2);
            if (GUI.Button(new Rect(panel.x + 35, panel.y + 248, 450, 48),
                "Этаж III • Запретное хранилище"))
                StartFloor(3);
        }
        else
        {
            string roomText = currentRoom == null ? "" : GetRoomLabel(currentRoom.Type);
            GUI.Label(new Rect(18, 16, 620, 32),
                "Этаж " + CurrentFloor + "   •   Комната " +
                (currentRoomIndex + 1) + "/" +
                (currentFloor != null ? currentFloor.Rooms.Count : 0) +
                "   •   " + roomText);
            GUI.Label(new Rect(18, 48, 420, 30),
                "Грибочки: " + data.Mushrooms + "   •   Врагов осталось: " + enemiesRemaining);

            if (currentRoom != null && currentRoom.IsExit && currentRoom.IsCleared &&
                GUI.Button(new Rect(Screen.width * 0.5f - 140,
                    Screen.height - 100, 280, 54), "Вернуться в приёмную (E)"))
                ReturnToLobby();

            if (shopOpen && activeMerchant != null)
                activeMerchant.DrawShopGUI(this);
            else
                GUI.Label(new Rect(18, Screen.height - 36, 720, 28),
                    "WASD • движение     ЛКМ • выстрел     E • взаимодействие     Tab • инвентарь");
        }

        if (!string.IsNullOrEmpty(hint) && Time.time < hintUntil)
        {
            Rect hintRect = new Rect(Screen.width * 0.5f - 320f, 18f, 640f, 42f);
            GUI.Box(hintRect, hint);
        }
    }
}
