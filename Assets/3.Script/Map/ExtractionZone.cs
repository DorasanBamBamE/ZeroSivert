using UnityEngine;
using UnityEngine.UI;

// 존의 탈출 지점. 범위 안에서 키를 누르고 있으면 게이지가 차고,
// 다 차면 허브로 돌아간다. 중간에 벗어나면 게이지가 줄어든다.
//
// 원작처럼 "탈출 직전이 가장 위험한" 긴장을 만드는 장치다.
// 즉시 탈출로 하면 이 맛이 사라진다.
//
// ★ 11 - 진행바를 코드로 만든다.
//   E를 몇 초 누르는 동안 아무 피드백이 없으면 버그로 보인다.
//   progressFill을 손으로 물려도 되고, 비워두면 Awake에서 직접 만든다
//   (World Space Canvas · Scale 1/16 - LootPrompt와 같은 방식).
//
// ★ 11 - 플레어.
//   ExtractionFlare를 같이 붙이면 멀리서도 붉은 불빛과 연기가 보인다.
//   원작에서 탈출구를 찾는 유일한 단서가 이것이다.
//
// BoxCollider2D + Is Trigger. 플레이어에 Tag "Player" 필요.
[RequireComponent(typeof(Collider2D))]
public class ExtractionZone : MonoBehaviour
{
    [SerializeField] private string hubSceneName = "Hub";
    [SerializeField] private KeyCode holdKey = KeyCode.E;

    [SerializeField] private float holdTime = 3f;

    // 손을 떼거나 벗어났을 때 줄어드는 속도 배율.
    [SerializeField] private float decayMultiplier = 2f;

    [Header("UI (선택)")]
    [SerializeField] private GameObject prompt;
    [SerializeField] private Image progressFill;

    [Header("진행바 자동 생성")]
    // progressFill이 비어 있을 때 직접 만든다.
    [SerializeField] private bool buildBarIfMissing = true;

    // 바가 뜰 높이(유닛). 머리 위쯤.
    [SerializeField] private float barHeight = 1.6f;

    // 픽셀 단위 크기. PPU 16 기준이라 48x6이면 3x0.375 유닛이다.
    [SerializeField] private int barWidthPx = 48;
    [SerializeField] private int barHeightPx = 6;

    [SerializeField] private Color barBackColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color barFillColor = new Color32(90, 220, 110, 255);

    [Header("플레어")]
    // 탈출 지점 표식을 자동으로 붙인다. 이미 있으면 그대로 둔다.
    [SerializeField] private bool addFlare = true;

    private PlayerStats stats;
    private bool inRange;
    private float progress;
    private bool done;

    // Show()가 켜고 끌 대상. 자동 생성했을 때만 채워진다.
    private GameObject barRoot;

    private void Awake()
    {
        if (progressFill == null && buildBarIfMissing)
        {
            BuildBar();
        }

        if (addFlare && GetComponentInChildren<ExtractionFlare>(true) == null)
        {
            gameObject.AddComponent<ExtractionFlare>();
        }

        Show(false);
    }

    // ───────────────── 진행바 만들기 ─────────────────

    private void BuildBar()
    {
        // LootPrompt와 같은 구조 - World Space Canvas에 1/16 스케일.
        // 이렇게 해야 UI 1픽셀이 월드 1픽셀과 맞아떨어진다.
        GameObject canvasGO = new GameObject("Canvas_Progress");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, barHeight, 0f);
        canvasGO.transform.localScale = Vector3.one * (1f / 16f);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(barWidthPx, barHeightPx);

        // BG와 Fill을 한 부모 밑에 둔다.
        // Show()가 progressFill.transform.parent를 켜고 끄기 때문이다.
        GameObject holder = new GameObject("Bar");
        holder.transform.SetParent(canvasGO.transform, false);
        RectTransform holderRT = holder.AddComponent<RectTransform>();
        holderRT.anchorMin = new Vector2(0.5f, 0.5f);
        holderRT.anchorMax = new Vector2(0.5f, 0.5f);
        holderRT.pivot = new Vector2(0.5f, 0.5f);
        holderRT.anchoredPosition = Vector2.zero;
        holderRT.sizeDelta = new Vector2(barWidthPx, barHeightPx);

        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(holder.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = barBackColor;
        bgImg.raycastTarget = false;
        RectTransform bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(holder.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = barFillColor;
        fillImg.raycastTarget = false;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f;

        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        // 1픽셀 테두리를 남긴다. 배경이 살짝 보여야 바로 읽힌다.
        fillRT.offsetMin = new Vector2(1f, 1f);
        fillRT.offsetMax = new Vector2(-1f, -1f);

        progressFill = fillImg;
        barRoot = holder;
    }

    private void Update()
    {
        if (done)
        {
            return;
        }

        bool holding = inRange
                       && stats != null
                       && !stats.IsDead
                       && Input.GetKey(holdKey);

        if (holding)
        {
            progress += Time.deltaTime;
        }
        else
        {
            progress -= Time.deltaTime * decayMultiplier;
        }

        progress = Mathf.Clamp(progress, 0f, holdTime);

        if (progressFill != null)
        {
            progressFill.fillAmount = progress / holdTime;
        }

        if (progress >= holdTime)
        {
            Extract();
        }
    }

    private void Extract()
    {
        done = true;

        // 탈출 성공은 상태를 그대로 유지한 채 허브로 돌아간다.
        RunData.Instance.Save(stats);
        RunData.Instance.SetOutcome(RunData.Outcome.Extracted);

        // 살아 나왔으니 통계에 기록한다. 결과 화면의 경험치 줄이 여기서 나온다.
        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.EndRaidSurvived();
        }

        KillReport.Clear();

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.Load(hubSceneName);
        }
        else
        {
            Debug.LogWarning("ExtractionZone: 씬에 SceneLoader가 없다.", this);
        }
    }

    private void Show(bool on)
    {
        if (prompt != null)
        {
            prompt.SetActive(on);
        }

        if (barRoot != null)
        {
            barRoot.SetActive(on);
            return;
        }

        if (progressFill != null && progressFill.transform.parent != null)
        {
            progressFill.transform.parent.gameObject.SetActive(on);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        stats = other.GetComponent<PlayerStats>();
        inRange = true;
        Show(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        inRange = false;
        Show(false);
    }
}
