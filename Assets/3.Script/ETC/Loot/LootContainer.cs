using UnityEngine;

// 루팅 가능한 대상. 상자와 시체가 같은 스크립트를 쓴다.
//
// 붙이는 법
//   상자 — 프리팹에 그냥 붙인다. Is Trigger 콜라이더 필요
//   시체 — 적 프리팹에 붙이되 컴포넌트를 꺼둔다. EnemyHealth가 사망 시 켠다
//
// 전리품은 씬 로드 때가 아니라 처음 열 때 굴린다. 존 하나에 컨테이너가
// 수십 개일 때 로드 시점에 전부 굴리면 프레임이 튄다.
[RequireComponent(typeof(Collider2D))]
public class LootContainer : MonoBehaviour
{
    [Header("표시")]
    // 우측 패널 머리글에 뜨는 이름. 원작처럼 "탄약 상자", "시체" 등을 넣는다.
    // 비워두면 GROUND로 뜬다.
    [SerializeField] private string displayName = "";

    [Header("전리품")]
    [SerializeField] private LootTable table;

    [Header("컨테이너 격자")]
    [SerializeField] private int gridWidth = 6;
    [SerializeField] private int gridHeight = 6;

    [Header("입력")]
    [SerializeField] private KeyCode useKey = KeyCode.E;

    // "E — 열기" 안내. 없으면 비워둬도 된다.
    [SerializeField] private GameObject prompt;

    [Header("연출 (선택)")]
    // 한 번 연 뒤 바뀔 스프라이트. 뚜껑 열린 상자 등.
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Sprite openedSprite;

    private InventoryController contents;
    private InventoryScreen screen;
    private bool rolled;
    private bool inRange;

    // 11 - 테이블과 별개로 확정 지급할 물건. 적이 들고 있던 총이 여기로 들어온다.
    private System.Collections.Generic.List<ItemData> extraItems;
    private System.Collections.Generic.List<int> extraCounts;

    // 11 - 시체가 겹쳤을 때 어느 것을 뒤지는지 구분되지 않던 문제.
    //   범위 안에 든 컨테이너를 전부 모아 두고, 매 프레임 플레이어에게
    //   가장 가까운 하나만 안내를 띄우고 E를 받는다.
    private static readonly System.Collections.Generic.List<LootContainer> inRangeAll
        = new System.Collections.Generic.List<LootContainer>();

    private static LootContainer focused;
    private static int focusFrame = -1;

    private static Transform playerTf;

    public static LootContainer Focused
    {
        get { return focused; }
    }

    // 이 프레임의 초점을 아직 안 골랐으면 고른다.
    private static void ResolveFocus()
    {
        if (focusFrame == Time.frameCount)
        {
            return;
        }

        focusFrame = Time.frameCount;

        if (playerTf == null)
        {
            PlayerStats ps = FindFirstObjectByType<PlayerStats>();
            playerTf = (ps != null) ? ps.transform : null;
        }

        focused = null;
        float best = float.MaxValue;

        for (int i = inRangeAll.Count - 1; i >= 0; i--)
        {
            LootContainer c = inRangeAll[i];

            if (c == null || !c.isActiveAndEnabled)
            {
                inRangeAll.RemoveAt(i);
                continue;
            }

            if (playerTf == null)
            {
                focused = c;
                break;
            }

            float d = ((Vector2)c.transform.position - (Vector2)playerTf.position).sqrMagnitude;

            if (d < best)
            {
                best = d;
                focused = c;
            }
        }
    }

    private void Enter()
    {
        if (!inRangeAll.Contains(this))
        {
            inRangeAll.Add(this);
        }
    }

    private void Leave()
    {
        inRangeAll.Remove(this);

        if (focused == this)
        {
            focused = null;
            focusFrame = -1;
        }
    }

    // 이 컨테이너의 내용물. 아직 안 열었으면 비어 있다.
    public InventoryController Contents
    {
        get { return contents; }
    }

    // 11 - 굴림과 무관하게 반드시 들어갈 물건을 예약한다.
    //   이미 연 뒤에 부르면 바로 넣는다. WeaponDrop이 사망 시 호출한다.
    public void AddExtra(ItemData item, int count)
    {
        if (item == null || count <= 0)
        {
            return;
        }

        if (rolled)
        {
            EnsureContents();
            contents.TryAdd(item, count);
            return;
        }

        if (extraItems == null)
        {
            extraItems = new System.Collections.Generic.List<ItemData>();
            extraCounts = new System.Collections.Generic.List<int>();
        }

        extraItems.Add(item);
        extraCounts.Add(count);
    }

    private void Awake()
    {
        if (prompt != null)
        {
            prompt.SetActive(false);
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
        }
    }

    // 시체는 죽는 순간 켜진다. 그때 플레이어가 이미 시체 위에 서 있으면
    // OnTriggerEnter2D가 다시 불리지 않아서 영영 뒤질 수 없다.
    // 켜지는 시점에 직접 겹침을 확인해야 한다.
    private void OnEnable()
    {
        RefreshRange();
    }

