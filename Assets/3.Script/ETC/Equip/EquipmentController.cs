using System.Collections.Generic;
using UnityEngine;

// 장비 슬롯 4칸(무기1 · 무기2 · 방탄복 · 배낭)을 관리한다.
// 플레이어 오브젝트에 붙인다 — PlayerStats, InventoryController와 같은 GameObject.
//
// 여기서 나가는 효과는 셋이다.
//   무기  → Weapon.Equip()으로 실제로 손에 들린다. 1/2 키로 전환
//   방탄복 → PlayerStats.TakeDamage가 DamageReduction을 참조해 피해를 깎는다
//   배낭  → InventoryController.Capacity에 CarryBonus가 더해진다
public class EquipmentController : MonoBehaviour
{
    public const int SlotCount = 4;

    // 장비가 바뀔 때마다 알린다. UI가 구독해서 다시 그린다.
    public event System.Action Changed;

    [Header("참조")]
    // 비워두면 자식에서 찾는다.
    [SerializeField] private Weapon weapon;

    [Header("입력")]
    [SerializeField] private KeyCode weapon1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode weapon2Key = KeyCode.Alpha2;

    // 인덱스는 EquipSlot 값과 같다. null이면 빈 슬롯.
    [SerializeField] private ItemData[] equipped = new ItemData[SlotCount];

    private InventoryController inventory;
    private int activeWeapon;   // 0 = Weapon1, 1 = Weapon2

    // 11 - 무기 슬롯마다 남은 탄을 따로 기억한다.
    //   -1은 "아직 안 들어봤다"는 뜻이고, 그때는 가득 채운 상태로 시작한다.
    private readonly int[] weaponAmmo = new int[] { -1, -1 };

    public int ActiveWeapon { get { return activeWeapon; } }

    // 받는 피해를 깎는 비율. 0 = 감소 없음.
    public float DamageReduction
    {
        get
        {
            ItemData armor = equipped[(int)EquipSlot.Armor];
            return armor != null ? Mathf.Clamp(armor.damageReduction, 0f, 0.8f) : 0f;
        }
    }

    // 배낭이 더해주는 소지 무게(kg).
    public float CarryBonus
    {
        get
        {
            ItemData pack = equipped[(int)EquipSlot.Backpack];
            return pack != null ? Mathf.Max(0f, pack.carryBonus) : 0f;
        }
    }

    private void Awake()
    {
        if (equipped == null || equipped.Length != SlotCount)
        {
            equipped = new ItemData[SlotCount];
        }

        inventory = GetComponent<InventoryController>();

        if (weapon == null)
        {
            weapon = GetComponentInChildren<Weapon>();
        }
    }

    private void Start()
    {
        // 인스펙터로 미리 채워둔 장비가 있으면 시작할 때 손에 들린다.
        ApplyActiveWeapon();
        NotifyChanged();
    }

    private void Update()
    {
        // 인벤토리나 PDA가 열려 있으면 무기 전환 입력을 무시한다.
        if (UIBlocker.Any || Time.timeScale == 0f)
        {
            return;
        }

        if (Input.GetKeyDown(weapon1Key))
        {
            SetActiveWeapon(0);
        }
        else if (Input.GetKeyDown(weapon2Key))
        {
            SetActiveWeapon(1);
        }

        // 들고 있는 동안의 소모를 계속 반영해 둔다. 전환 순간에만 저장하면
        // 재장전 직후 바꿨을 때 값이 한 박자 늦는다.
        RememberAmmo();
    }

    private void NotifyChanged()
    {
        if (Changed != null)
        {
            Changed();
        }
    }

    // ───────────────── 조회 ─────────────────

    public ItemData Get(EquipSlot slot)
    {
        int i = (int)slot;

        if (i < 0 || i >= SlotCount)
        {
            return null;
        }

        return equipped[i];
    }

    // 이 슬롯에 이 아이템을 넣을 수 있는가.
    public static bool Accepts(EquipSlot slot, ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        switch (slot)
        {
            case EquipSlot.Weapon1:
            case EquipSlot.Weapon2:
                return item.category == ItemCategory.Weapon;

            case EquipSlot.Armor:
                return item.category == ItemCategory.Armor;

            case EquipSlot.Backpack:
                return item.category == ItemCategory.Backpack;
        }

        return false;
    }

    // ───────────────── 장착 / 해제 ─────────────────

