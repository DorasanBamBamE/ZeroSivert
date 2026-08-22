using UnityEngine;

// 지도 색표. 레이아웃 문자 하나에 색 하나를 대응시킨다.
// Project 창 우클릭 → Create → ZeroSievert → Map Palette
//
// ZoneGenerator가 이미 문자 격자로 맵을 만들고 있다. 그 격자를 그대로
// 한 칸 = 한 픽셀짜리 텍스처로 굽는 것이 이 지도의 전부다.
// 별도의 지도 데이터를 만들지 않는 이유가 이것이다 — 이미 있다.
[CreateAssetMenu(fileName = "MapPalette", menuName = "ZeroSievert/Map Palette")]
public class MapPalette : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("ZoneGenerator 레이아웃에서 쓰는 문자. 첫 글자만 본다.")]
        public string symbol = ".";

        [Tooltip("메모용")]
        public string label = "숲";

        public Color color = new Color(0.16f, 0.28f, 0.18f, 1f);

        public char Symbol
        {
            get { return string.IsNullOrEmpty(symbol) ? '\0' : symbol[0]; }
        }
    }

    public Entry[] entries;

    [Tooltip("맵 밖(공백 등). 알파 0이면 투명하게 뚫린다.")]
    public Color voidColor = new Color(0f, 0f, 0f, 0f);

    [Tooltip("팔레트에 없는 문자. 눈에 띄는 색으로 둬야 빠뜨린 걸 안다.")]
    public Color unknownColor = new Color(1f, 0f, 1f, 1f);

    public bool TryGet(char c, out Color color)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].Symbol == c)
                {
                    color = entries[i].color;
                    return true;
                }
            }
        }

        color = unknownColor;
        return false;
    }
}
