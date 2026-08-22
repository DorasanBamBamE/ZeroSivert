using UnityEngine;

// 씬 진입 시 저장된 상태를 플레이어에게 되돌린다.
// 플레이어 오브젝트에 붙인다.
//
// PlayerStats.Awake()가 값을 초기화한 뒤 여기 Start()가 덮어쓰는 순서라
// 실행 순서를 따로 건드릴 필요가 없다.
public class PlayerPersistence : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;

    private void Awake()
    {
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
    }

    private void Start()
    {
        if (stats == null)
        {
            return;
        }

        if (RunData.Instance.HasSnapshot)
        {
            RunData.Instance.Load(stats);
        }
    }
}