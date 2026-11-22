using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    private SpriteRenderer cachedPlayerRenderer;
    private Collider2D cachedPlayerCollider;
    private GameObject upperShelfPrefab;
    private GameObject lateralShelfPrefab;

    [Header("Прозрачность перекрывающих объектов")]
    [SerializeField, Range(0.1f, 0.9f)] private float occludedAlpha = 0.45f;

    private sealed class OcclusionState
    {
        public SpriteRenderer Renderer;
        public Color OriginalColor;
        public int OriginalSortingOrder;
    }

    private readonly List<OcclusionState> occlusionStates =
        new List<OcclusionState>();
    private readonly List<OcclusionState> lowerWallOcclusionStates =
        new List<OcclusionState>();
    private float lowerWallOcclusionY;
    private bool hasLowerWallOcclusion;

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

    private void Update()
    {
        if (cachedPlayerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null) return;
            cachedPlayerTransform = playerObject.transform;
            cachedPlayerRenderer = playerObject.GetComponent<SpriteRenderer>();
            cachedPlayerCollider = playerObject.GetComponent<Collider2D>();
        }

        Bounds playerBounds = cachedPlayerRenderer != null
            ? cachedPlayerRenderer.bounds
            : cachedPlayerCollider != null
                ? cachedPlayerCollider.bounds
                : new Bounds(cachedPlayerTransform.position, new Vector3(0.7f, 0.7f, 0.1f));
        int playerSortingOrder = cachedPlayerRenderer != null
            ? cachedPlayerRenderer.sortingOrder : 0;

        // Стеллажи изменяют прозрачность индивидуально в момент перекрытия.
        for (int i = occlusionStates.Count - 1; i >= 0; i--)
        {
            OcclusionState state = occlusionStates[i];
            SpriteRenderer renderer = state.Renderer;
            if (renderer == null)
            {
                occlusionStates.RemoveAt(i);
                continue;
            }

            Bounds objectBounds = renderer.bounds;
            bool overlapsPlayer = playerBounds.min.x <= objectBounds.max.x &&
                playerBounds.max.x >= objectBounds.min.x &&
                playerBounds.min.y <= objectBounds.max.y &&
                playerBounds.max.y >= objectBounds.min.y;
            bool playerBehind = playerBounds.center.y > objectBounds.center.y;
            Color color = state.OriginalColor;

            if (overlapsPlayer)
            {
                renderer.sortingOrder = playerBehind
                    ? Mathf.Max(state.OriginalSortingOrder, playerSortingOrder + 1)
                    : Mathf.Min(state.OriginalSortingOrder, playerSortingOrder - 1);

                if (playerBehind)
                    color.a *= occludedAlpha;
            }
            else
            {
                renderer.sortingOrder = state.OriginalSortingOrder;
            }

            renderer.color = color;
        }

        // Нижняя стена всегда рисуется поверх персонажа. Как только игрок
        // уходит за её внутреннюю линию, прозрачность меняется сразу у всей кладки
        // и у двери, а не только у отдельного тайла под ногами.
        bool playerBehindLowerWall = hasLowerWallOcclusion &&
            playerBounds.center.y < lowerWallOcclusionY;
        for (int i = lowerWallOcclusionStates.Count - 1; i >= 0; i--)
        {
            OcclusionState state = lowerWallOcclusionStates[i];
            SpriteRenderer renderer = state.Renderer;
            if (renderer == null)
            {
                lowerWallOcclusionStates.RemoveAt(i);
                continue;
            }

            Color color = state.OriginalColor;
            if (playerBehindLowerWall)
                color.a *= occludedAlpha;
            renderer.color = color;
            renderer.sortingOrder = state.OriginalSortingOrder;
        }
    }

    public int GenerateRoom(Transform environmentRoot, Transform enemiesRoot,
        Transform effectsRoot, Transform projectilesRoot, RoguelikeRoomData room,
        int floorNumber, int floorSeed, RoguelikeSession session)
    {
        Transform root = environmentRoot;
        occlusionStates.Clear();
        lowerWallOcclusionStates.Clear();
        hasLowerWallOcclusion = false;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        cachedPlayerTransform = playerObject != null ? playerObject.transform : null;
        cachedPlayerRenderer = playerObject != null
            ? playerObject.GetComponent<SpriteRenderer>() : null;
        cachedPlayerCollider = playerObject != null
            ? playerObject.GetComponent<Collider2D>() : null;
        // The player must render above the upper wall; the lower wall keeps
        // its separate foreground sorting order and opacity behaviour.
        if (cachedPlayerRenderer != null)
            cachedPlayerRenderer.sortingOrder = Mathf.Max(4, cachedPlayerRenderer.sortingOrder);

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
        const float entryInset = 1.15f;

        // Keep the wooden floor exactly between the inner faces of the side walls.
        FitWoodenFloorToRoom(left, right, bottom, top);

        float frameTileHeight = upperWallTileSprite != null
            ? Mathf.Max(0.001f, upperWallTileSprite.bounds.size.y)
            : bottomWallTileSprite != null
                ? Mathf.Max(0.001f, bottomWallTileSprite.bounds.size.y) : 0f;
        float sideWallBottom = bottom - 2f * frameTileHeight;
        float sideWallTop = top + 3f * frameTileHeight;

        CreateUpperWall(wallTilesRoot, wallCollidersRoot, doorsRoot,
            session, room, centre, width, height, top, hasUp);
        CreateVerticalWall(wallTilesRoot, wallCollidersRoot, doorsRoot,
            session, "Left", left - 0.5f, centre.y, sideWallBottom, sideWallTop,
            leftWallTileSprite, hasLeft, -1, 0,
            new Vector2(right - entryInset, centre.y));
        CreateVerticalWall(wallTilesRoot, wallCollidersRoot, doorsRoot,
            session, "Right", right + 0.5f, centre.y, sideWallBottom, sideWallTop,
            rightWallTileSprite, hasRight, 1, 0,
            new Vector2(left + entryInset, centre.y));
        CreateBottomWall(wallTilesRoot, wallCollidersRoot, doorsRoot,
            session, room.Index, centre, width, height, bottom, hasDown);
    }

    private static Transform CreateFolder(Transform parent, string folderName)
    {
        GameObject folder = new GameObject(folderName);
        folder.transform.SetParent(parent, false);
        return folder.transform;
    }

    private void CreateUpperWall(Transform tilesRoot, Transform collidersRoot,
        Transform doorsRoot, RoguelikeSession session, RoguelikeRoomData room,
        Vector2 centre, float width, float roomHeight, float innerY, bool hasDoor)
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
            Vector2 spawn = new Vector2(centre.x, centre.y - roomHeight * 0.5f + 1.15f);
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
        float wallX, float centreY, float bottomY, float topY, Sprite tileSprite,
        bool hasDoor, int dx, int dy, Vector2 spawnPosition)
    {
        if (tileSprite == null) return;

        float tileWidth = Mathf.Max(0.001f, tileSprite.bounds.size.x);
        float tileHeight = Mathf.Max(0.001f, tileSprite.bounds.size.y);
        float doorHeight = 2f * tileHeight;
        float doorBottom = centreY - doorHeight * 0.5f;
        float doorTop = centreY + doorHeight * 0.5f;

        // Tile the complete interval up to the upper wall and down to the
        // lower wall. Partial end tiles are scaled only along Y to avoid gaps.
        if (!hasDoor)
        {
            CreateVerticalWallTilesSegment(tilesRoot, sideName + " wall • tile",
                wallX, bottomY, topY, tileHeight, tileSprite, 3);
        }
        else
        {
            CreateVerticalWallTilesSegment(tilesRoot, sideName + " wall • tile",
                wallX, bottomY, doorBottom, tileHeight, tileSprite, 3);
            CreateVerticalWallTilesSegment(tilesRoot, sideName + " wall • tile",
                wallX, doorTop, topY, tileHeight, tileSprite, 3);
        }

        CreateVerticalWallCollidersBetween(collidersRoot, sideName + " wall",
            wallX, bottomY, topY, tileWidth, hasDoor, centreY, doorHeight);

        if (!hasDoor) return;

        Vector2 doorPosition = new Vector2(wallX, centreY);
        Color lockedTint = new Color(0.58f, 0.58f, 0.58f, 1f);
        CreateWallTileDoor(doorsRoot, session, sideName + " wall door",
            doorPosition, spawnPosition, new Vector2(tileWidth, doorHeight),
            new Vector2(1.0f, 1.2f),
            dx < 0 ? new Vector2(0.65f, 0f) : new Vector2(-0.65f, 0f),
            dx, dy, tileSprite, 1, 2, lockedTint);
    }

    private static void CreateVerticalWallTilesSegment(Transform parent,
        string objectName, float x, float bottomY, float topY,
        float tileHeight, Sprite tileSprite, int sortingOrder)
    {
        if (tileSprite == null || topY <= bottomY || tileHeight <= 0.001f) return;

        int rows = Mathf.CeilToInt((topY - bottomY) / tileHeight);
        for (int row = 0; row < rows; row++)
        {
            float rowBottom = bottomY + row * tileHeight;
            float actualHeight = Mathf.Min(tileHeight, topY - rowBottom);
            if (actualHeight <= 0.001f) continue;

            CreateWallTile(parent, objectName,
                new Vector2(x, rowBottom + actualHeight * 0.5f),
                tileSprite, Color.white, sortingOrder,
                new Vector2(1f, actualHeight / tileHeight));
        }
    }

    private void CreateBottomWall(Transform tilesRoot, Transform collidersRoot,
        Transform doorsRoot, RoguelikeSession session, int roomIndex, Vector2 centre,
        float width, float roomHeight, float innerY, bool hasDoor)
    {
        Sprite baseTile = upperWallTileSprite != null
            ? upperWallTileSprite : bottomWallTileSprite;
        if (baseTile == null) return;

        const int rows = 3;
        const int doorWidthTiles = 3;
        float tileWidth = Mathf.Max(0.001f, baseTile.bounds.size.x);
        float tileHeight = Mathf.Max(0.001f, baseTile.bounds.size.y);

        // Raise the whole lower wall by one tile into the room.
        float wallBaseY = innerY + tileHeight;
        lowerWallOcclusionY = wallBaseY - tileHeight * 1.5f;
        hasLowerWallOcclusion = true;

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
                bool insideDoor = hasDoor &&
                    column >= firstDoorColumn && column <= lastDoorColumn;
                if (insideDoor) continue;

                Sprite tile = ChooseUpperWallTile(roomIndex, column, row);
                SpriteRenderer renderer = CreateWallTile(tilesRoot,
                    "Нижняя стена • тайл",
                    new Vector2(left + (column + 0.5f) * tileWidth,
                        wallBaseY - (row + 0.5f) * tileHeight),
                    tile, Color.white, 30);
                RegisterLowerWallOcclusion(renderer);
            }

            float remainder = width - coveredWidth;
            if (remainder > 0.001f)
            {
                float partialWidth = remainder * 0.5f;
                float rowY = wallBaseY - (row + 0.5f) * tileHeight;
                Sprite leftTile = ChooseUpperWallTile(roomIndex, -1, row);
                Sprite rightTile = ChooseUpperWallTile(roomIndex, fullColumns, row);
                SpriteRenderer leftRenderer = CreateWallTile(tilesRoot,
                    "Нижняя стена • край",
                    new Vector2(centre.x - width * 0.5f + partialWidth * 0.5f, rowY),
                    leftTile, Color.white, 30,
                    new Vector2(partialWidth / tileWidth, 1f));
                SpriteRenderer rightRenderer = CreateWallTile(tilesRoot,
                    "Нижняя стена • край",
                    new Vector2(centre.x + width * 0.5f - partialWidth * 0.5f, rowY),
                    rightTile, Color.white, 30,
                    new Vector2(partialWidth / tileWidth, 1f));
                RegisterLowerWallOcclusion(leftRenderer);
                RegisterLowerWallOcclusion(rightRenderer);
            }
        }

        // The solid border sits on the outer row of the raised wall.
        CreateHorizontalWallColliders(collidersRoot, "Нижняя стена",
            centre.x, wallBaseY - tileHeight * 2.5f, width, tileHeight,
            hasDoor, doorWidthTiles * tileWidth);

        if (hasDoor)
        {
            Vector2 spawn = new Vector2(centre.x,
                centre.y + roomHeight * 0.5f - 1.15f);
            CreateBottomDoor(doorsRoot, session,
                new Vector2(centre.x, wallBaseY - tileHeight * 0.5f),
                spawn, doorWidthTiles * tileWidth, tileWidth, tileHeight);
        }
    }

    private void CreateBottomDoor(Transform doorsRoot, RoguelikeSession session,
        Vector2 position, Vector2 spawnPosition, float width,
        float tileWidth, float tileHeight)
    {
        GameObject door = CreateDoorRoot(doorsRoot, "Нижняя дверь", position);
        BoxCollider2D blocker = door.AddComponent<BoxCollider2D>();
        blocker.size = new Vector2(width, tileHeight);

        BoxCollider2D sensor = door.AddComponent<BoxCollider2D>();
        sensor.isTrigger = true;
        sensor.size = new Vector2(Mathf.Min(width, 1.2f), 1.15f);
        sensor.offset = new Vector2(0f, 0.65f);

        SpriteRenderer[] renderers = new SpriteRenderer[6];
        for (int i = 0; i < renderers.Length; i++)
        {
            int column = i % 3;
            int row = i / 3;
            float y = row == 0 ? 0f : -tileHeight;
            SpriteRenderer renderer = CreateDoorTile(door.transform,
                "Нижняя дверь • тайл " + i,
                new Vector2((column - 1) * tileWidth, y),
                closedUpperDoorSprites != null && i < closedUpperDoorSprites.Length
                    ? closedUpperDoorSprites[i] : null,
                Color.white);
            renderer.sortingOrder = 31;
            renderers[i] = renderer;
            RegisterLowerWallOcclusion(renderer);
        }

        SetSensorToDoorSpriteBounds(sensor, renderers);
        spawnPosition = GetSafeDoorSpawnPosition(
            sensor, 0, -1, spawnPosition);

        door.AddComponent<RoguelikeRoomTransition>().Initialize(
            session, 0, -1, spawnPosition, blocker, sensor, renderers,
            closedUpperDoorSprites, openedUpperDoorSprites, Color.white, false);
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
        sensor.size = new Vector2(Mathf.Min(width, 1.2f), 1.15f);
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

        SetSensorToDoorSpriteBounds(sensor, renderers);
        spawnPosition = GetSafeDoorSpawnPosition(
            sensor, 0, 1, spawnPosition);

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

        SetSensorToDoorSpriteBounds(sensor, renderers);
        spawnPosition = GetSafeDoorSpawnPosition(
            sensor, directionX, directionY, spawnPosition);

        door.AddComponent<RoguelikeRoomTransition>().Initialize(
            session, directionX, directionY, spawnPosition,
            blocker, sensor, renderers, lockedSprites,
            new Sprite[0], lockedTint, false);
    }

    private static void SetSensorToDoorSpriteBounds(
        BoxCollider2D sensor, SpriteRenderer[] renderers)
    {
        if (sensor == null || renderers == null || renderers.Length == 0) return;

        bool foundBounds = false;
        Bounds spriteBounds = new Bounds();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null) continue;

            if (!foundBounds)
            {
                spriteBounds = renderer.bounds;
                foundBounds = true;
            }
            else
            {
                spriteBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!foundBounds) return;

        Vector3 scale = sensor.transform.lossyScale;
        Vector3 localCentre = sensor.transform.InverseTransformPoint(spriteBounds.center);
        sensor.offset = new Vector2(localCentre.x, localCentre.y);
        sensor.size = new Vector2(
            spriteBounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            spriteBounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y))) * 0.92f;
        sensor.isTrigger = true;
        Physics2D.SyncTransforms();
    }

    private Vector2 GetSafeDoorSpawnPosition(
        BoxCollider2D sensor, int directionX, int directionY, Vector2 fallback)
    {
        if (sensor == null) return fallback;

        Bounds sensorBounds = sensor.bounds;
        Vector2 playerExtents = cachedPlayerCollider != null
            ? cachedPlayerCollider.bounds.extents
            : new Vector2(0.4f, 0.3f);
        const float separation = 0.3f;

        if (directionX != 0)
        {
            float distance = sensorBounds.extents.x + playerExtents.x + separation;
            return new Vector2(sensorBounds.center.x - directionX * distance,
                sensorBounds.center.y);
        }

        if (directionY != 0)
        {
            float distance = sensorBounds.extents.y + playerExtents.y + separation;
            return new Vector2(sensorBounds.center.x,
                sensorBounds.center.y - directionY * distance);
        }

        return fallback;
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

    private static SpriteRenderer CreateWallTile(Transform parent, string name,
        Vector2 position, Sprite sprite, Color tint, int sortingOrder,
        Vector2 scale = default)
    {
        if (sprite == null) return null;
        if (scale == default) scale = Vector2.one;

        GameObject tile = new GameObject(name);
        tile.transform.SetParent(parent, false);
        tile.transform.localPosition = new Vector3(position.x, position.y, 0f);
        tile.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        renderer.sortingOrder = sortingOrder;
        return renderer;
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

    private static void CreateVerticalWallCollidersBetween(Transform parent,
        string wallName, float x, float bottomY, float topY, float thickness,
        bool hasDoor, float doorCentreY, float doorHeight)
    {
        if (topY <= bottomY) return;

        if (!hasDoor || topY - bottomY <= doorHeight)
        {
            CreateWallCollider(parent, wallName, new Vector2(x, (bottomY + topY) * 0.5f),
                new Vector2(thickness, topY - bottomY));
            return;
        }

        float doorBottom = doorCentreY - doorHeight * 0.5f;
        float doorTop = doorCentreY + doorHeight * 0.5f;
        float lowerHeight = Mathf.Max(0f, doorBottom - bottomY);
        float upperHeight = Mathf.Max(0f, topY - doorTop);

        if (lowerHeight > 0.01f)
            CreateWallCollider(parent, wallName + " • нижняя часть",
                new Vector2(x, bottomY + lowerHeight * 0.5f),
                new Vector2(thickness, lowerHeight));
        if (upperHeight > 0.01f)
            CreateWallCollider(parent, wallName + " • верхняя часть",
                new Vector2(x, doorTop + upperHeight * 0.5f),
                new Vector2(thickness, upperHeight));
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

    private void RegisterOcclusion(SpriteRenderer renderer)
    {
        if (renderer == null) return;

        occlusionStates.Add(new OcclusionState
        {
            Renderer = renderer,
            OriginalColor = renderer.color,
            OriginalSortingOrder = renderer.sortingOrder
        });
    }

    private void RegisterLowerWallOcclusion(SpriteRenderer renderer)
    {
        if (renderer == null) return;
        lowerWallOcclusionStates.Add(new OcclusionState
        {
            Renderer = renderer,
            OriginalColor = renderer.color,
            OriginalSortingOrder = renderer.sortingOrder
        });
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
            // Боковой стеллаж в исходной сцене стоит вертикально.
            shelf.transform.localRotation = upper
                ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            // The upright (lateral) shelf is twice its previous size.
            shelf.transform.localScale = Vector3.one * (scale * (upper ? 2f : 4f));

            foreach (Collider2D collider in shelf.GetComponentsInChildren<Collider2D>(true))
                PrepareShelfCollider(collider, upper);

            SpriteRenderer[] renderers = shelf.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
                RegisterOcclusion(renderer);
            return;
        }

        GameObject fallbackShelf = CreateRect(root, objectName, position,
            new Vector2(2.6f, 0.85f), new Color(0.3f, 0.2f, 0.16f), -2, true);
        fallbackShelf.tag = "Wall";
        fallbackShelf.transform.localRotation = upper
            ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
        foreach (Collider2D collider in fallbackShelf.GetComponents<Collider2D>())
            PrepareShelfCollider(collider, upper);
        foreach (SpriteRenderer renderer in fallbackShelf.GetComponentsInChildren<SpriteRenderer>())
            RegisterOcclusion(renderer);
    }

    private static void PrepareShelfCollider(Collider2D collider, bool upper)
    {
        if (collider == null) return;

        BoxCollider2D box = collider as BoxCollider2D;
        if (box == null)
        {
            collider.enabled = true;
            collider.isTrigger = true;
            return;
        }

        box.enabled = true;
        box.usedByComposite = false;

        SpriteRenderer spriteRenderer = box.GetComponent<SpriteRenderer>();
        Bounds spriteBounds = spriteRenderer != null && spriteRenderer.sprite != null
            ? spriteRenderer.sprite.bounds
            : new Bounds(box.offset, box.size);

        // The trigger matches the complete shelf sprite. It can be used for
        // interaction/overlap checks, but does not prevent the player walking
        // behind the furniture.
        box.size = spriteBounds.size;
        box.offset = spriteBounds.center;
        box.isTrigger = true;

        // Only the narrow foot/base remains solid. This keeps the shelf
        // physically grounded while leaving its full visual depth traversable.
        Transform existingBlocker = box.transform.Find("Shelf Base Blocker");
        if (existingBlocker != null) return;

        GameObject blockerObject = new GameObject("Shelf Base Blocker");
        blockerObject.tag = "Wall";
        blockerObject.layer = box.gameObject.layer;
        blockerObject.transform.SetParent(box.transform, false);

        BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
        blocker.isTrigger = false;
        blocker.usedByComposite = false;

        float baseDepth = Mathf.Min(0.32f,
            upper ? spriteBounds.size.y : spriteBounds.size.x);
        if (upper)
        {
            blocker.size = new Vector2(spriteBounds.size.x, baseDepth);
            blocker.offset = new Vector2(spriteBounds.center.x,
                spriteBounds.min.y + baseDepth * 0.5f);
        }
        else
        {
            // The lateral shelf is rotated by 90 degrees: local -X is its
            // lower edge in world space.
            blocker.size = new Vector2(baseDepth, spriteBounds.size.y);
            blocker.offset = new Vector2(
                spriteBounds.min.x + baseDepth * 0.5f, spriteBounds.center.y);
        }
    }

    private static Vector2 FindFreeGhostPosition(Vector2 preferredPosition, float radius)
    {
        Physics2D.SyncTransforms();
        if (!OverlapsSolidWall(preferredPosition, radius))
            return preferredPosition;

        const int directions = 16;
        const int rings = 8;
        for (int ring = 1; ring <= rings; ring++)
        {
            float distance = radius + ring * 0.45f;
            for (int i = 0; i < directions; i++)
            {
                float angle = i * (Mathf.PI * 2f / directions);
                Vector2 candidate = preferredPosition +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (!OverlapsSolidWall(candidate, radius))
                    return candidate;
            }
        }

        return preferredPosition;
    }

    private static bool OverlapsSolidWall(Vector2 position, float radius)
    {
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(position, radius + 0.03f);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D obstacle = overlaps[i];
            if (obstacle == null || obstacle.isTrigger) continue;
            if (obstacle.CompareTag("Wall")) return true;
        }
        return false;
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
        ghost.transform.position = FindFreeGhostPosition(
            ghost.transform.position, collider.radius);

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

    private static void FitWoodenFloorToRoom(
        float left, float right, float bottom, float top)
    {
        GameObject floorObject = GameObject.Find("WoodenFloorTiles");
        Tilemap floor = floorObject != null ? floorObject.GetComponent<Tilemap>() : null;
        if (floor == null) return;

        BoundsInt originalBounds = floor.cellBounds;
        if (originalBounds.size.x <= 0 || originalBounds.size.y <= 0) return;

        Vector3Int minCell = floor.WorldToCell(
            new Vector3(left + 0.001f, bottom + 0.001f, 0f));
        Vector3Int maxCell = floor.WorldToCell(
            new Vector3(right - 0.001f, top - 0.001f, 0f));

        int minX = minCell.x;
        int maxX = maxCell.x;
        int minY = minCell.y;
        int maxY = maxCell.y;
        if (maxX < minX || maxY < minY) return;

        TileBase fallbackTile = null;
        for (int y = originalBounds.yMin; y < originalBounds.yMax && fallbackTile == null; y++)
        {
            for (int x = originalBounds.xMin; x < originalBounds.xMax; x++)
            {
                fallbackTile = floor.GetTile(new Vector3Int(x, y, 0));
                if (fallbackTile != null) break;
            }
        }
        if (fallbackTile == null) return;

        for (int y = minY; y <= maxY; y++)
        {
            TileBase rowTile = null;
            for (int x = originalBounds.xMin; x < originalBounds.xMax; x++)
            {
                rowTile = floor.GetTile(new Vector3Int(x, y, 0));
                if (rowTile != null) break;
            }
            if (rowTile == null) rowTile = fallbackTile;

            // Remove tiles beyond the room's side-wall inner faces.
            for (int x = originalBounds.xMin; x < originalBounds.xMax; x++)
            {
                if (x < minX || x > maxX)
                    floor.SetTile(new Vector3Int(x, y, 0), null);
            }

            // Fill gaps and extend the wooden floor exactly from wall to wall.
            for (int x = minX; x <= maxX; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (floor.GetTile(cell) == null)
                    floor.SetTile(cell, rowTile);
            }
        }
    }

    private static void GetPlayableMetrics(out Vector2 centre, out float width, out float height)
    {
        const float referenceWidth = 1920f;
        const float referenceHeight = 1080f;
        const float topMargin = 260f;
        const float leftMargin = 90f;
        const float rightMargin = 90f;
        const float bottomMargin = 130f;
        const float sideWallInsetPixels = 50f;

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

        float sideWallInset = sideWallInsetPixels *
            (viewportWidth / referenceWidth) / pixelsPerWorldUnitX;
        float leftInset = leftMargin * (viewportWidth / referenceWidth)
            / pixelsPerWorldUnitX + sideWallInset;
        float rightInset = rightMargin * (viewportWidth / referenceWidth)
            / pixelsPerWorldUnitX + sideWallInset;
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
