using System.Collections.Generic;
using UnityEngine;

// 존 전역에 나무와 풀을 흩뿌린다.
//
// 원작의 숲은 지면 타일만으로 만들어지지 않는다. 나무가 시야를 끊고
// 풀이 바닥을 덮어야 "숲"으로 보인다. 그게 없으면 격자 무늬 벌판이 된다.
//
// ★ 지면 청크에 그려 넣지 않고 오브젝트로 두는 이유
//   1. 나무는 시야와 이동을 막아야 한다 - 콜라이더가 필요하다
//   2. 같은 청크가 900번 깔려도 나무 배치는 매번 달라야 한다
//   3. Y 정렬이 필요하다 - 플레이어가 나무 뒤로 가면 가려져야 한다
//
// ★ 밀도를 2배로 올리면서 두 가지를 바꿨다.
//   1. 간격 검사를 격자 해시로 바꿨다.
//      전부와 거리를 재던 방식은 2만 개에서 수십억 번 계산이 된다.
//      해시는 주변 9칸만 보므로 개수가 늘어도 거의 그대로다.
//   2. blockSize 유닛짜리 블록 부모로 묶는다.
//      PropBlockCuller가 화면 근처 블록만 켜 둔다 - 실제로 살아 있는
//      프롭은 전체의 10% 남짓이 된다.
//
// ZoneGenerator보다 늦게 돌아야 한다.
[DefaultExecutionOrder(100)]
public class PropScatter : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public string label = "나무";

        [Tooltip("이 중 하나를 무작위로 고른다.")]
        public GameObject[] prefabs;

        [Tooltip("각 프리팹을 한 순환씩 고르게 섞어 사용한다. 색 변형을 골고루 깔 때 켠다.")]
        public bool distributeVariantsEvenly = false;

        [Tooltip("100x100 유닛당 개수. 나무 40, 풀 300 정도가 원작 감각이다.")]
        public float densityPer10000 = 40f;

        [Tooltip("서로 이만큼은 떨어뜨린다(유닛). 0이면 검사하지 않는다.")]
        public float minSpacing = 2.5f;

        [Tooltip("구조물 경계에서 이만큼 떨어뜨린다.")]
        public float structurePadding = 1.5f;

        [Tooltip("도로 칸에도 깔지. 풀은 켜고 나무는 끈다.")]
        public bool allowOnRoad = false;

        [Range(0.5f, 1.5f)] public float scaleMin = 1f;
        [Range(0.5f, 1.5f)] public float scaleMax = 1f;

        [Tooltip("지면 -10 · 구조물 -8 · 풀 0 · 나무 1. Y 정렬은 Sort Axis가 한다.")]
        public int sortingOrder = 1;
    }

    [SerializeField] private ZoneGenerator zone;
    [SerializeField] private Layer[] layers;

    [Header("도로 판정")]
    // 이 문자들이 찍힌 칸에만 깐다. '.'와 빌리지 외곽의 숲 문자에 배치한다.
    // C는 북쪽 진입로이므로 제외한다.
    [SerializeField] private string groundSymbols = ".ABDEFGHIJKLMNOP";

    [Header("입구 보호")]
    // 진입·탈출 지점 주변은 비워 둔다. 나무에 갇히면 시작하자마자 막힌다.
    [SerializeField] private float clearRadius = 8f;
    [SerializeField] private char[] clearSymbols = new char[] { 'S', 'X' };

    [Header("동작")]
    [SerializeField] private bool scatterOnStart = true;
    [SerializeField] private int seed = 0;
    [SerializeField] private bool useRandomSeed = true;

    // 정렬용.
    //
    // ★ sortingOrder에 -y를 넣으면 안 된다.
    //   지면 청크가 -10에 있어서 y가 5만 넘어도 지면 밑으로 깔려 안 보인다.
    //   Y 정렬은 Graphics의 Transparency Sort Axis(0,1,0)에 맡기고,
    //   여기서는 "어느 층에 속하는가"만 정한다.
    //
    //   지면 -10 · 마을 외곽 -9 · 구조물 -8 · 풀 0 · 나무/플레이어/적 1 · 총 2
    [SerializeField] private bool sortByY = true;

    // 옛 필드. Layer.sortingOrder가 0일 때의 기본값으로만 쓴다.
    [SerializeField] private int propSortingOrder = 1;

    [Header("부하 줄이기")]
    // 이 크기의 정사각형으로 묶는다. 화면(30x17)보다 조금 큰 값이 좋다.
    [SerializeField] private float blockSize = 20f;

    // 블록 단위로 켜고 끈다.
    [SerializeField] private bool useBlockCulling = true;

    // 화면 밖으로 이만큼 더 켜 둔다. 나무 콜라이더 때문에 넉넉해야 한다.
    [SerializeField] private float cullMargin = 18f;

    private Transform root;

    // ── 간격 검사용 격자 해시 ──
    private Dictionary<long, List<Vector2>> hash;
    private float hashCell = 1f;

    private void Start()
    {
        if (scatterOnStart)
        {
            Scatter();
        }
    }

    [ContextMenu("Scatter")]
    public void Scatter()
    {
        if (zone == null)
        {
            zone = FindFirstObjectByType<ZoneGenerator>();
        }

        if (zone == null || layers == null || layers.Length == 0)
        {
            return;
        }

        Clear();

        if (useRandomSeed)
        {
            seed = System.Environment.TickCount;
        }

        Random.State saved = Random.state;
        Random.InitState(seed);

        root = new GameObject("Props").transform;
        root.SetParent(transform, false);

        Vector2 size = zone.GetMapSize();
        Vector2 origin = zone.transform.position;

        // 구조물이 차지한 사각형을 미리 모아 둔다. 매번 다시 계산하면 느리다.
        List<Bounds> blocked = CollectStructureBounds();
        List<Vector2> clear = CollectClearPoints();

        // ── 블록 부모 만들기 ──
        float bs = Mathf.Max(5f, blockSize);
        int bw = Mathf.Max(1, Mathf.CeilToInt(size.x / bs));
        int bh = Mathf.Max(1, Mathf.CeilToInt(size.y / bs));

        Transform[] blocks = new Transform[bw * bh];
        Vector2[] centers = new Vector2[bw * bh];

        for (int by = 0; by < bh; by++)
        {
            for (int bx = 0; bx < bw; bx++)
            {
                int idx = by * bw + bx;

                GameObject b = new GameObject("B_" + bx + "_" + by);
                b.transform.SetParent(root, false);

                blocks[idx] = b.transform;
                centers[idx] = origin + new Vector2((bx + 0.5f) * bs, (by + 0.5f) * bs);
            }
        }

        int total = 0;

        for (int li = 0; li < layers.Length; li++)
        {
            Layer layer = layers[li];

            if (layer == null || layer.prefabs == null || layer.prefabs.Length == 0)
            {
                continue;
            }

            int want = Mathf.RoundToInt(size.x * size.y / 10000f * layer.densityPer10000);

            HashReset(layer.minSpacing);

            List<GameObject> variantBag = null;

            int attempts = 0;
            int made = 0;
            int limit = Mathf.Max(1000, want * 12);

            while (made < want && attempts < limit)
            {
                attempts++;

                Vector2 p = origin + new Vector2(
                    Random.Range(1f, size.x - 1f),
                    Random.Range(1f, size.y - 1f));

                if (!IsOk(p, layer, blocked, clear))
                {
                    continue;
                }

                GameObject prefab;

                if (layer.distributeVariantsEvenly)
                {
                    // 한 바퀴에 모든 변형을 한 번씩 쓴다. 위치와 순서는 무작위라
                    // 자연스러움은 유지하면서도 특정 색 변형만 몰리는 일을 막는다.
                    if (variantBag == null || variantBag.Count == 0)
                    {
                        variantBag = new List<GameObject>(layer.prefabs);
                    }

                    int pick = Random.Range(0, variantBag.Count);
                    prefab = variantBag[pick];
                    variantBag.RemoveAt(pick);
                }
                else
                {
                    prefab = layer.prefabs[Random.Range(0, layer.prefabs.Length)];
                }

                if (prefab == null)
                {
                    continue;
                }

                int bx2 = Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / bs), 0, bw - 1);
                int by2 = Mathf.Clamp(Mathf.FloorToInt((p.y - origin.y) / bs), 0, bh - 1);
                Transform parent = blocks[by2 * bw + bx2];

                GameObject go = Instantiate(prefab, p, Quaternion.identity, parent);

                float s = 1f;

                if (layer.scaleMax > layer.scaleMin)
                {
                    s = Random.Range(layer.scaleMin, layer.scaleMax);
                }

                // 좌우 반전으로 같은 나무가 반복되는 티를 줄인다.
                float sx = (Random.value < 0.5f) ? -s : s;
                go.transform.localScale = new Vector3(sx, s, 1f);

                if (sortByY)
                {
                    int order = (layer.sortingOrder != 0) ? layer.sortingOrder : propSortingOrder;
                    SpriteRenderer[] rs = go.GetComponentsInChildren<SpriteRenderer>(true);

                    for (int r = 0; r < rs.Length; r++)
                    {
                        rs[r].sortingOrder = order;
                    }
                }

                HAdd(p);
                made++;
            }

            total += made;
        }

        Random.state = saved;

        if (useBlockCulling)
        {
            PropBlockCuller c = root.GetComponent<PropBlockCuller>();

            if (c == null)
            {
                c = root.gameObject.AddComponent<PropBlockCuller>();
            }

            c.Init(blocks, centers, bs, cullMargin);
        }

        Debug.Log("[PropScatter] " + total + "개 배치 · 블록 " + blocks.Length +
                  (useBlockCulling ? " (거리 컬링 켬)" : " (컬링 끔)"), this);
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        if (root == null)
        {
            root = transform.Find("Props");
        }

        if (root == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(root.gameObject);
        }
        else
        {
            DestroyImmediate(root.gameObject);
        }

        root = null;
    }

    // ───────────────── 격자 해시 ─────────────────
    //
    // 칸 크기를 minSpacing으로 잡으면 "minSpacing보다 가까운 점"은
    // 반드시 주변 9칸 안에 있다. 그래서 9칸만 뒤지면 된다.

    private void HashReset(float cell)
    {
        hashCell = Mathf.Max(0.25f, cell);
        hash = new Dictionary<long, List<Vector2>>(4096);
    }

    private static long HKey(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
    }

    private void HAdd(Vector2 p)
    {
        long k = HKey(Mathf.FloorToInt(p.x / hashCell), Mathf.FloorToInt(p.y / hashCell));

        List<Vector2> l;

        if (!hash.TryGetValue(k, out l))
        {
            l = new List<Vector2>(4);
            hash[k] = l;
        }

        l.Add(p);
    }

    private bool HasNear(Vector2 p, float dist)
    {
        int cx = Mathf.FloorToInt(p.x / hashCell);
        int cy = Mathf.FloorToInt(p.y / hashCell);
        float sq = dist * dist;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                List<Vector2> l;

                if (!hash.TryGetValue(HKey(cx + dx, cy + dy), out l))
                {
                    continue;
                }

                for (int i = 0; i < l.Count; i++)
                {
                    if ((l[i] - p).sqrMagnitude < sq)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // ───────────────── 판정 ─────────────────

    private bool IsOk(Vector2 p, Layer layer, List<Bounds> blocked, List<Vector2> clear)
    {
        if (!zone.IsInsideMap(p))
        {
            return false;
        }

        if (!layer.allowOnRoad)
        {
            Vector2Int cell = zone.WorldToCell(p);
            char c = zone.GetSymbol(cell.x, cell.y);

            if (string.IsNullOrEmpty(groundSymbols) || groundSymbols.IndexOf(c) < 0)
            {
                return false;
            }
        }

        for (int i = 0; i < clear.Count; i++)
        {
            if ((clear[i] - p).sqrMagnitude < clearRadius * clearRadius)
            {
                return false;
            }
        }

        for (int i = 0; i < blocked.Count; i++)
        {
            Bounds b = blocked[i];
            b.Expand(layer.structurePadding * 2f);

            if (b.Contains(new Vector3(p.x, p.y, b.center.z)))
            {
                return false;
            }
        }

        if (layer.minSpacing > 0f && HasNear(p, layer.minSpacing))
        {
            return false;
        }

        return true;
    }

private List<Bounds> CollectStructureBounds()
    {
        List<Bounds> list = new List<Bounds>();
        IList<GameObject> structures = zone.PlacedStructures;

        for (int i = 0; i < structures.Count; i++)
        {
            GameObject structure = structures[i];

            if (structure == null)
            {
                continue;
            }

            SpriteRenderer[] renderers = structure.GetComponentsInChildren<SpriteRenderer>(true);
            Bounds combined = new Bounds();
            bool hasBounds = false;

            for (int k = 0; k < renderers.Length; k++)
            {
                SpriteRenderer sr = renderers[k];

                // 적·루팅처럼 구조물 위에 생성된 오브젝트는 제외한다.
                if (sr == null || sr.sprite == null || sr.sortingOrder > -7)
                {
                    continue;
                }

                // 북쪽 길은 빌리지 바깥 숲까지 이어지는 통로다.
                // 이 렌더러 때문에 외곽 숲 전체가 구조물 영역으로 묶이지 않게 한다.
                bool isRoadConnector = false;
                Transform cursor = sr.transform;

                while (cursor != null && cursor != structure.transform)
                {
                    if (cursor.name.StartsWith("RoadConnector_"))
                    {
                        isRoadConnector = true;
                        break;
                    }

                    cursor = cursor.parent;
                }

                if (isRoadConnector)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = sr.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(sr.bounds);
                }
            }

            if (hasBounds)
            {
                list.Add(combined);
            }
        }

        return list;
    }

    private List<Vector2> CollectClearPoints()
    {
        List<Vector2> list = new List<Vector2>();

        if (clearSymbols == null)
        {
            return list;
        }

        for (int i = 0; i < clearSymbols.Length; i++)
        {
            List<Vector2Int> cells = zone.FindCells(clearSymbols[i]);

            for (int k = 0; k < cells.Count; k++)
            {
                list.Add(zone.CellCenter(cells[k].x, cells[k].y));
            }
        }

        return list;
    }


private void Awake()
    {
        // 씬에 저장된 편집용 프롭은 새 존 구조물이 스폰되기 전에
        // 콜라이더부터 즉시 꺼 중복·스폰 방해를 막는다.
        Transform existing = transform.Find("Props");

        if (existing != null)
        {
            existing.gameObject.SetActive(false);
            root = existing;
        }
    }
}
