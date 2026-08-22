using UnityEngine;

// 플레이어 무기. 좌클릭 발사, R 재장전.
//
// 수치는 전부 WeaponData 에셋에서 읽는다. Equip()으로 무기를 교체한다.
// 총 오브젝트는 하나로 두고 스프라이트와 수치만 갈아끼우는 구조다.
//
// 발사 방향은 WeaponRecoil이 계산한 조준점(AimPoint)을 향한다.
// 랜덤 탄퍼짐 콘을 따로 쓰지 않는다 — 조준점이 반경 안을 떠도는 것 자체가
// 탄퍼짐이고, 크로스헤어가 그 지점을 보여주므로 보이는 곳에 맞는다.
// 산탄총만 예외로 펠릿을 부채꼴로 뿌린다.
public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponData data;

    [SerializeField] private PlayerController controller;
    [SerializeField] private WeaponRecoil recoil;
    [SerializeField] private BulletPooling pooling;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private WeaponMastery mastery;

    [Header("오브젝트")]
    // 총 스프라이트를 그리는 렌더러. 무기 교체 시 sprite를 바꾼다.
    [SerializeField] private SpriteRenderer gunRenderer;

    // 총알이 나가는 위치.
    [SerializeField] private Transform muzzle;

    // 총구 화염 스프라이트. muzzle 자식으로 두고 평소엔 꺼둔다.
    [SerializeField] private SpriteRenderer muzzleFlash;

    [SerializeField] private AudioSource audioSource;

    [Header("입력")]
    [SerializeField] private KeyCode reloadKey = KeyCode.R;
    [SerializeField] private KeyCode fireModeKey = KeyCode.B;

    // PDA가 열려 있으면 입력을 무시한다.
    [SerializeField] private PDAController pda;

    private int currentAmmo;
    private float fireTimer;
    private float reloadTimer;
    private float flashTimer;
    private bool isReloading;
    private WeaponData.FireMode currentMode;

    // HUD에서 탄약 표시에 사용한다.
    public int CurrentAmmo
    {
        get { return currentAmmo; }
    }

    // 11 - 장비 슬롯이 기억해 둔 잔탄을 되돌린다. Equip 직후에 부른다.
    public void SetAmmo(int value)
    {
        int max = (data != null) ? data.magazineSize : 0;
        currentAmmo = Mathf.Clamp(value, 0, max);
        isReloading = false;
    }

    public int MagazineSize
    {
        get { return data != null ? data.magazineSize : 0; }
    }

    public WeaponData.AmmoType Ammo
    {
        get { return data != null ? data.ammoType : WeaponData.AmmoType.Small; }
    }

    public WeaponData.FireMode Mode
    {
        get { return currentMode; }
    }

    public bool IsReloading
    {
        get { return isReloading; }
    }

    public WeaponData Data
    {
        get { return data; }
    }

    private void Awake()
    {
        if (recoil == null && controller != null)
        {
            recoil = controller.GetComponent<WeaponRecoil>();
        }

        if (mastery == null && controller != null)
        {
            mastery = controller.GetComponent<WeaponMastery>();
        }

        if (cameraFollow == null)
        {
            cameraFollow = FindFirstObjectByType<CameraFollow>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        Equip(data);
    }

    // 무기 교체. EquipmentController가 장비 슬롯이 바뀔 때 호출한다.
    //
    // null을 넣으면 맨손이 된다. 장비 슬롯이 비어 있는 상태가 이 경우다.
    public void Equip(WeaponData newData)
    {
        data = newData;

        if (data == null)
        {
            if (gunRenderer != null)
            {
                gunRenderer.sprite = null;
            }

            if (muzzleFlash != null)
            {
                muzzleFlash.enabled = false;
            }

            currentAmmo = 0;
            isReloading = false;
            return;
        }

        if (gunRenderer != null)
        {
            gunRenderer.sprite = data.gameSprite;
        }

        // 무기마다 총구 위치가 다르므로 Muzzle을 옮긴다.
        // 총알도 여기서 나가므로 muzzleFlash 유무와 무관하게 적용해야 한다.
        if (muzzle != null)
        {
            muzzle.localPosition = data.muzzleOffset;
        }

        if (muzzleFlash != null)
        {
            muzzleFlash.sprite = data.muzzleFlash;
            muzzleFlash.enabled = false;
        }

        // 11 - 탄약은 무기별로 따로 기억한다.
        //   EquipmentController가 슬롯마다 남은 탄을 들고 있다가 Equip 직후 SetAmmo로 되돌린다.
        //   여기서 이전 총의 탄을 이어받으면 5발 저격총과 30발 소총이 탄창을 공유해버린다.
        currentAmmo = data.magazineSize;
        isReloading = false;

        // 무기를 바꾸면 조준이 다시 잡힌다.
        if (recoil != null)
        {
            recoil.ResetRecoil();
        }
    }

    private void Update()
    {
        UpdateMuzzleFlash();

        // 07번 추가 — 인벤토리(Tab)가 열려 있으면 입력을 전부 무시한다.
        //
        // Time.timeScale이 0이어도 Update와 Input은 계속 돈다.
        // 이 가드가 없으면 아이템을 좌클릭으로 드래그하는 순간 그게 발사 입력이 되고,
        // 아이템 회전용 R이 재장전을, B가 발사모드 전환을 같이 걸어버린다.
        if (UIBlocker.Any)
        {
            return;
        }

        if (pda != null && pda.IsOpen)
        {
            return;
        }

        if (data == null)
        {
            return;
        }

        fireTimer += Time.deltaTime;

        if (isReloading)
        {
            UpdateReload();
            return;
        }

        if (data.canSwitchFireMode && Input.GetKeyDown(fireModeKey))
        {
            currentMode = (currentMode == WeaponData.FireMode.Auto)
                ? WeaponData.FireMode.Semi
                : WeaponData.FireMode.Auto;
        }

        if (Input.GetKeyDown(reloadKey) && currentAmmo < data.magazineSize)
        {
            BeginReload();
            return;
        }

        bool pressed = (currentMode == WeaponData.FireMode.Auto)
            ? Input.GetMouseButton(0)
            : Input.GetMouseButtonDown(0);

        if (pressed)
        {
            TryFire();
        }
    }

    private void TryFire()
    {
        if (fireTimer < data.fireRate)
        {
            return;
        }

        if (currentAmmo <= 0)
        {
            // 빈 탄창 소리는 누를 때마다가 아니라 연사 간격마다 한 번만 낸다.
            fireTimer = 0f;
            PlaySound(data.emptySound);
            BeginReload();
            return;
        }

        if (pooling == null || muzzle == null || recoil == null)
        {
            return;
        }

        fireTimer = 0f;
        currentAmmo--;

        if (GameStats.Instance != null)
        {
            GameStats.Instance.Add(GameStats.StatId.ShotsFired);
        }

        // 총구에서 조준점을 향하는 각도. 반동은 이 각도 안에 이미 반영되어 있다.
        Vector2 toAim = recoil.AimPoint - (Vector2)muzzle.position;
        float angle = Mathf.Atan2(toAim.y, toAim.x) * Mathf.Rad2Deg;

        if (data.IsShotgun)
        {
            for (int i = 0; i < data.pelletCount; i++)
            {
                Spawn(angle + Random.Range(-data.pelletSpread, data.pelletSpread));
            }
        }
        else
        {
            Spawn(angle);
        }

        // 반동 누적은 발사 이후. 첫 발은 크로스헤어가 보여준 그대로 나간다.
        float recoilMul = (mastery != null) ? mastery.GetRecoilMultiplier(data.weaponClass) : 1f;
        recoil.AddShot(data.recoilPerShot * recoilMul, data.recoilMax * recoilMul);

        if (cameraFollow != null)
        {
            cameraFollow.Shake(data.shakeAmount, angle);
        }

        ShowMuzzleFlash();
        PlaySound(data.fireSound);
    }

    private void Spawn(float angle)
    {
        Bullet bullet = pooling.Get();
        bullet.SetSource(mastery, data.weaponClass);
        bullet.Fire(muzzle.position, angle, data.bulletSpeed, data.damage, data.bulletLifeTime);
    }

    private void BeginReload()
    {
        if (isReloading)
        {
            return;
        }

        isReloading = true;
        reloadTimer = 0f;
        PlaySound(data.reloadSound);
    }

    private void UpdateReload()
    {
        reloadTimer += Time.deltaTime;

        if (reloadTimer < data.reloadTime)
        {
            return;
        }

        // 인벤토리가 생기면 여기서 예비 탄약을 차감한다. 지금은 무한이다.
        currentAmmo = data.magazineSize;
        isReloading = false;

        if (recoil != null)
        {
            recoil.ResetRecoil();
        }
    }

    private void ShowMuzzleFlash()
    {
        if (muzzleFlash == null || data.muzzleFlash == null)
        {
            return;
        }

        muzzleFlash.enabled = true;
        flashTimer = data.muzzleFlashTime;
    }

    // timeScale이 0이어도 꺼지도록 unscaled를 쓴다.
    private void UpdateMuzzleFlash()
    {
        if (flashTimer <= 0f)
        {
            return;
        }

        flashTimer -= Time.unscaledDeltaTime;

        if (flashTimer <= 0f && muzzleFlash != null)
        {
            muzzleFlash.enabled = false;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        // 같은 소리가 반복될 때 단조롭지 않도록 음정을 살짝 흔든다.
        float v = data.pitchVariation;
        audioSource.pitch = 1f + Random.Range(-v, v);
        audioSource.PlayOneShot(clip);
    }
}