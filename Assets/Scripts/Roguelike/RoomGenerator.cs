using UnityEngine;

/// <summary>
/// Creates the contents and collision geometry of the currently active room:
/// bookcase walls, shelves, traps, ghosts, chests and merchant.
/// The room graph itself is generated in RoguelikeGenerator.cs.
/// </summary>
public class RoomGenerator : MonoBehaviour
{
    public const float RoomWidth = 18f;
    public const float RoomHeight = 12f;

    private static Sprite whiteSprite;
    private static Sprite circleSprite;

    [SerializeField] private bool useOnGUIIfNoSprite = true;
    [SerializeField] private Sprite meleeGhostSprite;
    [SerializeField] private Sprite rangedGhostSprite;
    [SerializeField] private Sprite chestSprite;
    [SerializeField] private Sprite merchantSprite;
    [SerializeField] private Sprite exitSprite;
    [SerializeField] private Sprite trapSprite;
    [SerializeField] private Sprite mushroomSprite;
    [SerializeField] private Sprite itemSprite;

    [Header("Геометрия комнаты")]
    [SerializeField] private Sprite roomWallSprite;
    [SerializeField] private Sprite[] closedDoorSprites = new Sprite[6];
    [SerializeField] private Sprite[] openedDoorSprites = new Sprite[6];
    [SerializeField] private Sprite interiorObstacleSprite;

    [Header("Миникарта")]
    [SerializeField] private Sprite minimapFrameSprite;

    public Sprite MinimapFrameSprite => minimapFrameSprite;
    public static Sprite WhiteSquareSprite => WhiteSprite;

    public static Sprite CircleSprite
    {
        get
        {
            if (circleSprite == null) circleSprite = CreateCircleSprite();
            return circleSprite;
        }
    }

    public int GenerateRoom(Transform root, RoguelikeRoomData room, int floorNumber,
        RoguelikeSession session)
    {
        CreateRect(root, "Floor", Vector2.zero, new Vector2(RoomWidth, RoomHeight),
            new Color(0.16f, 0.17f, 0.22f), -10, false);

        bool left = room.Neighbours.Contains(room.Index - 1) && room.X > 0;
        bool right = room.Neighbours.Contains(room.Index + 1) && room.X < 3;
        bool down = room.Neighbours.Contains(room.Index - 4) && room.Y > 0;
        bool up = room.Neighbours.Contains(room.Index + 4) && room.Y < 2;

        BuildBoundaries(root, left, right, down, up, session);
        BuildObstaclePattern(root, room.Index, floorNumber);

        if (room.Type == RoguelikeRoomType.Combat && !room.IsCleared)
        {
            int count = Random.Range(2 + floorNumber, 4 + floorNumber);
            for (int i = 0; i < count; i++)
                SpawnGhost(root, session, floorNumber, RandomPointAwayFromCentre(),
                    Random.value > 0.30f);
            for (int i = 0; i < Mathf.Clamp(floorNumber, 1, 3); i++)
                SpawnTrap(root, RandomPointAwayFromCentre());
            if (room.IsExit) SpawnExit(root, session);
            session.SetEnemyCount(count);
            return count;
        }

        if (room.Type == RoguelikeRoomType.Chest && !room.IsCleared)
        {
            GameObject chest = CreateRect(root, "Сундук", Vector2.zero,
                new Vector2(1.5f, 1.1f), new Color(0.56f, 0.31f, 0.12f), 4, false);
            BoxCollider2D trigger = chest.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            chest.AddComponent<RoguelikeChest>().Initialize(session);
            ApplyPlaceholder(chest, chestSprite, "СУНДУК", session);
        }
        else if (room.Type == RoguelikeRoomType.Merchant)
        {
            GameObject merchant = CreateRect(root, "Торговец", Vector2.zero,
                new Vector2(1.1f, 1.5f), new Color(0.55f, 0.38f, 0.72f), 4, false);
            CircleCollider2D trigger = merchant.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            merchant.AddComponent<RoguelikeMerchant>().Initialize(session);
            ApplyPlaceholder(merchant, merchantSprite, "ТОРГОВЕЦ", session);
        }
        if (room.IsExit)
            SpawnExit(root, session);

        return 0;
    }

