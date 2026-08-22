using UnityEngine;

// 적이 드는 무기. 플레이어의 Weapon과 같은 WeaponData를 쓰지만
// 입력 대신 AI 호출로 동작하고, 반동 대신 고정 탄퍼짐을 쓴다.
//
// 밴딧은 스폰 시 possibleWeapons에서 랜덤으로 하나를 뽑아 든다.
//
// 구성 — 적 오브젝트의 자식으로 둔다.
//   Bandit
//   └─ GunPivot        EnemyWeapon.cs, SpriteRenderer, AudioSource
//       └─ Muzzle      총알이 나가는 위치. 몸통 콜라이더 바깥에 둘 것.
public class EnemyWeapon : MonoBehaviour
{
    [SerializeField] private WeaponData[] possibleWeapons;
    [SerializeField] private BulletPooling pooling;
    [SerializeField] private SpriteRenderer gunRenderer;
    [SerializeField] private Transform muzzle;
    [SerializeField] private AudioSource audioSource;

    [Header("AI 사격")]
    // 적은 플레이어보다 부정확하다. 도(°) 단위 고정 탄퍼짐.
    [SerializeField] private float spreadAngle = 7f;

    // 연사 무기를 계속 갈기지 않도록 몇 발씩 끊어 쏜다.
    [SerializeField] private int burstMin = 2;
    [SerializeField] private int burstMax = 4;
    [SerializeField] private float burstPauseMin = 0.6f;
    [SerializeField] private float burstPauseMax = 1.4f;

    // 데미지 배율. 적 총이 너무 아프면 낮춘다.
    [SerializeField] private float damageMultiplier = 0.7f;

    // 총구가 몸통에 겹쳐도 자기 총에 맞지 않도록 앞으로 밀어내는 거리(유닛).
    [SerializeField] private float spawnOffset = 0.3f;

    private WeaponData data;
    private Collider2D ownerCollider;
    private int currentAmmo;
    private float fireTimer;
    private float reloadTimer;
    private float pauseTimer;
    private int burstLeft;
    private bool isReloading;

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
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (pooling == null)
        {
            pooling = FindFirstObjectByType<BulletPooling>();
        }
    }

    private void OnEnable()
    {
        EquipRandom();
    }

    // 소유자의 콜라이더. 자기가 쏜 총알에 맞지 않도록 총알에 넘긴다.
    // RangedEnemyController가 Awake에서 호출한다.
    public void SetOwner(Collider2D collider)
    {
        ownerCollider = collider;
    }

    // 스폰 시 무작위 무장. 나중에 사살 후 드랍에도 이 데이터를 넘긴다.
    public void EquipRandom()
    {
        if (possibleWeapons == null || possibleWeapons.Length == 0)
        {
            return;
        }

        Equip(possibleWeapons[Random.Range(0, possibleWeapons.Length)]);
    }

    public void Equip(WeaponData newData)
    {
        data = newData;

        if (data == null)
        {
            return;
        }

        currentAmmo = data.magazineSize;
        isReloading = false;
        fireTimer = data.fireRate;
        pauseTimer = 0f;
        burstLeft = Random.Range(burstMin, burstMax + 1);

        if (gunRenderer != null)
        {
            gunRenderer.sprite = data.gameSprite;
        }
    }

    private void Update()
    {
        if (data == null)
        {
            return;
        }

        fireTimer += Time.deltaTime;

        if (isReloading)
        {
            reloadTimer += Time.deltaTime;

            if (reloadTimer >= data.reloadTime)
            {
                currentAmmo = data.magazineSize;
                isReloading = false;
                burstLeft = Random.Range(burstMin, burstMax + 1);
            }

            return;
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
        }
    }

    // 총구를 목표 방향으로 돌린다. 매 프레임 AI가 호출한다.
    public void Aim(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 왼쪽을 조준하면 총 스프라이트가 뒤집히므로 Y축으로 반전시킨다.
        bool facingLeft = (angle > 90f || angle < -90f);
        Vector3 scale = transform.localScale;
        scale.y = facingLeft ? -1f : 1f;
        transform.localScale = scale;
    }

    // AI가 사격을 시도한다. 조건이 안 맞으면 조용히 넘어간다.
    public void TryFire(Vector2 direction)
    {
        if (data == null || isReloading || pooling == null || muzzle == null)
        {
            return;
        }

        if (pauseTimer > 0f || fireTimer < data.fireRate)
        {
            return;
        }

        if (currentAmmo <= 0)
        {
            BeginReload();
            return;
        }

        fireTimer = 0f;
        currentAmmo--;

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float damage = data.damage * damageMultiplier;

        if (data.IsShotgun)
        {
            for (int i = 0; i < data.pelletCount; i++)
            {
                Spawn(baseAngle + Random.Range(-data.pelletSpread, data.pelletSpread), damage);
            }
        }
        else
        {
            Spawn(baseAngle + Random.Range(-spreadAngle, spreadAngle), damage);
        }

        Play(data.fireSound);

        // 버스트를 다 쐈으면 잠깐 쉰다.
        burstLeft--;

        if (burstLeft <= 0)
        {
            burstLeft = Random.Range(burstMin, burstMax + 1);
            pauseTimer = Random.Range(burstPauseMin, burstPauseMax);
        }
    }

    private void Spawn(float angle, float damage)
    {
        Bullet bullet = pooling.Get();

        // 적 총알은 플레이어와 다른 적을 때린다. 소유자만 예외로 둔다.
        bullet.SetFaction(Bullet.Faction.Enemy, ownerCollider);
        bullet.SetSource(null, data.weaponClass);

        // 총구가 몸통에 겹쳐도 자기가 맞지 않도록 앞으로 밀어 발사한다.
        float rad = angle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * spawnOffset;

        bullet.Fire((Vector2)muzzle.position + offset, angle,
            data.bulletSpeed, damage, data.bulletLifeTime);
    }

    private void BeginReload()
    {
        if (isReloading)
        {
            return;
        }

        isReloading = true;
        reloadTimer = 0f;
        Play(data.reloadSound);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(0.94f, 1.06f);
        audioSource.PlayOneShot(clip);
    }
}