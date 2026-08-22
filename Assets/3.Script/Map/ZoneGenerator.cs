using System.Collections.Generic;
using UnityEngine;

// 숲(존) 맵 생성기.
//
// 두 층으로 나눠 생성한다.
//
//   1. 지면(Ground)  — 10x10 타일 청크를 격자에 깐다. 레이아웃 문자 격자로 모양을 정한다.
//   2. 구조물(Structure) — 지정한 타일 좌표에 건물·야영지 청크를 올린다.
//      크기가 10x10이든 15x15든 상관없다. 격자에 스냅하지 않는다.
//
// 지면만 격자로 관리하고 구조물은 자유 좌표로 두는 이유는,
// 원작 청크가 10x10과 15x15로 섞여 있어 하나의 격자에 안 맞기 때문이다.
// 구조물은 지면 위에 얹히는 별개 레이어라 격자를 공유할 필요가 없다.
//
// PPU 16, 타일 16px → **타일 1개 = 1 유닛**. 슬롯 좌표는 곧 유닛 좌표다.
// 청크 스프라이트의 Pivot은 반드시 Bottom-Left.
[DefaultExecutionOrder(-100)]
public class ZoneGenerator : MonoBehaviour
{
    // 레이아웃 문자 하나에 대응하는 지면 청크 종류.
    [System.Serializable]
    public class GroundEntry
    {
        [Tooltip("레이아웃에서 이 청크를 나타내는 문자. 첫 글자만 쓴다.")]
        public string symbol = ".";

        [Tooltip("메모용 이름. 생성된 오브젝트 이름에 쓰인다.")]
        public string label = "Forest";

        [Tooltip("이 중 하나를 랜덤으로 고른다.")]
        public GameObject[] variants;

        public char Symbol
        {
            get { return string.IsNullOrEmpty(symbol) ? '\0' : symbol[0]; }
        }
    }

    // 구조물 종류 묶음. 슬롯이 이름으로 참조한다.
    [System.Serializable]
    public class StructureSet
    {
        [Tooltip("슬롯에서 이 묶음을 참조할 이름. 예: bandit, garage")]
        public string setName = "bandit";

        [Tooltip("이 중 하나를 랜덤으로 고른다.")]
        public GameObject[] variants;
    }

    // 구조물이 놓일 자리 하나.
    [System.Serializable]
    public class StructureSlot
    {
        [Tooltip("메모용. 어느 자리인지 알아보기 위한 이름")]
        public string label = "";

        [Tooltip("좌하단 좌표. 타일 = 유닛 단위, 맵 원점 기준")]
        public Vector2Int position;

        [Tooltip("여기에 놓을 구조물 묶음 이름")]
        public string setName = "bandit";

        [Tooltip("이 자리에 실제로 놓일 확률. 1이면 항상")]
        [Range(0f, 1f)]
        public float chance = 1f;
    }

    [Header("지면 레이아웃")]
    // TextAsset(.txt)이 있으면 그쪽을 우선 사용한다.
    [SerializeField] private TextAsset layoutAsset;

    [TextArea(8, 30)]
    [SerializeField] private string layoutInline = "";

    [SerializeField] private GroundEntry[] groundPalette;

    // 지면 셀 한 변의 크기(유닛). 10x10 타일 청크 = 10
    [SerializeField] private int cellSize = 10;

    [Tooltip("맵 밖으로 취급할 문자들. 경고를 내지 않는다.")]
    [SerializeField] private string voidSymbols = " _";

    [Header("구조물")]
    [SerializeField] private StructureSet[] structureSets;
    [SerializeField] private StructureSlot[] slots;

    [Header("난수")]
    [SerializeField] private int seed = 0;
    [SerializeField] private bool useRandomSeed = false;

    [Header("동작")]
    [SerializeField] private bool generateOnStart = true;

    private Transform groundRoot;
    private Transform structureRoot;

    // [x, y] — y = 0이 맵의 맨 아래.
    private char[,] grid;
    private int gridWidth;
    private int gridHeight;

    // 배치된 구조물 목록. 적 스폰 시 "구조물 안"을 피하거나 노리는 데 쓴다.
    private readonly List<GameObject> placedStructures = new List<GameObject>();

    public int GridWidth
    {
        get { EnsureParsed(); return gridWidth; }
    }

    public int GridHeight
    {
        get { EnsureParsed(); return gridHeight; }
    }

    public int CellSize
    {
        get { return cellSize; }
    }

