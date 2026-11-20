using UnityEngine;

/// <summary>
/// Creates the contents and collision geometry of the currently active room:
/// bookcase walls, shelves, traps, ghosts, chests and merchant.
/// The room graph itself is generated in RoguelikeGenerator.cs.
/// </summary>
public class RoomGenerator : MonoBehaviour
{
    // Room dimensions include the wall frame. The walkable wooden area sits inside it.
    public const float RoomWidth = 21f;
    public const float RoomHeight = 10.1f;
    private const float FloorWidth = 23f;
    private const float FloorHeight = 12f;
    private static readonly Vector2 PlayAreaCenter = Vector2.zero;

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
        int floorSeed, RoguelikeSession session)
    {
        CreateRect(root, "Floor", PlayAreaCenter, new Vector2(FloorWidth, FloorHeight),
            new Color(0.16f, 0.17f, 0.22f), -10, false);

        RoguelikeRoomLayout layout = RoguelikeRoomLayoutGenerator.Create(
            room, floorNumber, floorSeed, RoomWidth, RoomHeight, PlayAreaCenter);

        // The layout file decides where every wall, door, shelf, trap and enemy belongs.
        foreach (RoguelikeWallSection wall in layout.Walls)
            CreateWallSegment(root, wall.Name, wall.Position, wall.Size, wall.Rotation);

        foreach (RoguelikeDoorPlacement door in layout.Doors)
            CreateDoor(root, session, door.Position, door.SpawnPosition,
                door.DirectionX, door.DirectionY, door.Rotation);

        foreach (RoguelikeShelfPlacement shelf in layout.Shelves)
            CreateShelf(root, shelf.Name, shelf.Position, shelf.Upper, shelf.Scale);

        foreach (Vector2 trapPosition in layout.TrapPositions)
            SpawnTrap(root, trapPosition);

        foreach (RoguelikeGhostPlacement ghost in layout.Ghosts)
            SpawnGhost(root, session, floorNumber, ghost.Position,
                ghost.Melee, ghost.MiniBoss);

        if (layout.SpawnChest)
        {
            GameObject chest = CreateRect(root, "Сундук", layout.ChestPosition,
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

        if (layout.SpawnMerchant)
        {
            GameObject merchant = CreateRect(root, "Торговец", layout.MerchantPosition,
                new Vector2(1.1f, 1.5f), new Color(0.55f, 0.38f, 0.72f), 4, false);
            CircleCollider2D trigger = merchant.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            merchant.AddComponent<RoguelikeMerchant>().Initialize(session);
            ApplyPlaceholder(merchant, merchantSprite, "ТОРГОВЕЦ", session);
        }

        if (layout.SpawnExit)
            SpawnExit(root, session, layout.ExitPosition);

        session.SetEnemyCount(layout.Ghosts.Count);
        return layout.Ghosts.Count;
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

    private void SpawnExit(Transform root, RoguelikeSession session, Vector2 position)
    {
        GameObject exit = CreateRect(root, "Выход", position,
            new Vector2(1.8f, 2.1f), new Color(0.32f, 0.78f, 0.64f), 4, false);
        CircleCollider2D trigger = exit.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 1.1f;
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
        blocker.size = new Vector2(2.1f, 0.68f);
        blocker.isTrigger = false;

        BoxCollider2D trigger = door.AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(2.35f, 1.05f);
        trigger.isTrigger = true;
        trigger.enabled = false;

        SpriteRenderer fallback = door.AddComponent<SpriteRenderer>();
        fallback.sprite = WhiteSprite;
        fallback.drawMode = SpriteDrawMode.Tiled;
        fallback.size = new Vector2(2.55f, 0.62f);
        fallback.sortingOrder = 3;
        fallback.enabled = false;

        SpriteRenderer[] parts = new SpriteRenderer[6];
        const float tileWidth = 0.68f;
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
            shelf.transform.localPosition = new Vector3(position.x, position.y, 0f);
            shelf.transform.localRotation = Quaternion.identity;
            shelf.transform.localScale = Vector3.one * scale;
            foreach (Collider2D collider in shelf.GetComponents<Collider2D>())
                collider.enabled = true;
            return;
        }

        CreateRect(root, objectName, position, new Vector2(2.6f, 0.85f),
            new Color(0.3f, 0.2f, 0.16f), 2, true);
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
