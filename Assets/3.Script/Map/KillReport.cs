// 플레이어를 마지막으로 때린 것이 무엇인지 기록한다.
//
// 원작 결과 화면의 "사망 원인 / Weapon / 탄약" 세 줄이 여기서 나온다.
// 죽는 순간이 아니라 맞을 때마다 갱신한다 - 마지막 타격이 곧 사인이다.
//
// 정적 클래스인 이유: 총알은 풀에서 재사용되고 적은 죽으면 사라진다.
// 참조를 들고 있으면 결과 화면을 띄우는 시점에 이미 null이다. 문자열만 남긴다.
public static class KillReport
{
    public static string Attacker;
    public static string Weapon;
    public static string Ammo;

    public static string AttackerText
    {
        get { return string.IsNullOrEmpty(Attacker) ? "알 수 없음" : Attacker; }
    }

    public static bool HasWeapon
    {
        get { return !string.IsNullOrEmpty(Weapon); }
    }

    public static bool HasAmmo
    {
        get { return !string.IsNullOrEmpty(Ammo); }
    }

    public static void Set(string attacker, string weapon, string ammo)
    {
        Attacker = attacker;
        Weapon = weapon;
        Ammo = ammo;
    }

    // 새 출격을 시작할 때 지운다. 지난 판의 사인이 남아 있으면 안 된다.
    public static void Clear()
    {
        Attacker = null;
        Weapon = null;
        Ammo = null;
    }
}
