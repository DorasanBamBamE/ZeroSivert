// 장비 슬롯 종류. EquipmentController의 배열 인덱스로도 쓰이므로
// 값의 순서를 바꾸면 기존 씬의 인스펙터 설정이 어긋난다. 추가는 뒤에만 할 것.
public enum EquipSlot
{
    None = -1,
    Weapon1 = 0,
    Weapon2 = 1,
    Armor = 2,
    Backpack = 3,
}