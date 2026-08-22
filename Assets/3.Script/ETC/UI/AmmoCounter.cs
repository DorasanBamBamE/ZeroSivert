using UnityEngine;
using UnityEngine.UI;

// 탄창의 남은 탄을 아이콘 하나당 한 발로 표시한다.
// 무기의 탄종에 따라 아이콘 스프라이트가 바뀐다.
// 컨테이너에는 Horizontal Layout Group을 붙여 아이콘 간격을 조절할 것.
public class AmmoCounter : MonoBehaviour
{
    [SerializeField] private Weapon weapon;
    [SerializeField] private RectTransform container;
    [SerializeField] private Image iconPrefab;

    [SerializeField] private Sprite smallLoaded;
    [SerializeField] private Sprite smallSpent;
    [SerializeField] private Sprite bigLoaded;
    [SerializeField] private Sprite bigSpent;
    [SerializeField] private Sprite shotgunLoaded;
    [SerializeField] private Sprite shotgunSpent;

    private Image[] icons;
    private int lastAmmo = -1;
    private WeaponData.AmmoType lastType;

    private void Start()
    {
        Build();
    }

    private void Update()
    {
        //Debug.Log($"mag={weapon?.MagazineSize} icons={icons?.Length}");
        if (weapon == null)
        {
            return;
        }

        // 무기 교체로 탄창 크기나 탄종이 바뀌면 아이콘을 다시 만든다.
        if (icons == null || icons.Length != weapon.MagazineSize || lastType != weapon.Ammo)
        {
            Build();
        }

        if (weapon.CurrentAmmo == lastAmmo)
        {
            return;
        }

        lastAmmo = weapon.CurrentAmmo;
        Refresh();
    }

    private void Build()
    {
        if (weapon == null || container == null || iconPrefab == null)
        {
            return;
        }

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }

        int count = weapon.MagazineSize;
        icons = new Image[count];

        for (int i = 0; i < count; i++)
        {
            icons[i] = Instantiate(iconPrefab, container);
            icons[i].gameObject.SetActive(true);
        }

        lastType = weapon.Ammo;
        lastAmmo = -1;
    }

    private Sprite GetSprite(bool loaded)
    {
        switch (lastType)
        {
            case WeaponData.AmmoType.Small:
                return loaded ? smallLoaded : smallSpent;
            case WeaponData.AmmoType.Shotgun:
                return loaded ? shotgunLoaded : shotgunSpent;
            default:
                return loaded ? bigLoaded : bigSpent;
        }
    }

    private void Refresh()
    {
        Sprite loaded = GetSprite(true);
        Sprite spent = GetSprite(false);

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null)
            {
                continue;
            }

            icons[i].sprite = (i < lastAmmo) ? loaded : spent;
            icons[i].SetNativeSize();
        }
    }
}