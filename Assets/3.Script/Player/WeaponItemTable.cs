using UnityEngine;

// WeaponData(사격 수치) ↔ ItemData(인벤토리 물건)를 잇는 표.
//
// 둘을 따로 둔 이유는 07에서 정한 그대로다.
//   WeaponData — 데미지 · 탄창 · 반동 같은 "총의 성능"
//   ItemData   — 아이콘 · 무게 · 칸 크기 · 가격 같은 "물건으로서의 성질"
//
// 그런데 밴딧이 들고 있던 총을 시체에서 주우려면 반대 방향 조회가 필요하다.
// 어느 한쪽에 필드를 넣으면 순환 참조가 생기므로 표를 따로 둔다.
//
// Project 창 우클릭 → Create → ZeroSievert → Weapon Item Table
[CreateAssetMenu(fileName = "WeaponItemTable", menuName = "ZeroSievert/Weapon Item Table")]
public class WeaponItemTable : ScriptableObject
{
    [System.Serializable]
    public class Pair
    {
        public WeaponData weapon;
        public ItemData item;
    }

    [SerializeField] private Pair[] pairs;

    // 이 총에 해당하는 인벤토리 물건. 없으면 null.
    public ItemData Find(WeaponData weapon)
    {
        if (weapon == null || pairs == null)
        {
            return null;
        }

        for (int i = 0; i < pairs.Length; i++)
        {
            if (pairs[i] != null && pairs[i].weapon == weapon)
            {
                return pairs[i].item;
            }
        }

        return null;
    }
}
