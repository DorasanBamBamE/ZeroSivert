using UnityEngine;

// 플레이어 생존 스탯. 체력, 스태미나 외에 에너지 · 허기 · 갈증 · 방사능 4종을 관리한다.
// 4종은 수치에 따라 5단계 등급으로 나뉘며 등급별로 버프 또는 디버프가 적용된다.
// 출혈은 등급이 없는 별도 디버프로, 피격 시 확률적으로 발생한다.
public class PlayerStats : MonoBehaviour
{
    // 녹색(버프) > 백색(무효과) > 노랑 > 주황 > 빨강(강한 디버프)
    public enum Tier
    {
        Green,
        White,
        Yellow,
        Orange,
        Red,
    }

    [SerializeField] private PlayerAnimator playerAnimator;

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegen = 8f;
    [SerializeField] private float staminaDrain = 10f;
    [SerializeField] private float energyDrainPerMinute = 0.4f;
    [SerializeField] private float hungerDrainPerMinute = 0.5f;
    [SerializeField] private float thirstDrainPerMinute = 0.7f;
    [SerializeField] private float sprintExtraDrain = 2f;
    [SerializeField] private float bleedChance = 0.2f;
    [SerializeField] private float bleedDuration = 15f;
    [SerializeField] private float bleedDamagePerSecond = 1.5f;

    [Header("무게 초과 (11)")]
    // 원작은 상한을 넘겨 담을 수 있고 대신 느려진다.
    // 초과율 1.0(= 상한의 2배)에서 페널티가 상한에 닿는다.
    [SerializeField] private float overweightPenaltyScale = 1f;

    // 페널티 상한. 0.5를 넘기면 안 된다 - 속도가 0에 수렴해
    // 버릴 곳까지 걸어갈 수 없는 상태에 갇힌다.
    [Range(0f, 0.9f)]
    [SerializeField] private float overweightPenaltyMax = 0.5f;

    private float health;
    private float stamina;
    private float energy;
    private float hunger;
    private float thirst;
    private float radiation;
    private float bleedTimer;
    private bool isBleeding;

    // 사망 시 알린다. RunEndHandler가 구독한다.
    public event System.Action Died;

    private PlayerController controller;

    // 07-C 추가. 방탄복의 피해 감소를 읽어온다. 없으면 감소 0.
    private EquipmentController equipment;

    // 11 추가. 무게 초과 페널티를 계산하려고 본다.
    private InventoryController inventory;

    private float tickTimer;
    private bool isDead;

    public float HealthRatio { get { return health / maxHealth; } }
    public float StaminaRatio { get { return stamina / maxStamina; } }
    public float EnergyRatio { get { return energy / 100f; } }
    public float HungerRatio { get { return hunger / 100f; } }
    public float ThirstRatio { get { return thirst / 100f; } }
    public float RadiationRatio { get { return radiation / 100f; } }
    public bool IsDead { get { return isDead; } }
    public bool IsBleeding { get { return isBleeding; } }

    public Tier EnergyTier { get { return GetTier(energy); } }
    public Tier HungerTier { get { return GetTier(hunger); } }
    public Tier ThirstTier { get { return GetTier(thirst); } }

    // 방사능은 높을수록 나쁘므로 등급 판정을 뒤집는다.
    public Tier RadiationTier { get { return GetTier(100f - radiation); } }

    public bool CanSprint { get { return !isDead && stamina > 0f; } }

    // 허기 등급에 따른 추가 소지 무게. 인벤토리 시스템에서 사용한다.
    public float CarryWeightBonus
    {
        get
        {
            switch (HungerTier)
            {
                case Tier.Green: return 2f;
                case Tier.White: return 0f;
                case Tier.Yellow: return -1f;
                case Tier.Orange: return -2f;
                default: return -4f;
            }
        }
    }

    // 갈증 등급에 따른 루팅 속도 배율. 루팅 시스템에서 사용한다.
    public float LootSpeedMultiplier
    {
        get
        {
            switch (ThirstTier)
            {
                case Tier.Green: return 1.15f;
                case Tier.White: return 1f;
                case Tier.Yellow: return 0.9f;
                case Tier.Orange: return 0.8f;
                default: return 0.7f;
            }
        }
    }

    // 11 - 무게 초과분. 0이면 정상, 0.5면 상한의 1.5배를 지고 있다는 뜻.
    public float OverweightRatio
    {
        get
        {
            if (inventory == null)
            {
                inventory = GetComponent<InventoryController>();
            }

            if (inventory == null)
            {
                return 0f;
            }

            float cap = inventory.Capacity;

            if (cap <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, (inventory.CurrentWeight - cap) / cap);
        }
    }

    // 11 - 무게 때문에 깎인 속도 배율만 따로 본다. HUD에서 쓰기 좋다.
    public float WeightSpeedMultiplier
    {
        get
        {
            float over = OverweightRatio;

            if (over <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Min(overweightPenaltyMax, over * overweightPenaltyScale);
        }
    }

    // 이동속도 배율. 허기가 심하게 낮으면 느려지고, 무게를 넘기면 더 느려진다.
    public float SpeedMultiplier
    {
        get
        {
            float m = (HungerTier == Tier.Red) ? 0.85f : 1f;
            return m * WeightSpeedMultiplier;
        }
    }

    // 에너지 등급에 따른 스태미나 회복 배율.
    private float StaminaRegenMultiplier
    {
        get
        {
            switch (EnergyTier)
            {
                case Tier.Green: return 1.3f;
                case Tier.White: return 1f;
                case Tier.Yellow: return 0.8f;
                case Tier.Orange: return 0.6f;
                default: return 0.4f;
            }
        }
    }

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        equipment = GetComponent<EquipmentController>();
        inventory = GetComponent<InventoryController>();

        health = maxHealth;
        stamina = maxStamina;
        energy = 100f;
        hunger = 100f;
        thirst = 100f;
        radiation = 0f;
        isBleeding = false;
        bleedTimer = 0f;
        isDead = false;
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        UpdateStamina();
        UpdateTick();
    }

