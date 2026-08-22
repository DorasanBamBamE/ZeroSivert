using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 퀘스트 로그. Q로 열고 닫는다.
//
// 원작에서는 PDA 안의 한 탭이다. 지금은 독립 창으로 만들고,
// PDA를 정리할 때 이 패널을 통째로 PDA 자식으로 옮기면 된다 —
// 그때는 toggleKey를 None으로 두고 PDA가 root를 켜고 끄면 끝난다.
//
// ★ 계층 주의 — InventoryScreen·DialogueUI와 같다.
//   이 스크립트는 항상 켜져 있는 부모에 붙이고, 켜고 끌 패널만 root에 넣는다.
public class QuestListUI : MonoBehaviour
{
    [Header("루트")]
    [SerializeField] private GameObject root;

    [Header("입력")]
    // PDA 탭으로 옮기면 None으로 바꾼다.
    [SerializeField] private KeyCode toggleKey = KeyCode.Q;

    [Header("목록")]
    [SerializeField] private RectTransform entryRoot;
    [SerializeField] private QuestEntryUI entryPrefab;

    // 선택한 퀘스트의 설명이 뜨는 곳. 없어도 된다.
    [SerializeField] private Text detailText;

    // 수락한 퀘스트가 하나도 없을 때 켜는 안내.
    [SerializeField] private GameObject emptyLabel;

    [Header("세력 평판 (선택)")]
    [SerializeField] private Text greenArmyText;
    [SerializeField] private Text crimsonText;

    [Header("커서")]
    [SerializeField] private GameObject crosshair;

    [Header("동작")]
    // 로그를 볼 때 시간을 멈출지. 원작 PDA는 멈춘다.
    [SerializeField] private bool pauseWhileOpen = true;

    private readonly List<QuestEntryUI> spawned = new List<QuestEntryUI>();
    private float savedTimeScale = 1f;
    private bool cursorWasVisible;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        if (root == null)
        {
            Debug.LogError("[QuestListUI] root가 비어 있다.", this);
        }
        else
        {
            root.SetActive(false);
        }
    }

    private void OnEnable()
    {
        QuestManager.Instance.Changed -= Refresh;
        QuestManager.Instance.Changed += Refresh;
    }

    private void OnDisable()
    {
        // Instance가 아니라 Exists를 본다. 끄는 중에 새로 만들지 않기 위해서다.
        if (QuestManager.Exists)
        {
            QuestManager.Instance.Changed -= Refresh;
        }

        if (IsOpen)
        {
            RestoreWorld();
        }
    }

    private void Update()
    {
        if (toggleKey == KeyCode.None)
        {
            return;
        }

        if (!Input.GetKeyDown(toggleKey))
        {
            return;
        }

        // 대화 중이나 인벤토리가 열려 있으면 Q를 먹지 않는다.
        if (!IsOpen && (InventoryScreen.IsOpen || DialogueUI.IsOpen || UIBlocker.PdaOpen))
        {
            return;
        }

        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    // ───────────────── 열고 닫기 ─────────────────

    public void Open()
    {
        if (root == null || IsOpen)
        {
            return;
        }

        root.SetActive(true);
        IsOpen = true;
        UIBlocker.QuestLogOpen = true;

        if (pauseWhileOpen)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // 조준선 먼저, 커서 나중. 순서를 바꾸면 커서가 안 보인다.
        if (crosshair != null)
        {
            crosshair.SetActive(false);
        }

        cursorWasVisible = Cursor.visible;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Refresh();
    }

    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        ClearEntries();

        if (IsOpen)
        {
            RestoreWorld();
        }
    }

    private void RestoreWorld()
    {
        IsOpen = false;
        UIBlocker.QuestLogOpen = false;

        if (pauseWhileOpen)
        {
            Time.timeScale = (savedTimeScale <= 0f) ? 1f : savedTimeScale;
        }

        if (crosshair != null)
        {
            crosshair.SetActive(true);
        }

        Cursor.visible = cursorWasVisible;
    }

    // ───────────────── 내용 ─────────────────

    public void Refresh()
    {
        if (root == null || !root.activeSelf)
        {
            return;
        }

        ClearEntries();

        QuestManager qm = QuestManager.Instance;
        IReadOnlyList<QuestState> list = qm.Active;

        for (int i = 0; i < list.Count; i++)
        {
            QuestData q = list[i].quest;

            if (q == null || entryPrefab == null || entryRoot == null)
            {
                continue;
            }

            QuestEntryUI e = Instantiate(entryPrefab, entryRoot);
            spawned.Add(e);

            bool ready = qm.CanTurnIn(q);
            string status = ready ? "완료 — 의뢰인에게" : q.ProgressText(qm.GetProgress(q));

            // 로그에서는 수락·완료를 하지 않는다. 눌러도 설명만 뜬다.
            e.Bind(q, status, ready ? 2 : 0, ShowDetail);
        }

        if (emptyLabel != null)
        {
            emptyLabel.SetActive(list.Count == 0);
        }

        if (detailText != null && list.Count == 0)
        {
            detailText.text = "진행 중인 의뢰가 없다.";
        }

        RefreshReputation(qm);
    }

    private void RefreshReputation(QuestManager qm)
    {
        if (greenArmyText != null)
        {
            greenArmyText.text = "그린 아미  " + qm.GetReputation(Faction.GreenArmy);
        }

        if (crimsonText != null)
        {
            crimsonText.text = "크림슨  " + qm.GetReputation(Faction.Crimson);
        }
    }

    private void ShowDetail(QuestData q)
    {
        if (detailText == null || q == null)
        {
            return;
        }

        detailText.text = q.title + "\n\n" + q.description
                          + "\n\n보상  EXP " + q.rewardExp + " · " + q.rewardRubles + " 루블";
    }

    private void ClearEntries()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                Destroy(spawned[i].gameObject);
            }
        }

        spawned.Clear();
    }
}
