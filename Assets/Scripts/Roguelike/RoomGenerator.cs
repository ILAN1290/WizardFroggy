using UnityEngine;

/// <summary>
/// Creates the contents and collision geometry of the currently active room:
/// bookcase walls, shelves, traps, ghosts, chests and merchant.
/// The room graph itself is generated in RoguelikeGenerator.cs.
/// </summary>
public class RoomGenerator : MonoBehaviour
{
    // Playable rectangle matches the red boundary shown in the boss-arena reference.
    public const float RoomWidth = 16.67f;
    public const float RoomHeight = 8.6263f;
    private const float FloorWidth = 18f;
    private const float FloorHeight = 12f;
    private static readonly Vector2 PlayAreaCenter = new Vector2(-0.04f, -2.3277f);

    private static Sprite whiteSprite;
    private static Sprite circleSprite;

    [SerializeField] private bool useOnGUIIfNoSprite = true;
    [SerializeField] private Sprite meleeGhostSprite;
    [SerializeField] private Sprite rangedGhostSprite;
    [SerializeField] private Sprite[] ghostAttackSprites = new Sprite[3];
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

    private GameObject upperShelfPrefab;
    private GameObject lateralShelfPrefab;

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
        CreateRect(root, "Floor", PlayAreaCenter, new Vector2(FloorWidth, FloorHeight),
            new Color(0.16f, 0.17f, 0.22f), -10, false);

        bool left = room.Neighbours.Contains(room.Index - 1) && room.X > 0;
        bool right = room.Neighbours.Contains(room.Index + 1) && room.X < 3;
        bool down = room.Neighbours.Contains(room.Index - 4) && room.Y > 0;
        bool up = room.Neighbours.Contains(room.Index + 4) && room.Y < 3;

        BuildBoundaries(root, left, right, down, up, session);

        // The first room is a guaranteed safe spawn. Special rooms stay uncluttered.
        bool specialRoom = room.IsStartRoom || room.Type == RoguelikeRoomType.Chest ||
            room.Type == RoguelikeRoomType.Merchant || room.Type == RoguelikeRoomType.Stairs;
        if (!specialRoom) BuildObstaclePattern(root, room.Index, floorNumber);

        if (room.IsStartRoom) return 0;

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

        if (room.Type == RoguelikeRoomType.Stairs && !room.IsCleared)
        {
            SpawnGhost(root, session, floorNumber, PlayAreaCenter + new Vector2(0f, -2f), true, true);
            session.SetEnemyCount(1);
            SpawnExit(root, session);
            return 1;
        }

        if (room.Type == RoguelikeRoomType.Chest && !room.IsCleared)
        {
            GameObject chest = CreateRect(root, "Сундук", PlayAreaCenter,
                new Vector2(1.5f, 1.1f), new Color(0.56f, 0.31f, 0.12f), 4, false);
            BoxCollider2D trigger = chest.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            chest.AddComponent<RoguelikeChest>().Initialize(session);
            ApplyPlaceholder(chest, chestSprite, "СУНДУК", session);
            if (chestSprite != null)
            {
                Vector2 spriteSize = chestSprite.bounds.size;
                chest.transform.localScale = new Vector3(
                    1.5f / Mathf.Max(0.001f, spriteSize.x),
                    1.1f / Mathf.Max(0.001f, spriteSize.y), 1f);
                trigger.size = new Vector2(
                    1.5f / Mathf.Max(0.001f, chest.transform.localScale.x),
                    1.1f / Mathf.Max(0.001f, chest.transform.localScale.y));
            }
        }
        else if (room.Type == RoguelikeRoomType.Merchant)
        {
            GameObject merchant = CreateRect(root, "Торговец", PlayAreaCenter,
                new Vector2(1.1f, 1.5f), new Color(0.55f, 0.38f, 0.72f), 4, false);
            CircleCollider2D trigger = merchant.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            merchant.AddComponent<RoguelikeMerchant>().Initialize(session);
            ApplyPlaceholder(merchant, merchantSprite, "ТОРГОВЕЦ", session);
        }
        if (room.IsExit && (room.Type != RoguelikeRoomType.Stairs || room.IsCleared))
            SpawnExit(root, session);

