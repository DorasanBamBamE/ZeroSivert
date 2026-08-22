using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// NPC 대화창. 초상화 · 이름 · 대사 · 퀘스트 목록.
//
// 원작 방식을 따른다.
//   바텐더  메인 스토리를 하나씩 순서대로 준다
//   의사    의뢰를 한꺼번에 늘어놓는다 (NPCData.sequential 끄기)
//   네트워커 오늘의 일일 임무 3개를 뿌린다. 하루가 지나면 새 목록
//
// 판매·치료·수리 버튼은 10번(상점·경제)에서 이 창에 붙인다.
// 지금은 자리만 비워둔다.
//
// ★ 계층은 InventoryScreen과 같은 방식으로 짠다.
//   이 스크립트는 부모(항상 켜져 있는 오브젝트)에 붙이고,
//   실제로 켜고 끄는 패널은 root에 드래그한다.
//   자기 자신을 끄면 Update가 멈춰서 다시 못 켠다.
public class DialogueUI : MonoBehaviour
{
    [Header("루트")]
    // 실제로 켜고 끌 패널. 이 스크립트가 붙은 오브젝트가 아니어야 한다.
    [SerializeField] private GameObject root;

    [Header("머리말")]
    [SerializeField] private Image portrait;
    [SerializeField] private Text nameText;
    [SerializeField] private Text bodyText;

    [Header("퀘스트 목록")]
    // 줄이 쌓일 부모. Vertical Layout Group을 붙여둘 것.
    [SerializeField] private RectTransform entryRoot;
    [SerializeField] private QuestEntryUI entryPrefab;

    // 줄 만한 퀘스트가 하나도 없을 때 켜는 안내. 없어도 된다.
    [SerializeField] private GameObject emptyLabel;

    [Header("거래·서비스 (10번)")]
    // NPCData.merchant가 비어 있으면 자동으로 꺼진다.
    [SerializeField] private Button tradeButton;

    [Header("존 출발")]
    [SerializeField] private Button departButton;

    // 의사의 치료·지혈·제염 버튼 셋. 필요 없는 것은 스스로 꺼진다.
    [SerializeField] private ServiceButtonUI[] serviceButtons;

    // 루블 잔액 표시. 대화창 안에 두면 값을 치르는 게 눈에 보인다.
    [SerializeField] private WalletLabel walletLabel;

