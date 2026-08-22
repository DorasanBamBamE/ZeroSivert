using UnityEngine;

// 구조물 프리팹 안에 놓는 스폰 지점 마커.
// 원작 레이아웃 룸의 흰 마커 / 초록 마커에 대응한다.
//
// 빈 오브젝트에 이것만 붙여 구조물 프리팹의 자식으로 둔다.
// 실제 스폰은 StructureSpawner가 이 마커들 중 일부를 골라서 처리한다.
public class SpawnPoint : MonoBehaviour
{
    public enum PointType
    {
        Enemy,
        Loot,
    }

    [SerializeField] private PointType type = PointType.Enemy;

    // 이 지점에서만 나오게 하고 싶은 것이 있으면 지정한다.
    // 비워두면 StructureSpawner의 기본 목록에서 고른다.
    [SerializeField] private GameObject[] overridePrefabs;

    public PointType Type
    {
        get { return type; }
    }

    public GameObject PickOverride()
    {
        if (overridePrefabs == null || overridePrefabs.Length == 0)
        {
            return null;
        }

        return overridePrefabs[Random.Range(0, overridePrefabs.Length)];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = (type == PointType.Enemy)
            ? new Color(1f, 0.3f, 0.3f, 0.8f)
            : new Color(0.3f, 0.8f, 1f, 0.8f);

        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawLine(transform.position + Vector3.left * 0.2f,
                        transform.position + Vector3.right * 0.2f);
    }
}