        return 0;
    }

    private void BuildBoundaries(Transform root, bool left, bool right, bool down,
        bool up, RoguelikeSession session)
    {
        const float wall = 0.55f;
        float horizontalSegment = (RoomWidth - 2.6f) * 0.5f;
        float verticalSegment = (RoomHeight - 2.6f) * 0.5f;

        // Side walls are 20% thinner; the bottom wall is half-thickness.
        // Their outer edges sit exactly on the playable rectangle.
        float sideOffset = RoomWidth * 0.5f - wall * 0.8f * 0.5f;
        float topY = PlayAreaCenter.y + RoomHeight * 0.5f - wall * 0.5f;
        float bottomY = PlayAreaCenter.y - RoomHeight * 0.5f + wall * 0.5f * 0.5f;
        float leftX = PlayAreaCenter.x - sideOffset;
        float rightX = PlayAreaCenter.x + sideOffset;

        if (left)
        {
            CreateWallSegment(root, "Западная стена • верх",
                new Vector2(leftX, PlayAreaCenter.y + verticalSegment * 0.5f + 1.3f),
                new Vector2(verticalSegment, wall), -90f);
            CreateWallSegment(root, "Западная стена • низ",
                new Vector2(leftX, PlayAreaCenter.y - verticalSegment * 0.5f - 1.3f),
                new Vector2(verticalSegment, wall), -90f);
            CreateDoor(root, session, new Vector2(leftX, PlayAreaCenter.y),
                PlayAreaCenter + new Vector2(7f, 0f), -1, 0, -90f);
        }
        else
        {
            CreateWallSegment(root, "Западная стена",
                new Vector2(leftX, PlayAreaCenter.y), new Vector2(RoomHeight, wall), -90f);
        }

        if (right)
        {
            CreateWallSegment(root, "Восточная стена • верх",
                new Vector2(rightX, PlayAreaCenter.y + verticalSegment * 0.5f + 1.3f),
                new Vector2(verticalSegment, wall), 90f);
            CreateWallSegment(root, "Восточная стена • низ",
                new Vector2(rightX, PlayAreaCenter.y - verticalSegment * 0.5f - 1.3f),
                new Vector2(verticalSegment, wall), 90f);
            CreateDoor(root, session, new Vector2(rightX, PlayAreaCenter.y),
                PlayAreaCenter + new Vector2(-7f, 0f), 1, 0, 90f);
        }
        else
        {
            CreateWallSegment(root, "Восточная стена",
                new Vector2(rightX, PlayAreaCenter.y), new Vector2(RoomHeight, wall), 90f);
        }

        if (down)
        {
            CreateWallSegment(root, "Южная стена • левая",
                new Vector2(PlayAreaCenter.x - horizontalSegment * 0.5f - 1.3f, bottomY),
                new Vector2(horizontalSegment, wall), 180f);
            CreateWallSegment(root, "Южная стена • правая",
                new Vector2(PlayAreaCenter.x + horizontalSegment * 0.5f + 1.3f, bottomY),
                new Vector2(horizontalSegment, wall), 180f);
            CreateDoor(root, session, new Vector2(PlayAreaCenter.x, bottomY),
                PlayAreaCenter + new Vector2(0f, RoomHeight * 0.5f - 1.5f), 0, -1, 180f);
        }
        else
        {
            CreateWallSegment(root, "Южная стена",
                new Vector2(PlayAreaCenter.x, bottomY), new Vector2(RoomWidth, wall), 180f);
        }

        if (up)
        {
            CreateWallSegment(root, "Северная стена • левая",
                new Vector2(PlayAreaCenter.x - horizontalSegment * 0.5f - 1.3f, topY),
                new Vector2(horizontalSegment, wall), 0f);
            CreateWallSegment(root, "Северная стена • правая",
                new Vector2(PlayAreaCenter.x + horizontalSegment * 0.5f + 1.3f, topY),
                new Vector2(horizontalSegment, wall), 0f);
            CreateDoor(root, session, new Vector2(PlayAreaCenter.x, topY),
                PlayAreaCenter + new Vector2(0f, -RoomHeight * 0.5f + 1.5f), 0, 1, 0f);
        }
        else
        {
            CreateWallSegment(root, "Северная стена",
                new Vector2(PlayAreaCenter.x, topY), new Vector2(RoomWidth, wall), 0f);
        }
    }

    private void CreateWallSegment(Transform root, string objectName, Vector2 position,
        Vector2 size, float rotation)
    {
        GameObject wall = new GameObject(objectName);
        wall.transform.SetParent(root, false);
        wall.transform.localPosition = new Vector3(position.x, position.y, 0f);
        wall.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        // Side walls use the north-wall texture turned by 90 degrees and are
        // shortened by 20%; the southern wall is turned 180 degrees and halved.
        float angle = Mathf.Abs(rotation);
        float compression = Mathf.Abs(angle - 90f) < 0.01f
            ? 0.8f : Mathf.Abs(Mathf.DeltaAngle(rotation, 180f)) < 0.01f ? 0.5f : 1f;
        Vector2 compressedSize = new Vector2(size.x, size.y * compression);

        SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
        renderer.sprite = roomWallSprite != null ? roomWallSprite : WhiteSprite;
        renderer.sortingOrder = 2;

        BoxCollider2D blocker = wall.AddComponent<BoxCollider2D>();
        if (roomWallSprite != null)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = compressedSize;
            renderer.color = Color.white;
            blocker.size = compressedSize;
        }
        else
        {
            wall.transform.localScale = new Vector3(compressedSize.x, compressedSize.y, 1f);
            renderer.color = new Color(0.25f, 0.19f, 0.16f);
        }
    }

    private void SpawnExit(Transform root, RoguelikeSession session)
    {
        GameObject exit = CreateRect(root, "Выход", PlayAreaCenter + new Vector2(0f, 1.5f),
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
        // Shelf artwork is perspective-specific: place the authored prefabs as-is
        // and never rotate them. Eight layouts keep the combat lane and doorways open.
        int pattern = Mathf.Abs(roomIndex + floorNumber - 1) % 8;
        float y = 2.65f;
        switch (pattern)
        {
            case 0:
                CreateShelf(root, "Стеллаж СЗ", new Vector2(-4.8f, y), false, 0.48f);
                CreateShelf(root, "Стеллаж СВ", new Vector2(4.8f, y), false, 0.48f);
                CreateShelf(root, "Стеллаж ЮЗ", new Vector2(-4.8f, -y), false, 0.48f);
                CreateShelf(root, "Стеллаж ЮВ", new Vector2(4.8f, -y), false, 0.48f);
                break;
            case 1:
                CreateShelf(root, "Стеллаж север", new Vector2(0f, 3.4f), true, 0.42f);
                CreateShelf(root, "Стеллаж юг", new Vector2(0f, -3.4f), true, 0.42f);
                CreateShelf(root, "Стеллаж запад", new Vector2(-5.8f, 0f), false, 0.34f);
                CreateShelf(root, "Стеллаж восток", new Vector2(5.8f, 0f), false, 0.34f);
                break;
            case 2:
                CreateShelf(root, "Стеллаж СЗ", new Vector2(-4.5f, 2.6f), true, 0.34f);
                CreateShelf(root, "Стеллаж СВ", new Vector2(4.5f, 1.5f), true, 0.34f);
                CreateShelf(root, "Стеллаж ЮЗ", new Vector2(-4.5f, -1.5f), true, 0.34f);
                CreateShelf(root, "Стеллаж ЮВ", new Vector2(4.5f, -2.6f), true, 0.34f);
                break;
            case 3:
                CreateShelf(root, "Стеллаж СЗ", new Vector2(-5.1f, 3.15f), false, 0.38f);
                CreateShelf(root, "Стеллаж СВ", new Vector2(5.1f, 3.15f), false, 0.38f);
                CreateShelf(root, "Стеллаж ЮЗ", new Vector2(-5.1f, -3.15f), false, 0.38f);
                CreateShelf(root, "Стеллаж ЮВ", new Vector2(5.1f, -3.15f), false, 0.38f);
                break;
            case 4:
                CreateShelf(root, "Архивная линия север", new Vector2(0f, 3.1f), true, 0.5f);
                CreateShelf(root, "Архивная линия юг", new Vector2(0f, -3.1f), true, 0.5f);
                break;
            case 5:
                CreateShelf(root, "Стеллаж запад север", new Vector2(-5.3f, 2.0f), false, 0.38f);
                CreateShelf(root, "Стеллаж запад юг", new Vector2(-5.3f, -2.0f), false, 0.38f);
                CreateShelf(root, "Стеллаж восток север", new Vector2(5.3f, 2.0f), false, 0.38f);
                CreateShelf(root, "Стеллаж восток юг", new Vector2(5.3f, -2.0f), false, 0.38f);
                break;
            case 6:
                CreateShelf(root, "Полка северо-запад", new Vector2(-3.7f, 3.0f), true, 0.3f);
                CreateShelf(root, "Полка северо-восток", new Vector2(3.7f, 3.0f), true, 0.3f);
                CreateShelf(root, "Полка юго-запад", new Vector2(-3.7f, -3.0f), true, 0.3f);
                CreateShelf(root, "Полка юго-восток", new Vector2(3.7f, -3.0f), true, 0.3f);
                break;
            default:
                CreateShelf(root, "Центральный архив слева", new Vector2(-4.7f, 0f), false, 0.44f);
                CreateShelf(root, "Центральный архив справа", new Vector2(4.7f, 0f), false, 0.44f);
                CreateShelf(root, "Полка севера", new Vector2(0f, 3.5f), true, 0.3f);
                CreateShelf(root, "Полка юга", new Vector2(0f, -3.5f), true, 0.3f);
                break;
        }
    }

    private void CreateShelf(Transform root, string objectName, Vector2 position,
        bool upper, float scale)
    {
        if (upperShelfPrefab == null)
            upperShelfPrefab = Resources.Load<GameObject>("RoguelikePrefabs/Upper Book Case");
        if (lateralShelfPrefab == null)
            lateralShelfPrefab = Resources.Load<GameObject>("RoguelikePrefabs/Lateral Book Case");

        GameObject prefab = upper ? upperShelfPrefab : lateralShelfPrefab;
        if (prefab != null)
        {
            GameObject shelf = Instantiate(prefab, root);
            shelf.name = objectName;
            Vector2 alignedPosition = position + PlayAreaCenter;
            shelf.transform.localPosition = new Vector3(alignedPosition.x, alignedPosition.y, 0f);
            shelf.transform.localRotation = Quaternion.identity;
            shelf.transform.localScale = Vector3.one * scale;
            foreach (Collider2D collider in shelf.GetComponents<Collider2D>())
                collider.enabled = true;
            return;
        }

        // Safe fallback if the resource prefab has been moved from Resources.
        CreateRect(root, objectName, position + PlayAreaCenter, new Vector2(2.6f, 0.85f),
            new Color(0.3f, 0.2f, 0.16f), 2, true);
    }

    private void SpawnGhost(Transform root, RoguelikeSession session, int floor,
        Vector2 position, bool melee, bool miniBoss = false)
    {
        GameObject ghost = new GameObject(miniBoss ? "Мини-босс • Красный призрак" : melee
            ? "Призрак-библиотекарь" : "Призрак-читатель");
        ghost.transform.SetParent(root, false);
        ghost.transform.localPosition = new Vector3(position.x, position.y, 0f);
        ghost.tag = "Enemy";

        // Keep physics on the root at unit scale. Only the visual child is resized,
        // so the hitbox remains correctly sized in world units.
        GameObject visual = new GameObject("Спрайт призрака");
        visual.transform.SetParent(ghost.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = melee ? meleeGhostSprite : rangedGhostSprite;
        if (renderer.sprite == null) renderer.sprite = CircleSprite;
        renderer.color = miniBoss ? new Color(1f, 0.35f, 0.35f, 1f) : melee
            ? Color.white : new Color(0.38f, 0.62f, 1f, 1f);
        renderer.sortingOrder = miniBoss ? 8 : 4;

        Vector2 desiredSize = miniBoss ? new Vector2(1.0f, 1.5f) : new Vector2(0.8f, 1.3f);
        Vector2 spriteSize = renderer.sprite.bounds.size;
        visual.transform.localScale = new Vector3(
            desiredSize.x / Mathf.Max(0.001f, spriteSize.x),
            desiredSize.y / Mathf.Max(0.001f, spriteSize.y), 1f);

        CircleCollider2D collider = ghost.AddComponent<CircleCollider2D>();
        collider.radius = miniBoss ? 0.72f : 0.56f;
        Rigidbody2D body = ghost.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        GhostEnemy enemy = ghost.AddComponent<GhostEnemy>();
        enemy.Initialize(session, floor, melee, miniBoss, ghostAttackSprites);
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
        point.x *= 6.0f;
        point.y *= 3.1f;
        if (point.magnitude < 2.8f) point = point.normalized * 3.0f;
        return PlayAreaCenter + point;
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
