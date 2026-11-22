using UnityEngine;

/// <summary>
/// Creates tiled room boundaries, locked doors and the contents of the active room:
/// shelves, traps, ghosts, chests and merchant.
/// The room graph itself is generated in RoguelikeGenerator.cs.
/// </summary>
public class RoomGenerator : MonoBehaviour
{
    // Room dimensions include the wall frame. The walkable wooden area sits inside it.
    public const float RoomWidth = 17.5f;
    public const float RoomHeight = 7.6f;

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

    [Header("Стены и двери")]
    [SerializeField] private Sprite upperWallTileSprite;
    [SerializeField] private Sprite leftWallTileSprite;
    [SerializeField] private Sprite rightWallTileSprite;
    [SerializeField] private Sprite bottomWallTileSprite;
    [SerializeField] private Sprite[] mossyWallTiles = new Sprite[3];
    [SerializeField] private Sprite[] closedUpperDoorSprites = new Sprite[6];
    [SerializeField] private Sprite[] openedUpperDoorSprites = new Sprite[6];

    private Transform cachedPlayerTransform;
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

    public int GenerateRoom(Transform environmentRoot, Transform enemiesRoot,
        Transform effectsRoot, Transform projectilesRoot, RoguelikeRoomData room,
        int floorNumber, int floorSeed, RoguelikeSession session)
    {
        Transform root = environmentRoot;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        cachedPlayerTransform = playerObject != null ? playerObject.transform : null;

        GetPlayableMetrics(out Vector2 roomCentre, out float roomWidth, out float roomHeight);
        RoguelikeRoomLayout layout = RoguelikeRoomLayoutGenerator.Create(
            room, floorNumber, floorSeed, roomWidth, roomHeight, roomCentre);

        CreateRoomBoundaries(root, session, room, roomCentre, roomWidth, roomHeight);

        foreach (RoguelikeShelfPlacement shelf in layout.Shelves)
            CreateShelf(root, shelf.Name, shelf.Position, shelf.Upper, shelf.Scale);

        foreach (Vector2 trapPosition in layout.TrapPositions)
            SpawnTrap(root, trapPosition);

        foreach (RoguelikeGhostPlacement ghost in layout.Ghosts)
            SpawnGhost(enemiesRoot, effectsRoot, projectilesRoot, session,
                floorNumber, ghost.Position, ghost.Melee, ghost.MiniBoss);

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

    private void CreateRoomBoundaries(Transform root, RoguelikeSession session,
        RoguelikeRoomData room, Vector2 centre, float width, float height)
    {
        if (room == null || session == null) return;

        Transform wallTilesRoot = CreateFolder(root, "Wall Tiles");
        Transform wallCollidersRoot = CreateFolder(root, "Wall Colliders");
        Transform doorsRoot = CreateFolder(root, "Doors");

        bool hasLeft = room.X > 0 && room.Neighbours.Contains(room.Index - 1);
        bool hasRight = room.X < 3 && room.Neighbours.Contains(room.Index + 1);
        bool hasDown = room.Y > 0 && room.Neighbours.Contains(room.Index - 4);
        bool hasUp = room.Y < 3 && room.Neighbours.Contains(room.Index + 4);

        float left = centre.x - width * 0.5f;
        float right = centre.x + width * 0.5f;
        float bottom = centre.y - height * 0.5f;
        float top = centre.y + height * 0.5f;

        CreateUpperWall(wallTilesRoot, wallCollidersRoot, doorsRoot,
            session, room, centre, width, top, hasUp);
        CreateVerticalWall(wallTilesRoot, wallCollidersRoot, doorsRoot,
            session, "Left", left - 0.5f, centre.y, height,
            leftWallTileSprite, hasLeft, -1, 0,
            new Vector2(right - 5.8f, centre.y));
        CreateVerticalWall(wallTilesRoot, wallCollidersRoot, doorsRoot,
            session, "Right", right + 0.5f, centre.y, height,
            rightWallTileSprite, hasRight, 1, 0,
            new Vector2(left + 5.8f, centre.y));
        CreateBottomWall(wallTilesRoot, wallCollidersRoot, doorsRoot,
            session, centre, width, bottom, hasDown);
    }

    private static Transform CreateFolder(Transform parent, string folderName)
    {
        GameObject folder = new GameObject(folderName);
        folder.transform.SetParent(parent, false);
        return folder.transform;
    }

    private void CreateUpperWall(Transform tilesRoot, Transform collidersRoot,
        Transform doorsRoot, RoguelikeSession session, RoguelikeRoomData room,
        Vector2 centre, float width, float innerY, bool hasDoor)
    {
        Sprite baseTile = upperWallTileSprite;
        if (baseTile == null) return;

        const int rows = 3;
        const int doorWidthTiles = 3;
        float tileWidth = Mathf.Max(0.001f, baseTile.bounds.size.x);
        float tileHeight = Mathf.Max(0.001f, baseTile.bounds.size.y);
        int fullColumns = Mathf.Max(1, Mathf.FloorToInt(width / tileWidth));
        float coveredWidth = fullColumns * tileWidth;
        float left = centre.x - coveredWidth * 0.5f;
        int centreColumn = fullColumns / 2;
        int firstDoorColumn = centreColumn - doorWidthTiles / 2;
        int lastDoorColumn = firstDoorColumn + doorWidthTiles - 1;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < fullColumns; column++)
            {
                bool insideDoor = hasDoor && row < 2 &&
                    column >= firstDoorColumn && column <= lastDoorColumn;
                if (insideDoor) continue;

                Sprite tile = ChooseUpperWallTile(room.Index, column, row);
                CreateWallTile(tilesRoot, "Верхняя стена • тайл",
                    new Vector2(left + (column + 0.5f) * tileWidth,
                        innerY + (row + 0.5f) * tileHeight),
                    tile, Color.white, 3);
            }

            // The source room width is not an integer number of 16x16 tiles.
            // Two narrow edge pieces close the remaining half-tile without
            // stretching the complete wall texture.
            float remainder = width - coveredWidth;
            if (remainder > 0.001f)
            {
                float partialWidth = remainder * 0.5f;
                float rowY = innerY + (row + 0.5f) * tileHeight;
                Sprite leftTile = ChooseUpperWallTile(room.Index, -1, row);
                Sprite rightTile = ChooseUpperWallTile(room.Index, fullColumns, row);
                CreateWallTile(tilesRoot, "Верхняя стена • край",
                    new Vector2(centre.x - width * 0.5f + partialWidth * 0.5f, rowY),
                    leftTile, Color.white, 3, new Vector2(partialWidth / tileWidth, 1f));
                CreateWallTile(tilesRoot, "Верхняя стена • край",
                    new Vector2(centre.x + width * 0.5f - partialWidth * 0.5f, rowY),
                    rightTile, Color.white, 3, new Vector2(partialWidth / tileWidth, 1f));
            }
        }