    private void RefreshRange()
    {
        inRange = false;

        Collider2D[] cols = GetComponents<Collider2D>();

        for (int i = 0; i < cols.Length && !inRange; i++)
        {
            if (cols[i] == null || !cols[i].isTrigger || !cols[i].enabled)
            {
                continue;
            }

            Bounds b = cols[i].bounds;
            Collider2D[] hits = Physics2D.OverlapAreaAll(b.min, b.max);

            for (int k = 0; k < hits.Length; k++)
            {
                if (hits[k] != null && hits[k].CompareTag("Player"))
                {
                    inRange = true;
                    break;
                }
            }
        }

        if (inRange)
        {
            Enter();
        }
        else
        {
            Leave();
        }

        if (prompt != null)
        {
            prompt.SetActive(inRange);
        }
    }

    private void OnDisable()
    {
        Leave();

        // 시체가 비활성 상태로 시작하는 경우를 대비해 안내를 정리한다.
        if (prompt != null)
        {
            prompt.SetActive(false);
        }

        inRange = false;
    }

    private void Update()
    {
        if (!inRange)
        {
            return;
        }

        // 겹친 것들 중 가장 가까운 하나만 안내를 띄우고 반응한다.
        ResolveFocus();

        bool mine = (focused == this);

        if (prompt != null && prompt.activeSelf != mine)
        {
            prompt.SetActive(mine);
        }

        if (!mine)
        {
            return;
        }

        // 창이 이미 열려 있으면 E를 먹지 않는다.
        // 안 그러면 상자 위에서 인벤토리를 열어둔 채 E를 눌러 창이 겹친다.
        if (InventoryScreen.IsOpen || Time.timeScale == 0f)
        {
            return;
        }

        // NPC와 상자가 겹쳐 있으면 대화가 열린 프레임에 상자도 열리려 한다.
        if (DialogueUI.IsOpen)
        {
            return;
        }

        if (Input.GetKeyDown(useKey))
        {
            Open();
        }
    }

    // 내용물 그릇을 자식 오브젝트에 만든다.
    // 이 오브젝트에 직접 붙이면 적의 다른 컴포넌트와 섞여서 헷갈린다.
    private void EnsureContents()
    {
        if (contents != null)
        {
            return;
        }

        GameObject go = new GameObject("Contents");
        go.transform.SetParent(transform, false);

        contents = go.AddComponent<InventoryController>();

        // 컨테이너는 무게 제한이 없다.
        contents.Configure(gridWidth, gridHeight, 99999f);
    }

    public void Open()
    {
        EnsureContents();

        // 처음 열 때 한 번만 굴린다. 닫았다 다시 열어도 내용이 같다.
        if (!rolled)
        {
            rolled = true;

            if (table != null)
            {
                table.RollInto(contents);
            }

            // 확정 물건은 굴림 뒤에 넣는다. 자리를 먼저 뺏기지 않게 하려는 것이 아니라,
            // 굴림 결과와 섞여도 자리 배치가 자연스럽기 때문이다.
            if (extraItems != null)
            {
                for (int i = 0; i < extraItems.Count; i++)
                {
                    contents.TryAdd(extraItems[i], extraCounts[i]);
                }

                extraItems = null;
                extraCounts = null;
            }

            ApplyOpenedSprite();

            // 통계 — 처음 연 것만 센다. 닫았다 다시 열어도 중복으로 안 오른다.
            if (GameStats.Instance != null)
            {
                GameStats.Instance.Add(GameStats.StatId.ContainersOpened);
            }
        }

        // 반드시 창을 열기 전에 대상을 지정한다.
        // InventoryScreen이 열릴 때 LootTarget.Current를 읽어 GROUND에 물린다.
        LootTarget.Set(contents, displayName);

        if (screen == null)
        {
            screen = FindFirstObjectByType<InventoryScreen>();
        }

        // 창을 못 열었으면 대상 지정을 풀어야 한다.
        // 안 그러면 다음에 그냥 Tab을 눌렀을 때 열지도 않은 상자가 우측에 뜬다.
        if (screen == null)
        {
            LootTarget.Clear();
            return;
        }

        screen.Open();

        if (!InventoryScreen.IsOpen)
        {
            LootTarget.Clear();
        }
    }

    private void ApplyOpenedSprite()
    {
        if (bodyRenderer != null && openedSprite != null)
        {
            bodyRenderer.sprite = openedSprite;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        inRange = true;
        Enter();

        if (prompt != null)
        {
            prompt.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        inRange = false;
        Leave();

        if (prompt != null)
        {
            prompt.SetActive(false);
        }

        // 열어둔 채로 멀어지면 창을 닫는다.
        if (contents != null && LootTarget.Current == contents && InventoryScreen.IsOpen)
        {
            if (screen == null)
            {
                screen = FindFirstObjectByType<InventoryScreen>();
            }

            if (screen != null)
            {
                screen.Close();
            }
        }
    }
}
