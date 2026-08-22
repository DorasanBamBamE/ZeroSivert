using UnityEngine;

// 바탕 지면(Draw Mode: Tiled)의 크기를 ZoneGenerator의 맵 크기에 맞춘다.
// 레이아웃을 고칠 때마다 Size를 손으로 바꾸지 않아도 된다.
//
// t_grass 스프라이트의 Pivot이 (0, 0)이어야 하고,
// 이 오브젝트와 ZoneGenerator의 Position이 같아야 한다.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ZoneGround : MonoBehaviour
{
    [SerializeField] private ZoneGenerator zone;

    // 맵 밖이 보이지 않도록 사방으로 더 깔 여유분(유닛).
    [SerializeField] private float margin = 0f;

    private SpriteRenderer sr;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    // 인스펙터에서 값을 바꾸면 에디터에서도 바로 반영된다.
    private void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        if (sr == null)
        {
            sr = GetComponent<SpriteRenderer>();
        }

        if (zone == null)
        {
            zone = FindFirstObjectByType<ZoneGenerator>();
        }

        if (sr == null || zone == null)
        {
            return;
        }

        if (sr.drawMode == SpriteDrawMode.Simple)
        {
            Debug.LogWarning("ZoneGround: Draw Mode를 Tiled로 바꿔야 크기가 적용된다.", this);
            return;
        }

        Vector2 size = zone.GetMapSize();
        sr.size = size + Vector2.one * (margin * 2f);

        // 여유분만큼 왼쪽 아래로 밀어 맵 원점을 맞춘다.
        transform.position = zone.transform.position - new Vector3(margin, margin, 0f);
    }
}