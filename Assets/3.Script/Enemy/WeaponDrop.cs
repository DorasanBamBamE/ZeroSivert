using UnityEngine;

// 적이 들고 있던 총을 시체에서 주울 수 있게 한다.
//
// 원작에서 밴딧을 쏘면 그가 쓰던 총이 시체에 남는다. 초반 무기 수급이
// 전부 여기서 나오므로 없으면 경제가 돌지 않는다.
//
// 붙이는 법 — 적 프리팹 루트에 붙이고 table을 채운다.
// EnemyHealth의 사망 처리가 DropInto()를 한 번 부른다.
public class WeaponDrop : MonoBehaviour
{
    [SerializeField] private WeaponItemTable table;

    // 비우면 자식에서 찾는다.
    [SerializeField] private EnemyWeapon source;

    // 총이 떨어질 확률. 원작도 항상 나오지는 않는다.
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.7f;

    // 같이 나올 탄약 아이템. 없으면 비워둔다.
    [SerializeField] private ItemData ammoItem;
    [SerializeField] private int ammoMin = 10;
    [SerializeField] private int ammoMax = 30;

    private bool dropped;

    public void DropInto(LootContainer container)
    {
        if (dropped || container == null)
        {
            return;
        }

        dropped = true;

        if (source == null)
        {
            source = GetComponentInChildren<EnemyWeapon>(true);
        }

        if (source == null || table == null)
        {
            return;
        }

        if (Random.value > dropChance)
        {
            return;
        }

        ItemData item = table.Find(source.Data);

        if (item == null)
        {
            return;
        }

        container.AddExtra(item, 1);

        if (ammoItem != null)
        {
            container.AddExtra(ammoItem, Random.Range(ammoMin, ammoMax + 1));
        }
    }
}
