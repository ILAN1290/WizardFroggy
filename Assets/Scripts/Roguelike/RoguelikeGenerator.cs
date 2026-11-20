using System;
using System.Collections.Generic;

/// <summary>
/// Builds a sparse, fully connected room graph inside a 4x4 map grid.
/// Empty grid cells are null and are not valid destinations.
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
    public bool IsStartRoom;
}

[Serializable]
public class RoguelikeFloorData
{
    public int FloorNumber;
    public int Seed;
    public int Width;
    public int Height;
    public int StartRoomIndex;
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
        const int height = 4;
        const int cellCount = width * height;
        int actualSeed = seed < 0 ? Environment.TickCount ^ (floorNumber * 7919) : seed;
        System.Random random = new System.Random(actualSeed);

        // Nine to eleven rooms occupy a 4x4 grid. A randomized frontier growth
        // keeps every generated room reachable without filling the entire map.
        int roomCount = random.Next(9, 12);
        int startX = random.Next(1, 3);
        int startY = random.Next(1, 3);
        int startIndex = startY * width + startX;

        HashSet<int> selected = new HashSet<int>();
        selected.Add(startIndex);

        while (selected.Count < roomCount)
        {
            List<int> frontier = new List<int>();
            for (int index = 0; index < cellCount; index++)
            {
                if (selected.Contains(index)) continue;
                int x = index % width;
                int y = index / width;
                bool touchesSelected =
                    (x > 0 && selected.Contains(index - 1)) ||
                    (x < width - 1 && selected.Contains(index + 1)) ||
                    (y > 0 && selected.Contains(index - width)) ||
                    (y < height - 1 && selected.Contains(index + width));

                if (touchesSelected) frontier.Add(index);
            }

            if (frontier.Count == 0) break;
            selected.Add(frontier[random.Next(frontier.Count)]);
        }

        RoguelikeFloorData floor = new RoguelikeFloorData
        {
            FloorNumber = floorNumber,
            Seed = actualSeed,
            Width = width,
            Height = height,
            StartRoomIndex = startIndex
        };

        // Preserve grid indices: empty cells remain null so map coordinates and
        // room indices never drift apart.
        for (int i = 0; i < cellCount; i++)
            floor.Rooms.Add(null);

        foreach (int index in selected)
        {
            int x = index % width;
            int y = index / width;
            floor.Rooms[index] = new RoguelikeRoomData
            {
                Index = index,
                X = x,
                Y = y,
                Type = RoguelikeRoomType.Combat,
                IsCleared = index == startIndex,
                IsStartRoom = index == startIndex
            };
        }

        // Build only doors between rooms that actually exist.
        foreach (RoguelikeRoomData room in floor.Rooms)
        {
            if (room == null) continue;

            if (room.X > 0 && floor.GetRoom(room.Index - 1) != null)
                room.Neighbours.Add(room.Index - 1);
            if (room.X < width - 1 && floor.GetRoom(room.Index + 1) != null)
                room.Neighbours.Add(room.Index + 1);
            if (room.Y > 0 && floor.GetRoom(room.Index - width) != null)
                room.Neighbours.Add(room.Index - width);
            if (room.Y < height - 1 && floor.GetRoom(room.Index + width) != null)
                room.Neighbours.Add(room.Index + width);
        }

        AssignSpecialRooms(floor, random);
        return floor;
    }

    private static void AssignSpecialRooms(RoguelikeFloorData floor, System.Random random)
    {
        int start = floor.StartRoomIndex;
        int[] distances = new int[floor.Width * floor.Height];
        for (int i = 0; i < distances.Length; i++) distances[i] = -1;

        Queue<int> queue = new Queue<int>();
        distances[start] = 0;
        queue.Enqueue(start);

        // BFS finds a distant dead-end for the stairs. The stairs room is
        // always reachable and has only one entrance.
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            RoguelikeRoomData room = floor.GetRoom(current);
            if (room == null) continue;

            for (int i = 0; i < room.Neighbours.Count; i++)
            {
                int next = room.Neighbours[i];
                if (distances[next] >= 0) continue;
                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }
        }

        int stairsIndex = -1;
        int farthestDistance = -1;
        for (int i = 0; i < floor.Rooms.Count; i++)
        {
            RoguelikeRoomData room = floor.Rooms[i];
            if (room == null || room.Index == start || room.Neighbours.Count != 1)
                continue;

            if (distances[room.Index] > farthestDistance)
            {
                farthestDistance = distances[room.Index];
                stairsIndex = room.Index;
            }
        }

        if (stairsIndex < 0)
        {
            for (int i = 0; i < floor.Rooms.Count; i++)
            {
                RoguelikeRoomData room = floor.Rooms[i];
                if (room == null || room.Index == start) continue;
                if (distances[room.Index] > farthestDistance)
                {
                    farthestDistance = distances[room.Index];
                    stairsIndex = room.Index;
                }
            }
        }

        RoguelikeRoomData stairs = floor.GetRoom(stairsIndex);
        if (stairs != null)
        {
            stairs.Type = RoguelikeRoomType.Stairs;
            stairs.IsExit = true;
        }

        List<int> specialCandidates = new List<int>();
        for (int i = 0; i < floor.Rooms.Count; i++)
        {
            RoguelikeRoomData room = floor.Rooms[i];
            if (room == null || room.Index == start || room.Index == stairsIndex)
                continue;
            specialCandidates.Add(room.Index);
        }

        if (specialCandidates.Count > 0)
        {
            int chestPick = random.Next(specialCandidates.Count);
            floor.GetRoom(specialCandidates[chestPick]).Type = RoguelikeRoomType.Chest;
            specialCandidates.RemoveAt(chestPick);
        }

        if (specialCandidates.Count > 0)
        {
            int merchantPick = random.Next(specialCandidates.Count);
            floor.GetRoom(specialCandidates[merchantPick]).Type = RoguelikeRoomType.Merchant;
        }
    }
}