    private void BuildBoundaries(Transform root, bool left, bool right, bool down,
        bool up, RoguelikeSession session)
    {
        const float wall = 0.55f;
        float horizontalSegment = (RoomWidth - 2.6f) * 0.5f;
        float verticalSegment = (RoomHeight - 2.6f) * 0.5f;
        float sideX = RoomWidth * 0.5f - wall * 0.5f;
        float topY = RoomHeight * 0.5f - wall * 0.5f;

        if (left)
        {
            CreateWallSegment(root, "Западная стена • верх",
                new Vector2(-sideX, verticalSegment * 0.5f + 1.3f),
                new Vector2(wall, verticalSegment), 90f);
            CreateWallSegment(root, "Западная стена • низ",
                new Vector2(-sideX, -verticalSegment * 0.5f - 1.3f),
                new Vector2(wall, verticalSegment), 90f);
            CreateDoor(root, session, new Vector2(-sideX, 0f),
                new Vector2(7f, 0f), -1, 0, 90f);
        }
        else
        {
            CreateWallSegment(root, "Западная стена",
                new Vector2(-sideX, 0f), new Vector2(wall, RoomHeight), 90f);
        }

        if (right)
        {
            CreateWallSegment(root, "Восточная стена • верх",
                new Vector2(sideX, verticalSegment * 0.5f + 1.3f),
                new Vector2(wall, verticalSegment), 90f);
            CreateWallSegment(root, "Восточная стена • низ",
                new Vector2(sideX, -verticalSegment * 0.5f - 1.3f),
                new Vector2(wall, verticalSegment), 90f);
            CreateDoor(root, session, new Vector2(sideX, 0f),
                new Vector2(-7f, 0f), 1, 0, 90f);
        }
        else
        {
            CreateWallSegment(root, "Восточная стена",
                new Vector2(sideX, 0f), new Vector2(wall, RoomHeight), 90f);
        }

        if (down)
        {
            CreateWallSegment(root, "Южная стена • левая",
                new Vector2(-horizontalSegment * 0.5f - 1.3f, -topY),
                new Vector2(horizontalSegment, wall), 180f);
            CreateWallSegment(root, "Южная стена • правая",
                new Vector2(horizontalSegment * 0.5f + 1.3f, -topY),
                new Vector2(horizontalSegment, wall), 180f);
            CreateDoor(root, session, new Vector2(0f, -topY),
                new Vector2(0f, 4.4f), 0, -1, 180f);
        }
        else
        {
            CreateWallSegment(root, "Южная стена",
                new Vector2(0f, -topY), new Vector2(RoomWidth, wall), 180f);
        }

        if (up)
        {
            CreateWallSegment(root, "Северная стена • левая",
                new Vector2(-horizontalSegment * 0.5f - 1.3f, topY),
                new Vector2(horizontalSegment, wall), 0f);
            CreateWallSegment(root, "Северная стена • правая",
                new Vector2(horizontalSegment * 0.5f + 1.3f, topY),
                new Vector2(horizontalSegment, wall), 0f);
            CreateDoor(root, session, new Vector2(0f, topY),
                new Vector2(0f, -4.4f), 0, 1, 0f);
        }
        else
        {
            CreateWallSegment(root, "Северная стена",
                new Vector2(0f, topY), new Vector2(RoomWidth, wall), 0f);
        }
    }

