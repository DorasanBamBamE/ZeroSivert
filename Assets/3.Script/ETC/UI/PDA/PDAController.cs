using UnityEngine;
using UnityEngine.UI;

// PDA 화면. J 키로 열고 닫으며 탭을 전환한다.
//
// 화면 구조 — 홈과 탭 패널을 분리한다.
//   열면 홈(시간·세로바·상태 4행)이 보인다.
//   탭을 누르면 홈이 숨고 해당 패널이 뜬다.
//   같은 탭을 다시 누르거나 홈 키를 누르면 홈으로 돌아온다.
//   currentTab이 -1이면 홈 상태다.
//
// 탭 색 — 선택 흰색 / 비선택 회색 / 마우스 올림 노란색.
// 별도 선택 프레임을 씌우지 않고 색으로만 구분한다.
// 호버는 각 탭의 PDATabButton이 처리하므로 여기서는 선택 여부만 알려준다.
//
// tabPanels와 tabButtons는 배열 순서가 서로 일치해야 한다.
// 회색 고정인 미구현 탭은 배열에 넣지 않는다.
public class PDAController : MonoBehaviour
{
    // 홈 상태를 나타내는 탭 인덱스.
    private const int HomeTab = -1;

    [SerializeField] private GameObject root;

    // 홈 화면 묶음. 탭을 열면 숨긴다.
    [SerializeField] private GameObject home;

    [SerializeField] private GameObject[] tabPanels;
    [SerializeField] private Image[] tabButtons;

    [SerializeField] private KeyCode toggleKey = KeyCode.J;
    [SerializeField] private bool pauseWhileOpen = true;

    // PDATabButton이 없는 탭에 대한 대체 색상.
    [Header("탭 색상 (PDATabButton 없을 때만 사용)")]
    [SerializeField] private Color tabNormalTint = new Color32(140, 140, 140, 255);
    [SerializeField] private Color tabSelectedTint = Color.white;

    [Header("탭 이동 키")]
    // 원작은 탭을 마우스로 고른다. Q는 퀘스트 로그, E는 대화와 겹쳐서 비웠다.
    [SerializeField] private KeyCode prevTabKey = KeyCode.None;
    [SerializeField] private KeyCode nextTabKey = KeyCode.None;

    // 홈으로 돌아가는 키. 원작 s_pda_icon_back에 대응한다.
    [SerializeField] private KeyCode homeKey = KeyCode.Tab;

    private int currentTab = HomeTab;
    private bool isOpen;

    // Tab_Quest · Tab_Map처럼 PDA 바깥 화면을 여는 탭이 PDA를 잠시 접어둔 상태.
    // 그 화면이 닫히면 PDA를 다시 펼친다.
    private System.Func<bool> suspendProbe;
    private bool suspended;

    public bool IsOpen
    {
        get { return isOpen; }
    }

    public int CurrentTab
    {
        get { return currentTab; }
    }

    public bool IsHome
    {
        get { return currentTab == HomeTab; }
    }

    private void Awake()
    {
        Close();
    }

    private void Update()
    {
        // 바깥 화면(퀘스트 로그 · 지도)이 닫히면 PDA로 돌아온다.
        if (suspended)
        {
            if (suspendProbe != null && suspendProbe())
            {
                return;
            }

            suspended = false;
            suspendProbe = null;
            Open();
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
            return;
        }

        if (!isOpen)
        {
            return;
        }

        // 탭을 보고 있으면 ESC가 홈으로, 홈에서는 PDA를 닫는다.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsHome)
            {
                Close();
            }
            else
            {
                ShowHome();
            }

            return;
        }

        if (Input.GetKeyDown(homeKey))
        {
            ShowHome();
            return;
        }

        if (prevTabKey != KeyCode.None && Input.GetKeyDown(prevTabKey))
        {
            CycleTab(-1);
        }
        else if (nextTabKey != KeyCode.None && Input.GetKeyDown(nextTabKey))
        {
            CycleTab(1);
        }
    }

    private void Toggle()
    {
        if (isOpen)
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
        isOpen = true;

        // 09 - 다른 창이 PDA 상태를 알아야 Q/M이 겹치지 않는다.
        UIBlocker.PdaOpen = true;

        if (root != null)
        {
            root.SetActive(true);
        }

        // 열 때는 항상 홈부터 보여준다.
        ShowHome();

        if (pauseWhileOpen)
        {
            Time.timeScale = 0f;
        }
    }

    public void Close()
    {
        isOpen = false;
        UIBlocker.PdaOpen = false;

        if (root != null)
        {
            root.SetActive(false);
        }

        if (pauseWhileOpen)
        {
            Time.timeScale = 1f;
        }
    }

    // 홈 화면으로 돌아간다. 홈 버튼의 OnClick에 연결해도 된다.
    public void ShowHome()
    {
        currentTab = HomeTab;
        ApplyState();
    }

    // 탭 버튼의 OnClick에서 인덱스를 넘겨 호출한다.
    public void SelectTab(int index)
    {
        if (tabPanels == null || tabPanels.Length == 0)
        {
            return;
        }

        int clamped = Mathf.Clamp(index, 0, tabPanels.Length - 1);

        // 이미 보고 있는 탭을 다시 누르면 홈으로 돌아간다.
        currentTab = (currentTab == clamped) ? HomeTab : clamped;

        ApplyState();
    }

    private void CycleTab(int direction)
    {
        if (tabPanels == null || tabPanels.Length == 0)
        {
            return;
        }

        // 홈에서 시작하면 양 끝 탭으로 진입한다.
        if (IsHome)
        {
            currentTab = (direction > 0) ? 0 : tabPanels.Length - 1;
            ApplyState();
            return;
        }

        int next = currentTab + direction;

        // 양 끝을 넘어가면 홈으로 빠진다.
        if (next < 0 || next >= tabPanels.Length)
        {
            ShowHome();
            return;
        }

        currentTab = next;
        ApplyState();
    }

    private void ApplyState()
    {
        //Debug.Log($"currentTab={currentTab} home={home} panels={tabPanels?.Length}");

        if (home != null)
        {
            home.SetActive(IsHome);
        }

        if (tabPanels != null)
        {
            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null)
                {
                    tabPanels[i].SetActive(i == currentTab);
                    //Debug.Log($"panel[{i}] {tabPanels[i].name} → {i == currentTab} / 실제 activeInHierarchy={tabPanels[i].activeInHierarchy}");

                }
            }
        }

        UpdateTabButtons();
    }

    private void UpdateTabButtons()
    {
        if (tabButtons == null)
        {
            return;
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null)
            {
                continue;
            }

            bool selected = (i == currentTab);

            // 호버 처리가 붙어 있으면 색은 그쪽에 맡긴다.
            PDATabButton tab = tabButtons[i].GetComponent<PDATabButton>();

            if (tab != null)
            {
                tab.SetSelected(selected);
            }
            else
            {
                tabButtons[i].color = selected ? tabSelectedTint : tabNormalTint;
            }
        }
    }

    // PDA 바깥 화면을 띄우기 위해 PDA를 접는다. screenStillOpen이 false가 되는
    // 순간 PDA가 다시 열린다. PDAScreenTab이 부른다.
    public void SuspendFor(System.Func<bool> screenStillOpen)
    {
        Close();

        suspendProbe = screenStillOpen;
        suspended = true;
    }

    // 원작 옵션 "Automatically close the PDA when hit" 대응.
    // PlayerStats.TakeDamage()에서 호출하면 된다.
    public void CloseIfOpen()
    {
        if (isOpen)
        {
            Close();
        }
    }
}