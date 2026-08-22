using UnityEngine;

// 존 생성 후 플레이어를 진입 지점 셀에 배치한다.
// 레이아웃에서 entrySymbol 문자가 찍힌 셀 중 하나를 고른다.
//
// ZoneGenerator보다 늦게 실행되어야 한다.
// Project Settings > Script Execution Order에서 ZoneGenerator를 앞에 둘 것.
public class ZoneSpawner : MonoBehaviour
{
    [SerializeField] private ZoneGenerator generator;
    [SerializeField] private Transform player;

    // 레이아웃에서 진입 지점을 나타내는 문자. 첫 글자만 쓴다.
    [SerializeField] private string entrySymbol = "S";

    private void Start()
    {
        Place();
    }

    public void Place()
    {
        if (generator == null)
        {
            generator = FindFirstObjectByType<ZoneGenerator>();
        }

        if (generator == null || player == null)
        {
            return;
        }

        char c = string.IsNullOrEmpty(entrySymbol) ? 'S' : entrySymbol[0];
        Vector2 pos = generator.GetSpawnPoint(c);

        // Z는 건드리지 않는다. 카메라 정렬에 쓰일 수 있다.
        player.position = new Vector3(pos.x, pos.y, player.position.z);
    }
}