    private void CreateWallSegment(Transform root, string objectName, Vector2 position,
        Vector2 size, float rotation)
    {
        GameObject wall = new GameObject(objectName);
        wall.transform.SetParent(root, false);
        wall.transform.localPosition = new Vector3(position.x, position.y, 0f);
        wall.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
        renderer.sprite = roomWallSprite != null ? roomWallSprite : WhiteSprite;
        renderer.sortingOrder = 2;

        BoxCollider2D blocker = wall.AddComponent<BoxCollider2D>();
        if (roomWallSprite != null)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
            renderer.color = Color.white;
            blocker.size = size;
        }
        else
        {
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            renderer.color = new Color(0.25f, 0.19f, 0.16f);
        }
    }

    private void SpawnExit(Transform root, RoguelikeSession session)
    {
        GameObject exit = CreateRect(root, "Выход", new Vector2(0f, 2.5f),
            new Vector2(1.8f, 2.1f), new Color(0.32f, 0.78f, 0.64f), 4, false);
        CircleCollider2D trigger = exit.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 2f;
        exit.AddComponent<RoguelikeExit>().Initialize(session);
        ApplyPlaceholder(exit, exitSprite, "ВЫХОД", session);
    }

    private void CreateDoor(Transform root, RoguelikeSession session, Vector2 position,
        Vector2 spawn, int dx, int dy, float rotation)
    {
        GameObject door = new GameObject("Дверь библиотеки");
        door.transform.SetParent(root, false);
        door.transform.localPosition = new Vector3(position.x, position.y, -0.04f);
        door.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        BoxCollider2D blocker = door.AddComponent<BoxCollider2D>();
        blocker.size = new Vector2(2.6f, 0.68f);
        blocker.isTrigger = false;

        BoxCollider2D trigger = door.AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(2.8f, 1.05f);
        trigger.isTrigger = true;
        trigger.enabled = false;

        SpriteRenderer fallback = door.AddComponent<SpriteRenderer>();
        fallback.sprite = WhiteSprite;
        fallback.drawMode = SpriteDrawMode.Tiled;
        fallback.size = new Vector2(2.55f, 0.62f);
        fallback.sortingOrder = 3;
        fallback.enabled = false;

        SpriteRenderer[] parts = new SpriteRenderer[6];
        const float tileWidth = 0.86f;
        const float tileHeight = 0.9f;
        for (int i = 0; i < parts.Length; i++)
        {
            GameObject tile = new GameObject("Дверной тайл " + i);
            tile.transform.SetParent(door.transform, false);
            int column = i % 3;
            int row = i / 3;
            tile.transform.localPosition = new Vector3(
                (column - 1) * tileWidth,
                (0.5f - row) * tileHeight, 0f);

            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 4;
            if (closedDoorSprites != null && i < closedDoorSprites.Length &&
                closedDoorSprites[i] != null)
            {
                renderer.sprite = closedDoorSprites[i];
                Vector2 spriteSize = closedDoorSprites[i].bounds.size;
                renderer.transform.localScale = new Vector3(
                    tileWidth / Mathf.Max(0.001f, spriteSize.x),
                    tileHeight / Mathf.Max(0.001f, spriteSize.y), 1f);
            }
            parts[i] = renderer;
        }

        door.AddComponent<RoguelikeDoor>().Initialize(session, dx, dy, spawn,
            parts, closedDoorSprites, openedDoorSprites, fallback, blocker, trigger);
    }

    private void BuildObstaclePattern(Transform root, int roomIndex, int floorNumber)
    {
        // Four authored layouts are rotated through the room graph. They are fixed,
        // keep the central combat lane open and never clutter the doorway approaches.
        int pattern = Mathf.Abs(roomIndex + floorNumber - 1) % 4;
        Color colour = new Color(
            0.28f + floorNumber * 0.018f, 0.19f, 0.15f, 1f);

        switch (pattern)
        {
            case 0: // Four low book piles, arranged symmetrically.
                CreateObstacle(root, "Книжный блок • СЗ", new Vector2(-4.5f, 2.25f), new Vector2(1.65f, 0.78f), colour);
                CreateObstacle(root, "Книжный блок • СВ", new Vector2(4.5f, 2.25f), new Vector2(1.65f, 0.78f), colour);
                CreateObstacle(root, "Книжный блок • ЮЗ", new Vector2(-4.5f, -2.25f), new Vector2(1.65f, 0.78f), colour);
                CreateObstacle(root, "Книжный блок • ЮВ", new Vector2(4.5f, -2.25f), new Vector2(1.65f, 0.78f), colour);
                break;

            case 1: // Two pillars with short side stacks.
                CreateObstacle(root, "Архивная колонна • З", new Vector2(-4.7f, 0f), new Vector2(0.82f, 2.15f), colour);
                CreateObstacle(root, "Архивная колонна • В", new Vector2(4.7f, 0f), new Vector2(0.82f, 2.15f), colour);
                CreateObstacle(root, "Стопка • С", new Vector2(0f, 3.35f), new Vector2(2.0f, 0.72f), colour);
                CreateObstacle(root, "Стопка • Ю", new Vector2(0f, -3.35f), new Vector2(2.0f, 0.72f), colour);
                break;

            case 2: // Offset paired rows, leaving a zig-zag passage.
                CreateObstacle(root, "Полка • СЗ", new Vector2(-3.5f, 2.75f), new Vector2(1.8f, 0.72f), colour);
                CreateObstacle(root, "Полка • СВ", new Vector2(3.5f, 1.75f), new Vector2(1.8f, 0.72f), colour);
                CreateObstacle(root, "Полка • ЮЗ", new Vector2(-3.5f, -1.75f), new Vector2(1.8f, 0.72f), colour);
                CreateObstacle(root, "Полка • ЮВ", new Vector2(3.5f, -2.75f), new Vector2(1.8f, 0.72f), colour);
                break;

            default: // Corner L-shapes.
                CreateObstacle(root, "Угол • СЗ горизонталь", new Vector2(-5.0f, 3.1f), new Vector2(2.0f, 0.68f), colour);
                CreateObstacle(root, "Угол • СЗ вертикаль", new Vector2(-5.65f, 2.0f), new Vector2(0.68f, 1.5f), colour);
                CreateObstacle(root, "Угол • СВ горизонталь", new Vector2(5.0f, -3.1f), new Vector2(2.0f, 0.68f), colour);
                CreateObstacle(root, "Угол • СВ вертикаль", new Vector2(5.65f, -2.0f), new Vector2(0.68f, 1.5f), colour);
                break;
        }
    }

    private void CreateObstacle(Transform root, string objectName, Vector2 position,
        Vector2 size, Color fallbackColour)
    {
        GameObject obstacle = CreateSolid(root, objectName, position, size, fallbackColour);
        SpriteRenderer renderer = obstacle.GetComponent<SpriteRenderer>();
        if (renderer != null && interiorObstacleSprite != null)
        {
            renderer.sprite = interiorObstacleSprite;
            renderer.color = Color.white;
        }
        if (renderer != null) renderer.sortingOrder = 2;
    }

    private void SpawnGhost(Transform root, RoguelikeSession session, int floor,
        Vector2 position, bool melee)
    {
        GameObject ghost = new GameObject(melee
            ? "Призрак-библиотекарь" : "Призрак-читатель");
        ghost.transform.SetParent(root, false);
        ghost.transform.position = position;
        ghost.tag = "Enemy";

        SpriteRenderer renderer = ghost.AddComponent<SpriteRenderer>();
        renderer.sprite = CircleSprite;
        renderer.color = melee
            ? new Color(0.55f, 0.9f, 0.9f)
            : new Color(0.75f, 0.68f, 0.98f);
        renderer.sortingOrder = 4;
        ApplyPlaceholder(ghost, melee ? meleeGhostSprite : rangedGhostSprite,
            melee ? "ПРИЗРАК • БЛИЖНИЙ БОЙ" : "ПРИЗРАК • ДАЛЬНИЙ БОЙ", session);

        CircleCollider2D collider = ghost.AddComponent<CircleCollider2D>();
        collider.radius = 0.56f;
        Rigidbody2D body = ghost.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        GhostEnemy enemy = ghost.AddComponent<GhostEnemy>();
        enemy.Initialize(session, floor, melee);
    }

    private void SpawnTrap(Transform root, Vector2 position)
    {
        GameObject trap = CreateRect(root, "Магическая ловушка", position,
            new Vector2(1.15f, 0.85f), new Color(0.65f, 0.19f, 0.29f), 1, false);
        BoxCollider2D collider = trap.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        trap.AddComponent<RoguelikeTrap>();
        ApplyPlaceholder(trap, trapSprite, "ЛОВУШКА", RoguelikeSession.Instance);
    }

    public Sprite MushroomSprite => mushroomSprite;
    public Sprite ItemSprite => itemSprite;

    public void ApplyPickupPlaceholder(GameObject target, Sprite itemIcon, string label,
        RoguelikeSession session)
    {
        ApplyPlaceholder(target, itemIcon != null ? itemIcon : itemSprite, label, session);
    }

    public void ApplyPlaceholder(GameObject target, Sprite sprite, string label,
        RoguelikeSession session)
    {
        if (target == null) return;
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer != null && sprite != null)
        {
            renderer.sprite = sprite;
            renderer.color = Color.white;
        }
        else if (sprite == null && useOnGUIIfNoSprite && session != null)
        {
            session.RegisterWorldPlaceholder(target.transform, label);
        }
    }

    private Vector2 RandomPointAwayFromCentre()
    {
        Vector2 point = Random.insideUnitCircle;
        point.x *= 6.2f;
        point.y *= 3.8f;
        if (point.magnitude < 3f) point = point.normalized * 3.5f;
        return point;
    }

    private GameObject CreateSolid(Transform parent, string objectName, Vector2 position,
        Vector2 size, Color color)
    {
        return CreateRect(parent, objectName, position, size, color, 2, true);
    }

    private GameObject CreateRect(Transform parent, string objectName, Vector2 position,
        Vector2 size, Color color, int sortingOrder, bool collider)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(position.x, position.y, 0f);
        obj.transform.localScale = new Vector3(
            Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = WhiteSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (collider)
            obj.AddComponent<BoxCollider2D>();

        return obj;
    }

    private static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] {
                    Color.white, Color.white, Color.white, Color.white
                });
                texture.Apply();
                whiteSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2),
                    new Vector2(0.5f, 0.5f), 2f);
            }
            return whiteSprite;
        }
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 centre = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), centre);
                pixels[y * size + x] = distance <= 14f ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 16f);
    }
}
