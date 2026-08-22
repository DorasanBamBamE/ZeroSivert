using TMPro;
using UnityEngine;

// 인게임 HUD. 스탯 바는 StatBar가 각자 처리하고 여기서는 탄약만 갱신한다.
public class HUD : MonoBehaviour
{
    [SerializeField] private Weapon weapon;
    [SerializeField] private TMP_Text ammoText;

    private void Update()
    {
        if (weapon == null || ammoText == null)
        {
            return;
        }

        if (weapon.IsReloading)
        {
            ammoText.text = "재장전 중";
            return;
        }

        ammoText.text = weapon.CurrentAmmo + " / " + weapon.MagazineSize;
    }
}