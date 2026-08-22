using UnityEngine;

// 발밑 지면. 인벤토리에서 GROUND 쪽으로 버린 아이템이 여기 쌓인다.
//
// 씬마다 하나만 둔다. 없으면 자동으로 만들어지므로 깜빡해도 동작은 한다.
//
// MVP 한계 — 플레이어 위치와 무관하게 존 전체가 하나의 지면을 공유한다.
// 원작처럼 버린 자리에 남기려면 월드에 아이템 오브젝트를 뿌리는 시스템이 필요한데
// 08 범위를 넘는다. 존을 나가면 지면 내용물은 사라진다.
[RequireComponent(typeof(InventoryController))]
public class GroundContainer : MonoBehaviour
{
    [Header("지면 격자")]
    [SerializeField] private int gridWidth = 8;
    [SerializeField] private int gridHeight = 10;

    private static GroundContainer instance;
    private InventoryController inventory;

    // 씬에 없으면 만들어서라도 돌려준다.
    public static InventoryController Inventory
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GroundContainer>();
            }

            if (instance == null)
            {
                GameObject go = new GameObject("GroundContainer");
                instance = go.AddComponent<GroundContainer>();
            }

            return instance.inventory;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        inventory = GetComponent<InventoryController>();

        // 지면은 무게 제한이 없다. 아무리 버려도 받아준다.
        inventory.Configure(gridWidth, gridHeight, 99999f);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
