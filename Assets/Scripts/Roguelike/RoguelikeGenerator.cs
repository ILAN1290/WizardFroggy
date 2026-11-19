using System;
using System.Collections.Generic;

/// <summary>
/// Builds a connected room graph for one of the three library floors.
/// This file contains level topology and room-type generation only.
/// </summary>
public enum RoguelikeRoomType
{
    Combat,
    Chest,
    Merchant,
    Stairs
}

[Serializable]
public class RoguelikeRoomData
{
    public int Index;
    public int X;
    public int Y;
    public RoguelikeRoomType Type;
    public List<int> Neighbours = new List<int>();
    public bool IsCleared;
    public bool IsVisited;
    public bool IsExit;
}

[Serializable]
public class RoguelikeFloorData
{
    public int FloorNumber;
    public int Seed;
    public int Width;
    public int Height;
    public List<RoguelikeRoomData> Rooms = new List<RoguelikeRoomData>();

    public RoguelikeRoomData GetRoom(int index)
    {
        if (index < 0 || index >= Rooms.Count) return null;
        return Rooms[index];
    }

    public int FindNeighbour(int roomIndex, int dx, int dy)
    {
        RoguelikeRoomData room = GetRoom(roomIndex);
        if (room == null) return -1;

        int targetX = room.X + dx;
        int targetY = room.Y + dy;
        if (targetX < 0 || targetX >= Width || targetY < 0 || targetY >= Height)
            return -1;

        int target = targetY * Width + targetX;
        return room.Neighbours.Contains(target) ? target : -1;
    }
}

public static class RoguelikeGenerator
{
    public static RoguelikeFloorData GenerateFloor(int floorNumber, int seed = -1)
    {
        const int width = 4;
        const int height = 3;
        int actualSeed = seed < 0 ? Environment.TickCount ^ (floorNumber * 7919) : seed;
        System.Random random = new System.Random(actualSeed);

        RoguelikeFloorData floor = new RoguelikeFloorData
        {
            FloorNumber = floorNumber,
            Seed = actualSeed,
            Width = width,
            Height = height
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                RoguelikeRoomType type;

                if (index == 0)
                    type = RoguelikeRoomType.Combat;
                else if (index == width * height - 1)
                    type = RoguelikeRoomType.Stairs;
                else
                    type = RollRoomType(random);

                floor.Rooms.Add(new RoguelikeRoomData
                {
                    Index = index,
                    X = x,
                    Y = y,
                    Type = type,
                    IsCleared = index == 0,
                    IsExit = index == width * height - 1
                });
            }
        }

        // Every adjacent grid cell is connected. The player can explore and backtrack.
        foreach (RoguelikeRoomData room in floor.Rooms)
        {
            if (room.X > 0) room.Neighbours.Add(room.Index - 1);
            if (room.X < width - 1) room.Neighbours.Add(room.Index + 1);
            if (room.Y > 0) room.Neighbours.Add(room.Index - width);
            if (room.Y < height - 1) room.Neighbours.Add(room.Index + width);
        }

        EnsureSpecialRooms(floor, random);
        return floor;
    }

    private static RoguelikeRoomType RollRoomType(System.Random random)
    {
        int roll = random.Next(0, 100);
        if (roll < 74) return RoguelikeRoomType.Combat;
        if (roll < 90) return RoguelikeRoomType.Chest;
        return RoguelikeRoomType.Merchant;
    }

    private static void EnsureSpecialRooms(RoguelikeFloorData floor, System.Random random)
    {
        bool hasChest = false;
        bool hasMerchant = false;

        for (int i = 1; i < floor.Rooms.Count - 1; i++)
        {
            hasChest |= floor.Rooms[i].Type == RoguelikeRoomType.Chest;
            hasMerchant |= floor.Rooms[i].Type == RoguelikeRoomType.Merchant;
        }

        if (!hasChest)
            floor.Rooms[random.Next(1, floor.Rooms.Count - 1)].Type = RoguelikeRoomType.Chest;

        if (!hasMerchant)
        {
            List<int> candidates = new List<int>();
            for (int i = 1; i < floor.Rooms.Count - 1; i++)
                if (floor.Rooms[i].Type == RoguelikeRoomType.Combat)
                    candidates.Add(i);

            if (candidates.Count > 0)
                floor.Rooms[candidates[random.Next(candidates.Count)]].Type = RoguelikeRoomType.Merchant;
        }
    }
}
