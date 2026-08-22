using UnityEngine;
using UnityEngine.UI;

// 퀘스트 목록 한 줄. 대화창과 퀘스트 로그가 같은 프리팹을 쓴다.
//
// 구조
//   QuestEntry (Button, Image)
//     Text_Title    좌측 정렬. 퀘스트 이름
//     Text_Status   우측 정렬. "수락 가능" / "3 / 5" / "완료!"
public class QuestEntryUI : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text statusText;
    [SerializeField] private Button button;
    [SerializeField] private Image background;

    [Header("색")]
    [SerializeField] private Color normalColor = new Color(0.16f, 0.16f, 0.14f, 1f);

    // 완료 조건을 채운 줄. 눈에 띄어야 한다.
    [SerializeField] private Color readyColor = new Color(0.35f, 0.30f, 0.10f, 1f);

    // 아직 수락 안 한 줄.
    [SerializeField] private Color offerColor = new Color(0.12f, 0.20f, 0.16f, 1f);

    private QuestData quest;
    private System.Action<QuestData> callback;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (background == null)
        {
            background = GetComponent<Image>();
        }

        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    // state 0 = 일반(진행 중) · 1 = 수락 가능 · 2 = 완료 가능
    public void Bind(QuestData q, string status, int state, System.Action<QuestData> onClick)
    {
        quest = q;
        callback = onClick;

        if (titleText != null)
        {
            titleText.text = (q != null) ? q.title : "";
        }

        if (statusText != null)
        {
            statusText.text = status;
        }

        if (background != null)
        {
            if (state == 2)
            {
                background.color = readyColor;
            }
            else if (state == 1)
            {
                background.color = offerColor;
            }
            else
            {
                background.color = normalColor;
            }
        }

        // 로그 화면처럼 누를 필요가 없는 곳에서는 onClick을 null로 넘긴다.
        if (button != null)
        {
            button.interactable = (onClick != null);
        }
    }

    private void OnClick()
    {
        if (callback != null)
        {
            callback(quest);
        }
    }
}