    private static Tier GetTier(float value)
    {
        if (value >= 80f) return Tier.Green;
        if (value >= 50f) return Tier.White;
        if (value >= 30f) return Tier.Yellow;
        if (value >= 15f) return Tier.Orange;
        return Tier.Red;
    }

    private void UpdateStamina()
    {
        bool sprinting = controller != null && controller.IsSprinting;

        if (sprinting)
        {
            stamina -= staminaDrain * Time.deltaTime;
        }
        else
        {
            stamina += staminaRegen * StaminaRegenMultiplier * Time.deltaTime;
        }

        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }

    private void UpdateTick()
    {
        tickTimer += Time.deltaTime;

        if (tickTimer < 1f)
        {
            return;
        }

        tickTimer -= 1f;

        bool sprinting = controller != null && controller.IsSprinting;
        float extra = sprinting ? sprintExtraDrain : 1f;

        energy = Mathf.Max(0f, energy - (energyDrainPerMinute / 60f) * extra);
        hunger = Mathf.Max(0f, hunger - (hungerDrainPerMinute / 60f) * extra);
        thirst = Mathf.Max(0f, thirst - (thirstDrainPerMinute / 60f) * extra);

        ApplyStarvation();
        ApplyRadiationEffect();
        ApplyBleeding();
    }

    private void ApplyStarvation()
    {
        if (hunger <= 0f)
        {
            TakeDamage(1f, false);
        }

        if (thirst <= 0f)
        {
            TakeDamage(1f, false);
        }
    }

    // 주황 등급부터 출혈 피해, 빨강 등급에서는 즉사 확률이 생긴다.
    private void ApplyRadiationEffect()
    {
        Tier tier = RadiationTier;

        if (tier == Tier.Orange && Random.value < 0.05f)
        {
            TakeDamage(2f, false);
        }
        else if (tier == Tier.Red)
        {
            if (Random.value < 0.01f)
            {
                TakeDamage(maxHealth, false);
                return;
            }

            TakeDamage(3f, false);
        }
    }

    private void ApplyBleeding()
    {
        if (!isBleeding)
        {
            return;
        }

        TakeDamage(bleedDamagePerSecond, false);
        bleedTimer -= 1f;

        if (bleedTimer <= 0f)
        {
            isBleeding = false;
        }
    }

    // canBleed는 피격 등 외부 피해일 때만 true로 넘긴다.
    public void TakeDamage(float amount, bool canBleed = true)
    {
        if (isDead)
        {
            return;
        }

        // 07-C — 방탄복은 외부 피격만 막는다.
        // 굶주림·방사능·출혈 피해는 canBleed가 false로 들어오므로 그대로 관통한다.
        if (canBleed && equipment != null)
        {
            amount *= (1f - equipment.DamageReduction);
        }

        health = Mathf.Max(0f, health - amount);

        if (canBleed && !isBleeding && Random.value < bleedChance)
        {
            isBleeding = true;
            bleedTimer = bleedDuration;
        }

        if (health <= 0f)
        {
            Die();
        }
    }

    // 붕대 사용 시 호출한다.
    public void StopBleeding()
    {
        isBleeding = false;
        bleedTimer = 0f;
    }

    public void Heal(float amount)
    {
        health = Mathf.Min(maxHealth, health + amount);
    }

    public void RestoreEnergy(float amount)
    {
        energy = Mathf.Min(100f, energy + amount);
    }

    public void EatFood(float amount)
    {
        hunger = Mathf.Min(100f, hunger + amount);
    }

    public void DrinkWater(float amount)
    {
        thirst = Mathf.Min(100f, thirst + amount);
    }

    public void AddRadiation(float amount)
    {
        radiation = Mathf.Clamp(radiation + amount, 0f, 100f);
    }

    public void ReduceRadiation(float amount)
    {
        radiation = Mathf.Max(0f, radiation - amount);
    }

    private void Die()
    {
        isDead = true;

        if (playerAnimator != null)
        {
            playerAnimator.SetDead();
        }

        if (Died != null)
        {
            Died();
        }
    }

    // 씬 전환 시 현재 수치를 스냅샷으로 뽑는다.
    public void CaptureTo(RunData.PlayerSnapshot s)
    {
        s.health = health;
        s.stamina = stamina;
        s.energy = energy;
        s.hunger = hunger;
        s.thirst = thirst;
        s.radiation = radiation;
        s.bleeding = isBleeding;
        s.bleedTimer = bleedTimer;
    }

    // 씬 진입 시 스냅샷을 되돌린다.
    public void RestoreFrom(RunData.PlayerSnapshot s)
    {
        health = Mathf.Clamp(s.health, 0f, maxHealth);
        stamina = Mathf.Clamp(s.stamina, 0f, maxStamina);
        energy = Mathf.Clamp(s.energy, 0f, 100f);
        hunger = Mathf.Clamp(s.hunger, 0f, 100f);
        thirst = Mathf.Clamp(s.thirst, 0f, 100f);
        radiation = Mathf.Clamp(s.radiation, 0f, 100f);
        isBleeding = s.bleeding;
        bleedTimer = s.bleedTimer;
        isDead = false;
    }
}