    [Header("닫기")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private KeyCode altCloseKey = KeyCode.E;
    [SerializeField] private Button closeButton;

    [Header("커서")]
    // 조준선 오브젝트. 인벤토리와 같은 것을 넣으면 된다.
    // 07에서 배운 것 — 조준선을 먼저 끈 다음 커서를 켜야 한다.
    // 순서를 바꾸면 조준선의 Update가 Cursor.visible을 다시 꺼버린다.
    [SerializeField] private GameObject crosshair;

    private NPCData current;
    private ShopController currentShop;
    private ZoneEntryPoint currentDeparture;
    private readonly List<QuestEntryUI> spawned = new List<QuestEntryUI>();
    private float savedTimeScale = 1f;
    private bool cursorWasVisible;

    // 11 - 연 프레임에는 닫기 키를 보지 않는다.
    //   altCloseKey가 E라서, NPCInteract가 E로 연 그 프레임에
    //   이 Update가 먼저 돌면 열리자마자 닫혀 버린다.
    //   NPC마다 실행 순서가 달라 "어떤 NPC는 되고 어떤 NPC는 안 되는" 증상이 났다.
    private int openedFrame = -1;

    public static bool IsOpen { get; private set; }

    public NPCData Current
    {
        get { return current; }
    }

private void Awake()
    {
        if (root == null)
        {
            Debug.LogError("[DialogueUI] root가 비어 있다. 켜고 끌 패널을 넣을 것.", this);
        }
        else
        {
            root.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (tradeButton != null)
        {
            tradeButton.onClick.AddListener(OnClickTrade);
        }

        if (departButton != null)
        {
            departButton.onClick.AddListener(OnClickDepart);
        }
    }

    private void OnEnable()
    {
        // 07 장비창에서 겪은 것 — OnDisable에서 끊긴 구독이 되살아나지 않았다.
        // 그래서 항상 끊고 다시 잇는다.
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

        // 열린 채로 씬이 넘어가면 시간이 멈춘 채 남는다.
        if (IsOpen)
        {
            RestoreWorld();
        }
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (Time.frameCount == openedFrame)
        {
            return;
        }

        // timeScale이 0이어도 Update와 Input은 계속 돈다. 07에서 배운 것.
        if (Input.GetKeyDown(closeKey) || Input.GetKeyDown(altCloseKey))
        {
            Close();
        }
    }

    // ───────────────── 열고 닫기 ─────────────────

    // shop은 없어도 된다. 넣으면 '거래' 버튼이 살아난다.
    public void Open(NPCData npc)
    {
        Open(npc, null, null);
    }

    public void Open(NPCData npc, ShopController shop)
    {
        Open(npc, shop, null);
    }

    public void Open(NPCData npc, ShopController shop, ZoneEntryPoint departure)
    {
        if (npc == null || root == null)
        {
            return;
        }

        current = npc;
        currentShop = shop;
        currentDeparture = departure;
        openedFrame = Time.frameCount;
        root.SetActive(true);

        if (!IsOpen)
        {
            IsOpen = true;
            UIBlocker.DialogueOpen = true;

            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // ★ 조준선을 먼저 끈다. 순서를 바꾸면 커서가 안 보인다.
            if (crosshair != null)
            {
                crosshair.SetActive(false);
            }

            cursorWasVisible = Cursor.visible;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        Say(npc.greeting);
        Refresh();
    }

    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        current = null;
        currentShop = null;
        currentDeparture = null;
        ClearEntries();

        if (IsOpen)
        {
            RestoreWorld();
        }
    }

    private void RestoreWorld()
    {
        IsOpen = false;
        UIBlocker.DialogueOpen = false;

        Time.timeScale = (savedTimeScale <= 0f) ? 1f : savedTimeScale;

        if (crosshair != null)
        {
            crosshair.SetActive(true);
        }

        Cursor.visible = cursorWasVisible;
    }

    // ───────────────── 내용 ─────────────────

    private void Say(string line)
    {
        if (bodyText != null)
        {
            bodyText.text = string.IsNullOrEmpty(line) ? "" : line;
        }
    }

    // QuestManager.Changed가 부른다. 수락·완료 직후 목록이 즉시 바뀐다.
    public void Refresh()
    {
        if (current == null || root == null || !root.activeSelf)
        {
            return;
        }

        if (nameText != null)
        {
            nameText.text = current.npcName;
        }

        if (portrait != null)
        {
            portrait.sprite = current.portrait;
            portrait.enabled = (current.portrait != null);
        }

        
        RefreshTrade();
        RefreshDeparture();

        ClearEntries();

        List<QuestData> list = BuildList();

        for (int i = 0; i < list.Count; i++)
        {
            AddEntry(list[i]);
        }

        if (emptyLabel != null)
        {
            emptyLabel.SetActive(list.Count == 0);
        }

        if (list.Count == 0)
        {
            string line = string.IsNullOrEmpty(current.idleLine) ? current.greeting : current.idleLine;
            Say(line);
        }
    }

    // ───────────────── 거래 · 서비스 (10번) ─────────────────

    private void RefreshTrade()
    {
        MerchantData m = (current != null) ? current.merchant : null;

        // 거래 버튼 — 파는 게 있는 NPC에게만 뜬다. 네트워커에게는 안 뜬다.
        if (tradeButton != null)
        {
            tradeButton.gameObject.SetActive(m != null);
        }

        // 서비스 버튼 — 각자 알아서 켜고 끈다.
        // 가격이 0이거나 지금 필요 없는 서비스면 스스로 사라진다.
        if (serviceButtons != null)
        {
            for (int i = 0; i < serviceButtons.Length; i++)
            {
                if (serviceButtons[i] != null)
                {
                    serviceButtons[i].Bind(m);
                }
            }
        }

        if (walletLabel != null)
        {
            walletLabel.Refresh();
        }
    }

    private void RefreshDeparture()
    {
        if (departButton == null)
        {
            return;
        }

        bool show = currentDeparture != null;
        departButton.gameObject.SetActive(show);

        if (show)
        {
            Text label = departButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "출발";
            }
        }
    }


    private void OnClickTrade()
    {
        if (currentShop == null)
        {
            // 인스펙터에서 안 넘겨줬으면 대화 상대에게서 직접 찾는다.
            Debug.LogWarning("[DialogueUI] 이 NPC에 ShopController가 없다.", this);
            return;
        }

        // 대화창을 먼저 닫는다. 둘 다 timeScale을 0으로 만들기 때문에
        // 겹쳐 열면 나중에 닫는 쪽이 시간을 되돌려 놓지 못한다.
        NPCData npc = current;
        ShopController shop = currentShop;

        Close();

        shop.OpenTrade();

        // 거래가 끝나면 다시 대화로 돌아오게 하고 싶다면 여기서 기억해두면 된다.
        // 원작은 거래를 닫으면 그냥 게임으로 돌아가므로 그대로 둔다.
        if (npc == null)
        {
            return;
        }
    }

    private void OnClickDepart()
    {
        ZoneEntryPoint departure = currentDeparture;
        Close();

        if (departure != null)
        {
            departure.Depart();
        }
    }


    // 이 NPC가 지금 보여줄 퀘스트들.
    private List<QuestData> BuildList()
    {
        List<QuestData> list = new List<QuestData>();
        QuestManager qm = QuestManager.Instance;

        // 네트워커 — 오늘의 일일 임무
        if (current.role == NPCRole.Networker)
        {
            IReadOnlyList<QuestData> daily = qm.GetDailyOffers(current.dailyPool, current.dailyOfferCount);

            for (int i = 0; i < daily.Count; i++)
            {
                QuestData q = daily[i];

                // 오늘 이미 끝낸 것은 목록에서 뺀다. 내일이면 다시 뜬다.
                if (q != null && !qm.IsCompleted(q))
                {
                    list.Add(q);
                }
            }

            return list;
        }

        // 바텐더 · 의사 — 고정 퀘스트
        if (current.quests == null)
        {
            return list;
        }

        for (int i = 0; i < current.quests.Length; i++)
        {
            QuestData q = current.quests[i];

            if (q == null || qm.IsCompleted(q))
            {
                continue;
            }

            list.Add(q);

            // 순차 진행이면 지금 열린 하나만 보여준다.
            if (current.sequential)
            {
                break;
            }
        }

        return list;
    }

    private void AddEntry(QuestData q)
    {
        if (entryPrefab == null || entryRoot == null || q == null)
        {
            return;
        }

        QuestEntryUI e = Instantiate(entryPrefab, entryRoot);
        spawned.Add(e);

        QuestManager qm = QuestManager.Instance;

        if (!qm.IsAccepted(q))
        {
            e.Bind(q, "수락 가능", 1, OnClickQuest);
        }
        else if (qm.CanTurnIn(q))
        {
            e.Bind(q, "완료!", 2, OnClickQuest);
        }
        else
        {
            e.Bind(q, q.ProgressText(qm.GetProgress(q)), 0, OnClickQuest);
        }
    }

    private void OnClickQuest(QuestData q)
    {
        if (q == null)
        {
            return;
        }

        QuestManager qm = QuestManager.Instance;

        if (!qm.IsAccepted(q))
        {
            if (qm.Accept(q))
            {
                Say(string.IsNullOrEmpty(q.acceptLine) ? q.description : q.acceptLine);
            }

            return;
        }

        if (qm.CanTurnIn(q))
        {
            if (qm.TurnIn(q))
            {
                Say(string.IsNullOrEmpty(q.completeLine) ? "수고했다." : q.completeLine);
            }

            return;
        }

        // 진행 중인 것을 누르면 내용을 다시 읽어준다.
        Say(q.description);
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
