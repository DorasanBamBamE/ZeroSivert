// 수락한 퀘스트 하나의 진행 상태.
//
// 진행도를 여기 저장하는 것과 매번 계산하는 것이 섞여 있다.
//   Collect — 저장하지 않는다. 인벤토리를 매번 세면 되고, 그래야 죽어서
//             아이템을 잃었을 때 진행도가 자동으로 되돌아간다
//   Kill    — 저장한다. 적은 죽고 나면 흔적이 없어서 셀 방법이 없다
[System.Serializable]
public class QuestState
{
    public QuestData quest;

    // Kill 전용 누적 카운터. 수락 이후에 죽인 것만 센다.
    public int killCount;

    public QuestState(QuestData quest)
    {
        this.quest = quest;
        this.killCount = 0;
    }
}
