using UnityEngine;

// Tab키로 여닫는 인벤토리 화면. PDA(J키)와는 별개의 창이다.
//
// ── 씬 구성 ─────────────────────────────────────
// InventoryScreen        ← 이 스크립트. 항상 켜져 있어야 Tab을 감지한다
//   └ Root               ← toggleRoot. 이걸 켜고 끈다. InventoryUI.cs가 여기 붙는다
//        ├ Backdrop      (화면 전체를 덮는 반투명 검정)
//        ├ Panel_Left    (INVENTORY — 내 가방)
//        ├ Panel_Right   (GROUND — 08 루팅에서 채운다. 오늘은 빈 틀)
//        └ DragLayer
//
// InventoryScreen을 Root에 붙이면 창을 닫는 순간 스크립트도 꺼져서
// 다시 열 수 없게 된다. 반드시 부모에 붙일 것.
public class InventoryScreen : MonoBehaviour
{
    // 다른 시스템이 "지금 인벤토리가 열려 있나"를 물어보는 창구.
    // 재장전(R) 같은 게임플레이 입력을 막는 데 쓴다.
    public static bool IsOpen { get; private set; }

    [Header("참조")]
    [SerializeField] private GameObject toggleRoot;

    // 08 루팅 — 우측 GROUND 패널의 InventoryUI.
    // 창이 열릴 때 LootTarget이 가리키는 대상(상자 / 시체 / 지면)을 물려준다.
    [SerializeField] private InventoryUI groundUI;

    // 10번 — 거래 중에만 켜지는 줄. 루블 잔액과 거래 안내를 여기 둔다.
    // 없어도 된다.
    [SerializeField] private GameObject shopOnlyRoot;

    [Header("입력")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private bool closeWithEscape = true;

    [Header("동작")]
    // 원작처럼 인벤토리를 열면 시간이 멈춘다.
    [SerializeField] private bool pauseGame = true;

    [Header("커서")]
    // 게임 중에는 크로스헤어가 마우스를 대신하고 OS 커서는 숨겨져 있다.
    // 창이 열리면 크로스헤어를 끄고 진짜 커서를 되돌려야 드래그를 할 수 있다.
    // Canvas > Crosshair 오브젝트를 여기 넣을 것.
    [SerializeField] private GameObject crosshair;

    [SerializeField] private bool showCursorWhileOpen = true;

    private float savedTimeScale = 1f;
    private bool savedCursorVisible;
    private CursorLockMode savedCursorLock;

    private void Awake()
    {
        if (toggleRoot != null)
        {
            toggleRoot.SetActive(false);
        }

        IsOpen = false;
    }

    private void OnDisable()
    {
        // 씬 전환 등으로 이 오브젝트가 꺼질 때 시간이 멈춘 채 남지 않게 한다.
        if (IsOpen)
        {
            Close();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
            return;
        }

        if (IsOpen && closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (IsOpen || toggleRoot == null)
        {
            return;
        }

        // PDA 등 다른 창이 이미 시간을 멈춰놨으면 열지 않는다.
        // 둘이 겹치면 하나를 닫을 때 timeScale이 0으로 남는다.
        if (pauseGame && Time.timeScale == 0f)
        {
            return;
        }

        IsOpen = true;
        toggleRoot.SetActive(true);

        // 우측 패널에 지금 봐야 할 것을 물린다.
        // LootContainer가 Open()을 부르기 전에 LootTarget.Set()을 해두므로,
        // 상자를 열었으면 상자 내용물이, 그냥 Tab이면 지면이 들어온다.
        if (groundUI != null)
        {
            groundUI.SetController(LootTarget.Current);
            groundUI.SetTitle(LootTarget.CurrentName);
        }

        // 10번 — 거래 중일 때만 뜨는 줄(잔액·안내). 없으면 그냥 넘어간다.
        if (shopOnlyRoot != null)
        {
            shopOnlyRoot.SetActive(ShopSession.IsOpen);
        }

        if (pauseGame)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // 크로스헤어를 먼저 끈다. 오브젝트가 꺼지면 그쪽 Update가 멈추므로
        // 아래에서 되돌린 Cursor.visible을 매 프레임 다시 false로 덮어쓰지 않는다.
        if (crosshair != null)
        {
            crosshair.SetActive(false);
        }

        if (showCursorWhileOpen)
        {
            savedCursorVisible = Cursor.visible;
            savedCursorLock = Cursor.lockState;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Close()
    {
        if (toggleRoot != null)
        {
            toggleRoot.SetActive(false);
        }

        if (pauseGame && IsOpen)
        {
            Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
        }

        if (IsOpen && showCursorWhileOpen)
        {
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }

        if (crosshair != null)
        {
            crosshair.SetActive(true);
        }

        // 10번 — 거래도 같이 끝난다.
        // ★ LootTarget.Clear()보다 먼저 불러야 한다. 순서를 바꾸면
        //   ShopSession이 이미 사라진 재고를 가리킨 채 남는다.
        if (ShopSession.IsOpen)
        {
            ShopSession.Close();
        }

        if (shopOnlyRoot != null)
        {
            shopOnlyRoot.SetActive(false);
        }

        // 창을 닫으면 상자 지정이 풀린다. 다음에 그냥 Tab을 누르면 지면이 보인다.
        LootTarget.Clear();

        IsOpen = false;
    }
}
