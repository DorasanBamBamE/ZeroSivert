using UnityEngine;
using UnityEngine.UI;

// 발사 모드 표시 아이콘. 원작 s_hud_fire_mode_auto / s_hud_fire_mode_single (각 16×16).
// 퀵슬롯 옆이나 탄약 카운터 근처에 배치한다.
public class FireModeIcon : MonoBehaviour
{
    [SerializeField] private Weapon weapon;
    [SerializeField] private Image icon;

    [SerializeField] private Sprite autoSprite;
    [SerializeField] private Sprite singleSprite;

    private WeaponData.FireMode lastMode;
    private bool initialized;

    private void Awake()
    {
        if (icon == null)
        {
            icon = GetComponent<Image>();
        }
    }

    private void Update()
    {
        if (weapon == null || icon == null)
        {
            return;
        }

        WeaponData.FireMode mode = weapon.Mode;

        if (initialized && mode == lastMode)
        {
            return;
        }

        initialized = true;
        lastMode = mode;

        Sprite sprite = (mode == WeaponData.FireMode.Auto) ? autoSprite : singleSprite;

        if (sprite != null)
        {
            icon.sprite = sprite;
            icon.SetNativeSize();
        }
    }
}