using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoguelikeRoomLayout
{
    public readonly List<RoguelikeWallSection> Walls = new List<RoguelikeWallSection>();
    public readonly List<RoguelikeDoorPlacement> Doors = new List<RoguelikeDoorPlacement>();
    public readonly List<RoguelikeShelfPlacement> Shelves = new List<RoguelikeShelfPlacement>();
    public readonly List<Vector2> TrapPositions = new List<Vector2>();
    public readonly List<RoguelikeGhostPlacement> Ghosts = new List<RoguelikeGhostPlacement>();

    public bool SpawnChest;
    public bool SpawnMerchant;
    public bool SpawnExit;
    public Vector2 ChestPosition;
    public Vector2 MerchantPosition;
    public Vector2 ExitPosition;
}

public struct RoguelikeWallSection
{
    public string Name;
    public Vector2 Position;
    public Vector2 Size;
    public float Rotation;

    public RoguelikeWallSection(string name, Vector2 position, Vector2 size, float rotation)
    {
        Name = name;
        Position = position;
        Size = size;
        Rotation = rotation;
    }
}

public struct RoguelikeDoorPlacement
{
    public Vector2 Position;
    public Vector2 SpawnPosition;
    public int DirectionX;
    public int DirectionY;
    public float Rotation;

    public RoguelikeDoorPlacement(Vector2 position, Vector2 spawnPosition,
        int directionX, int directionY, float rotation)
    {
        Position = position;
        SpawnPosition = spawnPosition;
        DirectionX = directionX;
        DirectionY = directionY;
        Rotation = rotation;
    }
}

public struct RoguelikeShelfPlacement
{
    public string Name;
    public Vector2 Position;
    public bool Upper;
    public float Scale;

    public RoguelikeShelfPlacement(string name, Vector2 position, bool upper, float scale)
    {
        Name = name;
        Position = position;
        Upper = upper;
        Scale = scale;
    }
}

public struct RoguelikeGhostPlacement
{
    public Vector2 Position;
    public bool Melee;
    public bool MiniBoss;

    public RoguelikeGhostPlacement(Vector2 position, bool melee, bool miniBoss)
    {
        Position = position;
        Melee = melee;
        MiniBoss = miniBoss;
    }
}

/// <summary>
/// Owns all room-level layout decisions: wall sections, door positions,
/// shelf patterns, trap positions, and the number/type/positions of enemies.
/// RoomGenerator only turns this plan into Unity GameObjects.
/// </summary>
public static class RoguelikeRoomLayoutGenerator
{
    public static RoguelikeRoomLayout Create(RoguelikeRoomData room, int floorNumber,
        int floorSeed, float roomWidth, float roomHeight, Vector2 centre)
    {
        RoguelikeRoomLayout layout = new RoguelikeRoomLayout();
        if (room == null) return layout;

        System.Random random = new System.Random(
            floorSeed ^ (room.Index * 104729) ^ (floorNumber * 7919));

        AddWallsAndDoors(layout, room, centre);

        layout.ChestPosition = centre;
        layout.MerchantPosition = centre;
        layout.ExitPosition = centre + new Vector2(0f, 1.15f);

        if (room.IsStartRoom)
            return layout;

        switch (room.Type)
        {
            case RoguelikeRoomType.Combat:
                AddShelfPattern(layout, room.Index, floorNumber, centre);
                AddCombatEncounter(layout, random, floorNumber, roomWidth, roomHeight, centre);
                break;

            case RoguelikeRoomType.Chest:
                layout.SpawnChest = !room.IsCleared;
                break;

            case RoguelikeRoomType.Merchant:
                layout.SpawnMerchant = true;
                break;

            case RoguelikeRoomType.Stairs:
                layout.SpawnExit = true;
                if (!room.IsCleared)
                    layout.Ghosts.Add(new RoguelikeGhostPlacement(
                        centre + new Vector2(0f, -1.25f), true, true));
                break;
        }

        return layout;
    }

