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
        const int startX = 0;
        const int startY = 0;
        int startIndex = startY * width + startX;

        HashSet<int> selected = new HashSet<int>();
        Dictionary<int, int> parentRoom = new Dictionary<int, int>();
        selected.Add(startIndex);
        parentRoom[startIndex] = -1;

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

            int nextRoom = frontier[random.Next(frontier.Count)];
            int nextX = nextRoom % width;
            int nextY = nextRoom / width;
            List<int> possibleParents = new List<int>();
            if (nextX > 0 && selected.Contains(nextRoom - 1)) possibleParents.Add(nextRoom - 1);
            if (nextX < width - 1 && selected.Contains(nextRoom + 1)) possibleParents.Add(nextRoom + 1);
            if (nextY > 0 && selected.Contains(nextRoom - width)) possibleParents.Add(nextRoom - width);
            if (nextY < height - 1 && selected.Contains(nextRoom + width)) possibleParents.Add(nextRoom + width);

            selected.Add(nextRoom);
            parentRoom[nextRoom] = possibleParents[random.Next(possibleParents.Count)];
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

        // Keep a tree-shaped route graph. Every room is reachable, and the
        // tree always has dead ends so the stairs can be placed at a true leaf.
        foreach (KeyValuePair<int, int> link in parentRoom)
        {
            if (link.Value < 0) continue;
            floor.GetRoom(link.Key).Neighbours.Add(link.Value);
            floor.GetRoom(link.Value).Neighbours.Add(link.Key);
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
