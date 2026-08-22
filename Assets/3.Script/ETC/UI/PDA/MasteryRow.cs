using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 총기 숙련도 한 종류를 표시하는 행. 원작 배치를 따른다.
//
//   [무기 아이콘 68×19]   ○ ○ ● ○ ○ ○ ● ○ ○ …
//
// 캡슐 하나가 레벨 하나다. 달성한 레벨은 filled 스프라이트,
// 마일스톤 레벨은 노란색으로 강조된다.
//
// 캡슐은 시작할 때 프리팹을 복제해 만든다. 컨테이너에는
// Horizontal Layout Group을 붙이고 Child Control / Force Expand는 꺼둘 것.
public class MasteryRow : MonoBehaviour
{
    [SerializeField] private WeaponMastery mastery;
    [SerializeField] private WeaponMastery.WeaponClass weaponClass;

    [Header("아이콘")]
    // s_hud_skills_gun_* (68×19). 종류별로 다른 스프라이트를 넣는다.
    [SerializeField] private Image icon;

    [Header("캡슐")]
    // 캡슐이 생성될 부모. Horizontal Layout Group 필요.
    [SerializeField] private RectTransform pipContainer;

    // 캡슐 프리팹. Image 하나짜리면 충분하다.
    [SerializeField] private Image pipPrefab;

    // s_hud_skills_pip_empty / _filled (각 5×13)
    [SerializeField] private Sprite pipEmpty;
    [SerializeField] private Sprite pipFilled;

    [Header("색상")]
    [SerializeField] private Color normalColor = Color.white;

    // 마일스톤 레벨(보너스 지급)은 노란색으로 구분한다.
    [SerializeField] private Color milestoneColor = new Color32(255, 216, 74, 255);

    private readonly List<Image> pips = new List<Image>();
    private int lastLevel = -1;

    private void Awake()
    {
        if (mastery == null)
        {
            mastery = FindFirstObjectByType<WeaponMastery>();
        }
    }

    // 패널이 켜질 때만 갱신한다. 꺼져 있으면 OnEnable이 호출되지 않는다.
    private void OnEnable()
    {
        Build();
        lastLevel = -1;
        Refresh();
    }

    private void Build()
    {
        if (pips.Count > 0 || mastery == null || pipContainer == null || pipPrefab == null)
        {
            return;
        }

        for (int i = 0; i < mastery.MaxLevel; i++)
        {
            Image pip = Instantiate(pipPrefab, pipContainer);
            pip.gameObject.SetActive(true);

            // 마일스톤 색은 레벨과 무관하게 고정이므로 생성 시 한 번만 정한다.
            int level = i + 1;
            pip.color = mastery.IsMilestone(level) ? milestoneColor : normalColor;

            pips.Add(pip);
        }
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (mastery == null)
        {
            return;
        }

        int level = mastery.Get(weaponClass).level;

        // 레벨은 자주 바뀌지 않으므로 변경 시에만 스프라이트를 갈아끼운다.
        if (level == lastLevel)
        {
            return;
        }

        lastLevel = level;

        for (int i = 0; i < pips.Count; i++)
        {
            if (pips[i] == null)
            {
                continue;
            }

            // 캡슐 i는 레벨 i+1을 나타낸다.
            bool reached = (i + 1) <= level;
            Sprite sprite = reached ? pipFilled : pipEmpty;

            if (sprite != null)
            {
                pips[i].sprite = sprite;
            }
        }
    }
}