    private static void AddWallsAndDoors(RoguelikeRoomLayout layout,
        RoguelikeRoomData room, Vector2 fallbackCentre)
    {
        // Pixel margins are authored against the project's 1920x1080 UI reference.
        // They scale with the active camera viewport, so the room bounds remain
        // in the same screen positions at different Game view resolutions.
        const float referenceWidth = 1920f;
        const float referenceHeight = 1080f;
        const float topMarginPixels = 260f;
        const float leftMarginPixels = 170f;
        const float rightMarginPixels = 170f;
        const float bottomMarginPixels = 130f;

        const float wallThickness = 0.22f;
        const float doorOpening = 2.2f;
        const float playerSpawnInset = 0.8f;

        Camera camera = Camera.main;
        float halfHeight = camera != null && camera.orthographic
            ? camera.orthographicSize : 6f;
        float aspect = camera != null
            ? camera.aspect
            : Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        Vector2 cameraCentre = camera != null
            ? new Vector2(camera.transform.position.x, camera.transform.position.y)
            : fallbackCentre;

        float halfWidth = halfHeight * aspect;

        // Convert the reference-pixel margins to world units using the current
        // viewport size. No visible wall sprite is created at these coordinates.
        float viewportWidth = camera != null && camera.pixelWidth > 0
            ? camera.pixelWidth : Mathf.Max(1, Screen.width);
        float viewportHeight = camera != null && camera.pixelHeight > 0
            ? camera.pixelHeight : Mathf.Max(1, Screen.height);

        float pixelsPerWorldUnit = viewportHeight / Mathf.Max(0.01f, halfHeight * 2f);
        float leftInset = leftMarginPixels * (viewportWidth / referenceWidth)
            / pixelsPerWorldUnit;
        float rightInset = rightMarginPixels * (viewportWidth / referenceWidth)
            / pixelsPerWorldUnit;
        float topInset = topMarginPixels * (viewportHeight / referenceHeight)
            / pixelsPerWorldUnit;
        float bottomInset = bottomMarginPixels * (viewportHeight / referenceHeight)
            / pixelsPerWorldUnit;

        float leftX = cameraCentre.x - halfWidth + leftInset;
        float rightX = cameraCentre.x + halfWidth - rightInset;
        float topY = cameraCentre.y + halfHeight - topInset;
        float bottomY = cameraCentre.y - halfHeight + bottomInset;
        float centreX = (leftX + rightX) * 0.5f;
        float centreY = (bottomY + topY) * 0.5f;

        bool hasLeftPassage = room.X > 0 && room.Neighbours.Contains(room.Index - 1);
        bool hasRightPassage = room.X < 3 && room.Neighbours.Contains(room.Index + 1);
        bool hasDownPassage = room.Y > 0 && room.Neighbours.Contains(room.Index - 4);
        bool hasUpPassage = room.Y < 3 && room.Neighbours.Contains(room.Index + 4);

        // Top edge: door stays unrotated.
        AddHorizontalBoundary(layout, "Верхняя невидимая стена",
            leftX, rightX, topY, wallThickness, hasUpPassage, doorOpening);
        if (hasUpPassage)
        {
            layout.Doors.Add(new RoguelikeDoorPlacement(
                new Vector2(centreX, topY),
                new Vector2(centreX, bottomY + playerSpawnInset),
                0, 1, 0f));
        }

        // Left and right doors rotate with the edge they occupy.
        AddVerticalBoundary(layout, "Левая невидимая стена",
            leftX, bottomY, topY, wallThickness, hasLeftPassage, doorOpening);
        if (hasLeftPassage)
        {
            layout.Doors.Add(new RoguelikeDoorPlacement(
                new Vector2(leftX, centreY),
                new Vector2(rightX - playerSpawnInset, centreY),
                -1, 0, -90f));
        }

        AddVerticalBoundary(layout, "Правая невидимая стена",
            rightX, bottomY, topY, wallThickness, hasRightPassage, doorOpening);
        if (hasRightPassage)
        {
            layout.Doors.Add(new RoguelikeDoorPlacement(
                new Vector2(rightX, centreY),
                new Vector2(leftX + playerSpawnInset, centreY),
                1, 0, 90f));
        }

        // Bottom passage is rotated by 180 degrees.
        AddHorizontalBoundary(layout, "Нижняя невидимая стена",
            leftX, rightX, bottomY, wallThickness, hasDownPassage, doorOpening);
        if (hasDownPassage)
        {
            layout.Doors.Add(new RoguelikeDoorPlacement(
                new Vector2(centreX, bottomY),
                new Vector2(centreX, topY - playerSpawnInset),
                0, -1, 180f));
        }
    }

