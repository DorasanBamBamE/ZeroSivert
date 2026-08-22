using UnityEngine;

// 퀘스트 하나의 정의. Project 창 우클릭 → Create → ZeroSievert → Quest
//
// 원작 구조를 따른다.
//   바텐더    — 메인 스토리. isDaily 끔, faction Neutral
//   의사      — 의뢰. isDaily 끔, faction Neutral
//   네트워커  — 일일 임무. isDaily 켬, faction으로 세력 지정
[CreateAssetMenu(fileName = "Quest_", menuName = "ZeroSievert/Quest")]
public class QuestData : ScriptableObject
{
    [Header("표시")]
    public string title = "이름 없는 의뢰";

    [TextArea(3, 6)]
    public string description;

    // 수락했을 때 NPC가 하는 말. 비우면 description을 쓴다.
    [TextArea(2, 4)]
    public string acceptLine;

    // 완료했을 때 NPC가 하는 말.
    [TextArea(2, 4)]
    public string completeLine;

    [Header("목표")]
    public QuestType type = QuestType.Collect;

    // Collect 전용. 이 아이템을 targetCount개 들고 오면 완료.
    public ItemData targetItem;

    // Kill 전용. 비우면 아무 적이나 센다.
    // 적 프리팹의 Tag가 아니라 EnemyHealth에 지정하는 종류 이름이다. (예: "Bandit")
    public string targetEnemyId;

    public int targetCount = 5;

    [Header("보상")]
    public int rewardExp = 100;

    // 10번 상점·경제에서 실제로 지급한다. 지금은 표시만.
    public int rewardRubles = 500;

    // 완료 시 이 세력의 평판이 오른다.
    public Faction faction = Faction.Neutral;
    public int rewardReputation = 10;

    // 보상 아이템. 비워도 된다. 08에서 만든 LootTable을 그대로 쓴다.
    public LootTable rewardItems;

    [Header("일일 임무")]
    // 네트워커가 하루마다 새로 뿌리는 임무인가.
    public bool isDaily = false;

    // 진행도 문자열. "3 / 5" 형태.
    public string ProgressText(int current)
    {
        return current + " / " + targetCount;
    }

    private void OnValidate()
    {
        targetCount = Mathf.Max(1, targetCount);
        rewardExp = Mathf.Max(0, rewardExp);
        rewardRubles = Mathf.Max(0, rewardRubles);
        rewardReputation = Mathf.Max(0, rewardReputation);

        // 중립 세력에는 평판이 없다.
        if (faction == Faction.Neutral)
        {
            rewardReputation = 0;
        }
    }
}