    public IList<GameObject> PlacedStructures
    {
        get { return placedStructures; }
    }

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    // ───────────────────────── 생성 ─────────────────────────

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!ParseLayout())
        {
            return;
        }

        Clear();

        if (useRandomSeed)
        {
            seed = System.Environment.TickCount;
        }
        Random.InitState(seed);

        groundRoot = MakeRoot("Ground");
        structureRoot = MakeRoot("Structures");

        PlaceGround();
        PlaceStructures();
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        placedStructures.Clear();

        DestroyRoot(ref groundRoot, "Ground");
        DestroyRoot(ref structureRoot, "Structures");
    }

    private Transform MakeRoot(string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(transform, false);
        return t;
    }

private void DestroyRoot(ref Transform target, string name)
    {
        if (target == null)
        {
            // 에디터에서 재생성할 때 이전 결과를 찾아 지운다.
            target = transform.Find(name);
        }

        if (target == null)
        {
            return;
        }

        // 플레이 모드의 Destroy는 프레임 끝까지 지연된다.
        // 저장돼 있던 생성 결과와 새 결과가 한 프레임 겹치지 않도록
        // 렌더러와 콜라이더를 포함한 이전 루트를 먼저 즉시 비활성화한다.
        target.gameObject.SetActive(false);

        if (Application.isPlaying)
        {
            Destroy(target.gameObject);
        }
        else
        {
            DestroyImmediate(target.gameObject);
        }

        target = null;
    }

    private void PlaceGround()
    {
        if (groundPalette == null || groundPalette.Length == 0)
        {
            Debug.LogWarning("ZoneGenerator: 지면 팔레트가 비어 있습니다.", this);
            return;
        }

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                char c = grid[x, y];

                if (IsVoid(c))
                {
                    continue;
                }

                GroundEntry entry = FindGround(c);

                if (entry == null)
                {
                    Debug.LogWarning("ZoneGenerator: 팔레트에 없는 문자 '" + c +
                                     "' (" + x + ", " + y + ")", this);
                    continue;
                }

                GameObject prefab = Pick(entry.variants, entry.label);

                if (prefab == null)
                {
                    continue;
                }

                Vector3 pos = transform.position +
                              new Vector3(x * cellSize, y * cellSize, 0f);

                GameObject go = Instantiate(prefab, pos, Quaternion.identity, groundRoot);
                go.name = entry.label + "_" + x + "_" + y;
            }
        }
    }

    private void PlaceStructures()
    {
        if (slots == null || slots.Length == 0)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            StructureSlot slot = slots[i];

            if (slot == null)
            {
                continue;
            }

            if (slot.chance < 1f && Random.value > slot.chance)
            {
                continue;
            }

            StructureSet set = FindSet(slot.setName);

            if (set == null)
            {
                Debug.LogWarning("ZoneGenerator: 구조물 묶음 '" + slot.setName +
                                 "'을 찾을 수 없습니다. (슬롯 " + i + ")", this);
                continue;
            }

            GameObject prefab = Pick(set.variants, set.setName);

            if (prefab == null)
            {
                continue;
            }

            // 슬롯 좌표는 타일 단위이고 타일 1개 = 1 유닛이므로 그대로 쓴다.
            Vector3 pos = transform.position +
                          new Vector3(slot.position.x, slot.position.y, 0f);

            GameObject go = Instantiate(prefab, pos, Quaternion.identity, structureRoot);
            go.name = (string.IsNullOrEmpty(slot.label) ? set.setName : slot.label) +
                      "_" + slot.position.x + "_" + slot.position.y;

            placedStructures.Add(go);

            // spawnOnStart를 끈 프리팹은 여기서 직접 부른다.
            StructureSpawner spawner = go.GetComponent<StructureSpawner>();

            if (spawner != null)
            {
                spawner.Spawn();
            }
        }
    }

    private GameObject Pick(GameObject[] variants, string label)
    {
        if (variants == null || variants.Length == 0)
        {
            Debug.LogWarning("ZoneGenerator: '" + label + "'에 프리팹이 없습니다.", this);
            return null;
        }

        return variants[Random.Range(0, variants.Length)];
    }

    private GroundEntry FindGround(char c)
    {
        for (int i = 0; i < groundPalette.Length; i++)
        {
            if (groundPalette[i] != null && groundPalette[i].Symbol == c)
            {
                return groundPalette[i];
            }
        }

        return null;
    }

    private StructureSet FindSet(string name)
    {
        if (structureSets == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        for (int i = 0; i < structureSets.Length; i++)
        {
            if (structureSets[i] != null && structureSets[i].setName == name)
            {
                return structureSets[i];
            }
        }

        return null;
    }

    private bool IsVoid(char c)
    {
        if (c == '\0')
        {
            return true;
        }

        return !string.IsNullOrEmpty(voidSymbols) && voidSymbols.IndexOf(c) >= 0;
    }

    // ───────────────────────── 파싱 ─────────────────────────

    private void EnsureParsed()
    {
        if (grid == null)
        {
            ParseLayout();
        }
    }

    private bool ParseLayout()
    {
        string text = (layoutAsset != null && !string.IsNullOrEmpty(layoutAsset.text))
            ? layoutAsset.text
            : layoutInline;

        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("ZoneGenerator: 레이아웃이 비어 있습니다.", this);
            return false;
        }

        string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        List<string> rows = new List<string>();

        for (int i = 0; i < raw.Length; i++)
        {
            rows.Add(raw[i].TrimEnd());
        }

        // 앞뒤 빈 줄 제거.
        while (rows.Count > 0 && rows[0].Length == 0)
        {
            rows.RemoveAt(0);
        }
        while (rows.Count > 0 && rows[rows.Count - 1].Length == 0)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        if (rows.Count == 0)
        {
            Debug.LogWarning("ZoneGenerator: 레이아웃에 유효한 줄이 없습니다.", this);
            return false;
        }

        gridHeight = rows.Count;
        gridWidth = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            gridWidth = Mathf.Max(gridWidth, rows[i].Length);
        }

        grid = new char[gridWidth, gridHeight];

        for (int i = 0; i < rows.Count; i++)
        {
            // 첫 줄이 맵의 맨 위이므로 뒤집어 담는다.
            int y = gridHeight - 1 - i;
            string row = rows[i];

            for (int x = 0; x < gridWidth; x++)
            {
                grid[x, y] = (x < row.Length) ? row[x] : ' ';
            }
        }

        return true;
    }

    // ───────────────────────── 조회 ─────────────────────────

    // 맵 전체 크기(유닛).
    public Vector2 GetMapSize()
    {
        EnsureParsed();
        return new Vector2(gridWidth * cellSize, gridHeight * cellSize);
    }

    public Vector2 GetCenter()
    {
        return (Vector2)transform.position + GetMapSize() * 0.5f;
    }

    public char GetSymbol(int x, int y)
    {
        EnsureParsed();

        if (grid == null || x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
        {
            return ' ';
        }

        return grid[x, y];
    }

    public Vector2Int WorldToCell(Vector2 world)
    {
        Vector2 local = world - (Vector2)transform.position;
        return new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.y / cellSize));
    }

    public Vector2 CellCenter(int x, int y)
    {
        return (Vector2)transform.position +
               new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
    }

    // 이 좌표가 맵 안(지면이 깔린 곳)인지. 적 스폰 판정에 쓴다.
    public bool IsInsideMap(Vector2 world)
    {
        Vector2Int cell = WorldToCell(world);
        return !IsVoid(GetSymbol(cell.x, cell.y));
    }

    public List<Vector2Int> FindCells(char symbol)
    {
        EnsureParsed();

        List<Vector2Int> result = new List<Vector2Int>();

        if (grid == null)
        {
            return result;
        }

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (grid[x, y] == symbol)
                {
                    result.Add(new Vector2Int(x, y));
                }
            }
        }

        return result;
    }

    // 해당 문자의 셀 중 하나를 골라 중앙 좌표를 돌려준다. 없으면 맵 중앙.
    public Vector2 GetSpawnPoint(char symbol)
    {
        List<Vector2Int> cells = FindCells(symbol);

        if (cells.Count == 0)
        {
            return GetCenter();
        }

        Vector2Int cell = cells[Random.Range(0, cells.Count)];
        return CellCenter(cell.x, cell.y);
    }

    // ───────────────────────── 기즈모 ─────────────────────────

    private void OnDrawGizmosSelected()
    {
        EnsureParsed();

        if (grid == null)
        {
            return;
        }

        Vector3 origin = transform.position;
        Vector2 size = GetMapSize();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(origin + (Vector3)size * 0.5f, size);

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                char c = grid[x, y];

                if (IsVoid(c))
                {
                    continue;
                }

                // 팔레트에 있는 문자는 노랑, 없는 문자는 빨강.
                bool known = groundPalette != null && FindGround(c) != null;
                Gizmos.color = known
                    ? new Color(1f, 1f, 0f, 0.3f)
                    : new Color(1f, 0f, 0f, 0.6f);

                Gizmos.DrawWireCube(
                    origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f),
                    new Vector3(cellSize, cellSize, 0f));
            }
        }

        // 구조물 슬롯 위치를 표시한다. 크기는 프리팹마다 달라 점으로만 찍는다.
        if (slots != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                Vector3 p = origin + new Vector3(slots[i].position.x, slots[i].position.y, 0f);
                Gizmos.DrawWireSphere(p, 1f);
                Gizmos.DrawLine(p, p + new Vector3(2f, 2f, 0f));
            }
        }
    }
}