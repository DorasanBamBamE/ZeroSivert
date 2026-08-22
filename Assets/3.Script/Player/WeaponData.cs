using UnityEngine;

// 무기 하나의 데이터. 에셋 파일로 만들어 관리한다.
//
// Project 창 우클릭 → Create → ZeroSievert → Weapon Data 로 생성.
// 무기 12정이면 에셋 12개를 만들고, Weapon 컴포넌트가 이걸 갈아끼워 쓴다.
//
// 무기마다 프리팹을 만들지 않는 이유는 관리 비용 때문이다.
// 총 오브젝트는 하나로 두고 스프라이트와 수치만 교체한다.
[CreateAssetMenu(fileName = "WeaponData", menuName = "ZeroSievert/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public enum FireMode
    {
        Auto,
        Semi,
    }

    public enum AmmoType
    {
        Small,
        Big,
        Shotgun,
    }

    [Header("표시")]
    public string displayName = "무기";

    // 인게임에서 손에 들리는 스프라이트. 원작 s_*_game 계열.
    // Pivot을 손잡이 위치로 잡아야 조준 회전이 자연스럽다.
    public Sprite gameSprite;

    // 인벤토리 아이콘. 원작 s_*_inv 계열. 인벤토리 작업 전까지는 비워둬도 된다.
    public Sprite inventorySprite;

    [Header("분류")]
    public WeaponMastery.WeaponClass weaponClass = WeaponMastery.WeaponClass.Rifle;
    public AmmoType ammoType = AmmoType.Small;

    [Header("사격")]
    public float damage = 20f;

    // 연사 간격(초). 작을수록 빠르다.
    public float fireRate = 0.1f;

    public float bulletSpeed = 18f;
    public float bulletLifeTime = 1.5f;

    public int magazineSize = 30;
    public float reloadTime = 2f;

    public FireMode fireMode = FireMode.Auto;

    // 두 모드를 모두 지원하는 무기만 체크. 대부분은 해제.
    public bool canSwitchFireMode;

    [Header("산탄")]
    // Shotgun일 때만 사용한다. 한 번에 나가는 펠릿 수와 부채꼴 각도.
    public int pelletCount = 1;
    public float pelletSpread = 9f;

    [Header("반동")]
    // 한 발당 누적되는 반동(게임 픽셀). WeaponRecoil의 shootPerShot을 대체한다.
    public float recoilPerShot = 3f;

    // 이 무기에서의 반동 상한.
    public float recoilMax = 14f;

    // 카메라 셰이크 세기(게임 픽셀).
    public float shakeAmount = 2.5f;

    [Header("총구 화염")]
    public Sprite muzzleFlash;

    // 화염이 보이는 시간(초).
    public float muzzleFlashTime = 0.05f;
    public Vector2 muzzleOffset = new Vector2(1.5f, 0f);

    [Header("사운드")]
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;

    // 같은 소리가 반복될 때 단조롭지 않도록 음정을 흔든다.
    public float pitchVariation = 0.06f;

    // 산탄총 판정. 펠릿 수가 2 이상이면 산탄으로 본다.
    public bool IsShotgun
    {
        get { return pelletCount > 1; }
    }
}