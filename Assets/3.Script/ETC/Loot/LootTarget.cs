using UnityEngine;

// GROUND 패널이 지금 무엇을 보여줘야 하는지 추적한다.
//
//   상자·시체를 E로 열었다  → 그 컨테이너의 내용물
//   아무것도 안 열었다      → 발밑 지면 컨테이너
//
// 원작의 우측 GROUND가 말 그대로 "발밑 지면"이라, 상자를 열면 그 자리가
// 상자 내용물로 바뀌는 구조를 그대로 따른다. 덕분에 "지면에 버리기"를 위한
// 별도 시스템이 필요 없다 — 인벤토리에서 오른쪽으로 드래그하면 그게 버리는 것이다.
public static class LootTarget
{
    // 아무것도 안 열었을 때 우측 패널에 뜨는 이름. 원작 그대로.
    public const string GroundName = "GROUND";

    private static InventoryController container;
    private static string containerName;

    // 우측 패널 머리글에 띄울 이름. 상자를 열었으면 그 이름, 아니면 GROUND.
    public static string CurrentName
    {
        get
        {
            if (container != null && !string.IsNullOrEmpty(containerName))
            {
                return containerName;
            }

            return GroundName;
        }
    }

    // 지금 열려 있는 컨테이너. 없으면 지면.
    public static InventoryController Current
    {
        get
        {
            if (container != null)
            {
                return container;
            }

            return GroundContainer.Inventory;
        }
    }

    // 상자나 시체를 열어둔 상태인가. 지면이면 false.
    public static bool HasContainer
    {
        get { return container != null; }
    }

    public static void Set(InventoryController c)
    {
        Set(c, null);
    }

    public static void Set(InventoryController c, string displayName)
    {
        container = c;
        containerName = displayName;
    }

    public static void Clear()
    {
        container = null;
        containerName = null;
    }

    // 플레이 모드를 껐다 켤 때 static이 남아 있으면 예전 씬의 파괴된 컨트롤러를
    // 붙들고 있게 된다. 도메인 리로드를 끈 프로젝트에서도 안전하도록 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        container = null;
        containerName = null;
    }
}
