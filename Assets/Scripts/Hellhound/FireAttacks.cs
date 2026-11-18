using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum FireFloorMode { Hybrid, Prefabs, Patterns, Sprites }
public enum FirePattern { Rows, Columns, Checker, Cross, Frame, Diagonals, Scatter, SafeLane }

[System.Serializable]
public class FireFloor
{
    public Tilemap attentionMap;
    public Tilemap fireMap;
}

// Chih: 0 — полы огня, 1 — метеоры, 2 — луч из пасти.
public class FireAttacks : BossAttack
{
    [Header("Атака 0 — Общее")]
    [SerializeField] private FireFloorMode mode = FireFloorMode.Hybrid;
    [SerializeField] private int countOfFireFloors = 10;
    [SerializeField] private float attentionDuration = 1f;
    [SerializeField] private float fireFloorDuration = 1f;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private Transform gridTransform;
    [HideInInspector] public Tilemap activeTilemap;

    [Header("Атака 0 — Твои префабы полов")]
    public FireFloor[] fireFloors;
    [Range(0f, 1f)] [SerializeField] private float prefabChance = 0.5f;

    [Header("Атака 0 — Паттерны")]
    [SerializeField] private Tilemap floorReference;
    [SerializeField] private TileBase attentionTile;   // пусто = берётся из твоих префабов
    [SerializeField] private TileBase fireTile;        // пусто = берётся из твоих префабов
    [SerializeField] private int columns = 16;
    [SerializeField] private int rows = 8;
    [SerializeField] private Vector2 gridOffset = Vector2.zero;
    [SerializeField] private float cellSize = 1f;

    [Header("Атака 0 — Доступная игровая зона")]
    [SerializeField] private bool useCustomFloorArea = false;
    [SerializeField] private Vector2 floorAreaCenter = new Vector2(-0.04f, -2.28f);
    [SerializeField] private Vector2 floorAreaSize = new Vector2(16.67f, 7.27f);

    [Header("Атака 0 — Спрайты (режим Sprites)")]
    [SerializeField] private Sprite attentionSprite;
    [SerializeField] private Sprite fireSprite;
    [SerializeField] private Color attentionColor = new Color(1f, 0.78f, 0.2f, 0.9f);
    [SerializeField] private Color fireColor = new Color(1f, 0.42f, 0.12f, 0.95f);

