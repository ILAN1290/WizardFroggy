using UnityEngine;

/// <summary>
/// Creates the contents and collision geometry of the currently active room:
/// bookcase walls, shelves, traps, ghosts, chests and merchant.
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
    [Header("Кирпичные стены")]
    [SerializeField] private Sprite wallTileSprite;
    [SerializeField] private Sprite wallFillTileSprite;
    [SerializeField] private Sprite[] mossyWallSprites = new Sprite[3];

    [SerializeField] private Sprite topLeftWallCornerSprite;
    [SerializeField] private Sprite topRightWallCornerSprite;
    [SerializeField] private Sprite bottomLeftWallCornerSprite;
    [SerializeField] private Sprite bottomRightWallCornerSprite;

    // The door frame is two 0.95-unit tile rows high. Walls use that full height.
    // Wall depth follows the same screen margins as the playable-area calculation:
    // top 260 px, sides 170 px, bottom 130 px at the reference resolution.
    private const float WallTileWorldSize = 1f;
    private const float IvyBandDepth = 0.72f;

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
        // The existing wooden Floor Tilemap is the walkable room surface.
        GetPlayableMetrics(out Vector2 roomCentre, out float roomWidth, out float roomHeight);
        RoguelikeRoomLayout layout = RoguelikeRoomLayoutGenerator.Create(
            room, floorNumber, floorSeed, roomWidth, roomHeight, roomCentre);

        // Collision segments stop at doorways. The visual wall bands run behind
        // the door art, so the wall face remains solid above each opening.
        foreach (RoguelikeWallSection wall in layout.Walls)
            CreateWallSegment(root, wall.Name, wall.Position, wall.Size, wall.Rotation);

        CreateWallBands(root, roomCentre, roomWidth, roomHeight);

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

        // The frog and all its animated child sprites must render above the
        // perspective wall frame, not disappear behind the foreground masonry.
        PutPlayerAboveWalls();

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
        // A thin collider marks the exact inner edge of the wall.
        // Visual masonry is generated separately as a perspective-shaped band.
        GameObject wall = new GameObject(objectName);
        wall.transform.SetParent(root, false);
        wall.transform.localPosition = new Vector3(position.x, position.y, 0f);
        wall.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        BoxCollider2D blocker = wall.AddComponent<BoxCollider2D>();
        blocker.isTrigger = false;
        blocker.size = size;
    }

    private void CreateWallBands(Transform root, Vector2 centre, float width, float height)
    {
        Camera camera = Camera.main;
        float halfHeight = camera != null && camera.orthographic
            ? camera.orthographicSize : 6f;
        float aspect = camera != null
            ? camera.aspect
            : Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;

        float viewHeight = halfHeight * 2f;
        float viewWidth = viewHeight * aspect;

        // Convert the same 1920x1080 safe margins used by GetPlayableMetrics
        // into world units. This makes the masonry finish at the screen frame.
        float sideDepth = viewWidth * (170f / 1920f);
        float topDepth = viewHeight * (260f / 1080f);
        float bottomDepth = viewHeight * (130f / 1080f);

        float left = centre.x - width * 0.5f;
        float right = centre.x + width * 0.5f;
        float top = centre.y + height * 0.5f;
        float bottom = centre.y - height * 0.5f;

        // First lay a continuous backing of ordinary brick tiles over the whole
        // wall footprint. The tapered rows and ivy are rendered above this pass.
        // This closes any transparent seams between the ivy band and masonry.
        CreateWallBackingBand(root, "Заполнение верхней стены",
            new Vector2(centre.x, top + topDepth * 0.5f),
            width + sideDepth * 2f, topDepth, 0f);
        CreateWallBackingBand(root, "Заполнение нижней стены",
            new Vector2(centre.x, bottom - bottomDepth * 0.5f),
            width + sideDepth * 2f, bottomDepth, 180f);
        CreateWallBackingBand(root, "Заполнение левой стены",
            new Vector2(left - sideDepth * 0.5f,
                centre.y + (topDepth - bottomDepth) * 0.5f),
            height + topDepth + bottomDepth, sideDepth, 90f);
        CreateWallBackingBand(root, "Заполнение правой стены",
            new Vector2(right + sideDepth * 0.5f,
                centre.y + (topDepth - bottomDepth) * 0.5f),
            height + topDepth + bottomDepth, sideDepth, -90f);

        // The wall strips become wider/taller as they approach the outside edge.
        // Together they form four trapezoids with diagonal, mitered corners.
        CreateHorizontalWallBand(root, "Верхняя кирпичная стена",
            centre.x, top, width, topDepth, sideDepth, 1f, 0f);
        CreateHorizontalWallBand(root, "Нижняя кирпичная стена",
            centre.x, bottom, width, bottomDepth, sideDepth, -1f, 180f);

        CreateVerticalWallBand(root, "Левая кирпичная стена",
            left, centre.y, height, sideDepth, topDepth, bottomDepth, -1f, 90f);
        CreateVerticalWallBand(root, "Правая кирпичная стена",
            right, centre.y, height, sideDepth, topDepth, bottomDepth, 1f, -90f);

    }

    private void CreateWallBackingBand(Transform root, string name,
        Vector2 position, float length, float depth, float rotation)
    {
        if (wallFillTileSprite == null || length <= 0f || depth <= 0f) return;

        GameObject backing = new GameObject(name);
        backing.transform.SetParent(root, false);
        backing.transform.localPosition = new Vector3(position.x, position.y, -0.08f);
        backing.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        SpriteRenderer renderer = backing.AddComponent<SpriteRenderer>();
        // Use the exact atlas tile requested for the plain masonry infill.
        renderer.sprite = wallFillTileSprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = new Vector2(length, depth);
        renderer.sortingOrder = 2;
    }

    private void CreateHorizontalWallBand(Transform root, string name, float centreX,
        float innerY, float innerWidth, float depth, float sideExpansion,
        float outwardSign, float rotation)
    {
        for (float distance = 0f; distance < depth - 0.001f; distance += WallTileWorldSize)
        {
            float rowDepth = Mathf.Min(WallTileWorldSize, depth - distance);
            float progress = (distance + rowDepth * 0.5f) / depth;
            float rowWidth = innerWidth + 2f * sideExpansion * progress;
            float rowY = innerY + outwardSign * (distance + rowDepth * 0.5f);

            CreateWallTileRow(root, name + " • ряд", new Vector2(centreX, rowY),
                rowWidth, rowDepth, rotation, 3);
        }

        float ivyDepth = Mathf.Min(IvyBandDepth, depth);
        float ivyDistance = Mathf.Max(0f, depth - ivyDepth);
        float ivyWidth = innerWidth + 2f * sideExpansion;
        float ivyY = innerY + outwardSign * (ivyDistance + ivyDepth * 0.5f);

        CreateIvyStrip(root, name + " • внешняя листва",
            new Vector2(centreX, ivyY), ivyWidth, ivyDepth, rotation);
    }

    private void CreateVerticalWallBand(Transform root, string name, float innerX,
        float centreY, float innerHeight, float depth, float topExpansion,
        float bottomExpansion, float outwardSign, float rotation)
    {
        for (float distance = 0f; distance < depth - 0.001f; distance += WallTileWorldSize)
        {
            float rowDepth = Mathf.Min(WallTileWorldSize, depth - distance);
            float progress = (distance + rowDepth * 0.5f) / depth;
            float rowHeight = innerHeight +
                (topExpansion + bottomExpansion) * progress;
            float rowCentreY = centreY +
                (topExpansion - bottomExpansion) * progress * 0.5f;
            float rowX = innerX + outwardSign * (distance + rowDepth * 0.5f);

            CreateWallTileRow(root, name + " • ряд", new Vector2(rowX, rowCentreY),
                rowHeight, rowDepth, rotation, 3);
        }

        float ivyDepth = Mathf.Min(IvyBandDepth, depth);
        float ivyDistance = Mathf.Max(0f, depth - ivyDepth);
        float ivyProgress = (ivyDistance + ivyDepth * 0.5f) / depth;
        float ivyHeight = innerHeight + (topExpansion + bottomExpansion) * ivyProgress;
        float ivyCentreY = centreY + (topExpansion - bottomExpansion) * ivyProgress * 0.5f;
        float ivyX = innerX + outwardSign * (ivyDistance + ivyDepth * 0.5f);

        CreateIvyStrip(root, name + " • внешняя листва",
            new Vector2(ivyX, ivyCentreY), ivyHeight, ivyDepth, rotation);
    }

    private void CreateWallTileRow(Transform root, string name, Vector2 position,
        float length, float depth, float rotation, int sortingOrder)
    {
        if (wallTileSprite == null) return;

        GameObject row = new GameObject(name);
        row.transform.SetParent(root, false);
        row.transform.localPosition = new Vector3(position.x, position.y, -0.06f);
        row.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        SpriteRenderer renderer = row.AddComponent<SpriteRenderer>();
        renderer.sprite = wallTileSprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        // Do not grow the depth toward the room: the inner edge must stay
        // exactly on the walkable boundary. A tiny overlap is used only along the run.
        renderer.size = new Vector2(Mathf.Max(0.01f, length + 0.01f), Mathf.Max(0.01f, depth));
        renderer.sortingOrder = sortingOrder;
    }

    private void CreateIvyStrip(Transform root, string name, Vector2 position,
        float length, float depth, float rotation)
    {
        if (mossyWallSprites == null || mossyWallSprites.Length == 0) return;

        const float segmentLength = 3f;
        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(length / segmentLength));
        float actualSegmentLength = length / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            Sprite sprite = mossyWallSprites[i % mossyWallSprites.Length];
            if (sprite == null) continue;

            GameObject segment = new GameObject(name);
            segment.transform.SetParent(root, false);
            segment.transform.localPosition = new Vector3(
                position.x + Mathf.Cos(rotation * Mathf.Deg2Rad) *
                    ((i + 0.5f) * actualSegmentLength - length * 0.5f),
                position.y + Mathf.Sin(rotation * Mathf.Deg2Rad) *
                    ((i + 0.5f) * actualSegmentLength - length * 0.5f),
                -0.075f);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

            SpriteRenderer renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            // Keep the leaf band outside the walkable area as well.
            renderer.size = new Vector2(actualSegmentLength + 0.01f, depth);
            renderer.sortingOrder = 4;
        }
    }

    private void PutPlayerAboveWalls()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null) return;

        SpriteRenderer[] playerRenderers =
            playerObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            SpriteRenderer renderer = playerRenderers[i];
            if (renderer == null) continue;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 20;
        }
    }

    private void CreateDoor(Transform root, RoguelikeSession session, Vector2 position,
        Vector2 spawn, int dx, int dy, float rotation)
    {
        GameObject door = new GameObject("Дверь библиотеки");
        door.transform.SetParent(root, false);
        door.transform.localPosition = new Vector3(position.x, position.y, -0.04f);
        // Door frame follows the room layout; the right-side artwork is turned 180° below.
        door.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        // Side doors are 70% as wide; the bottom door is 50% as tall.
        // The top door gains 20 screen pixels in width.
        if (dx != 0)
            door.transform.localScale = new Vector3(1f, 0.7f, 1f);
        else if (dy < 0)
            door.transform.localScale = new Vector3(1f, 0.5f, 1f);
        else if (dy > 0)
        {
            Camera camera = Camera.main;
            float pixelsPerWorldUnit = camera != null && camera.orthographic
                ? camera.pixelHeight / Mathf.Max(0.01f, camera.orthographicSize * 2f)
                : 100f;
            float extraWorldWidth = 20f / Mathf.Max(1f, pixelsPerWorldUnit);
            door.transform.localScale = new Vector3(1f + extraWorldWidth / 2.04f, 1f, 1f);
        }

        const float doorWidth = 2.1f;
        const float wallThickness = 0.22f;
        BoxCollider2D blocker = door.AddComponent<BoxCollider2D>();
        blocker.size = new Vector2(doorWidth, wallThickness);
        blocker.isTrigger = false;

        BoxCollider2D trigger = door.AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(doorWidth + 0.35f, 0.9f);
        trigger.isTrigger = true;
        trigger.enabled = false;

        // Door art keeps its original proportions. Only the GameObject rotates.
        SpriteRenderer fallback = door.AddComponent<SpriteRenderer>();
        fallback.sprite = WhiteSprite;
        fallback.drawMode = SpriteDrawMode.Tiled;
        fallback.size = new Vector2(2.04f, wallThickness);
        fallback.sortingOrder = 3;
        fallback.enabled = false;

        // Door graphics are a separate child so their masonry can sit outside
        // the room edge while all physics/transition colliders stay on the boundary.
        GameObject doorArtwork = new GameObject("Текстура двери");
        doorArtwork.transform.SetParent(door.transform, false);
        // Flip the entire right-hand door texture assembly, not each small tile in place.
        doorArtwork.transform.localRotation = Quaternion.Euler(0f, 0f, dx > 0 ? 180f : 0f);
        Vector3 outward = new Vector3(dx, dy, 0f);
        Vector3 localOutward = Quaternion.Euler(0f, 0f, -rotation) * outward;
        const float artworkHalfHeightLocal = 0.95f;
        const float artworkWidthLocal = 2.04f;
        const float artworkHeightLocal = 1.9f;
        float doorScaleY = Mathf.Max(0.01f, door.transform.localScale.y);
        float artworkOffsetLocal = artworkHalfHeightLocal;

        // Restore side doors closer to their openings. The previous outer-edge
        // compensation moved them too far into the side wall bands.
        if (dx != 0)
        {
            artworkOffsetLocal = artworkHalfHeightLocal;
        }

        // Stretch only the top and bottom door artwork, preserving the edge
        // that touches the wall. Pixel values are converted using the active camera.
        Camera camera = Camera.main;
        float pixelsPerWorldUnitY = camera != null && camera.orthographic
            ? camera.pixelHeight / Mathf.Max(0.01f, camera.orthographicSize * 2f)
            : Mathf.Max(1f, Screen.height) / 12f;
        float pixelsPerWorldUnitX = camera != null && camera.orthographic
            ? camera.pixelWidth / Mathf.Max(0.01f, camera.orthographicSize * 2f * camera.aspect)
            : Mathf.Max(1f, Screen.width) / 16f;

        float extraWidthWorld = 10f / Mathf.Max(1f, pixelsPerWorldUnitX);
        float artworkScaleX = (artworkWidthLocal + extraWidthWorld) / artworkWidthLocal;
        float artworkScaleY = 1f;

        if (dx == 0 && dy > 0)
        {
            // Upper doorway: +10 px wide and +5 px outward/upward.
            float extraHeightWorld = 5f / Mathf.Max(1f, pixelsPerWorldUnitY);
            artworkScaleY = (artworkHeightLocal + extraHeightWorld) / artworkHeightLocal;
            artworkOffsetLocal += extraHeightWorld * 0.5f;
        }
        else if (dx == 0 && dy < 0)
        {
            // Lower doorway: +10 px wide and +3 px outward/downward.
            // Its parent is already scaled to 50%, so convert the added height
            // back into the door's local coordinate system.
            float extraHeightWorld = 3f / Mathf.Max(1f, pixelsPerWorldUnitY);
            artworkScaleY = (artworkHeightLocal + extraHeightWorld / doorScaleY) /
                artworkHeightLocal;
            artworkOffsetLocal += extraHeightWorld * 0.5f / doorScaleY;
        }

        doorArtwork.transform.localScale = new Vector3(artworkScaleX, artworkScaleY, 1f);
        doorArtwork.transform.localPosition =
            localOutward.normalized * artworkOffsetLocal;

        SpriteRenderer[] parts = new SpriteRenderer[6];
        const float tileWidth = 0.68f;
        const float tileHeight = 0.95f;
        for (int i = 0; i < parts.Length; i++)
        {
            GameObject tile = new GameObject("Дверной тайл " + i);
            tile.transform.SetParent(doorArtwork.transform, false);
            int column = i % 3;
            int row = i / 3;
            tile.transform.localPosition = new Vector3(
                (column - 1) * tileWidth,
                (0.5f - row) * tileHeight, 0f);

            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 5;
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

    private void SpawnGhost(Transform root, RoguelikeSession session, int floor,
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
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            SpriteRenderer frogRenderer = playerObject.GetComponent<SpriteRenderer>();
            if (frogRenderer == null)
                frogRenderer = playerObject.GetComponentInChildren<SpriteRenderer>();
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
        enemy.Initialize(session, floor, melee, miniBoss, ghostAttackSprites);
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
