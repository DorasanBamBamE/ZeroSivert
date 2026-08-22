using System.Collections.Generic;
using UnityEngine;

// ������ �������� ��Ʈ�� ���δ�.
// �ڽ����� ���� SpawnPoint ��Ŀ �� �Ϻθ� ��� ���� ������ ��ġ�Ѵ�.
//
// �Ź� ���� �������� �ʴ� �� �ٽ��̴�. ��Ŀ�� 10�� �ΰ� 3~5���� ����
// ���� �������̶� �� ������ �ٸ��� ��������.
public class StructureSpawner : MonoBehaviour
{
    [Header("��")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private int enemyMin = 2;
    [SerializeField] private int enemyMax = 4;

    [Header("����")]
    [SerializeField] private GameObject[] lootPrefabs;
    [SerializeField] private int lootMin = 1;
    [SerializeField] private int lootMax = 3;

    [Header("����")]
    // ZoneGenerator�� ��ġ ���� Spawn()�� �θ��� ������� �����Ѵ�.
    [SerializeField] private bool spawnOnStart = true;

    // ������ �͵��� ��� �ڽ�. ������ �ڱ� �ڽſ� ���δ�.
    

    [Header("스폰 안전")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField, Min(0f)] private float spawnClearance = 0.55f;
    [SerializeField] private bool rejectStructureOverlap = true;
[SerializeField] private Transform container;

    private bool spawned;

    private void Start()
    {
        if (spawnOnStart)
        {
            Spawn();
        }
    }

    // �� �� �ҷ��� �ߺ� �������� �ʴ´�.
    public void Spawn()
    {
        if (spawned)
        {
            return;
        }

        spawned = true;

        List<SpawnPoint> enemyPoints = new List<SpawnPoint>();
        List<SpawnPoint> lootPoints = new List<SpawnPoint>();

        // ��Ȱ�� �ڽĵ� �����ؼ� ã�´�.
        SpawnPoint[] points = GetComponentsInChildren<SpawnPoint>(true);

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].Type == SpawnPoint.PointType.Enemy)
            {
                enemyPoints.Add(points[i]);
            }
            else
            {
                lootPoints.Add(points[i]);
            }
        }

        SpawnAt(enemyPoints, enemyPrefabs, enemyMin, enemyMax);
        SpawnAt(lootPoints, lootPrefabs, lootMin, lootMax);
    }

private void SpawnAt(List<SpawnPoint> points, GameObject[] defaults, int min, int max)
    {
        if (points.Count == 0)
        {
            return;
        }

        Shuffle(points);

        int want = Random.Range(Mathf.Min(min, max), Mathf.Max(min, max) + 1);
        int spawnedCount = 0;
        Transform parent = (container != null) ? container : transform;

        for (int i = 0; i < points.Count && spawnedCount < want; i++)
        {
            SpawnPoint point = points[i];

            if (point == null || !IsSpawnPointSafe(point.transform.position))
            {
                continue;
            }

            GameObject prefab = point.PickOverride();

            if (prefab == null)
            {
                if (defaults == null || defaults.Length == 0)
                {
                    continue;
                }

                prefab = defaults[Random.Range(0, defaults.Length)];
            }

            if (prefab == null)
            {
                continue;
            }

            Instantiate(prefab, point.transform.position, Quaternion.identity, parent);
            spawnedCount++;
        }

        if (spawnedCount < want)
        {
            Debug.LogWarning(name + ": 안전한 스폰 지점이 부족합니다. " +
                             spawnedCount + " / " + want, this);
        }
    }

    private static void Shuffle(List<SpawnPoint> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            SpawnPoint tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }


private bool IsSpawnPointSafe(Vector2 position)
    {
        if (rejectStructureOverlap)
        {
            BoxCollider2D[] localBoxes = GetComponentsInChildren<BoxCollider2D>(true);

            for (int i = 0; i < localBoxes.Length; i++)
            {
                BoxCollider2D box = localBoxes[i];

                if (box == null || !box.enabled || box.isTrigger)
                {
                    continue;
                }

                Vector2 local = (Vector2)box.transform.InverseTransformPoint(position) - box.offset;
                float dx = Mathf.Max(Mathf.Abs(local.x) - box.size.x * 0.5f, 0f);
                float dy = Mathf.Max(Mathf.Abs(local.y) - box.size.y * 0.5f, 0f);

                if (Mathf.Sqrt(dx * dx + dy * dy) < spawnClearance)
                {
                    return false;
                }
            }
        }

        if (obstacleMask.value != 0)
        {
            Collider2D[] externalHits =
                Physics2D.OverlapCircleAll(position, spawnClearance, obstacleMask);

            for (int i = 0; i < externalHits.Length; i++)
            {
                Collider2D hit = externalHits[i];

                if (hit != null && !hit.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
        }

        return true;
    }
}