    private static void AddHorizontalBoundary(RoguelikeRoomLayout layout, string name,
        float left, float right, float y, float thickness, bool hasDoor, float doorWidth)
    {
        if (!hasDoor)
        {
            layout.Walls.Add(new RoguelikeWallSection(name,
                new Vector2((left + right) * 0.5f, y),
                new Vector2(right - left, thickness), 0f));
            return;
        }

        float centre = (left + right) * 0.5f;
        float segmentLength = (right - left - doorWidth) * 0.5f;
        if (segmentLength > 0.05f)
        {
            layout.Walls.Add(new RoguelikeWallSection(name + " • левая часть",
                new Vector2(left + segmentLength * 0.5f, y),
                new Vector2(segmentLength, thickness), 0f));
            layout.Walls.Add(new RoguelikeWallSection(name + " • правая часть",
                new Vector2(centre + doorWidth * 0.5f + segmentLength * 0.5f, y),
                new Vector2(segmentLength, thickness), 0f));
        }
    }

    private static void AddVerticalBoundary(RoguelikeRoomLayout layout, string name,
        float x, float bottom, float top, float thickness, bool hasDoor, float doorHeight)
    {
        float length = top - bottom;
        if (!hasDoor)
        {
            layout.Walls.Add(new RoguelikeWallSection(name,
                new Vector2(x, (bottom + top) * 0.5f),
                new Vector2(length, thickness), 90f));
            return;
        }

        float centre = (bottom + top) * 0.5f;
        float segmentLength = (length - doorHeight) * 0.5f;
        if (segmentLength > 0.05f)
        {
            layout.Walls.Add(new RoguelikeWallSection(name + " • нижняя часть",
                new Vector2(x, bottom + segmentLength * 0.5f),
                new Vector2(segmentLength, thickness), 90f));
            layout.Walls.Add(new RoguelikeWallSection(name + " • верхняя часть",
                new Vector2(x, centre + doorHeight * 0.5f + segmentLength * 0.5f),
                new Vector2(segmentLength, thickness), 90f));
        }
    }

    private static void AddCombatEncounter(RoguelikeRoomLayout layout, System.Random random,
        int floorNumber, float roomWidth, float roomHeight, Vector2 centre)
    {
        int enemyCount = random.Next(2 + floorNumber, 4 + floorNumber);
        for (int i = 0; i < enemyCount; i++)
        {
            Vector2 position = RandomPosition(random, roomWidth, roomHeight, centre, 1.8f);
            bool melee = random.NextDouble() < 0.35;
            layout.Ghosts.Add(new RoguelikeGhostPlacement(position, melee, false));
        }

        int trapCount = Mathf.Clamp(floorNumber, 1, 3);
        for (int i = 0; i < trapCount; i++)
            layout.TrapPositions.Add(RandomPosition(random, roomWidth, roomHeight, centre, 2.2f));
    }