    [Header("Атака 0 — Отрисовка")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 2;
    [SerializeField] private bool embersEnabled = true;
    [SerializeField] private int emberCount = 28;

    [Header("Атака 1 — Метеоры: поведение")]
    [SerializeField] private float meteorDuration = 12f;
    [SerializeField] private float meteorInterval = 0.65f;
    [SerializeField] private float meteorTelegraph = 1.0f;
    [SerializeField] private float meteorRadius = 1.5f;
    [SerializeField] private float meteorDamage = 12f;
    [SerializeField] private float burnDps = 3f;
    [SerializeField] private float burnTime = 2f;
    [SerializeField] private float meteorLinger = 1.4f;
    [Range(0f, 1f)] [SerializeField] private float aimChance = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float aimLead = 0.35f;
    [SerializeField] private AudioClip boomSound;

    [Header("Атака 1 — Метеоры: визуал")]
    [SerializeField] private bool tileFirePool = true; // лужа после удара = анимированные тайлы как в атаке 0
    [SerializeField] private Sprite meteorSprite;
    [SerializeField] private float meteorSize = 1.3f;
    [SerializeField] private float fallHeight = 9f;
    [SerializeField] private float fallAngle = 22f;
    [SerializeField] private float fallTime = 0.55f;
    [SerializeField] private float meteorSpin = 400f;
    [SerializeField] private Color rockColor = Color.white;
    [SerializeField] private Color glowColor = new Color(1f, 0.55f, 0.15f, 0.55f);
    [SerializeField] private Color shadowColor = new Color(0.04f, 0.02f, 0f, 1f);
    [SerializeField] private float shadowMinScale = 0.28f;
    [SerializeField] private float shadowStartAlpha = 0.12f;
    [SerializeField] private float shadowMaxAlpha = 0.6f;
    [SerializeField] private float shadowTelegraphShare = 0.45f;
    [SerializeField] private Color trailHot = new Color(1f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color trailCold = new Color(1f, 0.25f, 0.05f, 0f);
    [SerializeField] private int particles = 22;
    [SerializeField] private int impactSparks = 16;
    [SerializeField] private float impactRingScale = 1.8f;
    [SerializeField] private int airSortingOrder = 20;
    [SerializeField] private AudioClip fallSound;
    [SerializeField] private bool cameraShake = false;
    [SerializeField] private float shakeAmount = 0.1f;
    [SerializeField] private float shakeTime = 0.16f;

    [Header("Атака 2 — Огненный луч")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private float beamLength = 15f;
    [SerializeField] private float beamHalfAngle = 5f;
    [SerializeField] private float sweepAngle = 130f;
    [SerializeField] private float sweepTime = 2.6f;
    [SerializeField] private float beamTelegraph = 1.0f;
    [SerializeField] private float beamPause = 0.8f;
    [SerializeField] private int sweepCount = 3;
    [SerializeField] private float beamDamage = 10f;
    [SerializeField] private float beamTickInterval = 0.45f;
    [SerializeField] private AudioClip beamSound;

    private const int PatternCount = 8;


    private TileBase cachedFireTile;
    private bool fireTileResolved;
    private GameObject floorRoot;
    private GameObject paintRoot;
    private Tilemap paintMap;
    private Grid paintGrid;
    private Vector3Int cellOrigin;
    private float activeCellSize = 1f;
    private Tile fallbackAttentionTile;
    private Tile fallbackFireTile;

    private SpriteRenderer[] cellSr;
    private Vector2[] cellPos;
    private bool[] burning;
    private int gridCols, gridRows;
    private float cellScale = 1f;
    private int sortLayerId = int.MinValue;
    private bool shaking;

    private readonly List<Vector2> hotCells = new List<Vector2>();

    private Transform[] emberTf;
    private SpriteRenderer[] emberSr;
    private Vector2[] emberVel;
    private float[] emberLife;
    private float[] emberMax;

    private Vector2 Origin
    {
        get { return muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position; }
    }

    protected override IEnumerator Attack(int index)
    {
        switch (index)
        {
            case 0: yield return Floors(); break;
            case 1: yield return Meteors(); break;
            default: yield return Breath(); break;
        }
    }

    protected override void Cleanup()
    {
        ClearCells();
        ClearPaint();
        if (activeTilemap != null)
        {
            Destroy(activeTilemap.gameObject);
            activeTilemap = null;
        }
    }

    // ---------- помощники ----------
    private int LayerId()
    {
        if (sortLayerId != int.MinValue) return sortLayerId;
        sortLayerId = 0;
        foreach (SortingLayer l in SortingLayer.layers)
            if (l.name == sortingLayerName) { sortLayerId = l.id; break; }
        return sortLayerId;
    }

    private SpriteRenderer NewSprite(Transform parent, Sprite sprite, int order, Color color, Vector2 worldPos, float scale)
    {
        GameObject go = new GameObject("fx");
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * scale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = BossFx.SpriteMaterial;
        sr.sortingLayerID = LayerId();
        sr.sortingOrder = order;
        sr.color = color;
        return sr;
    }

    private static float Fit(Sprite s, float size)
    {
        if (s == null) return size;
        float u = Mathf.Max(0.01f, Mathf.Max(s.bounds.size.x, s.bounds.size.y));
        return size / u;
    }

    private static Color A(Color c, float a) { c.a = a; return c; }

    private class Parts
    {
        public Transform[] tf;
        public SpriteRenderer[] sr;
        private Vector2[] vel;
        private float[] life, max, size, grav;
        private int cursor;

        public Parts(Transform root, int count, int order, int layerId)
        {
            tf = new Transform[count];
            sr = new SpriteRenderer[count];
            vel = new Vector2[count];
            life = new float[count];
            max = new float[count];
            size = new float[count];
            grav = new float[count];

            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("p" + i);
                go.transform.SetParent(root, false);
                go.transform.localScale = Vector3.one * 0.1f;
                SpriteRenderer s = go.AddComponent<SpriteRenderer>();
                s.sprite = BossFx.GetDotSprite();
                s.sharedMaterial = BossFx.SpriteMaterial;
                s.sortingLayerID = layerId;
                s.sortingOrder = order;
                s.enabled = false;
                tf[i] = go.transform;
                sr[i] = s;
            }
        }

        public void Emit(Vector2 pos, Vector2 v, float sz, float ttl, float g)
        {
            int i = cursor;
            cursor = (cursor + 1) % tf.Length;
            tf[i].position = pos;
            vel[i] = v;
            size[i] = sz;
            life[i] = ttl;
            max[i] = ttl;
            grav[i] = g;
            sr[i].enabled = true;
        }

        public void Step(float dt, Color hot, Color cold)
        {
            for (int i = 0; i < tf.Length; i++)
            {
                if (life[i] <= 0f) continue;
                life[i] -= dt;
                if (life[i] <= 0f) { sr[i].enabled = false; continue; }
                vel[i] += new Vector2(0f, grav[i]) * dt;
                tf[i].position = (Vector2)tf[i].position + vel[i] * dt;
                float k = Mathf.Clamp01(life[i] / Mathf.Max(0.01f, max[i]));
                sr[i].color = Color.Lerp(cold, hot, k);
                tf[i].localScale = Vector3.one * (size[i] * Mathf.Lerp(0.35f, 1f, k));
            }
        }
    }

    // ================= АТАКА 0 =================
    private IEnumerator Floors()
    {
        if (mode == FireFloorMode.Sprites) { yield return SpriteFloors(); yield break; }
        yield return TileFloors();
    }

    private IEnumerator TileFloors()
    {
        bool hasPrefabs = fireFloors != null && fireFloors.Length > 0;
        if (gridTransform == null || (!hasPrefabs && attentionTile == null && fireTile == null))
        {
            Debug.LogWarning("FireAttacks: назначь Grid Transform и список Fire Floors (или тайлы вручную)");
            yield break;
        }

        BuildPaintMap();

        int index = hasPrefabs ? Random.Range(0, fireFloors.Length) : 0;
        int pattern = Random.Range(0, PatternCount);

        for (int i = 0; i < Mathf.Max(1, countOfFireFloors); i++)
        {
            bool usePrefab = hasPrefabs &&
                             (mode == FireFloorMode.Prefabs ||
                              (mode == FireFloorMode.Hybrid && Random.value < prefabChance));

            if (usePrefab) yield return PrefabCycle(index);
            else yield return PatternCycle(index, pattern);

            pattern = NextPattern(pattern);
            if (hasPrefabs)
            {
                int next;
                do { next = Random.Range(0, fireFloors.Length); }
                while (next == index && fireFloors.Length > 1);
                index = next;
            }
        }

        ClearPaint();
        ClearCells();
    }

    // ---------- готовый пол из твоего списка ----------
    private IEnumerator PrefabCycle(int index)
    {
        FireFloor f = fireFloors[index];
        if (f == null || f.attentionMap == null || f.fireMap == null)
        {
            yield return PatternCycle(index, Random.Range(0, PatternCount));
            yield break;
        }

        if (paintMap != null) paintMap.ClearAllTiles();
        hotCells.Clear();
        HideEmbers();

        if (activeTilemap != null) Destroy(activeTilemap.gameObject);
        activeTilemap = Instantiate(f.attentionMap, gridTransform);
        activeTilemap.transform.localPosition = Vector3.zero;
        TrimTilemapToPlayableArea(activeTilemap);
        yield return new WaitForSeconds(attentionDuration);

        if (activeTilemap != null) Destroy(activeTilemap.gameObject);
        activeTilemap = null;

        boss.Sfx(fireSound, 0.3f, 2f);
        activeTilemap = Instantiate(f.fireMap, gridTransform);
        activeTilemap.transform.localPosition = Vector3.zero;
        TrimTilemapToPlayableArea(activeTilemap);
        CollectHotCells(activeTilemap);

        // Prefab floors do not contain a FireZone damage component in this project.
        // Apply damage from the same occupied cells used by the visual map.
        float t = 0f;
        float damageTick = 0f;
        while (t < fireFloorDuration)
        {
            float dt = Time.deltaTime;
            t += dt;
            damageTick -= dt;

            if (embersEnabled) UpdateEmbers(dt);
            if (damageTick <= 0f && PlayerInFire())
            {
                boss.Burn(burnDps, burnTime);
                damageTick = 0.5f;
            }

            yield return null;
        }

        if (activeTilemap != null) Destroy(activeTilemap.gameObject);
        activeTilemap = null;
        hotCells.Clear();
        HideEmbers();
    }

    // ---------- паттерн твоими тайлами ----------
    private IEnumerator PatternCycle(int index, int pattern)
    {
        TileBase at = attentionTile != null ? attentionTile : ExtractTile(index, true);
        TileBase ft = fireTile != null ? fireTile : ExtractTile(index, false);
        if (at == null) at = GetFallbackTile(true);
        if (ft == null) ft = GetFallbackTile(false);

        ApplyPattern(pattern);
        FillHotCellsFromPattern();
        PaintCells(at);

        float t = 0f;
        float tele = Mathf.Max(0.05f, attentionDuration);
        while (t < tele)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / tele);
            float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 7f));
            if (paintMap != null) paintMap.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.4f, 1f, k) * pulse);
            yield return null;
        }

        boss.Sfx(fireSound, 0.3f, 2f);
        PaintCells(ft);

        float f = 0f;
        float tick = 0f;
        while (f < fireFloorDuration)
        {
            float dt = Time.deltaTime;
            f += dt;
            tick -= dt;

            float flick = 0.5f + 0.5f * (Mathf.Sin(Time.time * 11f) * 0.5f + Mathf.Sin(Time.time * 23f) * 0.5f);
            if (paintMap != null)
            {
                Color c = Color.Lerp(Color.white, new Color(1f, 0.88f, 0.68f), flick * 0.25f);
                paintMap.color = A(c, Mathf.Lerp(0.88f, 1f, flick));
            }

            if (embersEnabled) UpdateEmbers(dt);
            if (tick <= 0f && PlayerInFire())
            {
                boss.Burn(burnDps, burnTime);
                tick = 0.5f;
            }
            yield return null;
        }

        if (paintMap != null) paintMap.ClearAllTiles();
        hotCells.Clear();
        HideEmbers();
    }

    private TileBase ExtractTile(int index, bool attention)
    {
        if (fireFloors == null || fireFloors.Length == 0) return null;
        index = Mathf.Clamp(index, 0, fireFloors.Length - 1);
        FireFloor f = fireFloors[index];
        Tilemap map = f == null ? null : (attention ? f.attentionMap : f.fireMap);
        if (map == null) return null;

        foreach (Vector3Int p in map.cellBounds.allPositionsWithin)
        {
            TileBase t = map.GetTile(p);
            if (t != null) return t;
        }
        return null;
    }

    private Tile GetFallbackTile(bool attention)
    {
        if (attention)
        {
            if (fallbackAttentionTile == null)
            {
                fallbackAttentionTile = ScriptableObject.CreateInstance<Tile>();
                fallbackAttentionTile.sprite = BossFx.GetCellSprite(true);
                fallbackAttentionTile.color = attentionColor;
                fallbackAttentionTile.colliderType = Tile.ColliderType.None;
            }
            return fallbackAttentionTile;
        }
        if (fallbackFireTile == null)
        {
            fallbackFireTile = ScriptableObject.CreateInstance<Tile>();
            fallbackFireTile.sprite = BossFx.GetCellSprite(false);
            fallbackFireTile.color = fireColor;
            fallbackFireTile.colliderType = Tile.ColliderType.None;
        }
        return fallbackFireTile;
    }

    private void ResolveGridLayout()
    {
        if (paintGrid == null && gridTransform != null)
            paintGrid = gridTransform.GetComponent<Grid>();

        activeCellSize = paintGrid != null
            ? Mathf.Max(0.1f, paintGrid.cellSize.x)
            : Mathf.Max(0.2f, cellSize);

        if (useCustomFloorArea)
        {
            Vector2 half = floorAreaSize * 0.5f;
            Vector2 worldMin = floorAreaCenter + gridOffset - half;
            Vector2 worldMax = floorAreaCenter + gridOffset + half;

            if (paintGrid != null)
            {
                // Include only cells whose centres are inside the playable rectangle.
                // This prevents a row of fire tiles from spilling under the boundary flames.
                Vector3 cellSizeWorld = paintGrid.cellSize;
                Vector2 halfCell = new Vector2(
                    Mathf.Abs(cellSizeWorld.x) * 0.5f,
                    Mathf.Abs(cellSizeWorld.y) * 0.5f);
                cellOrigin = paintGrid.WorldToCell(worldMin + halfCell);
                Vector3Int maxCell = paintGrid.WorldToCell(
                    worldMax - halfCell - Vector2.one * 0.001f);
                gridCols = Mathf.Max(1, maxCell.x - cellOrigin.x + 1);
                gridRows = Mathf.Max(1, maxCell.y - cellOrigin.y + 1);
            }
            else
            {
                // Fallback grid is aligned by tile centres, just like Unity's Grid.
                int minX = Mathf.CeilToInt(worldMin.x / activeCellSize - 0.5f);
                int minY = Mathf.CeilToInt(worldMin.y / activeCellSize - 0.5f);
                int maxX = Mathf.FloorToInt(worldMax.x / activeCellSize - 0.5f);
                int maxY = Mathf.FloorToInt(worldMax.y / activeCellSize - 0.5f);
                cellOrigin = new Vector3Int(minX, minY, 0);
                gridCols = Mathf.Max(1, maxX - minX + 1);
                gridRows = Mathf.Max(1, maxY - minY + 1);
            }
        }
        else if (floorReference != null && paintGrid != null)
        {
            // Use the complete reference Tilemap bounds. Do not center a smaller
            // 16x8 grid inside the much larger source map.
            BoundsInt bounds = floorReference.cellBounds;
            cellOrigin = bounds.min;
            gridCols = Mathf.Max(1, bounds.size.x);
            gridRows = Mathf.Max(1, bounds.size.y);
        }
        else if (boss != null)
        {
            Vector2 half = boss.ArenaSize * 0.5f;
            Vector2 worldMin = boss.Center + gridOffset - half;
            Vector2 worldMax = boss.Center + gridOffset + half;

            if (paintGrid != null)
            {
                cellOrigin = paintGrid.WorldToCell(worldMin);
                Vector3Int maxCell = paintGrid.WorldToCell(
                    worldMax - Vector2.one * 0.001f);
                gridCols = Mathf.Max(1, maxCell.x - cellOrigin.x + 1);
                gridRows = Mathf.Max(1, maxCell.y - cellOrigin.y + 1);
            }
            else
            {
                cellOrigin = new Vector3Int(
                    Mathf.FloorToInt(worldMin.x / activeCellSize),
                    Mathf.FloorToInt(worldMin.y / activeCellSize), 0);
                gridCols = Mathf.Max(1, Mathf.CeilToInt(boss.ArenaSize.x / activeCellSize));
                gridRows = Mathf.Max(1, Mathf.CeilToInt(boss.ArenaSize.y / activeCellSize));
            }
        }
        else
        {
            gridCols = Mathf.Max(2, columns);
            gridRows = Mathf.Max(2, rows);
            cellOrigin = ArenaOrigin();
        }

        if (!useCustomFloorArea && floorReference == null)
        {
            cellOrigin += new Vector3Int(
                Mathf.RoundToInt(gridOffset.x / activeCellSize),
                Mathf.RoundToInt(gridOffset.y / activeCellSize), 0);
        }
    }

    private void BuildPaintMap()
    {
        ClearPaint();
        ClearCells();

        paintGrid = gridTransform != null ? gridTransform.GetComponent<Grid>() : null;
        ResolveGridLayout();

        paintRoot = new GameObject("FireFloorTiles");
        if (gridTransform != null) paintRoot.transform.SetParent(gridTransform, false);
        paintRoot.transform.localPosition = Vector3.zero;
        paintMap = paintRoot.AddComponent<Tilemap>();
        TilemapRenderer tr = paintRoot.AddComponent<TilemapRenderer>();
        tr.sortingLayerID = LayerId();
        tr.sortingOrder = sortingOrder;

        int n = gridCols * gridRows;
        cellPos = new Vector2[n];
        burning = new bool[n];
        cellSr = null;

        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                Vector3Int cell = new Vector3Int(
                    cellOrigin.x + c, cellOrigin.y + r, 0);
                cellPos[r * gridCols + c] = paintGrid != null
                    ? (Vector2)paintGrid.GetCellCenterWorld(cell)
                    : new Vector2((cell.x + 0.5f) * activeCellSize,
                        (cell.y + 0.5f) * activeCellSize);
            }

        floorRoot = new GameObject("FireFloorFx");
        floorRoot.transform.position = Vector3.zero;
        BuildEmbers();
    }

    private Vector3Int ArenaOrigin()
    {
        Vector2 size = new Vector2(gridCols * activeCellSize, gridRows * activeCellSize);
        Vector2 originWorld = boss.Center - size * 0.5f;
        return paintGrid != null
            ? paintGrid.WorldToCell(originWorld)
            : new Vector3Int(Mathf.RoundToInt(originWorld.x), Mathf.RoundToInt(originWorld.y), 0);
    }

    private void PaintCells(TileBase tile)
    {
        if (paintMap == null || tile == null) return;
        paintMap.ClearAllTiles();
        paintMap.color = Color.white;
        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                int i = r * gridCols + c;
                if (!burning[i]) continue;
                paintMap.SetTile(new Vector3Int(cellOrigin.x + c, cellOrigin.y + r, 0), tile);
            }
    }

    private TileBase ResolveFireTile()
    {
        if (fireTileResolved) return cachedFireTile;
        fireTileResolved = true;
        cachedFireTile = fireTile;
        if (cachedFireTile == null && fireFloors != null)
        {
            for (int i = 0; i < fireFloors.Length; i++)
            {
                TileBase t = ExtractTile(i, false);
                if (t != null) { cachedFireTile = t; break; }
            }
        }
        return cachedFireTile;
    }

    private Tilemap NewPoolMap()
    {
        if (gridTransform == null) return null;
        GameObject go = new GameObject("MeteorFire");
        go.transform.SetParent(gridTransform, false);
        go.transform.localPosition = Vector3.zero;
        Tilemap tm = go.AddComponent<Tilemap>();
        TilemapRenderer tr = go.AddComponent<TilemapRenderer>();
        tr.sortingLayerID = LayerId();
        tr.sortingOrder = sortingOrder;
        Track(go);
        return tm;
    }

    private void PaintFireCircle(Tilemap tm, Vector2 center, float radius, TileBase tile)
    {
        if (tm == null || tile == null) return;
        Grid g = tm.layoutGrid;
        if (g == null) return;

        float cs = Mathf.Max(0.1f, g.cellSize.x);
        int reach = Mathf.CeilToInt(radius / cs) + 1;
        Vector3Int c0 = g.WorldToCell(center);

        for (int y = -reach; y <= reach; y++)
            for (int x = -reach; x <= reach; x++)
            {
                Vector3Int cc = new Vector3Int(c0.x + x, c0.y + y, 0);
                Vector2 wc = g.GetCellCenterWorld(cc);
                if ((wc - center).sqrMagnitude <= radius * radius) tm.SetTile(cc, tile);
            }
    }

    private void TrimTilemapToPlayableArea(Tilemap map)
    {
        if (map == null || !useCustomFloorArea) return;

        Vector2 center = floorAreaCenter + gridOffset;
        Vector2 half = floorAreaSize * 0.5f;
        Vector2 min = center - half;
        Vector2 max = center + half;

        BoundsInt bounds = map.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!map.HasTile(cell)) continue;

            Vector3 world = map.GetCellCenterWorld(cell);
            if (world.x < min.x || world.x >= max.x ||
                world.y < min.y || world.y >= max.y)
            {
                map.SetTile(cell, null);
            }
        }

        map.CompressBounds();
    }

    private void CollectHotCells(Tilemap map)
    {
        hotCells.Clear();
        if (map == null) return;
        Grid g = map.layoutGrid;
        if (g != null) activeCellSize = Mathf.Max(0.1f, g.cellSize.x);
        foreach (Vector3Int p in map.cellBounds.allPositionsWithin)
        {
            if (!map.HasTile(p)) continue;
            hotCells.Add(map.GetCellCenterWorld(p));
        }
    }

    private void FillHotCellsFromPattern()
    {
        hotCells.Clear();
        for (int i = 0; i < burning.Length; i++)
            if (burning[i]) hotCells.Add(cellPos[i]);
    }

    private void ClearPaint()
    {
        if (paintRoot != null) Destroy(paintRoot);
        paintRoot = null;
        paintMap = null;
        paintGrid = null;
        if (fallbackAttentionTile != null) { Destroy(fallbackAttentionTile); fallbackAttentionTile = null; }
        if (fallbackFireTile != null) { Destroy(fallbackFireTile); fallbackFireTile = null; }
    }

    // ---------- паттерны ----------
    private void ApplyPattern(int pattern)
    {
        int count = 0;
        int safe = Random.Range(1, Mathf.Max(2, gridCols - 2));

        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                int i = r * gridCols + c;
                bool on;
                switch ((FirePattern)pattern)
                {
                    case FirePattern.Rows: on = r % 2 == 0; break;
                    case FirePattern.Columns: on = c % 2 == 0; break;
                    case FirePattern.Checker: on = (c + r) % 2 == 0; break;
                    case FirePattern.Cross:
                        on = c == gridCols / 2 || c == gridCols / 2 - 1 || r == gridRows / 2 || r == gridRows / 2 - 1;
                        break;
                    case FirePattern.Frame:
                        on = c == 0 || r == 0 || c == gridCols - 1 || r == gridRows - 1;
                        break;
                    case FirePattern.Diagonals: on = (c + r) % 4 < 2; break;
                    case FirePattern.Scatter: on = Random.value < 0.42f; break;
                    default: on = c < safe || c > safe + 1; break;
                }
                burning[i] = on;
                if (on) count++;
            }

        if (count == 0)
        {
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                {
                    int i = r * gridCols + c;
                    burning[i] = (c + r) % 2 == 0;
                }
        }
    }

    private int NextPattern(int current)
    {
        int next;
        do { next = Random.Range(0, PatternCount); } while (next == current && PatternCount > 1);
        return next;
    }

    private bool PlayerInFire()
    {
        if (hotCells.Count == 0 || boss == null || boss.Player == null)
            return false;

        Collider2D playerCollider = boss.Player.GetComponent<Collider2D>();
        if (playerCollider == null)
            playerCollider = boss.Player.GetComponentInChildren<Collider2D>();

        if (playerCollider != null)
        {
            Bounds playerBounds = playerCollider.bounds;
            float halfCell = activeCellSize * 0.5f;

            for (int i = 0; i < hotCells.Count; i++)
            {
                Vector2 cell = hotCells[i];
                bool overlapsX = playerBounds.min.x <= cell.x + halfCell &&
                    playerBounds.max.x >= cell.x - halfCell;
                bool overlapsY = playerBounds.min.y <= cell.y + halfCell &&
                    playerBounds.max.y >= cell.y - halfCell;
                if (overlapsX && overlapsY)
                    return true;
            }

            return false;
        }

        Vector2 playerPosition = boss.PlayerPos;
        float positionHalfCell = activeCellSize * 0.5f;
        for (int i = 0; i < hotCells.Count; i++)
        {
            if (Mathf.Abs(playerPosition.x - hotCells[i].x) <= positionHalfCell &&
                Mathf.Abs(playerPosition.y - hotCells[i].y) <= positionHalfCell)
                return true;
        }

        return false;
    }

    // ---------- режим Sprites ----------
    private IEnumerator SpriteFloors()
    {
        BuildCells();
        int pattern = Random.Range(0, PatternCount);

        for (int i = 0; i < Mathf.Max(1, countOfFireFloors); i++)
        {
            ApplyPattern(pattern);
            FillHotCellsFromPattern();
            ShowAttention();

            float t = 0f;
            while (t < attentionDuration)
            {
                t += Time.deltaTime;
                AnimateAttention(Mathf.Clamp01(t / Mathf.Max(0.01f, attentionDuration)));
                yield return null;
            }

            boss.Sfx(fireSound, 0.3f, 2f);
            ShowFire();

            float f = 0f;
            float tick = 0f;
            while (f < fireFloorDuration)
            {
                float dt = Time.deltaTime;
                f += dt;
                tick -= dt;
                AnimateFire();
                if (embersEnabled) UpdateEmbers(dt);
                if (tick <= 0f && PlayerInFire())
                {
                    boss.Burn(burnDps, burnTime);
                    tick = 0.5f;
                }
                yield return null;
            }

            HideCells();
            hotCells.Clear();
            yield return null;
            pattern = NextPattern(pattern);
        }

        ClearCells();
    }

    private void BuildCells()
    {
        ClearCells();
        ClearPaint();

        paintGrid = gridTransform != null ? gridTransform.GetComponent<Grid>() : null;
        ResolveGridLayout();

        floorRoot = new GameObject("FireFloorGrid");
        floorRoot.transform.position = Vector3.zero;

        int n = gridCols * gridRows;
        cellSr = new SpriteRenderer[n];
        cellPos = new Vector2[n];
        burning = new bool[n];

        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
            {
                int i = r * gridCols + c;
                Vector3Int cell = new Vector3Int(
                    cellOrigin.x + c, cellOrigin.y + r, 0);
                Vector2 position = paintGrid != null
                    ? (Vector2)paintGrid.GetCellCenterWorld(cell)
                    : new Vector2((cell.x + 0.5f) * activeCellSize,
                        (cell.y + 0.5f) * activeCellSize);
                cellPos[i] = position;

                GameObject go = new GameObject("Cell" + i);
                go.transform.SetParent(floorRoot.transform, false);
                go.transform.position = position;
                go.transform.localScale = Vector3.one * activeCellSize * 0.94f;

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = BossFx.SpriteMaterial;
                sr.sortingLayerID = LayerId();
                sr.sortingOrder = sortingOrder;
                sr.enabled = false;
                cellSr[i] = sr;
            }

        BuildEmbers();
    }

    private void ShowAttention()
    {
        Sprite s = attentionSprite != null ? attentionSprite : BossFx.GetCellSprite(true);
        cellScale = Fit(s, activeCellSize * 0.94f);
        for (int i = 0; i < cellSr.Length; i++)
        {
            cellSr[i].enabled = burning[i];
            if (!burning[i]) continue;
            cellSr[i].sprite = s;
            cellSr[i].color = attentionSprite != null ? Color.white : attentionColor;
            cellSr[i].transform.localScale = Vector3.one * cellScale * 0.6f;
        }
        HideEmbers();
    }

    private void ShowFire()
    {
        Sprite s = fireSprite != null ? fireSprite : BossFx.GetCellSprite(false);
        cellScale = Fit(s, activeCellSize * 0.94f);
        for (int i = 0; i < cellSr.Length; i++)
        {
            if (!burning[i]) continue;
            cellSr[i].sprite = s;
            cellSr[i].color = fireSprite != null ? Color.white : fireColor;
            cellSr[i].transform.localScale = Vector3.one * cellScale;
        }
    }

    private void AnimateAttention(float k)
    {
        float grow = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(k * 2.2f));
        bool custom = attentionSprite != null;
        for (int i = 0; i < cellSr.Length; i++)
        {
            if (!burning[i]) continue;
            float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 7f + i * 0.37f));
            cellSr[i].color = custom ? new Color(1f, 1f, 1f, pulse) : A(attentionColor, attentionColor.a * pulse);
            cellSr[i].transform.localScale = Vector3.one * cellScale * grow;
        }
    }

    private void AnimateFire()
    {
        bool custom = fireSprite != null;
        for (int i = 0; i < cellSr.Length; i++)
        {
            if (!burning[i]) continue;
            float n = Mathf.Sin(Time.time * 11f + i * 1.7f) * 0.5f + Mathf.Sin(Time.time * 23f + i * 0.9f) * 0.5f;
            float k = 0.5f + 0.5f * n;
            if (custom)
            {
                Color cc = Color.Lerp(Color.white, new Color(1f, 0.86f, 0.62f), k * 0.3f);
                cellSr[i].color = A(cc, Mathf.Lerp(0.85f, 1f, k));
            }
            else
            {
                Color cc = Color.Lerp(fireColor, new Color(1f, 0.85f, 0.35f, fireColor.a), k * 0.4f);
                cellSr[i].color = A(cc, fireColor.a * Mathf.Lerp(0.75f, 1f, k));
            }
            cellSr[i].transform.localScale = Vector3.one * cellScale * Mathf.Lerp(0.94f, 1.04f, k);
        }
    }

    // ---------- угли ----------
    private void BuildEmbers()
    {
        int n = embersEnabled ? Mathf.Clamp(emberCount, 0, 128) : 0;
        emberTf = new Transform[n];
        emberSr = new SpriteRenderer[n];
        emberVel = new Vector2[n];
        emberLife = new float[n];
        emberMax = new float[n];

        for (int i = 0; i < n; i++)
        {
            GameObject go = new GameObject("Ember" + i);
            go.transform.SetParent(floorRoot.transform, false);
            go.transform.localScale = Vector3.one * 0.12f;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BossFx.GetDotSprite();
            sr.sharedMaterial = BossFx.SpriteMaterial;
            sr.sortingLayerID = LayerId();
            sr.sortingOrder = sortingOrder + 2;
            sr.enabled = false;

            emberTf[i] = go.transform;
            emberSr[i] = sr;
        }
    }

    private void UpdateEmbers(float dt)
    {
        if (emberTf == null || hotCells.Count == 0) return;

        for (int i = 0; i < emberTf.Length; i++)
        {
            if (emberLife[i] > 0f)
            {
                emberLife[i] -= dt;
                emberVel[i] += new Vector2(Mathf.Sin(Time.time * 3f + i) * 0.5f, 1.1f) * dt;
                emberTf[i].position = (Vector2)emberTf[i].position + emberVel[i] * dt;

                float k = Mathf.Clamp01(emberLife[i] / Mathf.Max(0.01f, emberMax[i]));
                emberSr[i].color = new Color(1f, Mathf.Lerp(0.3f, 0.8f, k), 0.18f, k * 0.9f);
                emberTf[i].localScale = Vector3.one * (0.05f + 0.11f * k);
                if (emberLife[i] <= 0f) emberSr[i].enabled = false;
            }
            else if (Random.value < dt * 7f)
            {
                Vector2 cell = hotCells[Random.Range(0, hotCells.Count)];
                emberTf[i].position = cell + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(-0.3f, 0.1f)) * activeCellSize;
                emberVel[i] = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.4f));
                emberMax[i] = Random.Range(0.5f, 1.1f);
                emberLife[i] = emberMax[i];
                emberSr[i].enabled = true;
            }
        }
    }

    private void HideEmbers()
    {
        if (emberSr == null) return;
        for (int i = 0; i < emberSr.Length; i++)
        {
            if (emberSr[i] != null) emberSr[i].enabled = false;
            emberLife[i] = 0f;
        }
    }

    private void HideCells()
    {
        if (cellSr != null)
            for (int i = 0; i < cellSr.Length; i++)
                if (cellSr[i] != null) cellSr[i].enabled = false;
        HideEmbers();
    }

    private void ClearCells()
    {
        if (floorRoot != null) Destroy(floorRoot);
        floorRoot = null;
        cellSr = null;
        cellPos = null;
        burning = null;
        hotCells.Clear();
        emberTf = null;
        emberSr = null;
        emberVel = null;
        emberLife = null;
        emberMax = null;
    }

    // ================= АТАКА 1: МЕТЕОРЫ =================
    private IEnumerator Meteors()
    {
        float end = Time.time + meteorDuration;
        while (Time.time < end)
        {
            Vector2 p;
            if (Random.value < aimChance)
            {
                Vector2 lead = boss.PlayerRb != null ? boss.PlayerRb.linearVelocity * meteorTelegraph * aimLead : Vector2.zero;
                p = boss.ClampToArena(boss.PlayerPos + lead, meteorRadius);
            }
            else
            {
                p = boss.RandomPoint(meteorRadius);
            }

            StartCoroutine(Meteor(p));
            yield return new WaitForSeconds(meteorInterval);
        }
        yield return new WaitForSeconds(meteorTelegraph + fallTime + meteorLinger + 0.2f);
    }

        private IEnumerator Meteor(Vector2 ground)
    {
        GameObject root = new GameObject("Meteor");
        root.transform.position = ground;
        Track(root);

        int gOrder = sortingOrder;
        int aOrder = airSortingOrder;

        Sprite dot = BossFx.GetDotSprite();
        Sprite shadowSpr = BossFx.GetShadowSprite();
        Sprite rockSpr = meteorSprite != null ? meteorSprite : dot;
        Sprite poolSpr = fireSprite != null ? fireSprite : BossFx.GetCellSprite(false);

        float shadowFull = Fit(shadowSpr, meteorRadius * 2f);
        float rockScale = Fit(rockSpr, meteorSize);
        float poolScale = Fit(poolSpr, meteorRadius * 2f);

        SpriteRenderer shadow = NewSprite(root.transform, shadowSpr, gOrder + 1,
        A(shadowColor, shadowStartAlpha), ground, shadowFull * shadowMinScale);
        
        // лужа: тайлы как в атаке 0, либо спрайт-фолбэк
        TileBase poolTile = tileFirePool ? ResolveFireTile() : null;
        Tilemap poolMap = poolTile != null ? NewPoolMap() : null;
        SpriteRenderer pool = null;
        if (poolMap == null)
        {
            pool = NewSprite(root.transform, poolSpr, gOrder, A(fireColor, 0f), ground, poolScale);
            pool.enabled = false;
        }

        LineRenderer blast = BossFx.CreateLine(new Color(1f, 0.75f, 0.35f, 0.9f), 0.22f);
        blast.transform.SetParent(root.transform, false);
        BossFx.SetAlpha(blast, 0f);

        Vector2 from = ground + BossFx.Rotate(Vector2.up, fallAngle) * fallHeight;
        SpriteRenderer glow = NewSprite(root.transform, dot, aOrder - 1, glowColor, from, rockScale * 1.5f);
        SpriteRenderer rock = NewSprite(root.transform, rockSpr, aOrder, rockColor, from, rockScale * 0.55f);
        glow.enabled = false;
        rock.enabled = false;

        Parts parts = new Parts(root.transform, Mathf.Clamp(particles, 4, 64), aOrder - 2, LayerId());

        // --- телеграф: тень растёт и темнеет ---
                float t = 0f;
        float tele = Mathf.Max(0.05f, meteorTelegraph);
        while (t < tele)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / tele) * shadowTelegraphShare;
            SetShadow(shadow, shadowFull, k);
            yield return null;
        }

        // --- падение ---
        glow.enabled = true;
        rock.enabled = true;
        boss.Sfx(fallSound, 0.25f);

        float ft = Mathf.Max(0.08f, fallTime);
        float f = 0f;
        float spin = Random.value < 0.5f ? meteorSpin : -meteorSpin;
        Vector2 fallDir = (ground - from).normalized;

        while (f < ft)
        {
            float dt = Time.deltaTime;
            f += dt;
            float k = Mathf.Clamp01(f / ft);
            Vector2 p = Vector2.Lerp(from, ground, k * k);

            rock.transform.position = p;
            rock.transform.Rotate(0f, 0f, spin * dt);
            rock.transform.localScale = Vector3.one * rockScale * Mathf.Lerp(0.55f, 1f, k);

            glow.transform.position = p;
            glow.transform.localScale = Vector3.one * rockScale * Mathf.Lerp(1.2f, 2.1f, k);
            glow.color = A(glowColor, glowColor.a * (0.7f + 0.3f * Mathf.Sin(Time.time * 30f)));

            float near = 1f - Mathf.Clamp01((p - ground).magnitude / Mathf.Max(0.01f, fallHeight));
            SetShadow(shadow, shadowFull, Mathf.Lerp(shadowTelegraphShare, 1f, near));

            for (int i = 0; i < 2; i++)
                parts.Emit(
                    p - fallDir * Random.Range(0f, meteorSize * 0.4f) + Random.insideUnitCircle * 0.12f,
                    -fallDir * Random.Range(0.4f, 1.4f) + Random.insideUnitCircle * 0.5f,
                    Random.Range(0.18f, 0.34f), Random.Range(0.18f, 0.35f), 1.2f);

            parts.Step(dt, trailHot, trailCold);
            yield return null;
        }

        // --- удар ---
        rock.enabled = false;
        glow.enabled = false;
        shadow.enabled = false;

        boss.Boom(ground, meteorRadius, new Color(1f, 0.55f, 0.18f));
        boss.Sfx(boomSound, 0.4f);
        if (cameraShake) StartCoroutine(Shake());

        for (int i = 0; i < Mathf.Clamp(impactSparks, 0, 48); i++)
        {
            Vector2 d = BossFx.Rotate(Vector2.up, Random.Range(-80f, 80f));
            parts.Emit(ground + Random.insideUnitCircle * meteorRadius * 0.3f,
                       d * Random.Range(2.5f, 6f), Random.Range(0.14f, 0.3f),
                       Random.Range(0.35f, 0.7f), -9f);
        }

        if (Vector2.Distance(boss.PlayerPos, ground) <= meteorRadius)
        {
            boss.Hit(meteorDamage);
            boss.Burn(burnDps, burnTime);
        }

        if (poolMap != null) PaintFireCircle(poolMap, ground, meteorRadius, poolTile);
        else pool.enabled = true;

        // --- горящая лужа ---
        float linger = Mathf.Max(0.1f, meteorLinger);
        float l = 0f, tick = 0f, ringT = 0f;
        bool customPool = fireSprite != null;
        while (l < linger)
        {
            float dt = Time.deltaTime;
            l += dt;
            tick -= dt;
            ringT += dt;

            if (ringT < 0.3f)
            {
                float rk = ringT / 0.3f;
                BossFx.SetCircle(blast, ground, meteorRadius * Mathf.Lerp(0.5f, impactRingScale, Mathf.Sqrt(rk)));
                BossFx.SetAlpha(blast, 0.9f * (1f - rk));
            }
            else BossFx.SetAlpha(blast, 0f);

            float fade = 1f - Mathf.Clamp01(l / linger);
            float flick = 0.75f + 0.25f * Mathf.Sin(Time.time * 14f + ground.x);

            if (poolMap != null)
            {
                Color tc = Color.Lerp(Color.white, new Color(1f, 0.88f, 0.68f), flick * 0.25f);
                poolMap.color = A(tc, fade * Mathf.Lerp(0.85f, 1f, flick));
            }
            else
            {
                if (customPool)
                {
                    Color pc = Color.Lerp(Color.white, new Color(1f, 0.86f, 0.62f), flick * 0.3f);
                    pool.color = A(pc, fade * flick);
                }
                else
                {
                    Color pc = Color.Lerp(fireColor, new Color(1f, 0.85f, 0.4f, 1f), 0.25f * flick);
                    pool.color = A(pc, fireColor.a * fade * flick);
                }
                pool.transform.localScale = Vector3.one * poolScale
                    * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(l * 6f))
                    * Mathf.Lerp(0.94f, 1f, flick);
            }

            if (Random.value < dt * 14f)
                parts.Emit(ground + Random.insideUnitCircle * meteorRadius * 0.7f,
                           new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.7f, 1.6f)),
                           Random.Range(0.1f, 0.2f), Random.Range(0.4f, 0.9f), 0.8f);

            parts.Step(dt, trailHot, trailCold);

            if (tick <= 0f && Vector2.Distance(boss.PlayerPos, ground) <= meteorRadius)
            {
                boss.Burn(burnDps, burnTime);
                tick = 0.5f;
            }
            yield return null;
        }

        if (poolMap != null) Drop(poolMap.gameObject);
        Drop(root);
    }

    private IEnumerator Shake()
    {
        if (shaking) yield break;
        Camera cam = Camera.main;
        if (cam == null) yield break;

        shaking = true;
        Vector3 origin = cam.transform.localPosition;
        float t = 0f;
        float st = Mathf.Max(0.05f, shakeTime);
        while (t < st)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / st);
            cam.transform.localPosition = origin + (Vector3)(Random.insideUnitCircle * shakeAmount * k);
            yield return null;
        }
        cam.transform.localPosition = origin;
        shaking = false;
    }

    // ================= АТАКА 2: ЛУЧ =================
    private IEnumerator Breath()
    {
        LineRenderer warn = NewRing(new Color(1f, 0.6f, 0.2f, 0.4f), 0.06f);
        LineRenderer beam = NewRing(new Color(1f, 0.5f, 0.15f, 0.9f), 0.55f);
        LineRenderer core = NewRing(new Color(1f, 0.95f, 0.7f, 0.9f), 0.2f);
        BossFx.SetAlpha(beam, 0f);
        BossFx.SetAlpha(core, 0f);

        for (int i = 0; i < Mathf.Max(1, sweepCount); i++)
        {
            Vector2 o = Origin;
            Vector2 toPlayer = boss.PlayerPos - o;
            float aim = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            float sign = Random.value < 0.5f ? -1f : 1f;
            float from = aim - sign * sweepAngle * 0.5f;
            float to = aim + sign * sweepAngle * 0.5f;

            float t = 0f;
            while (t < beamTelegraph)
            {
                t += Time.deltaTime;
                o = Origin;
                BossFx.SetBeam(warn, o, o + BossFx.Rotate(Vector2.right, from) * beamLength);
                BossFx.SetAlpha(warn, 0.2f + 0.4f * Mathf.Abs(Mathf.Sin(t * 10f)));
                yield return null;
            }
            BossFx.SetAlpha(warn, 0f);
            boss.Sfx(beamSound, 0.35f);

            float s = 0f;
            float tick = 0f;
            while (s < sweepTime)
            {
                s += Time.deltaTime;
                tick -= Time.deltaTime;
                o = Origin;
                float a = Mathf.Lerp(from, to, s / sweepTime);
                Vector2 dir = BossFx.Rotate(Vector2.right, a);
                Vector2 tip = o + dir * beamLength;

                BossFx.SetBeam(beam, o, tip);
                BossFx.SetBeam(core, o, tip);
                BossFx.SetAlpha(beam, 0.85f);
                BossFx.SetAlpha(core, 0.9f);

                Vector2 tp = boss.PlayerPos - o;
                if (tp.magnitude <= beamLength && Vector2.Angle(dir, tp) <= beamHalfAngle && tick <= 0f)
                {
                    tick = beamTickInterval;
                    if (boss.CanDamage())
                    {
                        boss.Hit(beamDamage);
                        boss.Burn(burnDps, burnTime);
                        boss.Boom(boss.PlayerPos, 0.9f, new Color(1f, 0.6f, 0.2f));
                    }
                }
                yield return null;
            }

            BossFx.SetAlpha(beam, 0f);
            BossFx.SetAlpha(core, 0f);
            yield return new WaitForSeconds(beamPause);
        }
    }

    private void SetShadow(SpriteRenderer shadow, float fullScale, float k)
    {
        if (shadow == null) return;
        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        shadow.transform.localScale = Vector3.one * fullScale * Mathf.Lerp(shadowMinScale, 1f, e);
        shadow.color = A(shadowColor, Mathf.Lerp(shadowStartAlpha, shadowMaxAlpha, e));
    }

}