    // 슬롯에 아이템을 넣는다. 원래 있던 것은 돌려준다(호출자가 인벤토리에 넣어야 한다).
    // 타입이 안 맞으면 false를 돌려주고 아무것도 하지 않는다.
    public bool TryEquip(EquipSlot slot, ItemData item, out ItemData replaced)
    {
        replaced = null;

        int i = (int)slot;

        if (i < 0 || i >= SlotCount || !Accepts(slot, item))
        {
            return false;
        }

        replaced = equipped[i];
        equipped[i] = item;

        // 11 - 무기를 갈아끼우면 그 슬롯의 잔탄 기억을 지운다. 새 총은 가득 찬 채로 들린다.
        if (slot == EquipSlot.Weapon1 || slot == EquipSlot.Weapon2)
        {
            weaponAmmo[(int)slot] = -1;
        }

        // 배낭을 바꾸면 상한이 달라진다. 짐이 넘치더라도 아이템을 강제로
        // 버리지는 않는다 — 무게 표시가 빨갛게 뜨는 것으로 알린다.
        RefreshDerived(slot);
        NotifyChanged();
        return true;
    }

    // 슬롯을 비우고 들어 있던 아이템을 돌려준다. 비어 있으면 null.
    public ItemData Unequip(EquipSlot slot)
    {
        int i = (int)slot;

        if (i < 0 || i >= SlotCount)
        {
            return null;
        }

        ItemData removed = equipped[i];

        if (removed == null)
        {
            return null;
        }

        equipped[i] = null;

        if (slot == EquipSlot.Weapon1 || slot == EquipSlot.Weapon2)
        {
            weaponAmmo[(int)slot] = -1;
        }

        RefreshDerived(slot);
        NotifyChanged();
        return removed;
    }

    private void RefreshDerived(EquipSlot slot)
    {
        if (slot == EquipSlot.Weapon1 || slot == EquipSlot.Weapon2)
        {
            ApplyActiveWeapon();
        }
    }

    // ───────────────── 무기 전환 ─────────────────

    public void SetActiveWeapon(int index)
    {
        index = Mathf.Clamp(index, 0, 1);

        if (activeWeapon == index)
        {
            return;
        }

        RememberAmmo();

        activeWeapon = index;
        ApplyActiveWeapon();
        NotifyChanged();
    }

    private void ApplyActiveWeapon()
    {
        if (weapon == null)
        {
            return;
        }

        ItemData item = equipped[activeWeapon];

        // 빈 슬롯이면 맨손이 된다. Weapon.Equip이 null을 견디도록 고쳐져 있다.
        weapon.Equip(item != null ? item.weaponData : null);

        // 11 - 이 슬롯이 기억하던 잔탄을 되돌린다.
        //   Equip은 항상 가득 채우므로, 처음 드는 무기는 그대로 두면 된다.
        if (item != null && weaponAmmo[activeWeapon] >= 0)
        {
            weapon.SetAmmo(weaponAmmo[activeWeapon]);
        }
        else if (item != null)
        {
            weaponAmmo[activeWeapon] = weapon.CurrentAmmo;
        }
    }

    // 지금 들고 있는 총의 잔탄을 그 슬롯에 적어 둔다.
    private void RememberAmmo()
    {
        if (weapon == null || equipped[activeWeapon] == null)
        {
            return;
        }

        weaponAmmo[activeWeapon] = weapon.CurrentAmmo;
    }

    // ───────────────── 씬 전환용 스냅샷 ─────────────────

    public void CaptureTo(RunData.PlayerSnapshot s)
    {
        if (s == null)
        {
            return;
        }

        s.equipment = new List<ItemData>(SlotCount);

        for (int i = 0; i < SlotCount; i++)
        {
            s.equipment.Add(equipped[i]);
        }

        s.activeWeapon = activeWeapon;
    }

    public void RestoreFrom(RunData.PlayerSnapshot s)
    {
        if (s == null)
        {
            return;
        }

        equipped = new ItemData[SlotCount];

        if (s.equipment != null)
        {
            int n = Mathf.Min(SlotCount, s.equipment.Count);

            for (int i = 0; i < n; i++)
            {
                equipped[i] = s.equipment[i];
            }
        }

        activeWeapon = Mathf.Clamp(s.activeWeapon, 0, 1);

        ApplyActiveWeapon();
        NotifyChanged();
    }

    // 인벤토리에 자리가 있으면 슬롯을 비우고 그쪽으로 옮긴다.
    // UI에서 장비 슬롯을 우클릭했을 때 부른다.
    public bool UnequipToInventory(EquipSlot slot)
    {
        ItemData item = Get(slot);

        if (item == null)
        {
            return false;
        }

        if (inventory == null)
        {
            inventory = GetComponent<InventoryController>();
        }

        if (inventory == null)
        {
            return false;
        }

        // 배낭을 빼면 상한이 줄어드는데, 그 전에 자리부터 확인해야 한다.
        if (inventory.TryAdd(item, 1) < 1)
        {
            return false;
        }

        Unequip(slot);
        return true;
    }
}