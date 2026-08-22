using UnityEngine;

// 구조물 밖 숲에 적을 흩뿌린다.
// 구조물 내부의 적은 청크 프리팹에 직접 배치돼 있으므로 여기서 다루지 않는다.
//
// 맵 밖(레이아웃의 빈 문자)과 플레이어 근처, 장애물 위는 피한다.
//
// ★ 11 - 늑대는 건물에 살지 않는다.
//   원작에서 늑대는 숲을 돌아다니고, 좀비는 폐허 주변에 몰려 있다.
//   그래서 늑대·좀비는 구조물이 아니라 여기서 뿌린다.
//
// ★ 11 - 밤에는 늘어난다.
//   GameClock.IsNight면 nightMultiplier를 곱한다.
//   원작의 "밤이 위험하다"가 여기서 나온다.
[DefaultExecutionOrder(200)]
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private ZoneGenerator generator;

    // 옛 필드. 하나만 쓰던 시절의 것이라 그대로 둔다.
    // enemyPrefabs가 비어 있을 때만 쓴다.
    [SerializeField] private GameObject enemyPrefab;

    // 이 중 하나를 무작위로 고른다. 늑대 · 좀비를 섞어 넣으면 된다.
    [SerializeField] private GameObject[] enemyPrefabs;

    [SerializeField] private Transform player;

    [SerializeField] private int count = 12;

    [Header("밤")]
    // 밤에 몇 배로 늘릴지. 1이면 낮과 같다.
    [SerializeField] private float nightMultiplier = 1.6f;

    // 플레이어에서 이 거리 안에는 스폰하지 않는다.
    [SerializeField] private float safeRadius = 15f;

    // 담장·나무 위에 겹쳐 나오는 것을 막는다.
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float clearance = 0.5f;

    // 서로 겹쳐 나오지 않게 하는 최소 간격.
    [SerializeField] private float minSpacing = 4f;

    [Header("야생 그룹 테이블")]
    [SerializeField] private WildGroupSpawnTable groupTable;
    [SerializeField, Range(1, 10)] private int groupMin = 4;
    [SerializeField, Range(1, 10)] private int groupMax = 5;
    [SerializeField, Min(5f)] private float groupMinSpacing = 35f;
    [SerializeField, Min(0f)] private float structurePadding = 8f;
    [SerializeField, Min(0.5f)] private float memberSpacing = 1.25f;

    [Header("동작")]
    [SerializeField] private bool spawnOnStart = true;

    private void Start()
    {
        if (spawnOnStart)
        {
            Spawn();
        }
    }