    private static Vector2 RandomPosition(System.Random random, float width,
        float height, Vector2 centre, float minimumDistanceFromCentre)
    {
        Vector2 position = Vector2.zero;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = (float)(random.NextDouble() * 2.0 - 1.0) * (width * 0.5f - 2.6f);
            float y = (float)(random.NextDouble() * 2.0 - 1.0) * (height * 0.5f - 2.0f);
            position = new Vector2(x, y);
            if (position.magnitude >= minimumDistanceFromCentre) break;
        }
        return centre + position;
    }

    private static void AddShelfPattern(RoguelikeRoomLayout layout,
        int roomIndex, int floorNumber, Vector2 centre)
    {
        int pattern = Mathf.Abs(roomIndex + floorNumber - 1) % 8;
        switch (pattern)
        {
            case 0:
                AddShelf(layout, centre, "Стеллаж СЗ", -5.2f, 2.1f, false, 0.42f);
                AddShelf(layout, centre, "Стеллаж СВ", 5.2f, 2.1f, false, 0.42f);
                AddShelf(layout, centre, "Стеллаж ЮЗ", -5.2f, -2.1f, false, 0.42f);
                AddShelf(layout, centre, "Стеллаж ЮВ", 5.2f, -2.1f, false, 0.42f);
                break;
            case 1:
                AddShelf(layout, centre, "Стеллаж север", 0f, 2.65f, true, 0.35f);
                AddShelf(layout, centre, "Стеллаж юг", 0f, -2.65f, true, 0.35f);
                AddShelf(layout, centre, "Стеллаж запад", -6.2f, 0f, false, 0.3f);
                AddShelf(layout, centre, "Стеллаж восток", 6.2f, 0f, false, 0.3f);
                break;
            case 2:
                AddShelf(layout, centre, "Стеллаж СЗ", -4.8f, 2.2f, true, 0.3f);
                AddShelf(layout, centre, "Стеллаж СВ", 4.8f, 1.6f, true, 0.3f);
                AddShelf(layout, centre, "Стеллаж ЮЗ", -4.8f, -1.6f, true, 0.3f);
                AddShelf(layout, centre, "Стеллаж ЮВ", 4.8f, -2.2f, true, 0.3f);
                break;
            case 3:
                AddShelf(layout, centre, "Стеллаж СЗ", -5.7f, 2.55f, false, 0.34f);
                AddShelf(layout, centre, "Стеллаж СВ", 5.7f, 2.55f, false, 0.34f);
                AddShelf(layout, centre, "Стеллаж ЮЗ", -5.7f, -2.55f, false, 0.34f);
                AddShelf(layout, centre, "Стеллаж ЮВ", 5.7f, -2.55f, false, 0.34f);
                break;
            case 4:
                AddShelf(layout, centre, "Архивная линия север", 0f, 2.45f, true, 0.42f);
                AddShelf(layout, centre, "Архивная линия юг", 0f, -2.45f, true, 0.42f);
                break;
            case 5:
                AddShelf(layout, centre, "Стеллаж запад север", -6.1f, 1.6f, false, 0.34f);
                AddShelf(layout, centre, "Стеллаж запад юг", -6.1f, -1.6f, false, 0.34f);
                AddShelf(layout, centre, "Стеллаж восток север", 6.1f, 1.6f, false, 0.34f);
                AddShelf(layout, centre, "Стеллаж восток юг", 6.1f, -1.6f, false, 0.34f);
                break;
            case 6:
                AddShelf(layout, centre, "Полка северо-запад", -3.6f, 2.5f, true, 0.28f);
                AddShelf(layout, centre, "Полка северо-восток", 3.6f, 2.5f, true, 0.28f);
                AddShelf(layout, centre, "Полка юго-запад", -3.6f, -2.5f, true, 0.28f);
                AddShelf(layout, centre, "Полка юго-восток", 3.6f, -2.5f, true, 0.28f);
                break;
            default:
                AddShelf(layout, centre, "Центральный архив слева", -5.3f, 0f, false, 0.38f);
                AddShelf(layout, centre, "Центральный архив справа", 5.3f, 0f, false, 0.38f);
                AddShelf(layout, centre, "Полка севера", 0f, 2.65f, true, 0.28f);
                AddShelf(layout, centre, "Полка юга", 0f, -2.65f, true, 0.28f);
                break;
        }
    }

    private static void AddShelf(RoguelikeRoomLayout layout, Vector2 centre,
        string name, float x, float y, bool upper, float scale)
    {
        layout.Shelves.Add(new RoguelikeShelfPlacement(
            name, centre + new Vector2(x, y), upper, scale));
    }
}