        float colliderY = innerY + tileHeight * 0.5f;
        CreateHorizontalWallColliders(collidersRoot, "Верхняя стена",
            centre.x, colliderY, width, tileHeight,
            hasDoor, doorWidthTiles * tileWidth);

        if (hasDoor)
        {
            Vector2 doorPosition = new Vector2(centre.x, colliderY);
            Vector2 spawn = new Vector2(centre.x, innerY - 4.1f);
            CreateUpperDoor(doorsRoot, session, doorPosition, spawn,
                doorWidthTiles * tileWidth, tileWidth, tileHeight);
        }
    }

    private Sprite ChooseUpperWallTile(int roomIndex, int column, int row)
    {
        if (mossyWallTiles == null || mossyWallTiles.Length == 0)
            return upperWallTileSprite;

        // Deterministic pattern: the vines are mixed into the stonework but
        // retain the exact 16x16 source tile proportions.
        int pattern = Mathf.Abs(roomIndex * 7 + column * 5 + row * 3) % 9;
        bool useVine = row > 0
            ? pattern == 0 || (row == 2 && pattern == 4)
            : pattern == 0;
        if (!useVine) return upperWallTileSprite;

        int vineIndex = Mathf.Abs(roomIndex + column + row) % mossyWallTiles.Length;
        return mossyWallTiles[vineIndex] != null
            ? mossyWallTiles[vineIndex] : upperWallTileSprite;
    }

    private void CreateVerticalWall(Transform tilesRoot, Transform collidersRoot,
        Transform doorsRoot, RoguelikeSession session, string sideName,
        float wallX, float centreY, float height, Sprite tileSprite,
        bool hasDoor, int dx, int dy, Vector2 spawnPosition)
    {
        if (tileSprite == null) return;

        float tileWidth = Mathf.Max(0.001f, tileSprite.bounds.size.x);
        float tileHeight = Mathf.Max(0.001f, tileSprite.bounds.size.y);
        int rows = Mathf.Max(2, Mathf.CeilToInt(height / tileHeight));
        if (rows % 2 != 0) rows++;
        int firstDoorRow = rows / 2 - 1;
        int lastDoorRow = firstDoorRow + 1;
        float coveredHeight = rows * tileHeight;
        float bottom = centreY - coveredHeight * 0.5f;

        for (int row = 0; row < rows; row++)
        {
            if (hasDoor && row >= firstDoorRow && row <= lastDoorRow)
                continue;

            CreateWallTile(tilesRoot, sideName + " wall • tile",
                new Vector2(wallX, bottom + (row + 0.5f) * tileHeight),
                tileSprite, Color.white, 3);
        }

        CreateVerticalWallColliders(collidersRoot, sideName + " wall",
            wallX, centreY, height, tileWidth,
            hasDoor, 2f * tileHeight);

        if (!hasDoor) return;

        Vector2 doorPosition = new Vector2(wallX, centreY);
        Color lockedTint = new Color(0.58f, 0.58f, 0.58f, 1f);
        CreateWallTileDoor(doorsRoot, session, sideName + " wall door",
            doorPosition, spawnPosition, new Vector2(tileWidth, 2f * tileHeight),
            new Vector2(tileWidth + 0.55f, 2.5f * tileHeight),
            dx < 0 ? new Vector2(0.65f, 0f) : new Vector2(-0.65f, 0f),
            dx, dy, tileSprite, 1, 2, lockedTint);
    }

    private void CreateBottomWall(Transform tilesRoot, Transform collidersRoot,
        Transform doorsRoot, RoguelikeSession session, Vector2 centre,
        float width, float innerY, bool hasDoor)
    {
        Sprite tile = bottomWallTileSprite;
        if (tile == null) return;

        float tileWidth = Mathf.Max(0.001f, tile.bounds.size.x);
        float tileHeight = Mathf.Max(0.001f, tile.bounds.size.y);
        int fullColumns = Mathf.Max(1, Mathf.FloorToInt(width / tileWidth));
        float coveredWidth = fullColumns * tileWidth;
        float left = centre.x - coveredWidth * 0.5f;
        const int doorWidthTiles = 3;
        int centreColumn = fullColumns / 2;
        int firstDoorColumn = centreColumn - doorWidthTiles / 2;
        int lastDoorColumn = firstDoorColumn + doorWidthTiles - 1;

        for (int column = 0; column < fullColumns; column++)
        {
            if (hasDoor && column >= firstDoorColumn && column <= lastDoorColumn)
                continue;

            CreateWallTile(tilesRoot, "Нижняя стена • тайл",
                new Vector2(left + (column + 0.5f) * tileWidth, innerY - tileHeight * 0.5f),
                tile, Color.white, 3);
        }

        float remainder = width - coveredWidth;
        if (remainder > 0.001f)
        {
            float partialWidth = remainder * 0.5f;
            CreateWallTile(tilesRoot, "Нижняя стена • край",
                new Vector2(centre.x - width * 0.5f + partialWidth * 0.5f,
                    innerY - tileHeight * 0.5f),
                tile, Color.white, 3, new Vector2(partialWidth / tileWidth, 1f));
            CreateWallTile(tilesRoot, "Нижняя стена • край",
                new Vector2(centre.x + width * 0.5f - partialWidth * 0.5f,
                    innerY - tileHeight * 0.5f),
                tile, Color.white, 3, new Vector2(partialWidth / tileWidth, 1f));
        }

        CreateHorizontalWallColliders(collidersRoot, "Нижняя стена",
            centre.x, innerY - tileHeight * 0.5f, width, tileHeight,
            hasDoor, doorWidthTiles * tileWidth);

        if (hasDoor)
        {
            Color lockedTint = new Color(0.58f, 0.58f, 0.58f, 1f);
            CreateWallTileDoor(doorsRoot, session, "Нижняя дверь",
                new Vector2(centre.x, innerY - tileHeight * 0.5f),
                new Vector2(centre.x, innerY + 4.1f),
                new Vector2(doorWidthTiles * tileWidth, tileHeight),
                new Vector2(doorWidthTiles * tileWidth + 0.5f, tileHeight + 0.8f),
                new Vector2(0f, 0.65f),
                0, -1, tile, doorWidthTiles, 2, lockedTint);
        }
    }

    private void CreateUpperDoor(Transform doorsRoot, RoguelikeSession session,
        Vector2 position, Vector2 spawnPosition, float width,
        float tileWidth, float tileHeight)
    {
        GameObject door = CreateDoorRoot(doorsRoot, "Верхняя дверь", position);
        BoxCollider2D blocker = door.AddComponent<BoxCollider2D>();
        blocker.size = new Vector2(width, tileHeight);

        BoxCollider2D sensor = door.AddComponent<BoxCollider2D>();
        sensor.isTrigger = true;
        sensor.size = new Vector2(width, tileHeight + 0.8f);
        sensor.offset = new Vector2(0f, -0.65f);

        SpriteRenderer[] renderers = new SpriteRenderer[6];
        for (int i = 0; i < renderers.Length; i++)
        {
            int column = i % 3;
            int row = i / 3;
            SpriteRenderer renderer = CreateDoorTile(door.transform,
                "Верхняя дверь • тайл " + i,
                new Vector2((column - 1) * tileWidth,
                    0.5f + (0.5f - row) * tileHeight),
                closedUpperDoorSprites != null && i < closedUpperDoorSprites.Length
                    ? closedUpperDoorSprites[i] : null,
                Color.white);
            renderers[i] = renderer;
        }

        door.AddComponent<RoguelikeRoomTransition>().Initialize(
            session, 0, 1, spawnPosition, blocker, sensor, renderers,
            closedUpperDoorSprites, openedUpperDoorSprites, Color.white, false);
    }

    private void CreateWallTileDoor(Transform doorsRoot, RoguelikeSession session,
        string doorName, Vector2 position, Vector2 spawnPosition,
        Vector2 blockerSize, Vector2 sensorSize, Vector2 sensorOffset,
        int directionX, int directionY, Sprite tileSprite,
        int columns, int rows, Color lockedTint)
    {
        GameObject door = CreateDoorRoot(doorsRoot, doorName, position);
        BoxCollider2D blocker = door.AddComponent<BoxCollider2D>();
        blocker.size = blockerSize;

        BoxCollider2D sensor = door.AddComponent<BoxCollider2D>();
        sensor.isTrigger = true;
        sensor.size = sensorSize;
        sensor.offset = sensorOffset;

        SpriteRenderer[] renderers = new SpriteRenderer[columns * rows];
        Sprite[] lockedSprites = new Sprite[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            int column = i % columns;
            int row = i / columns;
            float tileWidth = Mathf.Max(0.001f, tileSprite.bounds.size.x);
            float tileHeight = Mathf.Max(0.001f, tileSprite.bounds.size.y);
            float y = (0.5f - row) * tileHeight;
            SpriteRenderer renderer = CreateDoorTile(door.transform,
                doorName + " • тайл " + i,
                new Vector2((column - (columns - 1) * 0.5f) * tileWidth, y),
                tileSprite, lockedTint);
            renderers[i] = renderer;
            lockedSprites[i] = tileSprite;
        }

        door.AddComponent<RoguelikeRoomTransition>().Initialize(
            session, directionX, directionY, spawnPosition,
            blocker, sensor, renderers, lockedSprites,
            new Sprite[0], lockedTint, true);
    }

    private static GameObject CreateDoorRoot(Transform parent, string name, Vector2 position)
    {
        GameObject door = new GameObject(name);
        door.tag = "Wall";
        door.transform.SetParent(parent, false);
        door.transform.localPosition = new Vector3(position.x, position.y, 0f);
        return door;
    }

    private static SpriteRenderer CreateDoorTile(Transform parent, string name,
        Vector2 localPosition, Sprite sprite, Color tint)
    {
        GameObject tile = new GameObject(name);
        tile.transform.SetParent(parent, false);
        tile.transform.localPosition = new Vector3(localPosition.x, localPosition.y, -0.02f);

        SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        renderer.sortingOrder = 5;
        return renderer;
    }

    private static void CreateWallTile(Transform parent, string name,
        Vector2 position, Sprite sprite, Color tint, int sortingOrder,
        Vector2 scale = default)
    {
        if (sprite == null) return;
        if (scale == default) scale = Vector2.one;

        GameObject tile = new GameObject(name);
        tile.transform.SetParent(parent, false);
        tile.transform.localPosition = new Vector3(position.x, position.y, 0f);
        tile.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        renderer.sortingOrder = sortingOrder;
    }

    private static void CreateHorizontalWallColliders(Transform parent,
        string wallName, float centreX, float y, float totalWidth, float thickness,
        bool hasDoor, float doorWidth)
    {
        if (!hasDoor || totalWidth <= doorWidth)
        {
            CreateWallCollider(parent, wallName, new Vector2(centreX, y),
                new Vector2(totalWidth, thickness));
            return;
        }

        float segmentWidth = (totalWidth - doorWidth) * 0.5f;
        CreateWallCollider(parent, wallName + " • левая часть",
            new Vector2(centreX - doorWidth * 0.5f - segmentWidth * 0.5f, y),
            new Vector2(segmentWidth, thickness));
        CreateWallCollider(parent, wallName + " • правая часть",
            new Vector2(centreX + doorWidth * 0.5f + segmentWidth * 0.5f, y),
            new Vector2(segmentWidth, thickness));
    }

    private static void CreateVerticalWallColliders(Transform parent,
        string wallName, float x, float centreY, float totalHeight, float thickness,
        bool hasDoor, float doorHeight)
    {
        if (!hasDoor || totalHeight <= doorHeight)
        {
            CreateWallCollider(parent, wallName, new Vector2(x, centreY),
                new Vector2(thickness, totalHeight));
            return;
        }

        float segmentHeight = (totalHeight - doorHeight) * 0.5f;
        CreateWallCollider(parent, wallName + " • нижняя часть",
            new Vector2(x, centreY - doorHeight * 0.5f - segmentHeight * 0.5f),
            new Vector2(thickness, segmentHeight));
        CreateWallCollider(parent, wallName + " • верхняя часть",
            new Vector2(x, centreY + doorHeight * 0.5f + segmentHeight * 0.5f),
            new Vector2(thickness, segmentHeight));
    }

    private static void CreateWallCollider(Transform parent, string name,
        Vector2 position, Vector2 size)
    {
        if (size.x <= 0.01f || size.y <= 0.01f) return;

        GameObject wall = new GameObject(name);
        wall.tag = "Wall";
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = new Vector3(position.x, position.y, 0f);
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
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
            shelf.tag = "Wall";
            shelf.transform.localPosition = new Vector3(position.x, position.y, 0f);
            shelf.transform.localRotation = Quaternion.identity;
            // Enlarge room furniture uniformly so the source sprite proportions stay intact.
            shelf.transform.localScale = Vector3.one * (scale * 2f);
            foreach (Collider2D collider in shelf.GetComponents<Collider2D>())
                collider.enabled = true;
            return;
        }

        GameObject fallbackShelf = CreateRect(root, objectName, position,
            new Vector2(2.6f, 0.85f), new Color(0.3f, 0.2f, 0.16f), 2, true);
        fallbackShelf.tag = "Wall";
    }

    private void SpawnGhost(Transform root, Transform effectsRoot,
        Transform projectilesRoot, RoguelikeSession session, int floor,
        Vector2 position, bool melee, bool miniBoss = false)
    {
        GameObject ghost = new GameObject(miniBoss ? "Мини-босс • Красный призрак" : melee
            ? "Призрак-библиотекарь" : "Призрак-читатель");
        ghost.transform.SetParent(root, false);
        ghost.transform.localPosition = new Vector3(position.x, position.y, 0f);
        ghost.tag = "Enemy";

        // Physics stays on the root. The visual child receives one uniform scale,
        // preserving the source sprite aspect ratio and matching the frog's height.
        GameObject visual = new GameObject("Спрайт призрака");
        visual.transform.SetParent(ghost.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = melee ? meleeGhostSprite : rangedGhostSprite;
        if (renderer.sprite == null) renderer.sprite = CircleSprite;
        renderer.color = miniBoss ? new Color(1f, 0.28f, 0.32f, 1f) : melee
            ? Color.white : new Color(0.38f, 0.62f, 1f, 1f);
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = miniBoss ? 21 : 20;

        float frogHeight = 1f;
        if (cachedPlayerTransform != null)
        {
            SpriteRenderer frogRenderer = cachedPlayerTransform.GetComponent<SpriteRenderer>();
            if (frogRenderer == null)
                frogRenderer = cachedPlayerTransform.GetComponentInChildren<SpriteRenderer>();
            if (frogRenderer != null && frogRenderer.bounds.size.y > 0.05f)
                frogHeight = frogRenderer.bounds.size.y;
        }

        float targetHeight = Mathf.Clamp(frogHeight, 0.65f, 1.35f) *
            (miniBoss ? 1.2f : 1f);
        float sourceHeight = Mathf.Max(0.001f, renderer.sprite.bounds.size.y);
        visual.transform.localScale = Vector3.one * (targetHeight / sourceHeight);

        CircleCollider2D collider = ghost.AddComponent<CircleCollider2D>();
        collider.radius = miniBoss ? 0.52f : 0.43f;

        Rigidbody2D body = ghost.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        GhostEnemy enemy = ghost.AddComponent<GhostEnemy>();
        enemy.Initialize(session, floor, melee, miniBoss, ghostAttackSprites,
            cachedPlayerTransform, effectsRoot, projectilesRoot);
    }

    private void SpawnTrap(Transform root, Vector2 position)
    {
        // Keep the damage trigger, but do not draw the old solid-colour
        // placeholder rectangle when the scene has no trap texture assigned.
        GameObject trap = new GameObject("Магическая ловушка");
        trap.transform.SetParent(root, false);
        trap.transform.localPosition = new Vector3(position.x, position.y, 0f);

        BoxCollider2D collider = trap.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.15f, 0.85f);
        collider.isTrigger = true;
        trap.AddComponent<RoguelikeTrap>();

        if (trapSprite != null)
        {
            SpriteRenderer renderer = trap.AddComponent<SpriteRenderer>();
            renderer.sprite = trapSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 1;
        }
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

    private static void GetPlayableMetrics(out Vector2 centre, out float width, out float height)
    {
        const float referenceWidth = 1920f;
        const float referenceHeight = 1080f;
        const float topMargin = 260f;
        const float leftMargin = 170f;
        const float rightMargin = 170f;
        const float bottomMargin = 130f;

        Camera camera = Camera.main;
        float halfHeight = camera != null && camera.orthographic
            ? camera.orthographicSize : 6f;
        float aspect = camera != null
            ? camera.aspect
            : Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        Vector2 cameraCentre = camera != null
            ? new Vector2(camera.transform.position.x, camera.transform.position.y)
            : Vector2.zero;

        float halfWidth = halfHeight * aspect;
        float viewportWidth = camera != null && camera.pixelWidth > 0
            ? camera.pixelWidth : Mathf.Max(1, Screen.width);
        float viewportHeight = camera != null && camera.pixelHeight > 0
            ? camera.pixelHeight : Mathf.Max(1, Screen.height);
        float pixelsPerWorldUnitX = viewportWidth / Mathf.Max(0.01f, halfWidth * 2f);
        float pixelsPerWorldUnitY = viewportHeight / Mathf.Max(0.01f, halfHeight * 2f);

        float leftInset = leftMargin * (viewportWidth / referenceWidth)
            / pixelsPerWorldUnitX;
        float rightInset = rightMargin * (viewportWidth / referenceWidth)
            / pixelsPerWorldUnitX;
        float topInset = topMargin * (viewportHeight / referenceHeight)
            / pixelsPerWorldUnitY;
        float bottomInset = bottomMargin * (viewportHeight / referenceHeight)
            / pixelsPerWorldUnitY;

        float left = cameraCentre.x - halfWidth + leftInset;
        float right = cameraCentre.x + halfWidth - rightInset;
        float top = cameraCentre.y + halfHeight - topInset;
        float bottom = cameraCentre.y - halfHeight + bottomInset;

        centre = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        width = Mathf.Max(1f, right - left);
        height = Mathf.Max(1f, top - bottom);
    }

    private Vector2 RandomPointAwayFromCentre()
    {
        Vector2 point = Random.insideUnitCircle;
        point.x *= 6.0f;
        point.y *= 3.1f;
        if (point.magnitude < 2.8f) point = point.normalized * 3.0f;
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
