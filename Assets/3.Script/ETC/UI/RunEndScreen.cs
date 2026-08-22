using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 출격 결과 화면. 원작의 "교전 중 사망"(붉은색) · "생존함"(초록색) 두 장이다.
//
// 원작 구성 그대로 간다.
//   제목  →  +경험치  →  레벨 게이지(좌 현재레벨 / 우 다음레벨)  →
//   현재/다음 레벨 수치  →  사망이면 사인 3줄 / 생존이면 루블 수익  →  건너뛰기
//
// ★ 두 씬에 모두 둔다.
//   사망은 존에서 뜨고(RunEndHandler가 부른다),
//   생존은 허브로 돌아온 뒤에 뜬다(허브의 RunEndScreen이 Start에서 스스로 확인).
//
// ★ 시간이 멈춘 상태에서도 돌아야 한다.
//   사망·결과 화면은 timeScale을 0으로 잡으므로 페이드는 unscaledDeltaTime을 쓴다.
//   일반 deltaTime을 쓰면 화면이 영원히 투명한 채로 멈춘다 - 04에서 겪은 함정이다.
public class RunEndScreen : MonoBehaviour
{
    public static RunEndScreen Instance { get; private set; }

    [Header("루트")]
    // 켜고 끌 패널. 이 스크립트는 항상 켜져 있는 부모에 붙인다.
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup group;

    [Header("제목")]
    [SerializeField] private Text titleText;
    [SerializeField] private string deathTitle = "교전 중 사망";
    [SerializeField] private string surviveTitle = "생존함";

    [Header("경험치")]
    [SerializeField] private Text expGainText;      // "+10"
    [SerializeField] private Text expCaptionText;   // "경험치"
    [SerializeField] private Text levelFromText;    // 게이지 왼쪽 숫자
    [SerializeField] private Text levelToText;      // 게이지 오른쪽 숫자
    [SerializeField] private Image expFill;
    [SerializeField] private Text currentExpText;   // "현재: 360"
    [SerializeField] private Text nextExpText;      // "다음 레벨: 500"

    [Header("사망 정보")]
    [SerializeField] private Text causeText;        // "사망 원인: ..."
    [SerializeField] private Text weaponText;       // "Weapon: ..."
    [SerializeField] private Text ammoText;         // "탄약: ..."

    [Header("생존 정보")]
    [SerializeField] private Text rublesText;       // "대략적인 루블 수익: 15055"

    [Header("색")]
    // 원작은 사망이 붉고 생존이 초록이다. 제목·발광·게이지가 함께 물든다.
    [SerializeField] private Color deathColor = new Color32(220, 30, 30, 255);
    [SerializeField] private Color surviveColor = new Color32(40, 210, 60, 255);

    // 제목 뒤에 깔리는 넓은 발광. 없어도 된다.
    [SerializeField] private Image glow;

    [Header("버튼")]
    [SerializeField] private Button skipButton;
    [SerializeField] private Text skipLabel;

    [Header("동작")]
    [SerializeField] private float fadeTime = 1f;

    // 이 시간이 지나기 전에는 건너뛰기를 눌러도 넘어가지 않는다.
    // 페이드가 끝나기도 전에 실수로 클릭해 화면을 못 보는 일을 막는다.
    [SerializeField] private float minShowTime = 1.2f;

    [SerializeField] private bool pauseWhileOpen = true;

    // 허브에서 "생존함"을 스스로 띄울지. 존 쪽 인스턴스는 꺼둔다.
    [SerializeField] private bool showSurvivedOnStart = false;

    public static bool IsOpen { get; private set; }

    private System.Action onSkip;
    private float shownAt;
    private bool skipped;

