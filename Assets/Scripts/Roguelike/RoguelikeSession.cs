using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private readonly List<WorldPlaceholder> worldPlaceholders = new List<WorldPlaceholder>();

    private sealed class WorldPlaceholder
    {
        public Transform Target;
        public string Label;
    }
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

        PlayerRunData runData = PlayerRunData.EnsureInstance();
        if (!runData.CanStartFloor(floorNumber))
        {
            ShowHint(runData.RoguelikeCompleted
                ? "Все три этажа уже пройдены."
                : "Сначала завершите предыдущий этаж.");
            return;
        }

        currentFloor = RoguelikeGenerator.GenerateFloor(floorNumber);
        currentRoomIndex = 0;
        inLobby = false;
        shopOpen = false;

        if (lobbyEnvironment != null) lobbyEnvironment.SetActive(false);
        dungeonRoot.SetActive(true);
        ShowHint("Этаж " + floorNumber + ". Исследуйте комнаты и найдите выход.");
        GenerateCurrentRoom(Vector2.zero);
    }

    public bool IsCurrentRoomCleared => currentRoom != null && currentRoom.IsCleared;

    private void GenerateCurrentRoom(Vector2 spawnPosition)
    {
        if (currentFloor == null || roomGenerator == null) return;

        currentRoom = currentFloor.GetRoom(currentRoomIndex);
        if (currentRoom == null) return;
        currentRoom.IsVisited = true;

        worldPlaceholders.Clear();
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
        renderer.sprite = roomGenerator != null && roomGenerator.MushroomSprite != null
            ? roomGenerator.MushroomSprite : RoomGenerator.CircleSprite;
        renderer.color = new Color(0.88f, 0.42f, 0.74f);
        renderer.sortingOrder = 5;
        if (roomGenerator != null)
            roomGenerator.ApplyPlaceholder(pickup, roomGenerator.MushroomSprite, "ГРИБОЧКИ", this);

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
        Sprite itemSprite = item.icon != null ? item.icon :
            roomGenerator != null && roomGenerator.ItemSprite != null
                ? roomGenerator.ItemSprite : RoomGenerator.CircleSprite;
        renderer.sprite = itemSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 5;
        if (roomGenerator != null)
            roomGenerator.ApplyPlaceholder(pickup, item.icon != null ? item.icon : roomGenerator.ItemSprite,
                "ПРЕДМЕТ: " + item.itemName, this);

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
            currentRoomIndex = 0;
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

    private void DrawMiniMap()
    {
        if (currentFloor == null || currentRoom == null) return;

        Rect frame = new Rect(Screen.width - 202f, 14f, 188f, 148f);
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

        Rect map = new Rect(frame.x + 15f, frame.y + 14f,
            frame.width - 30f, frame.height - 28f);
        DrawMiniMapRect(map, new Color(0.16f, 0.075f, 0.045f, 1f));

        float left = map.x + 18f;
        float top = map.y + 18f;
        float pitchX = currentFloor.Width > 1
            ? (map.width - 36f) / (currentFloor.Width - 1)
            : 0f;
        float pitchY = currentFloor.Height > 1
            ? (map.height - 36f) / (currentFloor.Height - 1)
            : 0f;
        float roomWidth = 16f;
        float roomHeight = 12f;
        const float corridorThickness = 3.5f;

        Vector2 RoomCentre(RoguelikeRoomData room)
        {
            return new Vector2(
                left + room.X * pitchX,
                top + (currentFloor.Height - 1 - room.Y) * pitchY);
        }

        // Corridors are drawn first. Only explored rooms are drawn as room blocks;
        // a short corridor stub marks a possible route into an undiscovered room.
        for (int i = 0; i < currentFloor.Rooms.Count; i++)
        {
            RoguelikeRoomData room = currentFloor.Rooms[i];
            if (room == null || !room.IsVisited) continue;

            Vector2 from = RoomCentre(room);
            for (int n = 0; n < room.Neighbours.Count; n++)
            {
                RoguelikeRoomData neighbour = currentFloor.GetRoom(room.Neighbours[n]);
                if (neighbour == null) continue;
                if (neighbour.IsVisited && room.Index > neighbour.Index) continue;

                Vector2 to = RoomCentre(neighbour);
                if (!neighbour.IsVisited)
                {
                    Vector2 direction = new Vector2(neighbour.X - room.X,
                        -(neighbour.Y - room.Y)).normalized;
                    float stubLength = direction.x != 0f ? pitchX * 0.42f : pitchY * 0.42f;
                    to = from + direction * stubLength;
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
            if (room == null || !room.IsVisited) continue;

            Vector2 centre = RoomCentre(room);
            Rect roomRect = new Rect(centre.x - roomWidth * 0.5f,
                centre.y - roomHeight * 0.5f, roomWidth, roomHeight);
            Color roomColour = room.Index == currentRoomIndex
                ? new Color(0.78f, 0.78f, 0.78f, 1f)
                : room.IsCleared
                    ? new Color(0.46f, 0.46f, 0.46f, 1f)
                    : new Color(0.34f, 0.34f, 0.34f, 1f);
            DrawMiniMapRect(roomRect, roomColour);
        }
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

        float[] sx = { source.x, source.x + sourceBorderX,
            source.xMax - sourceBorderX, source.xMax };
        float[] sy = { source.y, source.y + sourceBorderY,
            source.yMax - sourceBorderY, source.yMax };
        float[] dx = { destination.x, destination.x + destinationBorderX,
            destination.xMax - destinationBorderX, destination.xMax };
        float[] dy = { destination.y, destination.y + destinationBorderY,
            destination.yMax - destinationBorderY, destination.yMax };

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                Rect target = Rect.MinMaxRect(dx[column], dy[row],
                    dx[column + 1], dy[row + 1]);
                Rect uv = new Rect(sx[column] / texture.width,
                    sy[row] / texture.height,
                    (sx[column + 1] - sx[column]) / texture.width,
                    (sy[row + 1] - sy[row]) / texture.height);
                GUI.DrawTextureWithTexCoords(target, texture, uv, true);
            }
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

            if (data.RoguelikeCompleted)
            {
                GUI.Label(new Rect(panel.x + 35, panel.y + 150, 450, 56),
                    "Все три этажа пройдены. Следующая остановка: TowerDefence.");
            }
            else
            {
                GUI.Label(new Rect(panel.x + 35, panel.y + 134, 450, 34),
                    "Доступен этаж " + data.CurrentProgressFloor + " из 3");
                string floorName = data.CurrentProgressFloor == 1
                    ? "Забытые каталоги"
                    : data.CurrentProgressFloor == 2
                        ? "Архив призраков"
                        : "Запретное хранилище";
                if (GUI.Button(new Rect(panel.x + 35, panel.y + 190, 450, 58),
                    "Начать экспедицию • " + floorName))
                    StartFloor(data.CurrentProgressFloor);
            }
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

            DrawMiniMap();

            if (currentRoom != null && currentRoom.IsExit && currentRoom.IsCleared &&
                GUI.Button(new Rect(Screen.width * 0.5f - 160,
                    Screen.height - 100, 320, 54), CurrentFloor < 3
                        ? "Перейти на следующий этаж (E)"
                        : "Завершить библиотеку (E)"))
                CompleteCurrentFloor();

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

        if (Camera.main != null)
        {
            for (int i = worldPlaceholders.Count - 1; i >= 0; i--)
            {
                WorldPlaceholder placeholder = worldPlaceholders[i];
                if (placeholder.Target == null)
                {
                    worldPlaceholders.RemoveAt(i);
                    continue;
                }

                Vector3 screen = Camera.main.WorldToScreenPoint(placeholder.Target.position);
                if (screen.z <= 0f) continue;
                Rect labelRect = new Rect(screen.x - 55f, Screen.height - screen.y - 28f, 110f, 24f);
                GUI.Box(labelRect, placeholder.Label);
            }
        }
    }
}
