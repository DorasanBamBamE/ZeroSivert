// 아이템 분류. 사용 효과 매핑과 장비 슬롯 판정에 쓴다.
//
// 별도 파일로 뺀 이유 — enum이 ItemData.cs 안에 있으면 나중에 그 파일을
// 통째로 갈아끼울 때 enum까지 같이 날아간다. 프로젝트에서 반복해서 겪은 함정이다.
//
// 값의 순서를 바꾸면 기존 ItemData 에셋의 category가 어긋난다. 추가는 뒤에만 할 것.
public enum ItemCategory
{
    Weapon,      // 무기 — Weapon1 / Weapon2 슬롯에 장착
    Ammo,        // 탄약
    Medical,     // 붕대, 구급킷
    Food,        // 통조림 등
    Drink,       // 물
    Antirad,     // 항방사능제
    Stimulant,   // 각성제
    Material,    // 제작 재료
    Key,         // 열쇠, 퀘스트 아이템
    Misc,        // 잡화 (판매용)
    Armor,       // 방탄복 — Armor 슬롯. 피해 감소
    Backpack,    // 배낭 — Backpack 슬롯. 소지 무게 상한 증가
}