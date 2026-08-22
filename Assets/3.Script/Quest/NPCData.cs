using UnityEngine;

// NPC 한 명의 정의. Project 창 우클릭 → Create → ZeroSievert → NPC
//
// 원작 벙커 기준으로 세 개만 만들면 된다.
//   NPC_Bartender  Role Bartender  quests에 메인 스토리 순서대로
//   NPC_Doctor     Role Doctor     quests에 의뢰
//   NPC_Networker  Role Networker  dailyPool에 일일 임무 후보를 잔뜩
[CreateAssetMenu(fileName = "NPC_", menuName = "ZeroSievert/NPC")]
public class NPCData : ScriptableObject
{
    [Header("표시")]
    public string npcName = "이름 없음";

    // 대화창 좌측에 뜨는 초상화. 없으면 그냥 비워둬도 된다.
    public Sprite portrait;

    public NPCRole role = NPCRole.Bartender;

    [Header("대사")]
    // 말을 걸었을 때 기본으로 뜨는 인사말.
    [TextArea(2, 5)]
    public string greeting = "무슨 일이지.";

    // 줄 만한 퀘스트가 하나도 없을 때 대신 뜨는 말. 비우면 greeting을 쓴다.
    [TextArea(2, 5)]
    public string idleLine;

    [Header("퀘스트")]
    // 고정 퀘스트. 위에서부터 순서대로 하나씩 열린다.
    //
    // ★ 순차 진행이 원작 메인 스토리의 방식이다.
    //   앞의 것을 끝내지 않으면 뒤의 것은 목록에 뜨지 않는다.
    //   의사처럼 순서가 없어도 되는 NPC는 sequential을 끄면 전부 한꺼번에 뜬다.
    public QuestData[] quests;

    public bool sequential = true;

    [Header("거래 (10번)")]
    // 이 NPC가 물건을 판다면 MerchantData를 넣는다.
    // 비워두면 대화창에 '거래' 버튼이 아예 안 뜬다 — 네트워커가 그 경우다.
    //
    // ★ 실제 재고와 매매는 NPC 오브젝트에 붙은 ShopController가 한다.
    //   여기 있는 건 "거래 버튼을 띄울지"와 "서비스 가격표"를 위한 참조다.
    public MerchantData merchant;

    [Header("일일 임무 (네트워커 전용)")]
    // 여기서 매일 무작위로 뽑는다. QuestData의 isDaily를 반드시 켜 둘 것.
    public QuestData[] dailyPool;

    [Range(1, 6)]
    public int dailyOfferCount = 3;

    private void OnValidate()
    {
        // 일일 임무 풀에 isDaily가 꺼진 게 섞이면 하루가 지나도 다시 안 뜬다.
        if (dailyPool != null)
        {
            for (int i = 0; i < dailyPool.Length; i++)
            {
                if (dailyPool[i] != null && !dailyPool[i].isDaily)
                {
                    Debug.LogWarning("[NPCData] " + npcName + "의 일일 임무 풀에 있는 "
                                     + dailyPool[i].title + "은(는) isDaily가 꺼져 있다.", this);
                }
            }
        }
    }
}
