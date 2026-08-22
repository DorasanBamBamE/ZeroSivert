// 어떤 UI든 하나라도 열려 있으면 플레이어 조작을 막는다.
//
// 07에서는 InventoryScreen.IsOpen 하나만 보면 됐지만 09에서 대화창과
// 퀘스트 로그가, 06에서 지도가 늘었다. 여기저기서 조건을 OR로 늘려 붙이면
// UI가 하나 늘 때마다 PlayerController·Weapon을 또 고쳐야 한다.
//
// 그래서 판정을 한 곳으로 모은다. 앞으로 UI가 늘면 여기에 bool 하나만 추가한다.
//
// ★ 이 파일을 넣은 뒤 PlayerController와 Weapon에서
//   InventoryScreen.IsOpen  →  UIBlocker.Any
//   로 딱 한 단어씩만 바꾸면 된다.
//
// 상점은 여기에 없다. 07의 인벤토리 창을 그대로 쓰기 때문에
// InventoryScreen.IsOpen이 이미 켜져 있다.
public static class UIBlocker
{
    // 각 UI가 열고 닫을 때 스스로 켜고 끈다.
    public static bool DialogueOpen;    // 09 대화창
    public static bool QuestLogOpen;    // 09 퀘스트 로그 (Q)
    public static bool MapOpen;         // 06 지도 (M)
    public static bool PdaOpen;         // 04 PDA (J)

    // 조작을 막아야 하는가.
    public static bool Any
    {
        get
        {
            return InventoryScreen.IsOpen
                   || DialogueOpen
                   || QuestLogOpen
                   || MapOpen
                   || PdaOpen;
        }
    }

    // 씬을 다시 로드했을 때 켜진 채로 남는 사고를 막는다.
    // SceneLoader나 RunEndHandler에서 부르면 안전하다. 안 불러도 각 UI의
    // OnDisable이 정리하므로 필수는 아니다.
    public static void Clear()
    {
        DialogueOpen = false;
        QuestLogOpen = false;
        MapOpen = false;
        PdaOpen = false;
    }
}
