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
        AddBookshelves(root, floorNumber);

        if (room.Type == RoguelikeRoomType.Combat && !room.IsCleared)
        {
            int count = Random.Range(2 + floorNumber, 4 + floorNumber);
            for (int i = 0; i < count; i++)
                SpawnGhost(root, session, floorNumber, RandomPointAwayFromCentre(),
                    Random.value > 0.30f);
            for (int i = 0; i < Mathf.Clamp(floorNumber, 1, 3); i++)
                SpawnTrap(root, RandomPointAwayFromCentre());
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
        }
        else if (room.Type == RoguelikeRoomType.Merchant)
        {
            GameObject merchant = CreateRect(root, "Торговец", Vector2.zero,
                new Vector2(1.1f, 1.5f), new Color(0.55f, 0.38f, 0.72f), 4, false);
            CircleCollider2D trigger = merchant.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            merchant.AddComponent<RoguelikeMerchant>().Initialize(session);
        }
        else if (room.Type == RoguelikeRoomType.Exit)
        {
            GameObject exit = CreateRect(root, "Выход", Vector2.zero,
                new Vector2(1.8f, 2.1f), new Color(0.32f, 0.78f, 0.64f), 4, false);
            CircleCollider2D trigger = exit.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 2f;
            exit.AddComponent<RoguelikeExit>().Initialize(session);
        }

        return 0;
    }

    private void BuildBoundaries(Transform root, bool left, bool right, bool down,
        bool up, RoguelikeSession session)
    {
        float wall = 0.55f;
        float horizontalSegment = (RoomWidth - 2.6f) * 0.5f;
        float verticalSegment = (RoomHeight - 2.6f) * 0.5f;

        if (left)
        {
            CreateSolid(root, "Шкаф западный верх",
                new Vector2(-RoomWidth / 2, RoomHeight / 2 - verticalSegment / 2),
                new Vector2(wall, verticalSegment), new Color(0.35f, 0.23f, 0.16f));
            CreateSolid(root, "Шкаф западный низ",
                new Vector2(-RoomWidth / 2, -RoomHeight / 2 + verticalSegment / 2),
                new Vector2(wall, verticalSegment), new Color(0.35f, 0.23f, 0.16f));
            CreateDoor(root, session, new Vector2(-8.85f, 0f),
                new Vector2(7f, 0f), -1, 0);
        }
        else
            CreateSolid(root, "Стена запад", new Vector2(-RoomWidth / 2, 0f),
                new Vector2(wall, RoomHeight), new Color(0.29f, 0.19f, 0.14f));

        if (right)
        {
            CreateSolid(root, "Шкаф восточный верх",
                new Vector2(RoomWidth / 2, RoomHeight / 2 - verticalSegment / 2),
                new Vector2(wall, verticalSegment), new Color(0.35f, 0.23f, 0.16f));
            CreateSolid(root, "Шкаф восточный низ",
                new Vector2(RoomWidth / 2, -RoomHeight / 2 + verticalSegment / 2),
                new Vector2(wall, verticalSegment), new Color(0.35f, 0.23f, 0.16f));
            CreateDoor(root, session, new Vector2(8.85f, 0f),
                new Vector2(-7f, 0f), 1, 0);
        }
        else
            CreateSolid(root, "Стена восток", new Vector2(RoomWidth / 2, 0f),
                new Vector2(wall, RoomHeight), new Color(0.29f, 0.19f, 0.14f));

        if (down)
        {
            CreateSolid(root, "Шкаф южный левый",
                new Vector2(-RoomWidth / 2 + horizontalSegment / 2, -RoomHeight / 2),
                new Vector2(horizontalSegment, wall), new Color(0.35f, 0.23f, 0.16f));
            CreateSolid(root, "Шкаф южный правый",
                new Vector2(RoomWidth / 2 - horizontalSegment / 2, -RoomHeight / 2),
                new Vector2(horizontalSegment, wall), new Color(0.35f, 0.23f, 0.16f));
            CreateDoor(root, session, new Vector2(0f, -5.85f),
                new Vector2(0f, 4.4f), 0, -1);
        }
        else
            CreateSolid(root, "Стена юг", new Vector2(0f, -RoomHeight / 2),
                new Vector2(RoomWidth, wall), new Color(0.29f, 0.19f, 0.14f));

        if (up)
        {
            CreateSolid(root, "Шкаф северный левый",
                new Vector2(-RoomWidth / 2 + horizontalSegment / 2, RoomHeight / 2),
                new Vector2(horizontalSegment, wall), new Color(0.35f, 0.23f, 0.16f));
            CreateSolid(root, "Шкаф северный правый",
                new Vector2(RoomWidth / 2 - horizontalSegment / 2, RoomHeight / 2),
                new Vector2(horizontalSegment, wall), new Color(0.35f, 0.23f, 0.16f));
            CreateDoor(root, session, new Vector2(0f, 5.85f),
                new Vector2(0f, -4.4f), 0, 1);
        }
        else
            CreateSolid(root, "Стена север", new Vector2(0f, RoomHeight / 2),
                new Vector2(RoomWidth, wall), new Color(0.29f, 0.19f, 0.14f));
    }

    private void CreateDoor(Transform root, RoguelikeSession session, Vector2 position,
        Vector2 spawn, int dx, int dy)
    {
        GameObject door = CreateRect(root, "Проход", position, new Vector2(1.0f, 2.1f),
            new Color(0.45f, 0.62f, 0.66f), 1, false);
        BoxCollider2D trigger = door.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.6f, 2.6f);
        door.AddComponent<RoguelikeDoor>().Initialize(session, dx, dy, spawn);
    }

    private void AddBookshelves(Transform root, int floorNumber)
    {
        int count = Random.Range(3, 6);
        for (int i = 0; i < count; i++)
        {
            Vector2 point = RandomPointAwayFromCentre();
            Vector2 size = Random.value > 0.5f
                ? new Vector2(2.2f, 0.72f)
                : new Vector2(0.72f, 2.2f);
            CreateSolid(root, "Шкаф с книгами", point, size,
                new Color(0.28f + floorNumber * 0.025f, 0.17f, 0.12f));
        }
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