    private void Awake()
    {
        Instance = this;

        if (root != null)
        {
            root.SetActive(false);
        }

        if (group == null && root != null)
        {
            group = root.GetComponent<CanvasGroup>();
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(Skip);
            skipButton.onClick.AddListener(Skip);
        }

        if (skipLabel != null && string.IsNullOrEmpty(skipLabel.text))
        {
            skipLabel.text = "건너뛰기";
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        if (IsOpen)
        {
            IsOpen = false;

            if (pauseWhileOpen)
            {
                Time.timeScale = 1f;
            }
        }
    }

    private void Start()
    {
        if (!showSurvivedOnStart)
        {
            return;
        }

        // 허브로 돌아온 직후에만 뜬다. 한 번 보여주면 결과를 지운다.
        RunData run = RunData.Instance;

        if (run == null || run.LastOutcome != RunData.Outcome.Extracted)
        {
            return;
        }

        run.SetOutcome(RunData.Outcome.None);
        ShowSurvived();
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // 아무 키나 눌러도 넘어간다. 원작도 그렇다.
        if (Time.unscaledTime - shownAt >= minShowTime
            && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.Escape)))
        {
            Skip();
        }
    }

    // ───────────────── 띄우기 ─────────────────

    public void ShowDeath()
    {
        ShowDeath(null);
    }

    public void ShowDeath(System.Action skipAction)
    {
        onSkip = skipAction;
        Show(true);
    }

    public void ShowSurvived()
    {
        ShowSurvived(null);
    }

    public void ShowSurvived(System.Action skipAction)
    {
        onSkip = skipAction;
        Show(false);
    }

    private void Show(bool died)
    {
        if (root == null)
        {
            // 화면이 없어도 흐름은 막지 않는다. 바로 다음 단계로 넘긴다.
            Debug.LogWarning("[RunEndScreen] root가 비어 있다.", this);
            Finish();
            return;
        }

        skipped = false;
        root.SetActive(true);
        IsOpen = true;
        shownAt = Time.unscaledTime;

        Color tint = died ? deathColor : surviveColor;

        if (titleText != null)
        {
            titleText.text = died ? deathTitle : surviveTitle;
            titleText.color = tint;
        }

        if (glow != null)
        {
            Color g = tint;
            g.a = 0.35f;
            glow.color = g;
        }

        FillExp(tint);
        FillCause(died);
        FillRubles(died);

        if (pauseWhileOpen)
        {
            Time.timeScale = 0f;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        if (group == null)
        {
            yield break;
        }

        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;

        float t = 0f;

        while (t < fadeTime)
        {
            // timeScale이 0이므로 반드시 unscaled를 쓴다.
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / fadeTime);
            yield return null;
        }

        group.alpha = 1f;
    }

    // ───────────────── 내용 채우기 ─────────────────

    private void FillExp(Color tint)
    {
        PlayerLevel lv = PlayerLevel.Instance;

        int gained = (lv != null) ? lv.RaidExp : 0;
        int level = (lv != null) ? lv.Level : 1;
        int cur = (lv != null) ? lv.CurrentExp : 0;
        int need = (lv != null) ? lv.ExpForNextLevel : 0;

        if (expGainText != null)
        {
            expGainText.text = "+" + gained;
            expGainText.color = tint;
        }

        if (expCaptionText != null)
        {
            expCaptionText.text = "경험치";
        }

        if (levelFromText != null)
        {
            levelFromText.text = level.ToString();
        }

        if (levelToText != null)
        {
            levelToText.text = (level + 1).ToString();
        }

        if (expFill != null)
        {
            expFill.fillAmount = (lv != null) ? lv.ExpRatio : 0f;
            expFill.color = tint;
        }

        if (currentExpText != null)
        {
            currentExpText.text = "현재: " + cur;
        }

        if (nextExpText != null)
        {
            nextExpText.text = "다음 레벨: " + need;
        }
    }

    private void FillCause(bool died)
    {
        if (causeText != null)
        {
            causeText.gameObject.SetActive(died);

            if (died)
            {
                causeText.text = "사망 원인: " + KillReport.AttackerText;
            }
        }

        if (weaponText != null)
        {
            bool on = died && KillReport.HasWeapon;
            weaponText.gameObject.SetActive(on);

            if (on)
            {
                weaponText.text = "Weapon: " + KillReport.Weapon;
            }
        }

        if (ammoText != null)
        {
            bool on = died && KillReport.HasAmmo;
            ammoText.gameObject.SetActive(on);

            if (on)
            {
                ammoText.text = "탄약: " + KillReport.Ammo;
            }
        }
    }

    private void FillRubles(bool died)
    {
        if (rublesText == null)
        {
            return;
        }

        rublesText.gameObject.SetActive(!died);

        if (died)
        {
            return;
        }

        // 원작의 "대략적인 루블 수익" - 들고 나온 물건을 다 팔면 받을 값이다.
        rublesText.text = "대략적인 루블 수익: " + EstimateHaulValue();
    }

    private static int EstimateHaulValue()
    {
        RunData run = RunData.Instance;

        if (run == null || !run.HasSnapshot)
        {
            return 0;
        }

        int total = 0;
        var list = run.Snapshot.inventory;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || list[i].item == null)
            {
                continue;
            }

            total += Mathf.Max(0, list[i].item.basePrice) * Mathf.Max(1, list[i].count);
        }

        return total;
    }

    // ───────────────── 넘기기 ─────────────────

    public void Skip()
    {
        if (skipped)
        {
            return;
        }

        if (Time.unscaledTime - shownAt < minShowTime)
        {
            return;
        }

        skipped = true;
        Finish();
    }

    private void Finish()
    {
        IsOpen = false;

        if (root != null)
        {
            root.SetActive(false);
        }

        if (pauseWhileOpen)
        {
            Time.timeScale = 1f;
        }

        System.Action a = onSkip;
        onSkip = null;

        if (a != null)
        {
            a();
        }
    }
}
