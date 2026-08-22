using UnityEngine;

// 인벤토리 아이템 한 종류의 정의. ScriptableObject 에셋으로 만든다.
// Project 창에서 우클릭 → Create → ZeroSievert → Item
//
// WeaponData(무기의 사격 스탯)와는 별개다.
// category가 Weapon인 아이템은 weaponData 칸에 해당 WeaponData를 연결해야
// 장비 슬롯에 넣었을 때 실제로 손에 들린다.
[CreateAssetMenu(fileName = "Item_", menuName = "ZeroSievert/Item")]
public class ItemData : ScriptableObject
{
    [Header("표시")]
    public string displayName = "이름 없음";

    [TextArea(2, 5)]
    public string description;

    // 크기는 gridWidth × 16 , gridHeight × 16 픽셀로 맞춰서 만들 것.
    // 임포트 설정은 프로젝트 공통(Point / Compression None / Extrude 0 / Full Rect).
    public Sprite icon;

    [Header("분류")]
    public ItemCategory category = ItemCategory.Misc;

    [Header("가치 (10번 상점)")]
    // 기준가(루블). 상인이 이 값에 배율을 곱해 사고판다.
    //
    // ★ 0으로 두면 아무도 사주지 않는다.
    //   퀘스트 전용 아이템처럼 팔면 안 되는 물건은 0으로 둔다.
    public int basePrice = 100;

    [Header("무게 · 칸 크기")]
    // 1개당 무게(kg). 스택이면 개수만큼 곱해진다.
    public float weight = 1f;

    // 그리드에서 차지하는 칸 수. 회전하면 두 값이 뒤바뀐다.
    [Range(1, 10)] public int gridWidth = 1;
    [Range(1, 8)] public int gridHeight = 1;

    [Header("스택")]
    public bool stackable = false;

    // stackable이 false면 무시된다.
    public int maxStack = 1;

    [Header("사용 효과 (소비 아이템 전용)")]
    // category에 따라 InventoryController가 알아서 매핑한다.
    // Medical=회복량 / Food=허기 / Drink=갈증 / Antirad=방사능 감소 / Stimulant=에너지
    public float effectAmount = 0f;

    // Medical 전용. 켜면 사용 시 출혈도 멎는다.
    public bool curesBleeding = false;

    [Header("장비 (Weapon / Armor / Backpack 전용)")]
    // category가 Weapon일 때 필수. 이걸 비워두면 슬롯에 들어가도 손에 안 들린다.
    public WeaponData weaponData;

    // category가 Armor일 때. 받는 피해를 이 비율만큼 줄인다. 0.25면 25% 감소.
    [Range(0f, 0.8f)] public float damageReduction = 0f;

    // category가 Backpack일 때. 소지 무게 상한에 이만큼 더해진다(kg).
    public float carryBonus = 0f;

    // 실제로 스택 가능한 최대 개수. stackable이 꺼져 있으면 항상 1이다.
    public int MaxStackSafe
    {
        get { return stackable ? Mathf.Max(1, maxStack) : 1; }
    }

    // 우클릭으로 사용할 수 있는 카테고리인지.
    public bool IsConsumable
    {
        get
        {
            return category == ItemCategory.Medical
                || category == ItemCategory.Food
                || category == ItemCategory.Drink
                || category == ItemCategory.Antirad
                || category == ItemCategory.Stimulant;
        }
    }

    // 이 아이템이 들어갈 수 있는 장비 슬롯. 장비가 아니면 None.
    // 무기는 Weapon1 / Weapon2 아무 데나 들어가므로 여기서는 Weapon1을 대표로 돌려주고,
    // 실제 판정은 EquipmentSlotUI.Accepts()가 한다.
    public EquipSlot DefaultEquipSlot
    {
        get
        {
            switch (category)
            {
                case ItemCategory.Weapon: return EquipSlot.Weapon1;
                case ItemCategory.Armor: return EquipSlot.Armor;
                case ItemCategory.Backpack: return EquipSlot.Backpack;
                default: return EquipSlot.None;
            }
        }
    }

    public bool IsEquippable
    {
        get { return DefaultEquipSlot != EquipSlot.None; }
    }

    // 인스펙터에서 값을 이상하게 넣었을 때 조용히 바로잡는다.
    private void OnValidate()
    {
        gridWidth = Mathf.Clamp(gridWidth, 1, 10);
        gridHeight = Mathf.Clamp(gridHeight, 1, 8);
        weight = Mathf.Max(0f, weight);
        maxStack = Mathf.Max(1, maxStack);
        carryBonus = Mathf.Max(0f, carryBonus);

        if (!stackable)
        {
            maxStack = 1;
        }

        // 장비는 스택되지 않는다.
        if (IsEquippable)
        {
            stackable = false;
            maxStack = 1;
        }
    }
}
