using UnityEngine;
using UnityEngine.UI;

// PDA 탭바에서 PDA 바깥의 독립 화면을 여는 탭.
//   Tab_Quest -> 09 퀘스트 로그(QuestListUI)
//   Tab_Map   -> 06 존 지도(MapUI)
//
// 원작에서는 의뢰와 지도가 PDA 안의 페이지다. 여기서는 06/09에서 이미 만든
// 독립 창을 그대로 재사용하되, 탭을 누르면 PDA를 잠시 접고 그 창을 띄운다.
// 창을 닫으면 PDAController가 PDA를 다시 펼친다 - 원작에서 탭 사이를
// 오가는 느낌과 같아진다. Q · M 단축키는 그대로 살아 있다.
//
// 대상 창은 Awake에서 씬을 뒤져 찾는다. UI_Screens는 프리팹 인스턴스라
// 씬마다 참조를 손으로 이어주면 하나 빠뜨리기 쉽기 때문이다.
//
// 이 스크립트는 Button과 함께 붙인다. Button의 Transition은 None -
// 색은 PDATabButton이 칠하므로 Color Tint를 켜면 서로 덮어쓴다.
[RequireComponent(typeof(Image))]
public class PDAScreenTab : MonoBehaviour
{
    public enum Target
    {
        QuestLog = 0,
        Map = 1,
    }

    [SerializeField] private Target target = Target.QuestLog;

    // 비우면 부모에서 찾는다.
    [SerializeField] private PDAController pda;

    private QuestListUI questLog;
    private MapUI map;

    private void Awake()
    {
        if (pda == null)
        {
            pda = GetComponentInParent<PDAController>(true);
        }

        Button button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError("[PDAScreenTab] Button이 없다. " + name + "에 Button을 붙일 것.", this);
            return;
        }

        // 인스펙터에서 중복으로 걸려 있어도 두 번 열리지 않게 한다.
        button.onClick.RemoveListener(OnClick);
        button.onClick.AddListener(OnClick);
    }

    // 버튼 OnClick에서 직접 불러도 된다.
    public void OnClick()
    {
        if (target == Target.Map)
        {
            OpenMap();
        }
        else
        {
            OpenQuestLog();
        }
    }

    private void OpenQuestLog()
    {
        if (questLog == null)
        {
            questLog = FindFirstObjectByType<QuestListUI>(FindObjectsInactive.Include);
        }

        if (questLog == null)
        {
            Debug.LogWarning("[PDAScreenTab] 씬에 QuestListUI가 없다.", this);
            return;
        }

        // PDA를 먼저 접어야 timeScale이 창 쪽으로 넘어간다.
        if (pda != null)
        {
            pda.SuspendFor(IsQuestLogOpen);
        }

        questLog.Open();
    }

    private void OpenMap()
    {
        if (map == null)
        {
            map = FindFirstObjectByType<MapUI>(FindObjectsInactive.Include);
        }

        if (map == null)
        {
            Debug.LogWarning("[PDAScreenTab] 씬에 MapUI가 없다.", this);
            return;
        }

        if (pda != null)
        {
            pda.SuspendFor(IsMapOpen);
        }

        map.Open();
    }

    private static bool IsQuestLogOpen()
    {
        return QuestListUI.IsOpen;
    }

    private static bool IsMapOpen()
    {
        return MapUI.IsOpen;
    }
}
