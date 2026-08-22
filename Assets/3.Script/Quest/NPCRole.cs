// 벙커 NPC의 역할. 원작 벙커의 세 인물을 그대로 옮겼다.
//
//   Bartender — 잡화·무기·탄약 판매 + 메인 스토리 퀘스트
//   Doctor    — 의료품 판매 + 치료·방어구 수리 + 의뢰
//   Networker — 일일 임무 발주. 보상이 세력 평판
//
// 판매·치료·수리는 10번(상점·경제)에서 붙인다. 09는 대화와 퀘스트만 만든다.
//
// 값의 순서를 바꾸면 기존 NPCData 에셋이 어긋난다. 추가는 뒤에만 할 것.
public enum NPCRole
{
    Bartender = 0,
    Doctor = 1,
    Networker = 2,
    Departure = 3,
}
