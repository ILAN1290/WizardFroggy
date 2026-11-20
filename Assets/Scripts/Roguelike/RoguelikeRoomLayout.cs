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

        AddWallsAndDoors(layout, room, roomWidth, roomHeight, centre);

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
        RoguelikeRoomData room, float width, float height, Vector2 centre)
    {
        const float wallThickness = 0.22f;
        const float doorOpening = 2.2f;
        const float spawnInset = 0.8f;

        float leftX = centre.x - width * 0.5f;
        float rightX = centre.x + width * 0.5f;
        float bottomY = centre.y - height * 0.5f;
        float topY = centre.y + height * 0.5f;
        float centreX = centre.x;
        float centreY = centre.y;

        bool hasLeftPassage = room.X > 0 && room.Neighbours.Contains(room.Index - 1);
        bool hasRightPassage = room.X < 3 && room.Neighbours.Contains(room.Index + 1);
        bool hasDownPassage = room.Y > 0 && room.Neighbours.Contains(room.Index - 4);
        bool hasUpPassage = room.Y < 3 && room.Neighbours.Contains(room.Index + 4);

        // Top passage: the door stays at 0 degrees.
        AddHorizontalBoundary(layout, "Верхняя невидимая стена",
            leftX, rightX, topY, wallThickness, hasUpPassage, doorOpening);
        if (hasUpPassage)
        {
            layout.Doors.Add(new RoguelikeDoorPlacement(
                new Vector2(centreX, topY),
                new Vector2(centreX, bottomY + spawnInset),
                0, 1, 0f));
        }

        // Left door rotates 90 degrees to the left.
        AddVerticalBoundary(layout, "Левая невидимая стена",
            leftX, bottomY, topY, wallThickness, hasLeftPassage, doorOpening);
        if (hasLeftPassage)
        {
            layout.Doors.Add(new RoguelikeDoorPlacement(
                new Vector2(leftX, centreY),
                new Vector2(rightX - spawnInset, centreY),
                -1, 0, 90f));
        }

        // Right door rotates 90 degrees to the right.
        AddVerticalBoundary(layout, "Правая невидимая стена",
            rightX, bottomY, topY, wallThickness, hasRightPassage, doorOpening);
        if (hasRightPassage)
        {
            layout.Doors.Add(new RoguelikeDoorPlacement(
                new Vector2(rightX, centreY),
                new Vector2(leftX + spawnInset, centreY),
                1, 0, 90f));
        }

        // Bottom door rotates by 180 degrees.
        AddHorizontalBoundary(layout, "Нижняя невидимая стена",
            leftX, rightX, bottomY, wallThickness, hasDownPassage, doorOpening);
        if (hasDownPassage)
        {
            layout.Doors.Add(new RoguelikeDoorPlacement(
                new Vector2(centreX, bottomY),
                new Vector2(centreX, topY - spawnInset),
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
        if (segmentLength <= 0.05f) return;

        layout.Walls.Add(new RoguelikeWallSection(name + " • левая часть",
            new Vector2(left + segmentLength * 0.5f, y),
            new Vector2(segmentLength, thickness), 0f));
        layout.Walls.Add(new RoguelikeWallSection(name + " • правая часть",
            new Vector2(centre + doorWidth * 0.5f + segmentLength * 0.5f, y),
            new Vector2(segmentLength, thickness), 0f));
    }

    private static void AddVerticalBoundary(RoguelikeRoomLayout layout, string name,
        float x, float bottom, float top, float thickness, bool hasDoor, float doorHeight)
    {
        if (!hasDoor)
        {
            layout.Walls.Add(new RoguelikeWallSection(name,
                new Vector2(x, (bottom + top) * 0.5f),
                new Vector2(top - bottom, thickness), 90f));
            return;
        }

        float centre = (bottom + top) * 0.5f;
        float segmentLength = (top - bottom - doorHeight) * 0.5f;
        if (segmentLength <= 0.05f) return;

        layout.Walls.Add(new RoguelikeWallSection(name + " • нижняя часть",
            new Vector2(x, bottom + segmentLength * 0.5f),
            new Vector2(segmentLength, thickness), 90f));
        layout.Walls.Add(new RoguelikeWallSection(name + " • верхняя часть",
            new Vector2(x, centre + doorHeight * 0.5f + segmentLength * 0.5f),
            new Vector2(segmentLength, thickness), 90f));
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
