using UnityEngine;

// 로비의 튜토리얼 버튼으로 켜는 임시 튜토리얼 플래그(최소 구현).
// 켜져 있는 동안에는 이어하기 세이브를 저장하거나 지우지 않고, MainScene_Map에 안내 오버레이를 띄운다.
// Guided 는 처음 새 게임을 시작했을 때 한 번만 붙는 안내다. 오버레이만 띄우고 저장·몬스터 등 판 규칙은 그대로 둔다.
public static class TutorialMode
{
    // 튜토리얼 첫 전투 몬스터 체력. 한 턴에 잡힐 정도로 낮춘다.
    public const int MonsterHp = 5;

    const string SeenKey = "TrickYacht_TutorialSeen";
    const string SaveKey = "TrickYacht_Save";
    const string CollectionKey = "TrickYacht_CollectionData";

    public static bool Active { get; private set; }
    public static bool Guided { get; private set; }

    public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) == 1;

    // 첫 판 안내는 아직 한 번도 안 띄웠을 때만 보인다.
    public static bool ShowsOverlay => Active || (Guided && !Seen);

    public static void Start()
    {
        Active = true;
        Guided = false;
    }

    // 새 게임 버튼에서 부른다. 이미 플레이한 기록(이어하기 세이브·도감)이 있으면 본 것으로 치고 켜지 않는다.
    public static void StartGuidedIfFirstRun()
    {
        Guided = false;
        if (Seen) return;
        if (PlayerPrefs.HasKey(SaveKey) || !string.IsNullOrEmpty(PlayerPrefs.GetString(CollectionKey, "")))
        {
            MarkSeen();
            return;
        }
        Guided = true;
    }

    public static void MarkSeen()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    // 여러 번 불러도 안전하다(로비 진입 때마다 한 번 더 부른다).
    public static void End()
    {
        Active = false;
        Guided = false;
    }

    // 바이옴 일반 몬스터 중 최대 체력이 가장 낮은 몬스터. 없으면 null.
    public static MonsterDataSO WeakestMonster(BiomeDataSO biome)
    {
        if (biome == null || biome.biomeMonsters == null) return null;
        MonsterDataSO weakest = null;
        foreach (MonsterDataSO monster in biome.biomeMonsters)
        {
            if (monster != null && (weakest == null || monster.maxHp < weakest.maxHp))
                weakest = monster;
        }
        return weakest;
    }
}
