using UnityEngine;
using UnityEngine.UI;

// 통계 목록의 한 줄. [항목명 ......... 숫자]
// 프리팹으로 만들어 PDAStatsPanel이 복제해 쓴다.
public class StatRow : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private Text value;

    public void SetLabel(string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }

    public void SetValue(string text)
    {
        if (value != null)
        {
            value.text = text;
        }
    }

    // 카테고리 제목 줄은 값을 숨기고 색을 다르게 한다.
    public void SetHeader(bool isHeader, Color color)
    {
        if (label != null)
        {
            label.color = color;
        }

        if (value != null)
        {
            value.enabled = !isHeader;
        }
    }
}