public void Spawn()
    {
        ResolveReferences();

        if (generator == null)
        {
            return;
        }

        ClearPreviousSpawns();

        if (groupTable != null &&
            groupTable.Entries != null &&
            groupTable.Entries.Length > 0)
        {
            SpawnGroups();
            return;
        }

        SpawnIndividualsLegacy();
    }


    private void ResolveReferences()
    {
        if (generator == null)
        {
            generator = FindFirstObjectByType<ZoneGenerator>();
        }

        if (player == null)
        {
            PlayerStats ps = FindFirstObjectByType<PlayerStats>();

            if (ps != null)
            {
                player = ps.transform;
            }
        }
    }

    private void ClearPreviousSpawns()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private void SpawnGroups()
    {
        int low = Mathf.Clamp(Mathf.Min(groupMin, groupMax), 1, 10);
        int high = Mathf.Clamp(Mathf.Max(groupMin, groupMax), low, 10);

        // 밤에도 사용자가 정한 4~5개 범위를 넘기지 않고 상한 쪽으로 치우친다.
        int want = (GameClock.Instance != null && GameClock.Instance.IsNight)
            ? high
            : Random.Range(low, high + 1);

        Vector2[] centers = new Vector2[want];
        int spawnedGroups = 0;
        int attempts = 0;

        while (spawnedGroups < want && attempts < want * 120)
        {
            attempts++;

            Vector2 center;

            if (!TryPickGroupCenter(centers, spawnedGroups, out center))
            {
                continue;
            }

            WildGroupSpawnTable.Entry entry = groupTable.Pick();

            if (entry == null)
            {
                break;
            }

            SpawnGroup(spawnedGroups, center, entry);
            centers[spawnedGroups] = center;
            spawnedGroups++;
        }

        if (spawnedGroups < want)
        {
            Debug.LogWarning(
                "EnemySpawner: 야생 무리 " + spawnedGroups + " / " + want +
                "개만 배치됐다. 구조물 여백이나 무리 간격을 확인할 것.",
                this);
        }
    }

    private bool TryPickGroupCenter(
        Vector2[] centers,
        int used,
        out Vector2 center)
    {
        Vector2 size = generator.GetMapSize();
        Vector2 origin = generator.transform.position;
        Vector2 playerPos = (player != null)
            ? (Vector2)player.position
            : generator.GetCenter();

        center = origin + new Vector2(
            Random.Range(8f, Mathf.Max(8.01f, size.x - 8f)),
            Random.Range(8f, Mathf.Max(8.01f, size.y - 8f)));

        if (!generator.IsInsideMap(center))
        {
            return false;
        }

        if (Vector2.Distance(center, playerPos) < safeRadius)
        {
            return false;
        }

        if (Physics2D.OverlapCircle(center, clearance, obstacleMask) != null)
        {
            return false;
        }

        if (IsNearStructure(center, structurePadding))
        {
            return false;
        }

        return !TooCloseWithSpacing(centers, used, center, groupMinSpacing);
    }

    private void SpawnGroup(
        int groupIndex,
        Vector2 center,
        WildGroupSpawnTable.Entry entry)
    {
        string label = string.IsNullOrEmpty(entry.label)
            ? "Wild"
            : entry.label.Replace(" ", "_");

        GameObject root = new GameObject(
            "WildGroup_" + groupIndex.ToString("00") + "_" + label);
        root.transform.SetParent(transform, false);
        root.transform.position = center;

        int want = Mathf.Clamp(entry.PickMemberCount(), 3, 5);
        Vector2[] placed = new Vector2[want];
        int spawned = 0;
        int attempts = 0;
        Vector2 playerPos = (player != null)
            ? (Vector2)player.position
            : generator.GetCenter();

        while (spawned < want && attempts < want * 100)
        {
            attempts++;

            Vector2 pos = center +
                Random.insideUnitCircle * Mathf.Max(0.5f, entry.scatterRadius);

            if (!generator.IsInsideMap(pos))
            {
                continue;
            }

            if (Vector2.Distance(pos, playerPos) < safeRadius)
            {
                continue;
            }

            if (IsNearStructure(pos, structurePadding))
            {
                continue;
            }

            if (Physics2D.OverlapCircle(pos, clearance, obstacleMask) != null)
            {
                continue;
            }

            if (TooCloseWithSpacing(placed, spawned, pos, memberSpacing))
            {
                continue;
            }

            GameObject prefab = entry.PickPrefab();

            if (prefab == null)
            {
                break;
            }

            GameObject enemy = Instantiate(
                prefab,
                pos,
                Quaternion.identity,
                root.transform);
            enemy.name = prefab.name + "_" + spawned.ToString("00");

            EnemyControllerBase controller =
                enemy.GetComponent<EnemyControllerBase>();

            if (controller != null)
            {
                controller.ConfigureGroupPatrol(
                    center,
                    Mathf.Max(entry.scatterRadius, entry.roamRadius));
            }

            placed[spawned] = pos;
            spawned++;
        }

        if (spawned < want)
        {
            Debug.LogWarning(
                "EnemySpawner: " + root.name + "에 " + spawned + " / " + want +
                "마리만 배치됐다.",
                root);
        }
    }

    private bool IsNearStructure(Vector2 point, float padding)
    {
        if (padding <= 0f || generator == null)
        {
            return false;
        }

        System.Collections.Generic.IList<GameObject> structures =
            generator.PlacedStructures;

        for (int i = 0; i < structures.Count; i++)
        {
            GameObject structure = structures[i];

            if (structure == null || !structure.activeInHierarchy)
            {
                continue;
            }

            Renderer[] renderers =
                structure.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                continue;
            }

            Bounds bounds = renderers[0].bounds;

            for (int r = 1; r < renderers.Length; r++)
            {
                bounds.Encapsulate(renderers[r].bounds);
            }

            bounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));

            if (bounds.Contains(new Vector3(point.x, point.y, bounds.center.z)))
            {
                return true;
            }
        }

        return false;
    }

    private bool TooCloseWithSpacing(
        Vector2[] placed,
        int used,
        Vector2 pos,
        float spacing)
    {
        if (spacing <= 0f)
        {
            return false;
        }

        for (int i = 0; i < used; i++)
        {
            if (Vector2.Distance(placed[i], pos) < spacing)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnIndividualsLegacy()
    {
        if ((enemyPrefabs == null || enemyPrefabs.Length == 0) &&
            enemyPrefab == null)
        {
            return;
        }

        Vector2 size = generator.GetMapSize();
        Vector2 origin = generator.transform.position;
        Vector2 playerPos = (player != null)
            ? (Vector2)player.position
            : generator.GetCenter();

        int want = count;

        if (GameClock.Instance != null && GameClock.Instance.IsNight)
        {
            want = Mathf.RoundToInt(count * Mathf.Max(1f, nightMultiplier));
        }

        Vector2[] placed = new Vector2[want];
        int spawned = 0;
        int attempts = 0;

        while (spawned < want && attempts < want * 60)
        {
            attempts++;

            Vector2 pos = origin + new Vector2(
                Random.Range(1f, size.x - 1f),
                Random.Range(1f, size.y - 1f));

            if (!generator.IsInsideMap(pos) ||
                Vector2.Distance(pos, playerPos) < safeRadius ||
                Physics2D.OverlapCircle(pos, clearance, obstacleMask) != null ||
                TooClose(placed, spawned, pos))
            {
                continue;
            }

            GameObject prefab = Pick();

            if (prefab == null)
            {
                break;
            }

            Instantiate(prefab, pos, Quaternion.identity, transform);
            placed[spawned] = pos;
            spawned++;
        }

        if (spawned < want)
        {
            Debug.LogWarning(
                "EnemySpawner: " + spawned + " / " + want +
                "만 배치됐다. 맵이 좁거나 장애물이 많다.",
                this);
        }
    }

    private bool TooClose(Vector2[] placed, int used, Vector2 pos)
    {
        if (minSpacing <= 0f)
        {
            return false;
        }

        for (int i = 0; i < used; i++)
        {
            if (Vector2.Distance(placed[i], pos) < minSpacing)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject Pick()
    {
        if (enemyPrefabs != null && enemyPrefabs.Length > 0)
        {
            // null이 섞여 있어도 몇 번 더 뽑아 본다.
            for (int i = 0; i < 4; i++)
            {
                GameObject g = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

                if (g != null)
                {
                    return g;
                }
            }
        }

        return enemyPrefab;
    }
}
