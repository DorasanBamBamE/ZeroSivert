using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// PDA 통계 탭. 원작 stats 탭에 대응한다.
//
//   상단   Unknown Hunter / 경험치 바 / 현재: 368  다음 레벨: 500
//   본문   [항목명 ......... 숫자] 목록, 스크롤 가능
//
// 행은 프리팹 하나를 복제해 만든다. 표시할 항목은 인스펙터의 entries에 등록한다.
// 항목을 추가하려면 GameStats.StatId에 넣고 여기 목록에 라벨과 함께 추가하면 된다.
public class PDAStatsPanel : MonoBehaviour
{
    // 목록에 넣을 한 줄. 헤더는 값 없이 제목만 표시한다.
    [Serializable]
    public class Entry
    {
        public string label;
        public GameStats.StatId id;

        // 체크하면 카테고리 제목 줄이 된다. id는 무시된다.
        public bool isHeader;
    }

    [Header("상단")]
    [SerializeField] private Text hunterNameLabel;
    [SerializeField] private Image expFill;
    [SerializeField] private Text currentExpLabel;
    [SerializeField] private Text nextLevelLabel;

    [Header("목록")]
    // ScrollRect의 Content. Vertical Layout Group + Content Size Fitter 필요.
    [SerializeField] private RectTransform content;

    // 행 프리팹. StatRow 컴포넌트가 붙어 있어야 한다.
    [SerializeField] private StatRow rowPrefab;

    [SerializeField] private Entry[] entries;

    [Header("헤더 색상")]
    [SerializeField] private Color headerColor = new Color32(255, 216, 74, 255);
    [SerializeField] private Color normalColor = Color.white;

    private readonly List<StatRow> rows = new List<StatRow>();
    private bool built;

    // 패널이 켜질 때만 갱신한다. 꺼져 있으면 OnEnable이 호출되지 않는다.
    private void OnEnable()
    {
        Build();
        Refresh();
    }

    private void Build()
    {
        if (built || content == null || rowPrefab == null || entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            StatRow row = Instantiate(rowPrefab, content);
            row.gameObject.SetActive(true);
            row.SetLabel(entries[i].label);
            row.SetHeader(entries[i].isHeader, entries[i].isHeader ? headerColor : normalColor);
            rows.Add(row);
        }

        built = true;
    }

    private void Refresh()
    {
        RefreshHeader();
        RefreshRows();
    }

    private void RefreshHeader()
    {
        PlayerLevel player = PlayerLevel.Instance;

        if (player == null)
        {
            return;
        }

        if (hunterNameLabel != null)
        {
            hunterNameLabel.text = player.HunterName;
        }

        if (expFill != null)
        {
            expFill.fillAmount = player.ExpRatio;
        }

        if (currentExpLabel != null)
        {
            currentExpLabel.text = "현재: " + player.CurrentExp;
        }

        if (nextLevelLabel != null)
        {
            nextLevelLabel.text = "다음 레벨: " + player.ExpForNextLevel;
        }
    }

    private void RefreshRows()
    {
        GameStats stats = GameStats.Instance;

        if (stats == null || entries == null)
        {
            return;
        }

        int count = Mathf.Min(rows.Count, entries.Length);

        for (int i = 0; i < count; i++)
        {
            if (entries[i].isHeader)
            {
                rows[i].SetValue("");
                continue;
            }

            rows[i].SetValue(stats.Get(entries[i].id).ToString());
        }
    }
}