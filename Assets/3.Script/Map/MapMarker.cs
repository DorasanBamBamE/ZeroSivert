using System.Collections.Generic;
using UnityEngine;

// 지도에 점으로 표시될 월드 오브젝트.
// 탈출 지점 · 상인 · 관심 지역에 붙인다.
//
// 등록 방식이라 MapUI가 씬을 뒤지지 않는다. 오브젝트가 생기고 사라지는 것을
// 지도가 알아서 따라간다 — 시체나 상자에 붙여도 안전하다.
public class MapMarker : MonoBehaviour
{
    // 지금 씬에 살아 있는 마커 전부.
    private static readonly List<MapMarker> all = new List<MapMarker>();

    public static IReadOnlyList<MapMarker> All
    {
        get { return all; }
    }

    [Tooltip("지도에 찍힐 아이콘. 비우면 MapUI의 기본 점을 쓴다.")]
    [SerializeField] private Sprite icon;

    [SerializeField] private Color color = Color.white;

    [Tooltip("마우스를 올렸을 때 뜨는 이름. 비워도 된다.")]
    [SerializeField] private string label = "";

    [Tooltip("지도에서의 크기(픽셀). 0이면 MapUI 기본값.")]
    [SerializeField] private float size = 0f;

    public Sprite Icon { get { return icon; } }
    public Color Color { get { return color; } }
    public string Label { get { return label; } }
    public float Size { get { return size; } }

    private void OnEnable()
    {
        if (!all.Contains(this))
        {
            all.Add(this);
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
    }
}
